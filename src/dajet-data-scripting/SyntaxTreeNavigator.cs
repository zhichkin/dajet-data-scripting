﻿using DaJet.Metadata.Model;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using System.Collections.Generic;

namespace DaJet.Data.Scripting
{
    public interface ISyntaxNode
    {
        ISyntaxNode Parent { get; set; }
        TSqlFragment Fragment { get; set; } // fragment associated with this syntax node
        TSqlFragment ParentFragment { get; set; }
        string TargetProperty { get; set; } // property of the parent fragment which value is reference to this node's fragment
    }
    public abstract class SyntaxNode : ISyntaxNode
    {
        public ISyntaxNode Parent { get; set; }
        public TSqlFragment Fragment { get; set; }
        public TSqlFragment ParentFragment { get; set; }
        public string TargetProperty { get; set; }
    }
    internal sealed class ScriptNode : SyntaxNode
    {
        //public DatabaseInfo Database { get; set; } // initial catalog = default database name
        public List<ISyntaxNode> Statements { get; } = new List<ISyntaxNode>();
    }
    internal sealed class StatementNode : SyntaxNode // SELECT => QuerySpecification | InsertSpecification | UpdateSpecification | DeleteSpecification
    {
        public Dictionary<string, ISyntaxNode> Tables { get; } = new Dictionary<string, ISyntaxNode>(); // TableNode | StatementNode
        public List<ISyntaxNode> Columns { get; } = new List<ISyntaxNode>(); // ColumnNode | FunctionNode | CastNode
        public TSqlFragment VisitContext { get; set; } // current query clause context provided during AST traversing
        // TODO: special property visitor plus to type visitors !?
        // TODO: EnterContext + ExitContext !?
    }
    internal sealed class TableNode : SyntaxNode // Документ.ПоступлениеТоваровУслуг => NamedTableReference
    {
        public string Alias { get; set; }
        public ApplicationObject ApplicationObject { get; set; }
    }
    internal sealed class ColumnNode : SyntaxNode // Т.Ссылка AS [Ссылка] => SelectScalarExpression
    {
        public string Name { get; set; } // alias of the SELECT element, ex. in SELECT statement
    }
    internal sealed class FunctionNode : SyntaxNode // Т.Ссылка.type() => FunctionCall
    {
        public MetadataProperty MetadataProperty { get; set; }
    }
    internal sealed class CastNode : SyntaxNode // CAST(Т.Ссылка AS [УТ.Документ.ПоступлениеТоваровУслуг]) => CastCall + UserDataTypeReference
    {
        public MetadataProperty MetadataProperty { get; set; } // source cast value
        public ApplicationObject ApplicationObject { get; set; } // target cast type
    }
}