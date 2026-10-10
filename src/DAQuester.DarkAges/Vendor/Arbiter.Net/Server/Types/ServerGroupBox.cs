namespace Arbiter.Net.Server.Types;

public class ServerGroupBox
{
    public string Leader { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
    public byte MinLevel { get; set; }
    public byte MaxLevel { get; set; }
    public byte CurrentWarriors { get; set; }
    public byte MaxWarriors { get; set; }
    public byte CurrentWizards { get; set; }
    public byte MaxWizards { get; set; }
    public byte CurrentRogues { get; set; }
    public byte MaxRogues { get; set; }
    public byte CurrentPriests { get; set; }
    public byte MaxPriests { get; set; }
    public byte CurrentMonks { get; set; }
    public byte MaxMonks { get; set; }
}