namespace Arbiter.Net.Types;

public enum DialogMenuType : byte
{
    Menu = 0,
    MenuWithArgs = 1,
    TextInput = 2,
    TextInputWithArgs = 3,
    ItemChoices = 4,
    UserInventory = 5,
    SpellChoices = 6,
    SkillChoices = 7,
    UserSpells = 8,
    UserSkills = 9
}