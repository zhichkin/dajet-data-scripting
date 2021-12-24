using System;
using System.Collections.Generic;

namespace DaJet.Data.Scripting.SyntaxTree
{
    public enum JoinType
    {
        Inner = 0,
        Left = 1,
        Right = 2,
        Full = 3
    }
    public sealed class JoinNode : SyntaxNode, ITableScopeProvider
    {
        public JoinNode() { Where.Parent = this; }
        public WhereNode Where { get; } = new WhereNode();
        public JoinType JoinType { get; set; } = JoinType.Inner;
        public List<SyntaxNode> Tables { get; } = new List<SyntaxNode>();
    }
}