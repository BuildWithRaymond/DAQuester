namespace Arbiter.Net.Types;

public enum MessageBoardAction : byte
{
    ListBoards = 1,
    ViewBoard = 2,
    ViewPost = 3,
    CreatePost = 4,
    DeletePost = 5,
    SendMail = 6,
    HighlightPost = 7,
}