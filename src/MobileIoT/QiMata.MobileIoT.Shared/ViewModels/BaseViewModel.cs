using CommunityToolkit.Mvvm.ComponentModel;
using QiMata.MobileIoT.Shared.Services.Interfaces;
using System.Runtime.CompilerServices;

namespace QiMata.MobileIoT.Shared.ViewModels;

public abstract partial class BaseViewModel : ObservableObject, IDisposable
{
    /// <summary>
    /// Dispatch hook for marshalling work to the UI thread. Defaults to direct invocation
    /// (suitable for tests). The MAUI app overrides this at startup to use MainThread.
    /// </summary>
    public static Action<Action> DispatchToMain { get; set; } = static action => action();

    protected IAppLogger Logger { get; }
    private readonly List<Action> _unsubscribers = new();
    private bool _disposed;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _statusMessage;

    protected BaseViewModel(IAppLogger logger)
    {
        Logger = logger;
    }

    protected void Subscribe<TArgs>(
        Action<EventHandler<TArgs>> add,
        Action<EventHandler<TArgs>> remove,
        EventHandler<TArgs> handler)
    {
        add(handler);
        _unsubscribers.Add(() => remove(handler));
    }

    protected void Subscribe(
        Action<EventHandler> add,
        Action<EventHandler> remove,
        EventHandler handler)
    {
        add(handler);
        _unsubscribers.Add(() => remove(handler));
    }

    protected void Track(Action cleanup) => _unsubscribers.Add(cleanup);

    protected static void OnMain(Action action) => DispatchToMain(action);

    protected async Task RunSafeAsync(Func<Task> work, [CallerMemberName] string? caller = null)
    {
        try
        {
            await work().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logger.Error($"{caller ?? "operation"} failed", ex);
        }
    }

    protected void FireAndForget(Func<Task> work, [CallerMemberName] string? caller = null)
    {
        _ = RunSafeAsync(work, caller);
    }

    public virtual void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        for (int i = _unsubscribers.Count - 1; i >= 0; i--)
        {
            try { _unsubscribers[i](); }
            catch (Exception ex) { Logger.Warn("Cleanup failed during Dispose", ex); }
        }
        _unsubscribers.Clear();

        GC.SuppressFinalize(this);
    }
}
