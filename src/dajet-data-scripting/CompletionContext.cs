using DaJet.Data.Scripting.SyntaxTree;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace DaJet.Data.Scripting
{
    internal sealed class CompletionContext
    {
        internal CompletionContext(ScriptNode scriptNode,
            int cursorOffset, int fragmentOffset, int fragmentLength,
            TSqlFragment fragment, SyntaxNode syntaxNode)
        {
            ScriptNode = scriptNode;
            CursorOffset = cursorOffset;
            FragmentOffset = fragmentOffset;
            FragmentLength = fragmentLength;
            Fragment = fragment;
            SyntaxNode = syntaxNode;
        }
        internal int CursorOffset { get; }
        internal int FragmentOffset { get; }
        internal int FragmentLength { get; }
        internal ScriptNode ScriptNode { get; }
        internal TSqlFragment Fragment { get; }
        internal SyntaxNode SyntaxNode { get; }
    }
}