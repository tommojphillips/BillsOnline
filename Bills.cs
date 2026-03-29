using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using HutongGames.PlayMaker;
using I386API;

namespace BillsOnline;

internal class Bills {

    private List<IBill> bills;

    private BillsState state;
    private int index;
    private Coroutine routine;

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

    private readonly string[] DAYS = new string[] { "Segunda", "Terça", "Quarta", "Quinta", "Sexta", "Sábado", "Domingo" };
    
    private const float SECONDS_PER_MINUTE = 60f;
    private const float MINUTES_PER_HOUR = 60f;
    private const float HOURS_PER_DAY = 24;

    private const float SECONDS_PER_HOUR = SECONDS_PER_MINUTE * MINUTES_PER_HOUR; //  3600
    private const float SECONDS_PER_DAY = SECONDS_PER_HOUR * HOURS_PER_DAY;       // 86400

    public void load() {
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
        phoneBill1.load("Conta de Telefone da Casa", t1);

        Transform t2 = GameObject.Find("Systems/PhoneBills2").transform;
        PhoneBill phoneBill2 = new PhoneBill();
        phoneBill2.load("Conta de Telefone do Apartamento", t2);

        Transform t3 = GameObject.Find("Systems/ElectricityBills1").transform;
        ElectricityBill electricityBill1 = new ElectricityBill();
        electricityBill1.load("Conta de Eletricidade da Casa", t3);

        Transform t4 = GameObject.Find("Systems/ElectricityBills2").transform;
        ElectricityBill electricityBill2 = new ElectricityBill();
        electricityBill2.load("Conta de Eletricidade do Apartamento", t4);

        bills = new List<IBill>(4);
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
        I386.POS_WriteNewLine("                                  Contas Online");
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
                    weekString = $"Em {weekOffset} semana(s) em ";
                    dayString = dueDayName;
                }
                else if (daysLater == 7) {
                    weekString = $"Próxima semana em ";
                    dayString = dueDayName;
                }
                else {
                    weekString = "";
                    if (daysLater == 0)
                        dayString = "Hoje";
                    else if (daysLater == 1)
                        dayString = "Amanhã";
                    else
                        dayString = dueDayName;
                }

                switch (timeType2) {
                    case TimeType2._24H:
                        I386.POS_WriteNewLine($"{weekString}{dayString} at {dueHour}:{dueMinute:D2}");
                        break;
                    case TimeType2._12H:
                        string ampm = dueHour < 12 ? "AM" : "PM";
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
                I386.POS_WriteNewLine($"{dueHour}h {dueMinute:D2}m (tempo real)");
                break;

            case TimeType.GameTime:
                due *= REAL_TO_GAME;
                dueHour = (int)(due / SECONDS_PER_HOUR);
                dueMinute = (int)((due % SECONDS_PER_HOUR) / SECONDS_PER_MINUTE);
                I386.POS_WriteNewLine($"{dueHour}h {dueMinute:D2}m (tempo do jogo)");
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
        I386.POS_WriteNewLine($"   Descrição\t\t\t\t\t\tQuantidade\t\tPreço MK\t\tTotal MK\n");

        if (bill.isActive) {
            bill.view();

            if (I386.GetKeyDown(KeyCode.Space)) {
                state = BillsState.PaymentProcessing;
                return;
            }

            I386.POS_WriteNewLine("\n                                                                    [PAGAR AGORA] ");
            if (bill.isCutOff) {
                I386.POS_WriteNewLine("   Vencido");
            }
            else {
                I386.POS_Write("   Vencimento: ");
                printDueDate(bill.timeUntilCutOff);
            }
        }
        else {
            I386.POS_WriteNewLine("   Fatura não pronta");
            I386.POS_Write("   Vencimento: ");
            printDueDate(bill.timeUntilNextBill);
        }
    }
    private void viewNotConnected() {
        viewHeader();
        I386.POS_WriteNewLine("                                   Não Conectado");
        I386.POS_WriteNewLine("                           Pressione Espaço para Conectar");
        if (I386.GetKeyDown(KeyCode.Space)) {
            state = BillsState.Connect;
        }
    }
    private void viewConnect() {
        viewHeader();
        I386.POS_WriteNewLine("                                   Conectando...");
        connect();
    }
    private void viewPaymentProcessing() {
        viewHeader();
        I386.POS_WriteNewLine("                             Processando Pagamento...");
        processPayment();
    }
    private void viewPaymentSuccess() {
        viewHeader();
        I386.POS_WriteNewLine($"                             Pagamento Bem-sucedido");
        paymentSuccess();
    }
    private void viewPaymentFailed() {
        viewHeader();
        I386.POS_WriteNewLine($"                                Pagamento Falhou");
        paymentFailed();
    }

    private bool command_enter() {
        index = 0;
        state = BillsState.Connect;
        routine = null;
        reconnect = false;
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
}
