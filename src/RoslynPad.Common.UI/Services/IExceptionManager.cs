namespace RoslynPad.UI
{
    using System;

    public interface IExceptionManager
    {
        Exception? LastError { get; }

        event Action LastErrorChanged;

        void ClearLastError();

        void ReportError(Exception exception);
    }
}
