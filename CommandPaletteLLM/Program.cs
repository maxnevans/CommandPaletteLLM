using Microsoft.CommandPalette.Extensions;
using Shmuelie.WinRTServer;
using Shmuelie.WinRTServer.CsWinRT;
using System;
using System.Threading;

namespace CommandPaletteLLM;

public class Program
{
    [MTAThread]
    public static void Main(string[] args)
    {
        if (args.Length == 0 || args[0] != "-RegisterProcessAsComServer")
        {
            return;
        }

        ComServer server = new();
        ManualResetEvent extensionDisposedEvent = new(false);
        CommandPaletteLLM extension = new(extensionDisposedEvent);

        server.RegisterClass<CommandPaletteLLM, IExtension>(() => extension);
        server.Start();
        extensionDisposedEvent.WaitOne();
        server.Stop();
        server.UnsafeDispose();
    }
}
