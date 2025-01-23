using System.Collections.Generic;
using System.Threading;

namespace PSParallelPipeline;

internal record struct WorkerSettings(
    Dictionary<string, object?> UsingStatements,
    CancellationToken Token);
