using Arbiter.Net.Server.Messages;
using DAQuester.Core;

namespace DAQuester.DarkAges.Protocol;

// Retain sprite IDs and dyes as received. Item names are not appearance evidence.
public static class CharacterAppearancePackets
{
    public static CharacterAppearance Read(ServerDrawHumanObjectsMessage packet) => new(
        Head: packet.HeadSprite,
        Face: packet.FaceShape,
        Body: (byte)(packet.BodySprite ?? 0),
        HairColor: (byte)(packet.HairColor ?? 0),
        SkinColor: (byte)(packet.SkinColor ?? 0),
        Arms: packet.ArmsSprite ?? 0,
        Armor: packet.ArmorSprite ?? 0,
        PantsColor: (byte)(packet.PantsColor ?? 0),
        Boots: packet.BootsSprite ?? 0,
        BootsColor: (byte)(packet.BootsColor ?? 0),
        Weapon: packet.WeaponSprite ?? 0,
        Shield: packet.ShieldSprite ?? 0,
        Overcoat: packet.OvercoatSprite ?? 0,
        OvercoatColor: (byte)(packet.OvercoatColor ?? 0),
        Accessory1: packet.Accessory1Sprite ?? 0,
        Accessory1Color: (byte)(packet.Accessory1Color ?? 0),
        Accessory2: packet.Accessory2Sprite ?? 0,
        Accessory2Color: (byte)(packet.Accessory2Color ?? 0),
        Accessory3: packet.Accessory3Sprite ?? 0,
        Accessory3Color: (byte)(packet.Accessory3Color ?? 0),
        RestPosition: (byte)(packet.RestPosition ?? 0),
        Monster: packet.MonsterSprite,
        Hidden: packet.IsHidden,
        Translucent: packet.IsTranslucent);
}
