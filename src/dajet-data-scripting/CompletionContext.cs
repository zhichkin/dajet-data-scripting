using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace DaJet.Data.Scripting
{
    internal sealed class CompletionContext
    {
        internal TSqlParserToken Token { get; set; }
        internal TSqlParserToken Keyword { get; set; }
    }
}