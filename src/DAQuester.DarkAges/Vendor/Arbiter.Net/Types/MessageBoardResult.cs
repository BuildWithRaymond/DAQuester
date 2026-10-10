namespace Arbiter.Net.Types;

public enum MessageBoardResult : byte
{
    BoardList = 1,
    Board = 2,
    Post = 3,
    Mailbox = 4,
    MailLetter = 5,
    PostSubmitted = 6,
    PostDeleted = 7,
    PostHighlighted = 8,
}