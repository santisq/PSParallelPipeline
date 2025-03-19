using System.Collections.Generic;

namespace PSParallelPipeline;

internal sealed class TaskSettings(
    string script,
    Dictionary<string, object?> usingStatements)
{
    internal string Script { get; } = script;

    internal Dictionary<string, object?> UsingStatements { get; } = usingStatements;
}
