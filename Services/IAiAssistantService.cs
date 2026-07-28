using System.Threading;
using System.Threading.Tasks;

namespace FocusPanel.Services;

public interface IAiAssistantService
{
    Task<string> CompleteAsync(
        string apiKey,
        string model,
        string instructions,
        string input,
        CancellationToken cancellationToken);
}
