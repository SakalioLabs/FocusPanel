using System;

namespace FocusPanel.Models;

public class FeishuApiException : Exception
{
    public int ErrorCode { get; }

    public FeishuApiException(int code, string message)
        : base($"Feishu API error [{code}]: {message}")
    {
        ErrorCode = code;
    }
}
