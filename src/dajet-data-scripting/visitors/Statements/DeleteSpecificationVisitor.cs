using DaJet.Data.Scripting.SyntaxTree;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using System;
using System.Collections.Generic;

namespace DaJet.Data.Scripting
{
    internal sealed class DeleteSpecificationVisitor : ISyntaxTreeVisitor
    {
        private IScriptingService ScriptingService { get; }
        internal DeleteSpecificationVisitor(IScriptingService scriptingService)
        {
            ScriptingService = scriptingService ?? throw new ArgumentNullException(nameof(scriptingService));
        }
        public IList<string> PriorityProperties { get { return new List<string>() { "Target", "FromClause" }; } }
        public ISyntaxNode Visit(TSqlFragment node, TSqlFragment parent, string sourceProperty, ISyntaxNode result)
        {
            DeleteSpecification delete = node as DeleteSpecification;
            if (delete == null) return result;

            StatementNode statement = new StatementNode()
            {
                Parent = result,
                Fragment = node,
                ParentFragment = parent,
                TargetProperty = sourceProperty
            };
            if (result is ScriptNode script)
            {
                if (parent is DeleteStatement)
                {
                    script.Statements.Add(statement);
                    return statement;
                }
            }
            return result;
        }
    }
}