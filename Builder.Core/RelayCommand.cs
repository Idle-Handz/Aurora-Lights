using System;
using System.Diagnostics;
using System.Windows.Input;

namespace Builder.Core;

public class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool> _canExecute;

    public event EventHandler CanExecuteChanged;

    public RelayCommand(Action execute)
        : this(execute, null)
    {
    }

    public RelayCommand(Action execute, Func<bool> canExecute)
    {
        if (execute == null)
        {
            throw new ArgumentNullException("execute");
        }

        _execute = execute;
        _canExecute = canExecute;
    }

    [DebuggerStepThrough]
    public bool CanExecute(object parameter)
    {
        if (_canExecute != null)
        {
            return _canExecute();
        }

        return true;
    }

    public void Execute(object parameter)
    {
        _execute();
    }

    public void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}

public class RelayCommand<T> : ICommand
{
    private readonly Action<T> _execute;
    private readonly Func<T, bool> _canExecute;

    public event EventHandler CanExecuteChanged;

    public RelayCommand(Action<T> execute)
        : this(execute, null)
    {
    }

    public RelayCommand(Action<T> execute, Func<T, bool> canExecute)
    {
        if (execute == null)
        {
            throw new ArgumentNullException("execute");
        }

        _execute = execute;
        _canExecute = canExecute;
    }

    public bool CanExecute(object parameter)
    {
        if (_canExecute != null)
        {
            return _canExecute((T)parameter);
        }

        return true;
    }

    public void Execute(object parameter)
    {
        _execute((T)parameter);
    }

    public void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
