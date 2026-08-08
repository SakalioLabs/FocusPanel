using System;
using System.Collections.Generic;
using System.Linq;
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

    [Fact]
    public async Task ResolveAsync_BatchesEveryEligibleItemPastEighty()
    {
        var assistant = new FakeAssistant(
            """{"assignments":[{"id":0,"partition":"工作"}]}""");
        var service = new AiDesktopPartitionService(
            new FakeSettings(true, "key"),
            assistant,
            new FakeCatalog("工作"));
        DesktopAutoOrganizeItem[] items =
            CreateItems(81);

        IReadOnlyDictionary<string, string> result =
            await service.ResolveAsync(items);

        Assert.Equal(2, assistant.CallCount);
        Assert.Equal(2, result.Count);
        Assert.Contains(items[0].FullPath, result.Keys);
        Assert.Contains(items[80].FullPath, result.Keys);
        Assert.DoesNotContain(
            @"C:\Desktop",
            string.Join("\n", assistant.Inputs));
    }

    [Fact]
    public async Task ResolveAsync_PreservesCompletedBatchWhenLaterBatchFails()
    {
        var assistant = new FailingSecondAssistant(
            """{"assignments":[{"id":0,"partition":"工作"}]}""");
        var service = new AiDesktopPartitionService(
            new FakeSettings(true, "key"),
            assistant,
            new FakeCatalog("工作"));
        DesktopAutoOrganizeItem[] items =
            CreateItems(81);

        IReadOnlyDictionary<string, string> result =
            await service.ResolveAsync(items);

        Assert.Equal(2, assistant.CallCount);
        Assert.Equal("工作", result[items[0].FullPath]);
        Assert.DoesNotContain(items[80].FullPath, result.Keys);
    }

    [Fact]
    public async Task ResolveExplicitAsync_BatchesEveryItemPastEighty()
    {
        var assistant = new FakeAssistant(
            """{"assignments":[{"id":0,"partition":"工作"}]}""");
        var service = new AiDesktopPartitionService(
            new FakeSettings(true, "key"),
            assistant,
            new FakeCatalog("工作"));
        DesktopAutoOrganizeItem[] items =
            CreateItems(81);

        IReadOnlyDictionary<string, string> result =
            await service.ResolveExplicitAsync(
                items,
                new[] { "工作" });

        Assert.Equal(2, assistant.CallCount);
        Assert.Contains(items[0].FullPath, result.Keys);
        Assert.Contains(items[80].FullPath, result.Keys);
    }

    [Fact]
    public async Task ExplicitPlan_SendsCurrentLayoutAndUserPreference()
    {
        var assistant = new FakeAssistant(
            """{"assignments":[{"id":0,"partition":"工作","confidence":0.92,"reason":"客户报价属于工作资料"}]}""");
        var service = new AiDesktopPartitionService(
            new FakeSettings(true, "key"),
            assistant,
            new FakeCatalog("文档", "工作"));
        var item = new DesktopAutoOrganizeItem(
            "客户报价单.docx",
            @"C:\Users\someone\Desktop\客户报价单.docx",
            "Document",
            CurrentPartition: "文档",
            SemanticHint: "报价工具");

        IReadOnlyDictionary<string, AiDesktopPartitionDecision> result =
            await service.ResolveExplicitPlanAsync(
                new[] { item },
                new[] { "文档", "工作" },
                "客户资料统一放到工作区");

        Assert.Equal("工作", result[item.FullPath].Partition);
        Assert.Equal(0.92, result[item.FullPath].Confidence, 2);
        Assert.Equal("客户报价属于工作资料", result[item.FullPath].Reason);
        Assert.Contains("currentPartition", assistant.Input);
        Assert.Contains("文档", assistant.Input);
        Assert.Contains("客户资料统一放到工作区", assistant.Input);
        Assert.Contains("报价工具", assistant.Input);
        Assert.DoesNotContain("C:\\Users", assistant.Input);
    }

    [Fact]
    public async Task ExplicitPlan_BatchesAllItemsInsteadOfDroppingAfterEighty()
    {
        var assistant = new FakeAssistant(
            """{"assignments":[{"id":0,"partition":"工作","confidence":0.9,"reason":"测试"}]}""");
        var service = new AiDesktopPartitionService(
            new FakeSettings(true, "key"),
            assistant,
            new FakeCatalog("文档", "工作"));
        var items = new List<DesktopAutoOrganizeItem>();
        for (int index = 0; index < 81; index++)
        {
            items.Add(new DesktopAutoOrganizeItem(
                $"项目{index}.txt",
                $@"C:\Desktop\项目{index}.txt",
                "Document",
                CurrentPartition: "文档"));
        }

        IReadOnlyDictionary<string, AiDesktopPartitionDecision> result =
            await service.ResolveExplicitPlanAsync(
                items,
                new[] { "文档", "工作" });

        Assert.Equal(2, assistant.CallCount);
        Assert.Equal(2, result.Count);
        Assert.Contains(items[0].FullPath, result.Keys);
        Assert.Contains(items[80].FullPath, result.Keys);
    }

    [Fact]
    public async Task ExplicitPlan_PreservesCompletedBatchWhenLaterBatchFails()
    {
        var assistant = new FailingSecondAssistant(
            """{"assignments":[{"id":0,"partition":"工作","confidence":0.9,"reason":"测试"}]}""");
        var service = new AiDesktopPartitionService(
            new FakeSettings(true, "key"),
            assistant,
            new FakeCatalog("文档", "工作"));
        DesktopAutoOrganizeItem[] items =
            CreateItems(
                81,
                currentPartition: "文档");

        IReadOnlyDictionary<string, AiDesktopPartitionDecision> result =
            await service.ResolveExplicitPlanAsync(
                items,
                new[] { "文档", "工作" });

        Assert.Equal(2, assistant.CallCount);
        Assert.Equal("工作", result[items[0].FullPath].Partition);
        Assert.DoesNotContain(items[80].FullPath, result.Keys);
    }

    [Fact]
    public void DecisionParser_RejectsUnknownPartitionAndInvalidConfidence()
    {
        var item = new DesktopAutoOrganizeItem(
            "one.txt",
            @"C:\Desktop\one.txt",
            "Document",
            CurrentPartition: "文档");

        IReadOnlyDictionary<string, AiDesktopPartitionDecision> unknown =
            AiDesktopPartitionService.ParseDecisionResponse(
                """{"assignments":[{"id":0,"partition":"不存在","confidence":0.9,"reason":"猜测"}]}""",
                new[] { item },
                new[] { "文档", "工作" });
        IReadOnlyDictionary<string, AiDesktopPartitionDecision> invalid =
            AiDesktopPartitionService.ParseDecisionResponse(
                """{"assignments":[{"id":0,"partition":"工作","confidence":"high","reason":"猜测"}]}""",
                new[] { item },
                new[] { "文档", "工作" });

        Assert.Empty(unknown);
        Assert.Empty(invalid);
    }

    private static DesktopAutoOrganizeItem[] CreateItems(
        int count,
        string? currentPartition = null) =>
        Enumerable.Range(0, count)
            .Select(index =>
                new DesktopAutoOrganizeItem(
                    $"项目{index}.txt",
                    $@"C:\Desktop\项目{index}.txt",
                    "Document",
                    CurrentPartition:
                        currentPartition))
            .ToArray();

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
        internal List<string> Inputs { get; } = new();
        public Task<string> CompleteAsync(
            string apiKey, string model, string instructions,
            string input, CancellationToken cancellationToken)
        {
            CallCount++;
            Input = input;
            Inputs.Add(input);
            return Task.FromResult(_response);
        }
    }

    private sealed class FailingSecondAssistant :
        IAiAssistantService
    {
        private readonly string _firstResponse;

        internal FailingSecondAssistant(
            string firstResponse) =>
            _firstResponse = firstResponse;

        internal int CallCount { get; private set; }

        public Task<string> CompleteAsync(
            string apiKey,
            string model,
            string instructions,
            string input,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return CallCount == 1
                ? Task.FromResult(_firstResponse)
                : Task.FromException<string>(
                    new InvalidOperationException(
                        "second batch failed"));
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
