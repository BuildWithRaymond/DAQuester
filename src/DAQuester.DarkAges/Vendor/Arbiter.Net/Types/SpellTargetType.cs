namespace Arbiter.Net.Types;

public enum SpellTargetType : byte
{
    None = 0,
    Prompt = 1,
    Target = 2,
    PromptFourNumbers = 3,
    PromptThreeNumbers = 4,
    NoTarget = 5,
    PromptTwoNumbers = 6,
    PromptOneNumber = 7
}