using MSCLoader;

namespace BillsOnline;

public class GovOnlineFines : Mod {
    public override string ID => "BillsOnline";
    public override string Name => "Bills Online";
    public override string Author => "tommojphillips";
    public override string Version => "1.1.1";
    public override string Description => "Pay bills online!";
    public override Game SupportedGames => Game.MyWinterCar;

    public override void ModSetup() {

        SetupFunction(Setup.OnLoad, Mod_OnLoad);
    }
    private void Mod_OnLoad() {
        if (!ModLoader.IsModPresent("I386API")) {
            ModConsole.Error("[BillsOnline] I386 API required!");
            ModUI.ShowMessage("I386 API not installed.\nI386 API required!", Name);
            return;
        }

        Bills bills = new Bills();
        bills.load();
    }
}
