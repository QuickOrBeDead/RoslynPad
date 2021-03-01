namespace RoslynPad.Editor
{
    using System.Threading.Tasks;

    public interface ICodeEditorCompletionProvider
    {
        Task<CompletionResult> GetCompletionData(int position, char? triggerChar, bool useSignatureHelp);
    }
}
