namespace FocusPanel.Services;

public readonly record struct AudioStatusSnapshot(
    bool IsAvailable,
    float MasterVolume,
    bool IsMuted)
{
    public static AudioStatusSnapshot Unavailable { get; } =
        new(false, 0f, false);
}
