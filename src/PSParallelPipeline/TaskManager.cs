using System.Collections.Generic;
using System.Management.Automation.Runspaces;
using System.Threading.Tasks;

namespace PSParallelPipeline;

internal sealed class TaskManager
{
    private readonly List<Task<PSTask>> _tasks;

    private readonly Dictionary<int, Runspace> _assignedRunspaces;

    private readonly int _maxRunspaces;

    internal bool ShouldProcess { get => _tasks.Count == _maxRunspaces; }

    internal bool HasMoreTasks { get => _tasks.Count > 0; }

    internal TaskManager(int maxRunspaces)
    {
        _maxRunspaces = maxRunspaces;
        _tasks = new List<Task<PSTask>>(maxRunspaces);
        _assignedRunspaces = new Dictionary<int, Runspace>(maxRunspaces);
    }

    internal void Enqueue(PSTask psTask)
    {
        Task<PSTask> task = psTask.InvokeAsync();
        _assignedRunspaces[task.Id] = psTask.Runspace;
        _tasks.Add(task);
    }

    internal Task<Task<PSTask>> WhenAny() => Task.WhenAny(_tasks);

    internal Runspace Dequeue(Task<PSTask> psTask)
    {
        Runspace runspace = _assignedRunspaces[psTask.Id];
        _assignedRunspaces.Remove(psTask.Id);
        _tasks.Remove(psTask);
        return runspace;
    }
}
