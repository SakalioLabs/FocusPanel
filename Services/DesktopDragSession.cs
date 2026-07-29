namespace FocusPanel.Services;

internal sealed class DesktopDragSession
{
    internal bool IsActive { get; private set; }
    internal bool IsOwnedByPanel { get; private set; }

    internal void Begin(bool ownedByPanel)
    {
        IsActive = true;
        IsOwnedByPanel |= ownedByPanel;
    }

    internal bool EndExternal()
    {
        if (!IsActive || IsOwnedByPanel)
            return false;

        End();
        return true;
    }

    internal void End()
    {
        IsActive = false;
        IsOwnedByPanel = false;
    }
}
