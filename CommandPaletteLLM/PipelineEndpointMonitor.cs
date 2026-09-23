using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CommandPaletteLLM;

internal sealed class PipelineEndpointMonitor(ILlmEndpointMonitor[] monitors) : ILlmEndpointMonitor
{
    public LlmEndpointStatus Status => monitors.Any(monitor => monitor.Status == LlmEndpointStatus.Unavailable)
        ? LlmEndpointStatus.Unavailable
        : monitors.All(monitor => monitor.Status == LlmEndpointStatus.Available)
            ? LlmEndpointStatus.Available : LlmEndpointStatus.Unknown;

    public event Action? StatusChanged
    {
        add
        {
            foreach (var monitor in monitors)
            {
                monitor.StatusChanged += value;
            }
        }
        remove
        {
            foreach (var monitor in monitors)
            {
                monitor.StatusChanged -= value;
            }
        }
    }

    public async Task<bool> CheckAsync(bool force, CancellationToken cancellationToken) =>
        (await Task.WhenAll(monitors.Select(monitor => monitor.CheckAsync(force, cancellationToken)))
            .ConfigureAwait(false)).All(available => available);

    public void Invalidate()
    {
        foreach (var monitor in monitors)
        {
            monitor.Invalidate();
        }
    }

    // Individual clients report status to their own provider monitor.
    public void ReportReachable() { }

    public void ReportUnreachable() { }
}
