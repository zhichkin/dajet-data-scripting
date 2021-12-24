using DaJet.Data.Scripting.SyntaxTree;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using System;
using System.Collections.Generic;

namespace DaJet.Data.Scripting
{
    internal sealed class WhereClauseVisitor : ISyntaxTreeVisitor
    {
        private IScriptingService ScriptingService { get; }
        internal WhereClauseVisitor(IScriptingService scriptingService)
        {
            ScriptingService = scriptingService ?? throw new ArgumentNullException(nameof(scriptingService));
        }
        public IList<string> PriorityProperties { get { return null; } }
        public ISyntaxNode Visit(TSqlFragment node, TSqlFragment parent, string sourceProperty, ISyntaxNode result)
        {
            WhereClause where = node as WhereClause;
            if (where == null) return result;

            StatementNode statement = result as StatementNode;
            if (statement == null) return result;

            statement.VisitContext = where; // set current visiting context

            return result;
        }
    }
}