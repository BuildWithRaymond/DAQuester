namespace Arbiter.Net.Types;

public enum ExchangeClientActionType : byte
{
    BeginExchange = 0,
    AddItem = 1,
    AddStackableItem = 2,
    SetGold = 3,
    Cancel = 4,
    Accept = 5
}