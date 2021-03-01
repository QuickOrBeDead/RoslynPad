namespace RoslynPad.Editor
{
    using ICSharpCode.AvalonEdit.Highlighting;

    public interface IClassificationHighlightColors
    {
        HighlightingColor DefaultBrush { get; }

        HighlightingColor GetBrush(string classificationTypeName);
    }
}
