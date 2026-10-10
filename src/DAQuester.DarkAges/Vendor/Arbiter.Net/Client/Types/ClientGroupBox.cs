namespace Arbiter.Net.Client.Types;

public class ClientGroupBox
{
    public string Name { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;

    public byte MinLevel { get; set; }
    public byte MaxLevel { get; set; }

    public byte MaxWarriors { get; set; }
    public byte MaxWizards { get; set; }
    public byte MaxRogues { get; set; }
    public byte MaxPriests { get; set; }
    public byte MaxMonks { get; set; }
}