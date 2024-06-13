using System;
using System.Collections.Concurrent;
using System.Management.Automation;

namespace PSParallelPipeline;

internal sealed class PSOutputStreams : IDisposable
{
    internal PSDataCollection<PSObject> Success { get; } = new();

    internal PSDataCollection<ErrorRecord> Error { get; } = new();

    private readonly BlockingCollection<PSOutputData> _outputPipe;

    internal PSOutputStreams(BlockingCollection<PSOutputData> outputPipe)
    {
        _outputPipe = outputPipe;
        SetStreams();
    }

    internal void SetStreams()
    {
        Success.DataAdded += (s, e) =>
        {
            foreach (PSObject data in Success.ReadAll())
            {
                _outputPipe.Add(new() { Type = Type.Success, Output = data });
            }
        };

        Error.DataAdded += (s, e) =>
        {
            foreach (ErrorRecord error in Error.ReadAll())
            {
                _outputPipe.Add(new() { Type = Type.Error, Output = error });
            }
        };
    }

    public void Dispose()
    {
        Success.Dispose();
        Error.Dispose();
        _outputPipe.Dispose();
        GC.SuppressFinalize(this);
    }
}
