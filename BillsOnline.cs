using MSCLoader;

namespace BillsOnline;

public class GovOnlineFines : Mod {
    public override string ID => "BillsOnline_BR";
    public override string Name => "Contas Online";
    public override string Author => "tommojphillips&LucasMonOficial";
    public override string Version => "1.2";
    public override string Description => "Pague contas online!";
    public override Game SupportedGames => Game.MyWinterCar;

    public override void ModSetup() {

        SetupFunction(Setup.OnLoad, Mod_OnLoad);
    }
    private void Mod_OnLoad() {
        if (!ModLoader.IsModPresent("I386API")) {
            ModConsole.Error("[BillsOnline] I386 API obrigatória!");
            ModUI.ShowMessage("I386 API não instalada.\nI386 API obrigatória!", Name);
            return;
        }

        Bills bills = new Bills();
        bills.load();
    }
}
