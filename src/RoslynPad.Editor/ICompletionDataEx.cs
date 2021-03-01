namespace RoslynPad.Editor
{
    using ICSharpCode.AvalonEdit.CodeCompletion;

    public interface ICompletionDataEx : ICompletionData
    {
        bool IsSelected { get; }

        string SortText { get; }
    }

    public interface IOverloadProviderEx : IOverloadProvider
    {
        void Refresh();
    }
}
