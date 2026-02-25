using HutongGames.PlayMaker;
using I386API;
using MSCLoader;
using UnityEngine;

namespace BillsOnline;

internal interface IBill {
    public string name { get; }
    public float timeUntilNextBill { get; }
    public float timeUntilCutOff { get; }
    public bool isActive { get; }
    public bool isCutOff { get; }

    public float getPrice();
    public void pay();
    public void view();
    public void SetLocalizationFunction(System.Func<string, string> localize);
}
internal class PhoneBill : IBill {
    public PlayMakerFSM fsm;
    private System.Func<string, string> localizeFunc;

    public FsmGameObject envelope;

    public FsmFloat localMinutes;
    public FsmFloat pricePerLocalMinute;
    public FsmFloat callsLocal;
    public FsmFloat pricePerLocalCall;

    public FsmFloat longMinutes;
    public FsmFloat pricePerLongMinute;
    public FsmFloat callsLong;
    public FsmFloat pricePerLongCall;

    public FsmFloat nextBill;
    public FsmFloat nextCutOff;

    public FsmFloat waitBill;
    public FsmFloat waitCutOff;

    private string _name;

    public string name => _name;
    public float timeUntilNextBill => nextBill.Value - waitBill.Value;
    public float timeUntilCutOff => nextCutOff.Value - waitCutOff.Value;
    public bool isActive => envelope.Value.activeInHierarchy;
    public bool isCutOff => timeUntilCutOff <= 0;

    public void load(string name, Transform transform) {
        _name = name;
        fsm = transform.GetPlayMaker("Data");

        localMinutes = fsm.GetVariable<FsmFloat>("Minutes");
        callsLocal = fsm.GetVariable<FsmFloat>("Connects");
        pricePerLocalMinute = fsm.GetVariable<FsmFloat>("PriceMKperMinute");
        pricePerLocalCall = fsm.GetVariable<FsmFloat>("PriceMKperConnect");

        longMinutes = fsm.GetVariable<FsmFloat>("MinutesLong");
        callsLong = fsm.GetVariable<FsmFloat>("ConnectsLong");
        pricePerLongMinute = fsm.GetVariable<FsmFloat>("PriceMKperMinuteLong");
        pricePerLongCall = fsm.GetVariable<FsmFloat>("PriceMKperConnectLong");

        nextCutOff = fsm.GetVariable<FsmFloat>("NextCutoff");
        nextBill = fsm.GetVariable<FsmFloat>("NextBill");

        waitCutOff = fsm.GetVariable<FsmFloat>("WaitCutoff");
        waitBill = fsm.GetVariable<FsmFloat>("WaitBill");

        envelope = fsm.GetVariable<FsmGameObject>("Bill");
    }

    public void SetLocalizationFunction(System.Func<string, string> localize) {
        localizeFunc = localize;
    }

    public float getPrice() {

        float priceLocalMinutes = localMinutes.Value * pricePerLocalMinute.Value;
        float priceLocalConnects = callsLocal.Value * pricePerLocalCall.Value;
        
        float priceLongMinutes = longMinutes.Value * pricePerLongMinute.Value;
        float priceLongConnects = callsLong.Value * pricePerLongCall.Value;

        return 128 + priceLocalMinutes + priceLocalConnects + priceLongMinutes + priceLongConnects;
    }

    public void pay() {
        fsm.SendEvent("GLOBALEVENT");
    }

    public void view() {
        string localize(string s) => localizeFunc?.Invoke(s) ?? s;

        I386.POS_WriteNewLine("++ " + localize("Basic subscription fee") + ":\t\t\t1\t\t\t\t128 MK\t\t\t128 MK");

        I386.POS_WriteNewLine($"++ {localize("Local calls (min):")}\t\t\t\t{Mathf.RoundToInt(localMinutes.Value)}\t\t\t\t{pricePerLocalMinute.Value.ToString("F2")} MK\t\t\t{(localMinutes.Value * pricePerLocalMinute.Value).ToString("F2")} MK");
        I386.POS_WriteNewLine($"++ {localize("Local calls (number):")}\t\t\t{Mathf.RoundToInt(callsLocal.Value)}\t\t\t\t{pricePerLocalCall.Value.ToString("F2")} MK\t\t\t{(callsLocal.Value * pricePerLocalCall.Value).ToString("F2")} MK");

        I386.POS_WriteNewLine($"++ {localize("Long distance calls (min):")}\t\t{Mathf.RoundToInt(longMinutes.Value)}\t\t\t\t{pricePerLongMinute.Value.ToString("F2")} MK\t\t\t{(longMinutes.Value * pricePerLongMinute.Value).ToString("F2")} MK");
        I386.POS_WriteNewLine($"++ {localize("Long distance calls (number):")}\t{Mathf.RoundToInt(callsLong.Value)}\t\t\t\t{pricePerLongCall.Value.ToString("F2")} MK\t\t\t{(callsLong.Value * pricePerLongCall.Value).ToString("F2")} MK");
        I386.POS_WriteNewLine($"\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t{(getPrice()).ToString("F2")} MK");
    }
}

internal class ElectricityBill : IBill {
    public PlayMakerFSM fsm;
    private System.Func<string, string> localizeFunc;

    public FsmGameObject envelope;

    public FsmFloat pricePerKWH;
    public FsmFloat unpaidBill;

    public FsmFloat nextBill;
    public FsmFloat nextCutOff;

    public FsmFloat waitBill;
    public FsmFloat waitCutOff;

    private string _name;

    public string name => _name;
    public float unpaidAmount => unpaidBill.Value;
    public float timeUntilNextBill => nextBill.Value - waitBill.Value;
    public float timeUntilCutOff => nextCutOff.Value - waitCutOff.Value;    
    public bool isActive => envelope.Value.activeInHierarchy;
    public bool isCutOff => timeUntilCutOff <= 0;

    public float KWH => unpaidBill.Value / pricePerKWH.Value;

    public void load(string name, Transform transform) {
        _name = name;
        fsm = transform.GetPlayMaker("Data");

        pricePerKWH = fsm.GetVariable<FsmFloat>("PriceMKperKWH");
        unpaidBill = fsm.GetVariable<FsmFloat>("UnpaidBills");

        nextCutOff = fsm.GetVariable<FsmFloat>("NextCutoff");
        nextBill = fsm.GetVariable<FsmFloat>("NextBill");

        waitCutOff = fsm.GetVariable<FsmFloat>("WaitCutoff");
        waitBill = fsm.GetVariable<FsmFloat>("WaitBill");

        envelope = fsm.GetVariable<FsmGameObject>("Bill");
    }

    public void SetLocalizationFunction(System.Func<string, string> localize) {
        localizeFunc = localize;
    }

    public float getPrice() {
        return unpaidBill.Value;
    }

    public void pay() {
        fsm.SendEvent("GLOBALEVENT");
    }

    public void view() {
        string localize(string s) => localizeFunc?.Invoke(s) ?? s;
        I386.POS_WriteNewLine($"++ {localize("Consumption")}\t\t\t\t\t\t{KWH.ToString("F2")} kWh\t\t{pricePerKWH.Value.ToString("F2")} MK\t\t\t{(getPrice()).ToString("F2")} MK\n");
    }
}
