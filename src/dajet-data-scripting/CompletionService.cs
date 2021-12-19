using Microsoft.SqlServer.TransactSql.ScriptDom;
using System.Collections.Generic;

namespace DaJet.Data.Scripting
{
    internal sealed class CompletionService
    {
        private readonly IScriptingService ScriptingService;
        internal CompletionService(IScriptingService scripting)
        {
            ScriptingService = scripting;
        }
        internal List<CompletionItem> GetCompletionItems(TSqlFragment script, int offset)
        {
            List<CompletionItem> list = new List<CompletionItem>();

            CompletionContext context = GetCompletionContext(script, offset);

            if (context == null || context.Token == null)
            {
                return list;
            }

            if (context.Token.TokenType == TSqlTokenType.Dot ||
                context.Token.TokenType == TSqlTokenType.Variable || 
                context.Token.TokenType == TSqlTokenType.Identifier ||
                context.Token.TokenType == TSqlTokenType.QuotedIdentifier)
            {
                if (context.Keyword != null && context.Keyword.TokenType == TSqlTokenType.As)
                {
                    return list;
                }

                string identifier = context.Token.Text;
                
                TSqlParserToken token = TakeLeftOne(script.ScriptTokenStream, context.Token);

                while (token != null &&
                    (token.TokenType == TSqlTokenType.Dot ||
                    token.TokenType == TSqlTokenType.Identifier ||
                    token.TokenType == TSqlTokenType.QuotedIdentifier))
                {
                    identifier = token.Text + identifier;
                    token = TakeLeftOne(script.ScriptTokenStream, token);
                }

                token = TakeRightOne(script.ScriptTokenStream, context.Token);

                while (token != null &&
                    (token.TokenType == TSqlTokenType.Dot ||
                    token.TokenType == TSqlTokenType.Identifier ||
                    token.TokenType == TSqlTokenType.QuotedIdentifier))
                {
                    identifier += token.Text;
                    token = TakeRightOne(script.ScriptTokenStream, token);
                }

                list.Add(new CompletionItem($"{context.Keyword?.Text} = {identifier}"));
            }

            return list;
        }
        private TSqlParserToken TakeLeftOne(IList<TSqlParserToken> tokens, TSqlParserToken current)
        {
            if (tokens == null || tokens.Count == 0)
            {
                return null;
            }

            int index = tokens.IndexOf(current);
            
            if (index == 0)
            {
                return null;
            }

            return tokens[--index];
        }
        private TSqlParserToken TakeRightOne(IList<TSqlParserToken> tokens, TSqlParserToken current)
        {
            if (tokens == null || tokens.Count == 0)
            {
                return null;
            }

            int index = tokens.IndexOf(current);

            if (index == tokens.Count - 1)
            {
                return null;
            }

            return tokens[++index];
        }
        private bool IsFirstTokenToLeft(int cursorOffset, int tokenOffset, int tokenLength)
        {
            return (cursorOffset > tokenOffset) && ((tokenOffset + tokenLength) >= cursorOffset);
        }
        private CompletionContext GetCompletionContext(TSqlFragment script, int offset)
        {
            if (script == null || script.ScriptTokenStream == null || script.ScriptTokenStream.Count == 0)
            {
                return null;
            }

            int current = 0;
            TSqlParserToken token;
            CompletionContext context = new CompletionContext();

            while (current < script.ScriptTokenStream.Count)
            {
                token = script.ScriptTokenStream[current++]; // consume token

                if (token.IsKeyword())
                {
                    context.Keyword = token;
                }

                if (IsFirstTokenToLeft(offset, token.Offset, token.Text.Length))
                {
                    context.Token = token;
                    return context;
                }
            }

            return null;
        }
    }
}