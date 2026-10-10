namespace Arbiter.Net.Types;

public enum LoginResult : byte
{
    Success = 0,
    InvalidName = 3,
    NameTaken = 4,
    InvalidPassword = 5,
    CharacterNotFound = 14,
    IncorrectPassword = 15
}