namespace RoslynPadSample.Commands
{
    using System;
    using System.Threading.Tasks;
    using System.Windows.Input;

    public sealed class RelayCommand : ICommand
    {
        private readonly Action _executeAction;
        private readonly Func<Task> _executeAsyncAction;

        private readonly Func<object, bool> _canExecuteFunc;

        public RelayCommand(Action executeAction, Func<object, bool> canExecuteFunc = null)
        {
            _executeAction = executeAction ?? throw new ArgumentNullException(nameof(executeAction));
            _canExecuteFunc = canExecuteFunc;
        }

        public RelayCommand(Func<Task> executeAsyncAction, Func<object, bool> canExecuteFunc = null)
        {
            _executeAsyncAction = executeAsyncAction ?? throw new ArgumentNullException(nameof(executeAsyncAction));
            _canExecuteFunc = canExecuteFunc;
        }

        public bool CanExecute(object parameter)
        {
            return _canExecuteFunc == null || _canExecuteFunc.Invoke(parameter);
        }

        public void Execute(object parameter)
        {
            if (_executeAsyncAction != null)
            {
                _executeAsyncAction();
            }
            else
            {
                _executeAction();
            }
        }

        public event EventHandler CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
    }
}