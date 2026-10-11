using System.Collections.Concurrent;

namespace FlytExplorer.Shell;

// Runs tasks on a single STA thread — required for IFileOperation COM calls.
internal sealed class StaTaskScheduler : TaskScheduler, IDisposable
{
    private readonly BlockingCollection<Task> _queue = new();
    private readonly Thread _thread;

    internal StaTaskScheduler()
    {
        _thread = new Thread(WorkLoop)
        {
            Name = "STA-FileOperation",
            IsBackground = true
        };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
    }

    protected override IEnumerable<Task>? GetScheduledTasks() => _queue.ToArray();
    protected override void QueueTask(Task task) => _queue.Add(task);
    protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued) => false;

    private void WorkLoop()
    {
        foreach (var task in _queue.GetConsumingEnumerable())
            TryExecuteTask(task);
    }

    public void Dispose()
    {
        _queue.CompleteAdding();
        _thread.Join(TimeSpan.FromSeconds(5));
        _queue.Dispose();
    }
}
