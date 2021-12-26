using System.Collections.Generic;

namespace DaJet.Data.Scripting.SyntaxTree
{
    public interface ITableScopeProvider
    {
        WhereNode Where { get; }
        List<SyntaxNode> Tables { get; }
        public List<TableNode> GetTables()
        {
            List<TableNode> tables = new List<TableNode>();

            GetTables(tables, this);

            return tables;
        }
        private void GetTables(List<TableNode> tables, ITableScopeProvider scope)
        {
            foreach (SyntaxNode node in scope.Tables)
            {
                if (node is TableNode table)
                {
                    tables.Add(table);
                }
                else if (node is ITableScopeProvider provider) // QueryNode | JoinNode
                {
                    GetTables(tables, provider);
                }
            }
        }
    }
}