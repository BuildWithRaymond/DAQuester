using System.Text;

namespace DAQuester.DarkAges.Protocol;

public sealed record LoginResponse(byte Result, string Message)
{
    public bool Denied => Result != 0 || Message.Contains("already", StringComparison.OrdinalIgnoreCase) && Message.Contains("log", StringComparison.OrdinalIgnoreCase);
    public static LoginResponse Parse(byte[] payload)
    {
        // Read the result and complete String8 message, as the protocol reader does.
        // Following protocol fields do not make a valid login response malformed.
        if (payload.Length < 2 || payload.Length < payload[1] + 2)
            return new(255, "Login response could not be confirmed. Review before trying again.");
        return new(payload[0], Encoding.ASCII.GetString(payload, 2, payload[1]));
    }
}
