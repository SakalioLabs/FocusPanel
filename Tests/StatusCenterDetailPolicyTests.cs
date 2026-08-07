using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class StatusCenterDetailPolicyTests
{
    [Theory]
    [InlineData(
        (int)StatusCenterDetail.None,
        (int)StatusCenterDetail.Network,
        (int)StatusCenterDetail.Network)]
    [InlineData(
        (int)StatusCenterDetail.ApplicationAudio,
        (int)StatusCenterDetail.Network,
        (int)StatusCenterDetail.Network)]
    [InlineData(
        (int)StatusCenterDetail.Network,
        (int)StatusCenterDetail.Network,
        (int)StatusCenterDetail.None)]
    [InlineData(
        (int)StatusCenterDetail.MediaAndBattery,
        (int)StatusCenterDetail.MediaAndBattery,
        (int)StatusCenterDetail.None)]
    public void Toggle_SelectsOneDetailOrClosesTheCurrentOne(
        int currentValue,
        int requestedValue,
        int expectedValue)
    {
        StatusCenterDetail current =
            (StatusCenterDetail)currentValue;
        StatusCenterDetail requested =
            (StatusCenterDetail)requestedValue;
        Assert.Equal(
            (StatusCenterDetail)expectedValue,
            StatusCenterDetailPolicy.Toggle(
                current,
                requested));
    }
}
