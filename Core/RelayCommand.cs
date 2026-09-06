using System;

namespace ClinicaLongevidadApp.Core
{
    public class RelayCommand : ClinicaLongevidadApp.Commands.RelayCommand
    {
        public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
            : base(execute, canExecute)
        {
        }
    }
}
