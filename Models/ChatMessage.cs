namespace LanCopy.Models;

internal sealed class ChatMessage
{
    public required string Sender { get; init; }
    public required string Text { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.Now;
    public bool IsOwn { get; init; }
}
