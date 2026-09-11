using Microsoft.CommandPalette.Extensions;
using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace CommandPaletteLLM;

[Guid("B22C475A-BDEE-4FFA-AF3D-915D4E86C56A")]
public sealed partial class CommandPaletteLLM : IExtension, IDisposable
{
    private readonly ManualResetEvent _extensionDisposedEvent;
    private readonly CommandPaletteLLMCommandsProvider _provider = new();

    public CommandPaletteLLM(ManualResetEvent extensionDisposedEvent)
    {
        _extensionDisposedEvent = extensionDisposedEvent;
    }

    public object? GetProvider(ProviderType providerType) =>
        providerType == ProviderType.Commands ? _provider : null;

    public void Dispose() => _extensionDisposedEvent.Set();
}
