using DaJet.Data.Scripting.SyntaxTree;
using DaJet.Metadata.Model;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using System;
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
        internal List<CompletionItem> GetCompletionItems(TSqlFragment fragment, int offset)
        {
            CompletionContext context = GetCompletionContext(fragment, offset);

            if (context != null && context.SyntaxNode is TableNode table)
            {
                return GetTableCompletionItems(context, table.Name);
            }
            else if (context != null && context.SyntaxNode is ColumnNode column)
            {
                return GetColumnCompletionItems(context, column.Name);
            }

            if (fragment is TSqlScript script && script.Batches != null && script.Batches.Count == 0)
            {
                context = GetCompletionContextFromTokenStream(script, offset);

                if (context != null)
                {
                    return GetTableCompletionItems(context, ((TableNode)context.SyntaxNode).Name);
                }
            }

            return new List<CompletionItem>();
        }
        private string GetCompletionItemType(ApplicationObject item)
        {
            if (item is Catalog) return "Справочник";
            else if (item is Document) return "Документ";
            else if (item is InformationRegister) return "РегистрСведений";
            else if (item is AccumulationRegister) return "РегистрНакопления";
            else if (item is Publication) return "ПланОбмена";
            return string.Empty;
        }
        private CompletionContext GetCompletionContext(TSqlFragment script, int offset)
        {
            if (script == null || script.ScriptTokenStream == null || script.ScriptTokenStream.Count == 0)
            {
                return null;
            }

            SyntaxTreeBuilder builder = new SyntaxTreeBuilder()
            {
                CursorOffset = offset
            };
            builder.Build((TSqlScript)script, out SyntaxNode root);

            return new CompletionContext((ScriptNode)root,
                builder.CursorOffset,
                builder.FragmentOffset,
                builder.FragmentLength,
                builder.Fragment,
                builder.SyntaxNode);
        }

        private List<CompletionItem> GetTableCompletionItems(CompletionContext context, string tableIdentifier)
        {
            List<ApplicationObject> list = ScriptingService.MatchApplicationObjects(tableIdentifier);

            if (list.Count == 0)
            {
                return GetEntityTypeCompletionItems(context);
            }

            string itemType = GetCompletionItemType(list[0]);

            List<CompletionItem> suggestions = new List<CompletionItem>();

            foreach (ApplicationObject item in list)
            {
                suggestions.Add(new CompletionItem(item.Name, context.FragmentOffset, context.FragmentLength) { ItemType = itemType });
            }

            return suggestions;
        }
        private List<CompletionItem> GetEntityTypeCompletionItems(CompletionContext context)
        {
            List<CompletionItem> suggestions = new List<CompletionItem>();

            suggestions.Add(new CompletionItem("Справочник", context.FragmentOffset, context.FragmentLength) { ItemType = "Справочник" });
            suggestions.Add(new CompletionItem("Документ", context.FragmentOffset, context.FragmentLength) { ItemType = "Документ" });
            suggestions.Add(new CompletionItem("РегистрСведений", context.FragmentOffset, context.FragmentLength) { ItemType = "РегистрСведений" });
            suggestions.Add(new CompletionItem("РегистрНакопления", context.FragmentOffset, context.FragmentLength) { ItemType = "РегистрНакопления" });
            suggestions.Add(new CompletionItem("ПланОбмена", context.FragmentOffset, context.FragmentLength) { ItemType = "ПланОбмена" });

            return suggestions;
        }

        private List<CompletionItem> GetColumnCompletionItems(CompletionContext context, string columnIdentifier)
        {
            List<CompletionItem> suggestions = new List<CompletionItem>();

            List<TableNode> tables = context.SyntaxNode.TableScopeProvider().GetTables();
            if (tables.Count == 0)
            {
                return suggestions;
            }

            List<ApplicationObject> entities = new List<ApplicationObject>();

            foreach (TableNode table in tables)
            {
                ApplicationObject entity = ScriptingService.MatchApplicationObject(table.Name);
                if (entity != null)
                {
                    entities.Add(entity);
                }
            }

            if (entities.Count == 0)
            {
                return suggestions;
            }

            string[] names = columnIdentifier.Split('.', StringSplitOptions.RemoveEmptyEntries);
            string propertyName = names[names.Length - 1]; // last part of identifier

            foreach (ApplicationObject entity in entities)
            {
                List<MetadataProperty> properties = ScriptingService.MatchProperties(entity, propertyName);

                foreach (MetadataProperty property in properties)
                {
                    CompletionItem completion = new CompletionItem(property.Name, context.FragmentOffset, context.FragmentLength);

                    completion.ItemType = Enum.GetName(typeof(PropertyPurpose), property.Purpose);

                    suggestions.Add(completion);
                }
            }

            return suggestions;
        }

        #region "Trying to get context from token stream"

        private CompletionContext GetCompletionContextFromTokenStream(TSqlFragment script, int offset)
        {
            if (script == null || script.ScriptTokenStream == null || script.ScriptTokenStream.Count == 0)
            {
                return null;
            }

            TSqlParserToken token = GetCurrentToken(script.ScriptTokenStream, offset);
            if (token == null ||
                !(token.TokenType == TSqlTokenType.Dot
                || token.TokenType == TSqlTokenType.Identifier
                || token.TokenType == TSqlTokenType.WhiteSpace))
            {
                return null;
            }

            TSqlParserToken keyword = TakeFirstKeywordToLeft(script.ScriptTokenStream, token);
            if (keyword == null)
            {
                return null;
            }

            if (keyword.TokenType == TSqlTokenType.From || keyword.TokenType == TSqlTokenType.Join)
            {
                string identifier = TryGetFullIdentifier(script.ScriptTokenStream, token);

                int tokenOffset = token.Offset;
                int tokenLength = token.Text.Length;

                if (string.IsNullOrWhiteSpace(identifier)) // token.TokenType == TSqlTokenType.WhiteSpace
                {
                    tokenOffset++;
                    tokenLength = 0;
                }

                return new CompletionContext(null, offset, tokenOffset, tokenLength, null, new TableNode() { Name = identifier });
            }

            return null;
        }
        private bool IsFirstTokenToLeft(int cursorOffset, int tokenOffset, int tokenLength)
        {
            return (cursorOffset > tokenOffset) && ((tokenOffset + tokenLength) >= cursorOffset);
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
        private TSqlParserToken GetCurrentToken(IList<TSqlParserToken> tokens, int offset)
        {
            int current = 0;
            TSqlParserToken token;

            while (current < tokens.Count)
            {
                token = tokens[current++]; // consume token

                if (IsFirstTokenToLeft(offset, token.Offset, token.Text.Length))
                {
                    return token;
                }
            }

            return null;
        }
        private TSqlParserToken TakeFirstKeywordToLeft(IList<TSqlParserToken> tokens, TSqlParserToken current)
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

            TSqlParserToken token = TakeLeftOne(tokens, current);

            while (token != null)
            {
                if (token.IsKeyword())
                {
                    return token;
                }
                token = TakeLeftOne(tokens, token);
            }

            return null;
        }
        private string TryGetFullIdentifier(IList<TSqlParserToken> tokens, TSqlParserToken current)
        {
            string identifier = current.Text;

            TSqlParserToken token = TakeLeftOne(tokens, current);

            while (token != null &&
                (token.TokenType == TSqlTokenType.Dot ||
                token.TokenType == TSqlTokenType.Identifier ||
                token.TokenType == TSqlTokenType.QuotedIdentifier))
            {
                identifier = token.Text + identifier;

                token = TakeLeftOne(tokens, token);
            }

            return identifier;
        }

        #endregion
    }
}