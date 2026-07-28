using System.Threading;
using System.Threading.Tasks;
using System.Management.Automation;
using System.Management.Automation.Runspaces;

namespace PSParallelPipeline;

internal static class PSTask
{
    internal static async Task InvokeAsync(
        object? input,
        Runspace runspace,
        PSOutputStreams streams,
        TaskSettings settings,
        CancellationToken token)
    {
        using PowerShell powershell = PowerShell
            .Create()
            .WithStreams(streams)
            .AddInput(input)
            .AddScript(settings)
            .AddUsingStatements(settings);

        powershell.Runspace = runspace;

        using CancellationTokenRegistration _ = token.Register(() =>
        {
            powershell.BeginStop(null, null);
            runspace.Dispose();
        });

        await powershell
            .InvokeAsync(streams.Success)
            .NoContext();
    }
}
