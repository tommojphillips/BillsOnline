using MSCLoader;

namespace BillsOnline;

public class GovOnlineFines : Mod {
    public override string ID => "BillsOnline";
    public override string Name => "Bills Online";
    public override string Author => "tommojphillips";
    public override string Version => "1.2";
    public override string Description => "Pay bills online!";
    public override Game SupportedGames => Game.MyWinterCar;

    internal static GovOnlineFines instance;

    private Bills bills;

    public override void ModSetup() {

        if (!ModLoader.IsModPresent("I386API")) {
            ModConsole.Error("[BillsOnline] I386 API required!");
            ModUI.ShowMessage("I386 API not installed.\nI386 API required!", Name);
            return;
        }

        SetupFunction(Setup.OnLoad, Mod_OnLoad);
    }
    private void Mod_OnLoad() {
        instance = this;
        bills = new Bills();
        bills.load();
    }

    // Helper to expose translation to other classes
    public string Localize(string s) {
        return bills != null ? bills.Localize(s) : s;
    }
}
