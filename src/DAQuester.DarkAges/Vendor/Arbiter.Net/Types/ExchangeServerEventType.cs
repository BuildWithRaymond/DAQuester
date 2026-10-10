namespace Arbiter.Net.Types;

public enum ExchangeServerEventType : byte
{
    Started = 0,
    QuantityPrompt = 1,
    ItemAdded = 2,
    GoldAdded = 3,
    Cancelled = 4,
    Accepted = 5,
}