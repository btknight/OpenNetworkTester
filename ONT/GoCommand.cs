using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ONT
{

    /// <summary>
    /// A simple ICommand implementation.  Connects the Start button to the Flooder object to start the network testing threads.
    /// </summary>
    public class GoCommand : ICommand
    {
        private Action _action;
        public GoCommand(Action action, bool canExecute)
        {
            _action = action;
        }
        public event EventHandler CanExecuteChanged;
        public bool CanExecute(object parameter)
        {
            return true;
        }
        public void Execute(object parameter)
        {
            _action();
        }
    }
}
