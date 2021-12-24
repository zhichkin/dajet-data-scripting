using DaJet.Data.Scripting.SyntaxTree;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using System;
using System.Collections.Generic;

namespace DaJet.Data.Scripting
{
    internal sealed class QualifiedJoinVisitor : ISyntaxTreeVisitor
    {
        private IScriptingService ScriptingService { get; }
        internal QualifiedJoinVisitor(IScriptingService scriptingService)
        {
            ScriptingService = scriptingService ?? throw new ArgumentNullException(nameof(scriptingService));
        }
        public IList<string> PriorityProperties { get { return new List<string>() { "FirstTableReference", "SecondTableReference" }; } }
        public ISyntaxNode Visit(TSqlFragment node, TSqlFragment parent, string sourceProperty, ISyntaxNode result)
        {
            QualifiedJoin join = node as QualifiedJoin;
            if (join == null) return result;

            StatementNode statement = result as StatementNode;
            if (statement == null) return result;

            statement.VisitContext = join; // set current visiting context
            // TODO: how to set VisitingContext to null when QualifiedJoin visiting scope is missed ???
            // TODO: EnterContext !?
            // TODO: ExitContext  !?

            return result;
        }
    }
}