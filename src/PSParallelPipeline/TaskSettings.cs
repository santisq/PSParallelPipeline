using System.Collections.Generic;

namespace PSParallelPipeline;

internal record struct TaskSettings(
    string Script,
    Dictionary<string, object?> UsingStatements);
