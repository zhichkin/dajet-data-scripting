using Microsoft.SqlServer.TransactSql.ScriptDom;
using System;
using System.Collections.Generic;

namespace DaJet.Data.Scripting
{
    internal sealed class SelectElementVisitor : ISyntaxTreeVisitor
    {
        private IScriptingService ScriptingService { get; }
        internal SelectElementVisitor(IScriptingService scriptingService)
        {
            ScriptingService = scriptingService ?? throw new ArgumentNullException(nameof(scriptingService));
        }
        public IList<string> PriorityProperties { get { return null; } }

        public ISyntaxNode Visit(TSqlFragment node, TSqlFragment parent, string sourceProperty, ISyntaxNode result)
        {
            SelectElement element = node as SelectElement;
            if (element == null) return result;

            StatementNode statement = result as StatementNode;
            if (statement == null) return result;

            statement.VisitContext = element; // set current visiting context

            if (!(element is SelectScalarExpression expression)) return result;

            string columnName;
            if (expression.ColumnName == null)
            {
                columnName = string.Empty;
            }
            else
            {
                columnName = expression.ColumnName.Value;
            }

            statement.Columns.Add(new ColumnNode()
            {
                Parent = result,
                Fragment = node,
                ParentFragment = parent,
                TargetProperty = sourceProperty,
                Name = columnName
            });

            return result;
        }
    }
}