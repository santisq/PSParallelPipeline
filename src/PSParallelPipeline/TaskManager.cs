using System.Collections.Generic;
using System.Threading.Tasks;

namespace PSParallelPipeline;

internal sealed class TaskManager
{
    private readonly List<Task<PSTask>> _tasks;

    private readonly int _maxRunspaces;

    internal bool HasMoreTasks { get => _tasks.Count > 0; }

    internal bool ShouldProcess { get => _tasks.Count == _maxRunspaces; }

    internal TaskManager(int maxRunspaces)
    {
        _maxRunspaces = maxRunspaces;
        _tasks = new List<Task<PSTask>>(maxRunspaces);
    }

    internal void Enqueue(PSTask psTask) => _tasks.Add(psTask.InvokeAsync());

    internal Task<Task<PSTask>> WhenAny() => Task.WhenAny(_tasks);

    internal void Remove(Task<PSTask> psTask) => _tasks.Remove(psTask);
}
