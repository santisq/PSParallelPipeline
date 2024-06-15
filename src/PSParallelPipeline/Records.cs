using System.Collections.Generic;
using System.Management.Automation.Runspaces;

namespace PSParallelPipeline;

internal enum Type
{
    Success,
    Error
}

internal record struct PSOutputData(Type Type, object Output);

internal record struct PoolSettings(
    int MaxRunspaces,
    bool UseNewRunspace,
    InitialSessionState InitialSessionState,
    Dictionary<string, object?> UsingStatements);
