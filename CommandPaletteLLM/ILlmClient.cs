using System.Threading;
using System.Threading.Tasks;

namespace CommandPaletteLLM;

internal interface ILlmClient
{
    Task<string> CompleteAsync(string prompt, CancellationToken cancellationToken);
}
