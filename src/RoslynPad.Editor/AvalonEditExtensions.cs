namespace RoslynPad.Editor
{
    using ICSharpCode.AvalonEdit.CodeCompletion;

    public static class AvalonEditExtensions
    {
        public static bool IsOpen(this CompletionWindowBase window) => window?.IsVisible == true;
    }
}
