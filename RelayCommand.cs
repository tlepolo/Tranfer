 public class RelayCommand<T> : ICommand
    {
        public event EventHandler CanExecuteChanged;

        private readonly Action<T> execute;
        private readonly Func<T, bool> canExecute;

        public RelayCommand(Action<T> execute, Func<T, bool> canExecute)
        {
            this.execute = execute;
            this.canExecute = canExecute;
        }

        public void RaiseCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }


        public bool CanExecute(object parameter)
        {
            if (canExecute == null)
            {
                return true;
            }
            return canExecute((T)parameter);
        }

        public void Execute(object parameter)
        {
            execute((T)parameter);
        }
    }
