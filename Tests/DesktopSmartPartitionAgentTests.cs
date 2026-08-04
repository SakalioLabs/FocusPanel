using System;
using System.Collections.Generic;
using FocusPanel.Models;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class DesktopSmartPartitionAgentTests
{
    [Theory]
    [InlineData("帮我智能分区这些文件", true)]
    [InlineData("重新整理收纳盒", true)]
    [InlineData("帮我继续把那二十几个应用程序图标分下区", true)]
    [InlineData("把这些图标分类", true)]
    [InlineData("继续整理这些桌面文件", true)]
    [InlineData("什么是磁盘分区", false)]
    [InlineData("帮我给硬盘分区", false)]
    [InlineData("帮我给硬盘分下区", false)]
    [InlineData("应用程序图标是什么", false)]
    [InlineData("帮我安排今天", false)]
    public void ChatIntent_OnlyMatchesExplicitOrganizerActions(
        string text,
        bool expected) =>
        Assert.Equal(
            expected,
            SmartPartitionAgentIntent.IsRequested(text));

    [Fact]
    public void ApplyPolicy_RejectsLockedSourceOrTargetAndStalePlan()
    {
        var assignment = new SmartPartitionAssignment(
            7,
            "报价.docx",
            "文档",
            "工作");
        var preference = new DesktopFilePreference
        {
            Id = 7,
            FilePath = "报价.docx",
            PartitionName = "文档",
            IsHiddenFromDesktop = true
        };

        Assert.True(
            OrganizerLayoutRepository.SmartPartitionApplyPolicy
                .CanApply(
                    assignment,
                    preference,
                    new HashSet<string>(
                        new[] { "文档", "工作" },
                        StringComparer.OrdinalIgnoreCase)));
        Assert.False(
            OrganizerLayoutRepository.SmartPartitionApplyPolicy
                .CanApply(
                    assignment,
                    preference,
                    new HashSet<string>(
                        new[] { "文档" },
                        StringComparer.OrdinalIgnoreCase)));

        preference.PartitionName = "其他";
        Assert.False(
            OrganizerLayoutRepository.SmartPartitionApplyPolicy
                .CanApply(
                    assignment,
                    preference,
                    new HashSet<string>(
                        new[] { "文档", "工作", "其他" },
                        StringComparer.OrdinalIgnoreCase)));
    }

    [Theory]
    [InlineData(0.67, false)]
    [InlineData(0.68, true)]
    [InlineData(0.95, true)]
    public void ConfidencePolicy_KeepsUncertainItemsInPlace(
        double confidence,
        bool expected)
    {
        Assert.Equal(
            expected,
            DesktopSmartPartitionAgent.ShouldApplyDecision(
                new AiDesktopPartitionDecision(
                    "工作",
                    confidence,
                    "测试理由")));
    }
}
