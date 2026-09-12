using Microsoft.CommandPalette.Extensions;
using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace CommandPaletteLLM;

#if DEVELOPMENT_PACKAGE
[Guid("78C00F8F-3B66-47CE-B809-35BC6912AD98")]
#else
[Guid("B22C475A-BDEE-4FFA-AF3D-915D4E86C56A")]
#endif
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
