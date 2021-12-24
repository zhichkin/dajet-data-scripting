using DaJet.Data.Scripting.SyntaxTree;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using System;
using System.Collections.Generic;

namespace DaJet.Data.Scripting
{
    internal sealed class QuerySpecificationVisitor : ISyntaxTreeVisitor
    {
        private IScriptingService ScriptingService { get; }
        internal QuerySpecificationVisitor(IScriptingService scriptingService)
        {
            ScriptingService = scriptingService ?? throw new ArgumentNullException(nameof(scriptingService));
        }
        public IList<string> PriorityProperties { get { return new List<string>() { "FromClause" }; } }
        public ISyntaxNode Visit(TSqlFragment node, TSqlFragment parent, string sourceProperty, ISyntaxNode result)
        {
            QuerySpecification specification = node as QuerySpecification;
            if (specification == null) return result;

            StatementNode statement = new StatementNode()
            {
                Parent = result,
                Fragment = node,
                ParentFragment = parent,
                TargetProperty = sourceProperty
            };
            if (result is ScriptNode script)
            {
                if (parent is SelectStatement)
                {
                    script.Statements.Add(statement);
                }
            }
            else if (result is StatementNode query)
            {
                if (parent is TableReferenceWithAlias table)
                {
                    string alias = GetAlias(table);
                    query.Tables.Add(alias, statement);
                }
            }
            else
            {
                return result; // TODO: error ?
            }
            return statement;
        }
        private string GetAlias(TableReferenceWithAlias table)
        {
            if (table.Alias == null) // TODO: error ?
            {
                return string.Empty;
            }
            else
            {
                return table.Alias.Value;
            }
        }
    }
}