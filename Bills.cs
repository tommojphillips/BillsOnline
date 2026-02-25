using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using HutongGames.PlayMaker;
using I386API;
using MSCLoader;

namespace BillsOnline;

internal class Bills {

    private List<IBill> bills;

    private Dictionary<string, string> translations;
    private Dictionary<string, string> normalizedTranslations;

    private BillsState state;
    private int index;

    Coroutine routine;

    private bool reconnect;

    private enum TimeType {
        RealTime,
        GameTime,
        Date,
        COUNT,
    }
    private enum TimeType2 {
        _12H,
        _24H,
        COUNT,
    }
    private TimeType timeType;
    private TimeType2 timeType2;

    private FsmFloat bankAccount;
    private FsmInt day;
    private FsmInt hour;
    private FsmFloat minutes;
    private FsmFloat timeScale;

    private readonly string[] DAYS = new string[] { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" };
    
    private const float SECONDS_PER_MINUTE = 60f;
    private const float MINUTES_PER_HOUR = 60f;
    private const float HOURS_PER_DAY = 24;

    private const float SECONDS_PER_HOUR = SECONDS_PER_MINUTE * MINUTES_PER_HOUR; //  3600
    private const float SECONDS_PER_DAY = SECONDS_PER_HOUR * HOURS_PER_DAY;       // 86400

    public void load() {
        // Initialize translations dictionary; will be loaded from config file
        translations = new Dictionary<string, string>();
        try {
            LoadTranslationsFromFile();
        }
        catch (Exception) {
            // silent - critical errors use ModConsole.Error where appropriate
        }

        // Load diskette texture
        Texture2D texture = new Texture2D(128, 128);
        texture.LoadImage(Properties.Resources.FLOPPY_FINES);
        texture.name = "FLOPPY_BILLS";

        // Create command
        Command command = Command.Create("bills", command_enter, command_update);
        
        // Create diskette
        Diskette diskette = Diskette.Create("bills", new Vector3(-10.39007f, 0.2121807f, 14.01701f), new Vector3(270f, 198.4409f, 0f));
        diskette.SetTexture(texture);

        Transform t1 = GameObject.Find("Systems/PhoneBills1").transform;
        PhoneBill phoneBill1 = new PhoneBill();
        phoneBill1.load(Localize("House Phone Bill"), t1);
        phoneBill1.SetLocalizationFunction(Localize);

        Transform t2 = GameObject.Find("Systems/PhoneBills2").transform;
        PhoneBill phoneBill2 = new PhoneBill();
        phoneBill2.load(Localize("Apartment Phone Bill"), t2);
        phoneBill2.SetLocalizationFunction(Localize);

        Transform t3 = GameObject.Find("Systems/ElectricityBills1").transform;
        ElectricityBill electricityBill1 = new ElectricityBill();
        electricityBill1.load(Localize("House Electricity Bill"), t3);
        electricityBill1.SetLocalizationFunction(Localize);

        Transform t4 = GameObject.Find("Systems/ElectricityBills2").transform;
        ElectricityBill electricityBill2 = new ElectricityBill();
        electricityBill2.load(Localize("Apartment Electricity Bill"), t4);
        electricityBill2.SetLocalizationFunction(Localize);

        bills = new List<IBill>();
        bills.Add(phoneBill1);
        bills.Add(electricityBill1);
        bills.Add(phoneBill2);
        bills.Add(electricityBill2);

        bankAccount = PlayMakerGlobals.Instance.Variables.GetFsmFloat("PlayerBankAccount");

        day = PlayMakerGlobals.Instance.Variables.GetFsmInt("GlobalDay");
        hour = PlayMakerGlobals.Instance.Variables.GetFsmInt("GlobalHour");
        minutes = PlayMakerGlobals.Instance.Variables.GetFsmFloat("ClockMinutes");
        timeScale = PlayMakerGlobals.Instance.Variables.GetFsmFloat("GlobalTimeScale");

        timeType = TimeType.RealTime;
        timeType2 = TimeType2._12H;
    }

    private IEnumerator processPaymentAsync() {
        if (I386.ModemConnected) {
            if (reconnect) {
                state = BillsState.Connect;
            }
            else {
                yield return new WaitForSeconds(0.65f);
                if (payBill(bills[index])) {
                    state = BillsState.PaymentSuccess;
                }
                else {
                    state = BillsState.PaymentFailed;
                }
            }
        }
        else {
            yield return new WaitForSeconds(1.3f);
            state = BillsState.NotConnected;
        }
        routine = null;
    }
    private IEnumerator paymentSuccessAsync() {
        yield return new WaitForSeconds(1.3f);
        state = BillsState.Viewing;
        routine = null;
    }
    private IEnumerator paymentFailedAsync() {
        yield return new WaitForSeconds(1.3f);
        state = BillsState.Viewing;
        routine = null;
    }
    private IEnumerator connectAsync() {
        if (I386.ModemConnected) {
            yield return new WaitForSeconds(0.65f);
            state = BillsState.Viewing;
        }
        else {
            yield return new WaitForSeconds(1.3f);
            state = BillsState.NotConnected;
        }

        reconnect = false;
        routine = null;
    }

    private void processPayment() {
        if (routine == null) {
            routine = I386.StartCoroutine(processPaymentAsync());
        }
    }
    private void paymentSuccess() {
        if (routine == null) {
            routine = I386.StartCoroutine(paymentSuccessAsync());
        }
    }
    private void paymentFailed() {
        if (routine == null) {
            routine = I386.StartCoroutine(paymentFailedAsync());
        }
    }
    private void connect() {
        if (routine == null) {
            routine = I386.StartCoroutine(connectAsync());
        }
    }
    private bool payBill(IBill bill) {
        if (bankAccount.Value >= bill.getPrice()) {
            bankAccount.Value -= bill.getPrice();
            bill.pay();
            return true;
        }
        else {
            return false;
        }
    }
    private void viewHeader() {
        I386.POS_ClearScreen();
        I386.POS_WriteNewLine("                                   " + Localize("Bills Online"));
        I386.POS_WriteNewLine("--------------------------------------------------------------------------------");
    }
    private void printDueDate(float due) {
        float REAL_TO_GAME = SECONDS_PER_HOUR / timeScale.Value;
        int dueHour;
        int dueMinute;
        switch (timeType) {

            case TimeType.Date:
                float inGameSecondsUntilDue = due * REAL_TO_GAME;
                float currentInGameSeconds = (hour.Value * SECONDS_PER_HOUR) + (minutes.Value * SECONDS_PER_MINUTE);
                float totalInGameSeconds = currentInGameSeconds + inGameSecondsUntilDue;
                int daysLater = (int)(totalInGameSeconds / SECONDS_PER_DAY);
                float secondsIntoDay = totalInGameSeconds % SECONDS_PER_DAY;
                dueHour = (int)(secondsIntoDay / SECONDS_PER_HOUR);
                dueMinute = (int)((secondsIntoDay % SECONDS_PER_HOUR) / SECONDS_PER_MINUTE);
                int dayIndex = ((day.Value + daysLater) % 7 + 7) % 7;
                string dueDayName = DAYS[dayIndex];
                int weekOffset = (day.Value + daysLater) / 7;

                string weekString;
                string dayString;
                if (daysLater > 7) {
                    weekString = $"{Localize("In")} {weekOffset} {Localize("week(s) on")} ";
                    dayString = dueDayName;
                }
                else if (daysLater == 7) {
                    weekString = $"{Localize("Next week on")} ";
                    dayString = dueDayName;
                }
                else {
                    weekString = "";
                    if (daysLater == 0)
                        dayString = Localize("Today");
                    else if (daysLater == 1)
                        dayString = Localize("Tomorrow");
                    else
                        dayString = dueDayName;
                }

                switch (timeType2) {
                    case TimeType2._24H:
                        I386.POS_WriteNewLine($"{weekString}{dayString} at {dueHour}:{dueMinute:D2}");
                        break;
                    case TimeType2._12H:
                        string ampm = dueHour < 12 ? Localize("AM") : Localize("PM");
                        int hour12 = dueHour % 12;
                        if (hour12 == 0) {
                            hour12 = 12;
                        }
                        I386.POS_WriteNewLine($"{weekString}{dayString} at {hour12}:{dueMinute:D2} {ampm}");
                        break;
                }
                break;

            case TimeType.RealTime:
                dueHour = (int)(due / SECONDS_PER_HOUR);
                dueMinute = (int)((due % SECONDS_PER_HOUR) / SECONDS_PER_MINUTE);
                I386.POS_WriteNewLine($"{dueHour}h {dueMinute:D2}m ({Localize("real time")})");
                break;

            case TimeType.GameTime:
                due *= REAL_TO_GAME;
                dueHour = (int)(due / SECONDS_PER_HOUR);
                dueMinute = (int)((due % SECONDS_PER_HOUR) / SECONDS_PER_MINUTE);
                I386.POS_WriteNewLine($"{dueHour}h {dueMinute:D2}m ({Localize("game time")})");
                break;
        }
    }

    private void viewBill(IBill bill) {
        if (I386.GetKeyDown(KeyCode.LeftArrow)) {
            index = index - 1;
            if (index < 0) {
                index = bills.Count - 1;
            }
        }
        if (I386.GetKeyDown(KeyCode.RightArrow)) {
            index = index + 1;
            if (index >= bills.Count) {
                index = 0;
            }
        }
        if (I386.GetKeyDown(KeyCode.T)) {
            if (timeType >= TimeType.COUNT - 1) {
                timeType = 0;
            }
            else {
                timeType++;
            }
        }
        if (I386.GetKeyDown(KeyCode.Y)) {
            if (timeType2 >= TimeType2.COUNT - 1) {
                timeType2 = 0;
            }
            else {
                timeType2++;
            }
        }

        if (I386.ModemConnected) {
            if (reconnect) {
                state = BillsState.Connect;
                return;
            }
        }
        else {
            reconnect = true;
        }

        viewHeader();

        I386.POS_WriteNewLine($"   {bill.name}\n");
        I386.POS_WriteNewLine($"   {Localize("Definition")}\t\t\t\t\t\t{Localize("Quantity")}\t\t{Localize("Price MK")}\t\t{Localize("Total MK")}\n");

        if (bill.isActive) {
            bill.view();

            if (I386.GetKeyDown(KeyCode.Space)) {
                state = BillsState.PaymentProcessing;
                return;
            }

            I386.POS_WriteNewLine("\n                                                                    [" + Localize("PAY NOW") + "] ");
            if (bill.isCutOff) {
                I386.POS_WriteNewLine("   " + Localize("Overdue"));
            }
            else {
                I386.POS_Write("   " + Localize("Due") + ": ");
                printDueDate(bill.timeUntilCutOff);
            }
        }
        else {
            I386.POS_WriteNewLine("   " + Localize("Invoice not ready"));
            I386.POS_Write("   " + Localize("Due") + ": ");
            printDueDate(bill.timeUntilNextBill);
        }
    }
    private void viewNotConnected() {
        viewHeader();
        I386.POS_WriteNewLine("                                   " + Localize("Not Connected"));
        I386.POS_WriteNewLine("                               " + Localize("Press Space to Connect"));
        if (I386.GetKeyDown(KeyCode.Space)) {
            state = BillsState.Connect;
        }
    }
    private void viewConnect() {
        viewHeader();
        I386.POS_WriteNewLine("                                   " + Localize("Connecting..."));
        connect();
    }
    private void viewPaymentProcessing() {
        viewHeader();
        I386.POS_WriteNewLine("                               " + Localize("Processing Payment..."));
        processPayment();
    }
    private void viewPaymentSuccess() {
        viewHeader();
        I386.POS_WriteNewLine($"                                 " + Localize("Payment Success"));
        paymentSuccess();
    }
    private void viewPaymentFailed() {
        viewHeader();
        I386.POS_WriteNewLine($"                                 " + Localize("Payment Failed"));
        paymentFailed();
    }

    private bool command_enter() {
        index = 0;
        state = BillsState.Connect;
        return false; // do update
    }
    private bool command_update() {
        if ((I386.GetKey(KeyCode.LeftControl) || I386.GetKey(KeyCode.RightControl)) && I386.GetKeyDown(KeyCode.C)) {
            return true; // exit
        }

        switch (state) {
            case BillsState.Connect:
                viewConnect();
                break;
            case BillsState.NotConnected:
                viewNotConnected();
                break;
            case BillsState.Viewing:
                viewBill(bills[index]);
                break;
            case BillsState.PaymentProcessing:
                viewPaymentProcessing();
                break;
            case BillsState.PaymentSuccess:
                viewPaymentSuccess();
                break;
            case BillsState.PaymentFailed:
                viewPaymentFailed();
                break;
        }

        return false; // continue
    }

    internal string Localize(string s) {
        if (translations != null && translations.Count > 0) {
            if (translations.TryGetValue(s, out string translatedValue)) {
                return translatedValue;
            }

            // try normalized matching (case-insensitive, whitespace-tolerant)
            string normalizedInput = NormalizeKey(s);
            if (!string.IsNullOrEmpty(normalizedInput)) {
                string tv;
                if (normalizedTranslations.TryGetValue(normalizedInput, out tv)) {
                    return tv;
                }

                // try substring matching (input contains key or vice-versa)
                foreach (var kv in normalizedTranslations) {
                    if (normalizedInput.Contains(kv.Key) || kv.Key.Contains(normalizedInput)) {
                        // do not log successful translations to avoid noise
                        return kv.Value;
                    }
                }
            }
        }

        // no translation found (silent)
        return s;
    }

    // Normalizes keys: reduces multiple spaces, removes simple punctuation and converts to lower
    private string NormalizeKey(string s) {
        if (string.IsNullOrEmpty(s))
            return string.Empty;

        // collapse multiple spaces
        string collapsed = Regex.Replace(s, "\\s+", " ").Trim();

        // remove punctuation (keeps letters, numbers, / and space)
        string cleaned = Regex.Replace(collapsed, "[^a-zA-Z0-9\\/ ]+", "");

        return cleaned.ToLowerInvariant();
    }

    // Loads translations from a simple JSON file located at Mods\Config\Mod Settings\BillsOnline:
    // { "English text": "Translated text", ... }
    private void LoadTranslationsFromFile() {
        string asmLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
        string dir = Path.GetDirectoryName(asmLocation);
        // look for file at Mods\Config\Mod Settings\BillsOnline\translate.json
        string configDir = Path.Combine(Path.Combine(Path.Combine(dir, "Config"), "Mod Settings"), "BillsOnline");
        string file = Path.Combine(configDir, "translate.json");

        // Attempt to locate translations file; do not spam console on normal load
        if (!File.Exists(file)) {
            return;
        }

        string json = File.ReadAllText(file);
        // Simple regex for "key": "value" pairs
        Regex rx = new Regex("\"(.*?)\"\\s*:\\s*\"(.*?)\"", RegexOptions.Singleline);
        MatchCollection matches = rx.Matches(json);
        foreach (Match m in matches) {
            try {
                string key = Regex.Unescape(m.Groups[1].Value);
                string val = Regex.Unescape(m.Groups[2].Value);
                if (translations == null) translations = new Dictionary<string, string>();
                translations[key] = val;
            }
            catch {
                // ignore invalid entry
            }
        }

        // parsed silently

        // build normalized map for tolerant searches
        normalizedTranslations = new Dictionary<string, string>();
        if (translations != null) {
            foreach (var kv in translations) {
                string nk = NormalizeKey(kv.Key);
                if (!string.IsNullOrEmpty(nk)) {
                    normalizedTranslations[nk] = kv.Value;
                }
            }
            // loaded silently
        }
    }
}
