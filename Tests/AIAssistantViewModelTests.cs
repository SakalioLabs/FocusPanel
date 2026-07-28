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

        await viewModel.SendCommand.ExecuteAsync(null);

        Assert.Equal(0, assistant.CallCount);
        Assert.True(viewModel.IsSettingsOpen);
        Assert.Contains("API Key", viewModel.StatusText);
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
        public bool HasApiKey =>
            !string.IsNullOrWhiteSpace(ApiKey);
        public string Model { get; private set; } =
            AiSettingsService.DefaultModel;

        public string? LoadApiKey() => ApiKey;

        public void Save(string apiKey, string model)
        {
            if (!string.IsNullOrWhiteSpace(apiKey))
                ApiKey = apiKey;
            Model = model;
        }

        public void ClearApiKey() => ApiKey = null;
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
}
