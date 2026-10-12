using DAQuester.Core;

namespace DAQuester.App.Controls;

// Chooses artwork only. The default outfit is never written to snapshots or saved accounts.
internal sealed record CharacterPreviewAppearance(CharacterAppearance Look, CharacterMotion? Motion, bool IsDefault, bool IsRemembered = false)
{
    // These IDs select the original head, shirt, pants and boots from the installed DAT files.
    static readonly CharacterAppearance DefaultOutfit = new(
        Head: 1, Face: 1, Body: 0x10,
        HairColor: 2, SkinColor: 0,
        Arms: 1, Armor: 1, PantsColor: 6,
        Boots: 1, BootsColor: 0,
        Weapon: 0, Shield: 0,
        Overcoat: 0, OvercoatColor: 0,
        Accessory1: 0, Accessory1Color: 0,
        Accessory2: 0, Accessory2Color: 0,
        Accessory3: 0, Accessory3Color: 0,
        RestPosition: 0, Monster: null,
        Hidden: false, Translucent: false);

    static readonly CharacterMotion DefaultIdle = new(1, 0, DateTimeOffset.UnixEpoch, 0, false);

    public static CharacterPreviewAppearance From(CharacterSnapshot? snapshot, CharacterAppearance? remembered = null)
    {
        if (snapshot is { Connected: true, Appearance: { } look })
            return new(look, snapshot.Motion, false);

        if (remembered is not null)
            return new(remembered, DefaultIdle, false, true);

        return new(DefaultOutfit, DefaultIdle, true);
    }
}
