using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CommandPaletteLLM;

internal interface ILlmClient
{
    Task<IReadOnlyList<string>> CompleteAsync(
        string prompt,
        int variations,
        CancellationToken cancellationToken);
}
