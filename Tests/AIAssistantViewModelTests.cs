using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FocusPanel.Services;
using FocusPanel.ViewModels;
using Xunit;

namespace FocusPanel.Tests;

public sealed class AIAssistantViewModelTests
{
    [Fact]
    public async Task Send_DefaultDoesNotReadLocalContext()
    {
        var assistant = new FakeAssistant();
        var settings = new FakeSettings();
        var context = new FakeContext();
        using var viewModel =
            new AIAssistantViewModel(
                assistant,
                settings,
                context)
            {
                Prompt = "帮我安排今天"
            };
        await viewModel.InitializationTask;

        await viewModel.SendCommand.ExecuteAsync(null);

        Assert.Equal(0, context.BuildCount);
        Assert.Equal(2, viewModel.Messages.Count);
        Assert.True(viewModel.Messages[0].IsUser);
        Assert.False(viewModel.Messages[1].IsUser);
        Assert.Contains(
            "帮我安排今天",
            assistant.LastInput);
    }

    [Fact]
    public async Task Send_WithConsentAddsLocalSummary()
    {
        var assistant = new FakeAssistant();
        var context = new FakeContext();
        using var viewModel =
            new AIAssistantViewModel(
                assistant,
                new FakeSettings(),
                context)
            {
                Prompt = "给我建议",
                IncludeLocalContext = true
            };
        await viewModel.InitializationTask;

        await viewModel.SendCommand.ExecuteAsync(null);

        Assert.Equal(1, context.BuildCount);
        Assert.StartsWith(
            "本地授权摘要",
            assistant.LastInput);
    }

    [Fact]
    public async Task SendWithoutKeyOpensSettingsAndDoesNotCallApi()
    {
        var assistant = new FakeAssistant();
        using var viewModel =
            new AIAssistantViewModel(
                assistant,
                new FakeSettings { ApiKey = null },
                new FakeContext())
            {
                Prompt = "问题"
            };
        await viewModel.InitializationTask;

        await viewModel.SendCommand.ExecuteAsync(null);

        Assert.Equal(0, assistant.CallCount);
        Assert.True(viewModel.IsSettingsOpen);
        Assert.Contains("API Key", viewModel.StatusText);
    }

    [Fact]
    public async Task Constructor_DoesNotWaitForSettingsStorage()
    {
        var settings = new DelayedSettings();

        using var viewModel =
            new AIAssistantViewModel(
                new FakeAssistant(),
                settings,
                new FakeContext());

        Assert.False(viewModel.InitializationTask.IsCompleted);
        Assert.Equal("正在读取 AI 配置…", viewModel.StatusText);

        settings.Release();
        await viewModel.InitializationTask;

        Assert.True(viewModel.HasApiKey);
        Assert.Equal(
            "已就绪 · 对话仅在点击发送后联网",
            viewModel.StatusText);
    }

    [Fact]
    public async Task Send_WhenKeyReadFails_ReportsErrorWithoutCallingApi()
    {
        var assistant = new FakeAssistant();
        var settings = new FakeSettings
        {
            LoadApiKeyError =
                new InvalidOperationException("凭据不可读")
        };
        using var viewModel =
            new AIAssistantViewModel(
                assistant,
                settings,
                new FakeContext())
            {
                Prompt = "问题"
            };
        await viewModel.InitializationTask;

        await viewModel.SendCommand.ExecuteAsync(null);

        Assert.Equal(0, assistant.CallCount);
        Assert.True(viewModel.IsSettingsOpen);
        Assert.Contains("凭据不可读", viewModel.StatusText);
    }

    [Fact]
    public async Task ChatSmartPartition_CreatesPreviewWithoutImmediateMutation()
    {
        var agent = new FakeSmartPartitionAgent();
        using var viewModel = new AIAssistantViewModel(
            new FakeAssistant(),
            new FakeSettings(),
            new FakeContext(),
            agent)
        {
            Prompt = "帮我智能分区已收纳的项目"
        };
        await viewModel.InitializationTask;

        await viewModel.SendCommand.ExecuteAsync(null);

        Assert.Equal(1, agent.PlanCount);
        Assert.Equal(
            "帮我智能分区已收纳的项目",
            agent.LastInstruction);
        Assert.Equal(0, agent.ApplyCount);
        Assert.True(viewModel.HasPendingAgentAction);
        Assert.Contains("文档 → 工作", viewModel.AgentActionPreview);
    }

    [Fact]
    public async Task NaturalIconSortingPhrase_RoutesToPartitionAgent()
    {
        var agent = new FakeSmartPartitionAgent();
        using var viewModel = new AIAssistantViewModel(
            new FakeAssistant(),
            new FakeSettings(),
            new FakeContext(),
            agent)
        {
            Prompt = "帮我继续把那二十几个应用程序图标分下区"
        };
        await viewModel.InitializationTask;

        await viewModel.SendCommand.ExecuteAsync(null);

        Assert.Equal(1, agent.PlanCount);
        Assert.Equal(0, agent.ApplyCount);
        Assert.Equal(
            "帮我继续把那二十几个应用程序图标分下区",
            agent.LastInstruction);
        Assert.True(viewModel.HasPendingAgentAction);
    }

    [Fact]
    public async Task ConfirmedChatSmartPartition_AppliesSharedAgentPlan()
    {
        var agent = new FakeSmartPartitionAgent();
        using var viewModel = new AIAssistantViewModel(
            new FakeAssistant(),
            new FakeSettings(),
            new FakeContext(),
            agent)
        {
            Prompt = "重新整理收纳盒"
        };
        await viewModel.InitializationTask;
        await viewModel.SendCommand.ExecuteAsync(null);

        await viewModel.ApplyAgentActionCommand.ExecuteAsync(null);

        Assert.Equal(1, agent.ApplyCount);
        Assert.False(viewModel.HasPendingAgentAction);
        Assert.Contains("已重新分区", viewModel.Messages[^1].Content);
    }

    private sealed class FakeAssistant :
        IAiAssistantService
    {
        internal int CallCount { get; private set; }
        internal string LastInput { get; private set; } =
            string.Empty;

        public Task<string> CompleteAsync(
            string apiKey,
            string model,
            string instructions,
            string input,
            CancellationToken cancellationToken)
        {
            CallCount++;
            LastInput = input;
            return Task.FromResult("这是回答");
        }
    }

    private sealed class FakeSettings :
        IAiSettingsService
    {
        public string? ApiKey { get; set; } = "test-key";
        public Exception? LoadApiKeyError { get; set; }
        public string Model { get; private set; } =
            AiSettingsService.DefaultModel;

        public Task<AiSettingsState> LoadStateAsync() =>
            Task.FromResult(
                new AiSettingsState(
                    !string.IsNullOrWhiteSpace(ApiKey),
                    Model));

        public Task<string?> LoadApiKeyAsync() =>
            LoadApiKeyError == null
                ? Task.FromResult(ApiKey)
                : Task.FromException<string?>(
                    LoadApiKeyError);

        public Task<AiSettingsState> SaveAsync(
            string apiKey,
            string model,
            string provider,
            bool smartOrganizerEnabled)
        {
            if (!string.IsNullOrWhiteSpace(apiKey))
                ApiKey = apiKey;
            Model = model;
            return Task.FromResult(
                new AiSettingsState(
                    !string.IsNullOrWhiteSpace(ApiKey),
                    Model,
                    provider,
                    smartOrganizerEnabled));
        }

        public Task ClearApiKeyAsync()
        {
            ApiKey = null;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeContext :
        IAiLocalContextBuilder
    {
        internal int BuildCount { get; private set; }

        public Task<string> BuildAsync(
            CancellationToken cancellationToken)
        {
            BuildCount++;
            return Task.FromResult("本地授权摘要");
        }
    }

    private sealed class DelayedSettings :
        IAiSettingsService
    {
        private readonly TaskCompletionSource<AiSettingsState>
            _stateSource = new(
                TaskCreationOptions.RunContinuationsAsynchronously);

        internal void Release() =>
            _stateSource.TrySetResult(
                new AiSettingsState(
                    true,
                    AiSettingsService.DefaultModel));

        public Task<AiSettingsState> LoadStateAsync() =>
            _stateSource.Task;

        public Task<string?> LoadApiKeyAsync() =>
            Task.FromResult<string?>("test-key");

        public Task<AiSettingsState> SaveAsync(
            string apiKey,
            string model,
            string provider,
            bool smartOrganizerEnabled) =>
            Task.FromResult(
                new AiSettingsState(
                    true,
                    model,
                    provider,
                    smartOrganizerEnabled));

        public Task ClearApiKeyAsync() =>
            Task.CompletedTask;
    }

    private sealed class FakeSmartPartitionAgent :
        IDesktopSmartPartitionAgent
    {
        internal int PlanCount { get; private set; }
        internal int ApplyCount { get; private set; }
        internal string? LastInstruction { get; private set; }
        public event Action<int>? Applied;

        public Task<SmartPartitionPlan> CreatePlanAsync(
            string? userInstruction = null,
            CancellationToken cancellationToken = default)
        {
            PlanCount++;
            LastInstruction = userInstruction;
            return Task.FromResult(
                new SmartPartitionPlan(
                    new[]
                    {
                        new SmartPartitionAssignment(
                            1,
                            "报价.docx",
                            "文档",
                            "工作",
                            0.91,
                            "客户报价属于工作资料")
                    },
                    1,
                    "建议移动 1 个项目"));
        }

        public Task<int> ApplyAsync(
            SmartPartitionPlan plan,
            CancellationToken cancellationToken = default)
        {
            ApplyCount++;
            Applied?.Invoke(plan.Assignments.Count);
            return Task.FromResult(plan.Assignments.Count);
        }
    }
}
