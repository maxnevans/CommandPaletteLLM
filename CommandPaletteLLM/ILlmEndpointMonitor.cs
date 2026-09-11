using System;
using System.Threading;
using System.Threading.Tasks;

namespace CommandPaletteLLM;

internal enum LlmEndpointStatus
{
    Unknown,
    Available,
    Unavailable,
}

internal interface ILlmEndpointMonitor
{
    LlmEndpointStatus Status { get; }

    event Action? StatusChanged;

    Task<bool> CheckAsync(bool force, CancellationToken cancellationToken);

    void Invalidate();

    void ReportReachable();

    void ReportUnreachable();
}

internal sealed class AssumedAvailableEndpointMonitor : ILlmEndpointMonitor
{
    public LlmEndpointStatus Status => LlmEndpointStatus.Available;

    public event Action? StatusChanged
    {
        add { }
        remove { }
    }

    public Task<bool> CheckAsync(bool force, CancellationToken cancellationToken) =>
        Task.FromResult(true);

    public void Invalidate()
    {
    }

    public void ReportReachable()
    {
    }

    public void ReportUnreachable()
    {
    }
}
