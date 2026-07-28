using System;

namespace FocusPanel.Models;

public sealed record AiChatMessage(
    bool IsUser,
    string Content,
    DateTime CreatedAt)
{
    public string Sender => IsUser ? "你" : "Focus AI";
    public string TimeText => CreatedAt.ToString("HH:mm");
}
