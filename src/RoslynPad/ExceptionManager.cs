namespace RoslynPad
{
    using System;
    using System.Composition;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Threading;

    using RoslynPad.UI;

    [Export(typeof(IExceptionManager)), Shared]
    internal sealed class ExceptionManager : IExceptionManager
    {
        private Exception? _lastError;

        public ExceptionManager()
        {
            Application.Current.DispatcherUnhandledException += OnUnhandledDispatcherException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomainOnUnhandledException;
            TaskScheduler.UnobservedTaskException += TaskSchedulerOnUnobservedTaskException;
        }

        private void OnUnhandledDispatcherException(object sender, DispatcherUnhandledExceptionEventArgs args)
        {
            HandleException(args.Exception);
            args.Handled = true;
        }

        private void TaskSchedulerOnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs args)
        {
            HandleException(args.Exception.Flatten().InnerException);
        }

        private void CurrentDomainOnUnhandledException(object sender, UnhandledExceptionEventArgs args)
        {
            HandleException((Exception)args.ExceptionObject);
        }

        private void HandleException(Exception? exception)
        {
            if (exception is OperationCanceledException)
            {
                return;
            }

            LastError = exception;
        }

        public void ReportError(Exception exception)
        {
            HandleException(exception);
        }

        public Exception? LastError
        {
            get => _lastError;
            private set
            {
                _lastError = value;
                LastErrorChanged?.Invoke();
            }
        }

        public event Action? LastErrorChanged;

        public void ClearLastError()
        {
            LastError = null;
        }
    }
}
