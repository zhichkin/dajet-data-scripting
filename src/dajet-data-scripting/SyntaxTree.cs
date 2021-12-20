using DaJet.Metadata.Model;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using System;
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
        public T Ancestor<T>() where T : ISyntaxNode
        {
            Type ancestorType = typeof(T);
            
            ISyntaxNode ancestor = Parent;

            while (ancestor != null)
            {
                if (ancestor.GetType() != ancestorType)
                {
                    ancestor = ancestor.Parent;
                }
                else
                {
                    break;
                }
            }

            return (T)ancestor;
        }
        public T SelfOrAncestor<T>() where T : ISyntaxNode
        {
            if (this is T self)
            {
                return self;
            }
            return Ancestor<T>();
        }
        public ITableScopeProvider TableScopeProvider()
        {
            if (this is ITableScopeProvider self)
            {
                return self;
            }

            ISyntaxNode ancestor = Parent;

            while (ancestor != null)
            {
                if (ancestor is ITableScopeProvider)
                {
                    break;
                }
                
                ancestor = ancestor.Parent;
            }

            return (ITableScopeProvider)ancestor;
        }
    }
    public sealed class ScriptNode : SyntaxNode
    {
        //public DatabaseInfo Database { get; set; } // initial catalog = default database name
        public List<ISyntaxNode> Statements { get; } = new List<ISyntaxNode>();
    }
    public class StatementNode : SyntaxNode // SELECT => QuerySpecification | InsertSpecification | UpdateSpecification | DeleteSpecification
    {
        public Dictionary<string, ISyntaxNode> Tables { get; } = new Dictionary<string, ISyntaxNode>(); // TableNode | StatementNode
        public List<ISyntaxNode> Columns { get; } = new List<ISyntaxNode>(); // ColumnNode | FunctionNode | CastNode
        public WhereNode Where { get; set; }
        public TSqlFragment VisitContext { get; set; } // current query clause context provided during AST traversing
        // TODO: special property visitor plus to type visitors !?
        // TODO: EnterContext + ExitContext !?
    }
    
    
    
    public class SelectNode : SyntaxNode, ITableScopeProvider
    {
        public SelectNode() { Where.Parent = this; }
        public WhereNode Where { get; } = new WhereNode();
        public List<SyntaxNode> Columns { get; } = new List<SyntaxNode>();
        public List<SyntaxNode> Tables { get; } = new List<SyntaxNode>();
        public List<SyntaxNode> GetTableColumns()
        {
            throw new NotImplementedException();
        }
    }
    public sealed class TableNode : SyntaxNode // Документ.ПоступлениеТоваровУслуг => NamedTableReference
    {
        public string Name { get; set; }
        public string Alias { get; set; }
        public ApplicationObject ApplicationObject { get; set; }
    }
    public sealed class QueryNode : SelectNode
    {
        public string Alias { get; set; }
    }
    public sealed class JoinNode : SyntaxNode, ITableScopeProvider
    {
        public JoinNode() { Where.Parent = this; }
        public WhereNode Where { get; } = new WhereNode();
        public JoinType JoinType { get; set; } = JoinType.Inner;
        public List<SyntaxNode> Tables { get; } = new List<SyntaxNode>();
        public List<SyntaxNode> GetTableColumns()
        {
            throw new NotImplementedException();
        }
    }
    public sealed class ColumnNode : SyntaxNode // Т.Ссылка AS [Ссылка] => SelectScalarExpression
    {
        public string Name { get; set; } // alias of the SELECT element, ex. in SELECT statement
        public string Alias { get; set; }
    }
    public sealed class WhereNode : SyntaxNode
    {
        public List<SyntaxNode> Columns { get; } = new List<SyntaxNode>();
    }
    public sealed class FunctionNode : SyntaxNode // Т.Ссылка.type() => FunctionCall
    {
        public MetadataProperty MetadataProperty { get; set; }
    }
    public sealed class CastNode : SyntaxNode // CAST(Т.Ссылка AS [УТ.Документ.ПоступлениеТоваровУслуг]) => CastCall + UserDataTypeReference
    {
        public MetadataProperty MetadataProperty { get; set; } // source cast value
        public ApplicationObject ApplicationObject { get; set; } // target cast type
    }

    public enum JoinType
    {
        Inner = 0,
        Left  = 1,
        Right = 2,
        Full  = 3
    }

    public interface ITableScopeProvider
    {
        WhereNode Where { get; }
        List<SyntaxNode> Tables { get; }
        List<SyntaxNode> GetTableColumns();
    }
}