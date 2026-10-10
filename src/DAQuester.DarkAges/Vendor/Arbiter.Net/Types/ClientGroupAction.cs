namespace Arbiter.Net.Types;

public enum ClientGroupAction : byte
{
    Invite = 1,
    Request = 2,
    Accept = 3,
    RecruitStart = 4,
    RecruitView = 5,
    RecruitStop = 6,
    RecruitJoin = 7,
}