using System;
using System.Threading;
using System.Threading.Tasks;

namespace FocusPanel.Services;

public sealed class AiAssistantRouter : IAiAssistantService, IDisposable
{
    private readonly IAiAssistantService _deepSeek;
    private readonly IAiAssistantService _openAi;

    public AiAssistantRouter()
        : this(
            new DeepSeekChatCompletionService(),
            new OpenAiResponsesService())
    {
    }

    internal AiAssistantRouter(
        IAiAssistantService deepSeek,
        IAiAssistantService openAi)
    {
        _deepSeek = deepSeek;
        _openAi = openAi;
    }

    public Task<string> CompleteAsync(
        string apiKey,
        string model,
        string instructions,
        string input,
        CancellationToken cancellationToken) =>
        Select(model).CompleteAsync(
            apiKey,
            model,
            instructions,
            input,
            cancellationToken);

    private IAiAssistantService Select(string model) =>
        model.StartsWith(
            "deepseek-",
            StringComparison.OrdinalIgnoreCase)
            ? _deepSeek
            : _openAi;

    public void Dispose()
    {
        if (_deepSeek is IDisposable deepSeek)
            deepSeek.Dispose();
        if (_openAi is IDisposable openAi)
            openAi.Dispose();
    }
}
