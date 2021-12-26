using System;
using System.Collections.Generic;

namespace DaJet.Data.Scripting.SyntaxTree
{
    public class SelectNode : SyntaxNode, ITableScopeProvider
    {
        public SelectNode() { Where.Parent = this; }
        public WhereNode Where { get; } = new WhereNode();
        public List<SyntaxNode> Tables { get; } = new List<SyntaxNode>();
        public List<SyntaxNode> Columns { get; } = new List<SyntaxNode>();
    }
    public sealed class QueryNode : SelectNode
    {
        public string Alias { get; set; }
    }
}