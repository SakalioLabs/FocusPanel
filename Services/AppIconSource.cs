using System.Windows.Media;
using FocusPanel.Helpers;

namespace FocusPanel.Services;

internal interface IAppIconSource
{
    ImageSource? Load(string iconKey);
}

internal sealed class WindowsAppIconSource : IAppIconSource
{
    public ImageSource? Load(string iconKey) =>
        IconHelper.GetIcon(iconKey);
}
