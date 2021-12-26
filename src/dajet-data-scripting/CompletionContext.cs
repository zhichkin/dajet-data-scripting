using DaJet.Data.Scripting.SyntaxTree;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace DaJet.Data.Scripting
{
    internal sealed class CompletionContext
    {
        internal CompletionContext(int cursorOffset, int fragmentOffset, int fragmentLength)
        {
            CursorOffset = cursorOffset;
            FragmentOffset = fragmentOffset;
            FragmentLength = fragmentLength;
        }
        internal int CursorOffset { get; set; }
        internal int FragmentOffset { get; set; }
        internal int FragmentLength { get; set; }
        internal string Keyword { get; set; }
        internal SyntaxNode SyntaxNode { get; set; }
        internal TSqlFragment Fragment { get; set; }
    }
}