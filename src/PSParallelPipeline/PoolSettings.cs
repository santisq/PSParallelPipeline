using System.Collections.Generic;
using System.Management.Automation.Runspaces;

namespace PSParallelPipeline;

internal record struct PoolSettings(
    int MaxRunspaces,
    bool UseNewRunspace,
    InitialSessionState InitialSessionState,
    Dictionary<string, object?> UsingStatements);
