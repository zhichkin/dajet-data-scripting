using DaJet.Data.Scripting.SyntaxTree;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using System;
using System.Collections.Generic;

namespace DaJet.Data.Scripting
{
    internal sealed class UpdateSpecificationVisitor : ISyntaxTreeVisitor
    {
        private IScriptingService ScriptingService { get; }
        internal UpdateSpecificationVisitor(IScriptingService scriptingService)
        {
            ScriptingService = scriptingService ?? throw new ArgumentNullException(nameof(scriptingService));
        }
        public IList<string> PriorityProperties { get { return new List<string>() { "Target", "FromClause", "SetClauses"  }; } }
        public ISyntaxNode Visit(TSqlFragment node, TSqlFragment parent, string sourceProperty, ISyntaxNode result)
        {
            UpdateSpecification update = node as UpdateSpecification;
            if (update == null) return result;

            StatementNode statement = new StatementNode()
            {
                Parent = result,
                Fragment = node,
                ParentFragment = parent,
                TargetProperty = sourceProperty
            };
            if (result is ScriptNode script)
            {
                if (parent is UpdateStatement)
                {
                    script.Statements.Add(statement);
                    return statement;
                }
            }
            return result;
        }
    }
}