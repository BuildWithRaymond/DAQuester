namespace DAQuester.App.ViewModels;

public sealed partial class MainWindowViewModel
{
    bool? aislingExchangeSupplies;

    public bool AislingExchangeSupplies
    {
        get => aislingExchangeSupplies ?? Runtime.Settings.AislingExchangeSupplies;
        set
        {
            if (value == AislingExchangeSupplies) return;
            aislingExchangeSupplies = value;
            Raise(nameof(AislingExchangeSupplies));
        }
    }
}
