using System.Windows;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class FocusDialogServiceTests
{
    [Theory]
    [InlineData(MessageBoxButton.OK, MessageBoxResult.OK)]
    [InlineData(MessageBoxButton.OKCancel, MessageBoxResult.Cancel)]
    [InlineData(MessageBoxButton.YesNo, MessageBoxResult.No)]
    [InlineData(MessageBoxButton.YesNoCancel, MessageBoxResult.Cancel)]
    public void DialogFailure_UsesNonDestructiveFallback(
        MessageBoxButton buttons,
        MessageBoxResult expected)
    {
        Assert.Equal(
            expected,
            FocusDialogService
                .GetSafeFallbackResult(buttons));
    }
}
