using System.Windows.Input;

namespace NuGetExplorer.Infrastructure;

public sealed class AsyncRelayCommand : ICommand
{
    private readonly Func<object?, Task> _execute;
    private bool _isExecuting;
    public AsyncRelayCommand(Func<Task> execute) : this(_ => execute()) { }
    public AsyncRelayCommand(Func<object?, Task> execute) => _execute = execute;
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => !_isExecuting;
    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter)) return;
        _isExecuting = true;
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        try { await _execute(parameter); }
        finally { _isExecuting = false; CanExecuteChanged?.Invoke(this, EventArgs.Empty); }
    }
}
