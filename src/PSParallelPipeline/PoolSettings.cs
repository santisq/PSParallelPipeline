using System.Management.Automation.Runspaces;

namespace PSParallelPipeline;

internal readonly struct PoolSettings(
    int maxRunspaces,
    bool useNewRunspace,
    InitialSessionState initialSessionState)
{
    internal int MaxRunspaces { get; } = maxRunspaces;

    internal bool UseNewRunspace { get; } = useNewRunspace;

    internal InitialSessionState InitialSessionState { get; } = initialSessionState;
}
