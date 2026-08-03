using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class AiDesktopPartitionServiceTests
{
    [Fact]
    public async Task ResolveAsync_SendsMetadataOnlyAndAcceptsAllowedPartition()
    {
        var assistant = new FakeAssistant(
            """{"assignments":[{"id":0,"partition":"工作"}]}""");
        var service = new AiDesktopPartitionService(
            new FakeSettings(true, "key"),
            assistant,
            new FakeCatalog("工作"));
        var item = new DesktopAutoOrganizeItem(
            "客户报价单.docx",
            @"C:\Users\someone\Desktop\客户报价单.docx",
            "Document");

        IReadOnlyDictionary<string, string> result =
            await service.ResolveAsync(new[] { item });

        Assert.Equal("工作", result[item.FullPath]);
        Assert.Contains("客户报价单.docx", assistant.Input);
        Assert.DoesNotContain("C:\\Users", assistant.Input);
        Assert.DoesNotContain("file content", assistant.Input);
    }

    [Fact]
    public async Task ResolveAsync_DisabledOrFailedReturnsLocalFallbackSignal()
    {
        var disabledAssistant = new FakeAssistant("invalid");
        var disabled = new AiDesktopPartitionService(
            new FakeSettings(false, "key"),
            disabledAssistant,
            new FakeCatalog("工作"));
        var item = new DesktopAutoOrganizeItem(
            "one.png",
            @"C:\Desktop\one.png",
            "Image");

        Assert.Empty(await disabled.ResolveAsync(new[] { item }));
        Assert.Equal(0, disabledAssistant.CallCount);

        var failed = new AiDesktopPartitionService(
            new FakeSettings(true, "key"),
            new ThrowingAssistant(),
            new FakeCatalog("工作"));
        Assert.Empty(await failed.ResolveAsync(new[] { item }));
        Assert.Equal(
            "图片",
            DesktopAutoOrganizePolicy.GetTargetPartition(item));
    }

    [Fact]
    public async Task ResolveAsync_ManualPartitionAlwaysWinsAndSkipsAi()
    {
        var assistant = new FakeAssistant(
            """{"assignments":[{"id":0,"partition":"工作"}]}""");
        var service = new AiDesktopPartitionService(
            new FakeSettings(true, "key"),
            assistant,
            new FakeCatalog("工作"));
        var item = new DesktopAutoOrganizeItem(
            "manual.txt",
            @"C:\Desktop\manual.txt",
            "Document",
            PreferredPartition: "我的分区");

        Assert.Empty(await service.ResolveAsync(new[] { item }));
        Assert.Equal(0, assistant.CallCount);
        Assert.Equal(
            "我的分区",
            DesktopAutoOrganizePolicy.GetTargetPartition(
                item with { AiPartition = "工作" }));
    }

    private sealed class FakeSettings : IAiSettingsService
    {
        private readonly bool _enabled;
        private readonly string? _key;
        internal FakeSettings(bool enabled, string? key)
        {
            _enabled = enabled;
            _key = key;
        }

        public Task<AiSettingsState> LoadStateAsync() =>
            Task.FromResult(new AiSettingsState(
                !string.IsNullOrWhiteSpace(_key),
                "deepseek-v4-flash",
                AiProvider.DeepSeek,
                _enabled));
        public Task<string?> LoadApiKeyAsync() =>
            Task.FromResult(_key);
        public Task<AiSettingsState> SaveAsync(
            string apiKey, string model, string provider,
            bool smartOrganizerEnabled) =>
            throw new NotSupportedException();
        public Task ClearApiKeyAsync() =>
            throw new NotSupportedException();
    }

    private sealed class FakeAssistant : IAiAssistantService
    {
        private readonly string _response;
        internal FakeAssistant(string response) =>
            _response = response;
        internal int CallCount { get; private set; }
        internal string Input { get; private set; } = string.Empty;
        public Task<string> CompleteAsync(
            string apiKey, string model, string instructions,
            string input, CancellationToken cancellationToken)
        {
            CallCount++;
            Input = input;
            return Task.FromResult(_response);
        }
    }

    private sealed class ThrowingAssistant : IAiAssistantService
    {
        public Task<string> CompleteAsync(
            string apiKey, string model, string instructions,
            string input, CancellationToken cancellationToken) =>
            Task.FromException<string>(
                new InvalidOperationException("offline"));
    }

    private sealed class FakeCatalog : IDesktopPartitionCatalog
    {
        private readonly string[] _names;
        internal FakeCatalog(params string[] names) =>
            _names = names;
        public IReadOnlyList<string> LoadPartitionNames() => _names;
    }
}
