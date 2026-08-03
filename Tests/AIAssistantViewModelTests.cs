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
}
