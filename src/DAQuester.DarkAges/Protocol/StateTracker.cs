using DAQuester.Core;
using Arbiter.Net.Server;
using Arbiter.Net.Server.Messages;
using Arbiter.Net.Server.Types;
using Arbiter.Net.Types;
using System.Collections.Immutable;

namespace DAQuester.DarkAges.Protocol;

public class StateTracker(long generation)
{
    public CharacterSnapshot Snapshot
    {
        get; private set;
    } = new()
    {
        Generation = generation
    };
    public ushort Width
    {
        get; private set;
    }
    public ushort Height
    {
        get; private set;
    }
    public ServerFieldMapMessage? WorldMap
    {
        get; private set;
    }
    public long LastMovementRevision
    {
        get; private set;
    }
    public long LastPositionRevision
    {
        get; private set;
    }

    public event Action<CharacterSnapshot>? Changed;
    public event Action<byte, byte[]>? Observed;
    // Active messages (0x0A type 3) state action outcomes exactly ("You received 20,000 gold coins."); every other type is chat or popup.
    public event Action<byte, string>? ServerMessage;
    public static bool IsActiveMessage(byte messageType) => messageType == 3;
    public void Identity(string name, uint id) => Publish(Snapshot with { Name = name, Id = id, Connected = true, Appearance = Snapshot.Id == id && Snapshot.Name == name ? Snapshot.Appearance : null, Motion = Snapshot.Id == id && Snapshot.Name == name ? Snapshot.Motion : null });
    public void Disconnect() => Publish(Snapshot with { Connected = false, Effects = ImmutableDictionary<ushort, byte>.Empty, Appearance = null, Motion = null, Dialog = null, InventoryKnown = false, SpellsKnown = false, Class = null, DisplayClass = null, IsMaster = null, IsPure = null, Mana = null, LegendsKnown = false, Notice = null, Exchange = null, GroundItems = ImmutableDictionary<uint, GroundItem>.Empty });
    void Publish(CharacterSnapshot s)
    {
        Snapshot = s with
        {
            Revision = Snapshot.Revision + 1
        };
        Changed?.Invoke(Snapshot);
    }

    Exchange? Trade(Exchange? current, ServerExchangeMessage p, long rev)
    {
        if (p.Event == ExchangeServerEventType.Started)
            return new(generation, rev, p.TargetId ?? 0, p.TargetName ?? "");
        if (current is null || current.Cancelled)
            return current;
        bool own = p.Party == ExchangeParty.You;
        return p.Event switch
        {
            ExchangeServerEventType.ItemAdded => current with { Items = current.Items.Add(new((byte)(p.Party ?? ExchangeParty.Them), p.ItemName ?? "")) },
            ExchangeServerEventType.GoldAdded => own ? current with { OwnGold = p.GoldAmount ?? 0 } : current with { PartnerGold = p.GoldAmount ?? 0 },
            ExchangeServerEventType.Accepted => own ? current with { OwnAccepted = true, Message = p.Message } : current with { PartnerAccepted = true, Message = p.Message },
            ExchangeServerEventType.Cancelled => current with { Cancelled = true, Message = p.Message },
            _ => current
        };
    }

    public void Observe(byte opcode, byte[] payload)
    {
        if (opcode is not (0x04 or 0x05 or 0x07 or 0x08 or 0x0A or 0x0B or 0x0C or 0x0E or 0x0F or 0x10 or 0x11 or 0x13 or 0x15 or 0x17 or 0x18 or 0x1A or 0x29 or 0x2C or 0x2D or 0x2E or 0x2F or 0x30 or 0x33 or 0x37 or 0x38 or 0x39 or 0x3A or 0x3F or 0x42 or 0x58))
        {
            // Not read into state, but still recorded.
            Observed?.Invoke(opcode, payload);
            return;
        }

        // Failed decodes stop automation upstream; raw gameplay still passes through.
        IServerMessage? m;
        try
        {
            m = ServerMessageFactory.Default.Create(new ServerPacket(opcode, payload));
        }
        catch (Exception) when (opcode is 0x13 or 0x29 or 0x3A or 0x3F)
        {
            Observed?.Invoke(opcode, payload);
            return; // Combat-only state; an odd one never stops a quest.
        }
        var s = Snapshot;
        long rev = s.Revision + 1;
        switch (m)
        {
            case ServerUserAppearanceMessage p:
                s = s with
                {
                    Id = p.UserId,
                    Connected = s.Name is not null,
                    Motion = new((byte)p.Direction, 0, DateTimeOffset.UtcNow, 0, false)
                };
                break;
            case ServerStatusMessage p:
                s = s with
                {
                    Level = p.Level ?? s.Level,
                    Gold = p.Gold ?? s.Gold,
                    Experience = p.TotalExperience ?? s.Experience,
                    AbilityLevel = p.AbilityLevel ?? s.AbilityLevel,
                    ToNextLevel = p.ToNextLevel ?? s.ToNextLevel,
                    Hp = p.Health ?? s.Hp,
                    MaxHp = p.MaxHealth ?? s.MaxHp,
                    Mana = p.Mana ?? s.Mana,
                    Weight = p.Weight ?? s.Weight,
                    MaxWeight = p.MaxWeight ?? s.MaxWeight,
                    Stats = p.Strength is { } str ? new(str, p.Intelligence ?? 0, p.Wisdom ?? 0, p.Constitution ?? 0, p.Dexterity ?? 0, p.StatPoints ?? 0) : s.Stats
                };
                if (s.GoldCapAt is { } cap && s.Gold < cap)
                    s = s with
                    {
                        GoldCapAt = null
                    };
                break;
            case ServerSystemMessage p:
                var notice = new ServerNotice(generation, rev, (byte)p.MessageType, p.Message, DateTimeOffset.UtcNow);
                s = s with
                {
                    Notice = MundaneBirthEvidence.Matches(notice) || MuisirEvidence.IsKirilNotice(notice, s.Map) || EmeraldDawnEvidence.IsNotice(notice, s.Map) || SickChildEvidence.IsNotice(notice, s.Map) || AltarWorshipEvidence.IsNotice(notice, s.Map) ? notice : null
                };
                if (IsActiveMessage((byte)p.MessageType))
                {
                    s = CombatReply(s, p.Message, rev);
                    ServerMessage?.Invoke((byte)p.MessageType, p.Message);
                }
                break;
            case ServerAddInventoryMessage p:
                s = s with
                {
                    Inventory = s.Inventory.SetItem(p.Slot, new(p.Slot, p.Name, p.Quantity)
                    {
                        Stackable = p.IsStackable,
                        Sprite = p.Sprite
                    })
                };
                break;
            case ServerRemoveInventoryMessage p:
                s = s with
                {
                    Inventory = s.Inventory.Remove(p.Slot)
                };
                break;
            case ServerAddEquipMessage p:
                s = s with
                {
                    Equipment = s.Equipment.SetItem((byte)p.Slot, p.Name)
                };
                break;
            case ServerRemoveEquipMessage p:
                s = s with
                {
                    Equipment = s.Equipment.Remove((byte)p.Slot)
                };
                break;
            case ServerAddSkillMessage p:
                s = s with
                {
                    Skills = s.Skills.SetItem(p.Slot, p.Name)
                };
                break;
            case ServerRemoveSkillMessage p:
                s = s with
                {
                    Skills = s.Skills.Remove(p.Slot)
                };
                break;
            case ServerAddSpellMessage p:
                s = s with
                {
                    Spells = s.Spells.SetItem(p.Slot, p.Name)
                };
                break;
            case ServerRemoveSpellMessage p:
                s = s with
                {
                    Spells = s.Spells.Remove(p.Slot)
                };
                break;
            case ServerSelfLookMessage p:
                s = s with
                {
                    Legends = p.LegendMarks.Select(x => x.Text).ToImmutableArray(),
                    LegendsKnown = true,
                    LegendsRevision = rev,
                    Class = p.Class == CharacterClass.None ? null : p.Class.ToString(),
                    DisplayClass = p.DisplayClass,
                    IsMaster = p.ShowMasterMetadata || p.ShowAbilityMetadata || p.DisplayClass.Contains("Master", StringComparison.OrdinalIgnoreCase),
                    IsPure = p.DisplayClass.StartsWith("Pure ", StringComparison.OrdinalIgnoreCase) ? true : null
                };
                break;
            case ServerUserPositionMessage p:
                LastPositionRevision = rev;
                s = s with
                {
                    Position = new(p.X, p.Y)
                };
                break;
            case ServerMoveMessage p:
                LastMovementRevision = rev;
                var prev = new Tile(p.PreviousX, p.PreviousY);
                s = s with
                {
                    Position = GridStep(prev, (byte)p.Direction),
                    Motion = new((byte)p.Direction, 0, DateTimeOffset.UtcNow, 456, true)
                };
                break;
            case ServerMapSizeMessage p:
                Width = p.Width;
                Height = p.Height;
                WorldMap = null;
                // The client keeps an open popup across a same-map refresh (insignia discovery, 2026-10-11).
                s = s with
                {
                    Map = p.MapId,
                    MapName = p.Name,
                    Position = null,
                    Dialog = p.MapId == s.Map ? s.Dialog : null,
                    SkillsBlockedOnMap = p.MapId == s.Map && s.SkillsBlockedOnMap,
                    Entities = ImmutableDictionary<uint, Entity>.Empty,
                    GroundItems = ImmutableDictionary<uint, GroundItem>.Empty
                };
                break;
            case ServerDrawObjectsMessage p:
                foreach (var e in p.Entities)
                {
                    if (e is ServerItemEntity item)
                    {
                        s = s with
                        {
                            GroundItems = s.GroundItems.SetItem(e.Id, new(e.Id, item.Sprite, new(e.X, e.Y)))
                        };
                        continue;
                    }

                    var creature = e as ServerCreatureEntity;
                    var seen = s.Entities.GetValueOrDefault(e.Id);
                    s = s with
                    {
                        Entities = s.Entities.SetItem(e.Id, new(e.Id, creature?.Name ?? "", new(e.X, e.Y), creature is not null && creature.CreatureType != CreatureType.Passable)
                        {
                            Sprite = e.Sprite,
                            Kind = (byte)(creature?.CreatureType ?? CreatureType.Solid),
                            Direction = (byte)(creature?.Direction ?? 0),
                            LastStep = seen?.LastStep ?? default,
                            FirstSeen = seen?.FirstSeen ?? DateTimeOffset.UtcNow,
                            HealthPercent = seen?.HealthPercent ?? 100
                        })
                    };
                }

                break;
            case ServerDrawHumanObjectsMessage p:
                s = s with
                {
                    Entities = p.EntityId == s.Id ? s.Entities.Remove(p.EntityId) : s.Entities.SetItem(p.EntityId, new(p.EntityId, p.Name ?? "", new(p.X, p.Y), true)
                    {
                        IsPlayer = true
                    })
                };
                if (p.EntityId == s.Id)
                    s = s with
                    {
                        Appearance = CharacterAppearancePackets.Read(p),
                        Motion = new((byte)p.Direction, 0, DateTimeOffset.UtcNow, 0, false)
                    };
                break;
            case ServerChangeDirectionMessage p when p.EntityId == s.Id && (byte)p.Direction <= 3:
                s = s with
                {
                    Motion = new((byte)p.Direction, 0, DateTimeOffset.UtcNow, 0, false)
                };
                break;
            case ServerChangeDirectionMessage p when (byte)p.Direction <= 3 && s.Entities.TryGetValue(p.EntityId, out var turned):
                s = s with
                {
                    Entities = s.Entities.SetItem(p.EntityId, turned with
                    {
                        Direction = (byte)p.Direction
                    })
                };
                break;
            case ServerDamageEffectMessage p when s.Entities.TryGetValue(p.EntityId, out var struck):
                s = s with
                {
                    Entities = s.Entities.SetItem(p.EntityId, struck with
                    {
                        HealthPercent = p.Percent
                    })
                };
                break;
            case ServerEffectLayerMessage p:
                // 263/278: a killing blow; 268: our own target reflected an attack with asgall (Talos AnimationHandler).
                if (p.TargetAnimation is 263 or 278 && s.Entities.TryGetValue(p.TargetId, out var felled))
                    s = s with
                    {
                        Entities = s.Entities.SetItem(p.TargetId, felled with
                        {
                            HealthPercent = 0
                        })
                    };
                if (p.TargetAnimation == 268 && p.TargetId == s.Id && p.SourceId == s.Id)
                    s = s with
                    {
                        AsgallReflectRevision = rev
                    };
                break;
            case ServerSpelledMessage p:
                s = s with
                {
                    Effects = p.Duration == StatusEffectDuration.None ? s.Effects.Remove(p.Icon) : s.Effects.SetItem(p.Icon, (byte)p.Duration)
                };
                break;
            case ServerActionDelayMessage p when p.AbilityType == AbilityType.Skill && s.Skills.TryGetValue(p.Slot, out var cooled):
                s = s with
                {
                    SkillCooldowns = s.SkillCooldowns.SetItem(SkillDatabase.BaseName(cooled), new(DateTimeOffset.UtcNow, p.Seconds))
                };
                break;
            case ServerMotionMessage p when p.EntityId == s.Id:
                s = s with
                {
                    Motion = new(s.Motion?.Direction ?? 0, (byte)p.Animation, DateTimeOffset.UtcNow, p.Duration * 10, false)
                };
                break;
            case ServerRemoveObjectsMessage p:
                s = s with
                {
                    Entities = s.Entities.Remove(p.EntityId),
                    GroundItems = s.GroundItems.Remove(p.EntityId)
                };
                break;
            case ServerMoveObjectMessage p:
                if (p.EntityId == s.Id && (byte)p.Direction <= 3)
                    s = s with
                    {
                        Motion = new((byte)p.Direction, 0, DateTimeOffset.UtcNow, 456, true)
                    };
                if (s.Entities.TryGetValue(p.EntityId, out var entity))
                    s = s with
                    {
                        Entities = s.Entities.SetItem(p.EntityId, entity with
                        {
                            Position = GridStep(new(p.OriginX, p.OriginY), (byte)p.Direction),
                            Direction = (byte)p.Direction <= 3 ? (byte)p.Direction : entity.Direction,
                            LastStep = DateTimeOffset.UtcNow
                        })
                    };
                break;
            case ServerUserReadyMessage:
                s = s with
                {
                    InventoryKnown = true,
                    SpellsKnown = true
                };
                break;
            case ServerScreenMenuMessage p:
                if (p.Content?.Contains("items have been repaired", StringComparison.OrdinalIgnoreCase) == true)
                    s = s with
                    {
                        Durability = s.Durability.Clear()
                    };
                s = s with
                {
                    Dialog = new(generation, rev, p.EntityId ?? 0, (byte)p.EntityType, p.Name ?? "", p.Content ?? "", false, p.PursuitId ?? 0, 0, (byte)p.MenuType, p.MenuChoices.Select((x, i) => new DialogChoice(x.Text, x.PursuitId, (byte)(i + 1))).ToImmutableArray(), p.Prompt)
                    {
                        ObservedAt = DateTimeOffset.UtcNow,
                        Items = p.ItemChoices.Select(x => new ShopItem(x.Name, x.Price)).ToImmutableArray()
                    }
                };
                break;
            case ServerPursuitMessage p:
                s = s with
                {
                    Dialog = p.DialogType == DialogType.CloseDialog ? null : new(generation, rev, p.EntityId ?? 0, (byte)p.EntityType, p.Name ?? "", p.Content ?? "", true, p.PursuitId ?? 0, p.StepId ?? 0, (byte)p.DialogType, p.MenuChoices.Select((x, i) => new DialogChoice(x, p.PursuitId ?? 0, (byte)(i + 1))).ToImmutableArray(), p.InputPrompt, p.HasNextButton)
                    {
                        ObservedAt = DateTimeOffset.UtcNow
                    }
                };
                break;
            case ServerFieldMapMessage p:
                WorldMap = p;
                break;
            case ServerExchangeMessage p:
                s = s with
                {
                    Exchange = Trade(s.Exchange, p, rev)
                };
                break;
        }

        Publish(s);
        Observed?.Invoke(opcode, payload);
    }

    // Exact replies the basher reacts to (Talos ActiveMessageHandler).
    static CharacterSnapshot CombatReply(CharacterSnapshot s, string text, long rev) => text switch
    {
        "You can't use skills here." => s with { SkillsBlockedOnMap = true },
        "You can't attack." or "You cannot attack." => s with { CantAttackRevision = rev },
        "You can't move freely if you use it." or "You can't use the weapon when holding it." => s with { SkillRefusedRevision = rev },
        "You can't have more." => s with { BagFullRevision = rev },
        // Crypt stairs below the character's level: "You almost understand the secrets of this entrance."
        _ when text.Contains("secrets of this entrance", StringComparison.Ordinal) => s with { EntranceRefusedRevision = rev, EntranceRefusedText = text },
        _ when bagFull.IsMatch(text) => s with { BagFullRevision = rev },
        _ when durability.Match(text) is { Success: true } d && int.TryParse(d.Groups[2].Value, out int percent) => s with { Durability = s.Durability.SetItem(d.Groups[1].Value, percent) },
        // Sent when the pickup would cross the cap; a pile too big to take is not the cap itself.
        _ when goldCap.Match(text) is { Success: true } m && uint.TryParse(m.Groups[1].Value, out uint limit) && (s.Gold ?? 0) + 100_000u >= limit => s with { GoldCapAt = s.Gold },
        _ => s
    };
    static readonly System.Text.RegularExpressions.Regex goldCap = new(@"^You can't carry more that (\d+) gold\.$"), bagFull = new("([a-zA-Z0-9 ]+), You can't have more than [0-9]+"), durability = new(@"^The durability of (.+) is now (\d+)%$");

    public static Tile GridStep(Tile p, byte direction) => direction switch
    {
        0 => p with
        {
            Y = p.Y - 1
        },
        1 => p with
        {
            X = p.X + 1
        },
        2 => p with
        {
            Y = p.Y + 1
        },
        3 => p with
        {
            X = p.X - 1
        },
        _ => p
    };
}
