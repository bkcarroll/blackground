using System;
using System.Threading;

namespace blackground.Startup;

public sealed class SingleInstance : IDisposable
{
    private const string MutexName = @"Global\blackground-7E2E1B6C-7B8D-4B2D-90BE-8A87F1B0A111";
    private const string EventName = @"Global\blackground-Activate-7E2E1B6C-7B8D-4B2D-90BE-8A87F1B0A111";

    private Mutex? _mutex;
    private EventWaitHandle? _activationEvent;
    private Thread? _watcher;
    private volatile bool _stopping;

    public bool IsFirstInstance { get; private set; }

    public event EventHandler? AnotherInstanceLaunched;

    public bool TryAcquire()
    {
        _mutex = new Mutex(initiallyOwned: false, name: MutexName, createdNew: out bool createdNew);
        IsFirstInstance = createdNew;

        // Whether or not we're first, we open/create the event handle so it always exists.
        _activationEvent = new EventWaitHandle(false, EventResetMode.AutoReset, EventName);

        if (createdNew)
        {
            _watcher = new Thread(WatcherLoop) { IsBackground = true, Name = "blackground-SingleInstance-Watcher" };
            _watcher.Start();
            return true;
        }

        // Notify the existing instance that another launch happened.
        try { _activationEvent.Set(); } catch { }
        return false;
    }

    private void WatcherLoop()
    {
        while (!_stopping)
        {
            try
            {
                if (_activationEvent is null) break;
                if (_activationEvent.WaitOne(500))
                {
                    if (_stopping) break;
                    AnotherInstanceLaunched?.Invoke(this, EventArgs.Empty);
                }
            }
            catch
            {
                break;
            }
        }
    }

    public void Dispose()
    {
        _stopping = true;
        try { _activationEvent?.Set(); } catch { }
        try { _watcher?.Join(1000); } catch { }
        _activationEvent?.Dispose();
        // Note: the mutex was opened with initiallyOwned: false and never WaitOne'd,
        // so we don't own it and must not call ReleaseMutex (it would always throw).
        _mutex?.Dispose();
    }
}
