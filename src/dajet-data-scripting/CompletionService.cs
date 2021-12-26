using DaJet.Data.Scripting.SyntaxTree;
using DaJet.Metadata.Model;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace DaJet.Data.Scripting
{
    internal sealed class CompletionService
    {
        private readonly IScriptingService ScriptingService;
        internal CompletionService(IScriptingService scripting)
        {
            ScriptingService = scripting;
        }
        internal CultureInfo GetCultureInfo()
        {
            CultureInfo culture;
            try
            {
                culture = CultureInfo.GetCultureInfo("ru-RU");
            }
            catch (CultureNotFoundException)
            {
                culture = CultureInfo.CurrentUICulture;
            }
            return culture;
        }
        private List<string> keywords = new List<string>()
        {
            "SELECT",
            "FROM",
            "WHERE",
            "TOP",
            "ON",
            "AS",
            "INNER JOIN",
            "LEFT JOIN",
            "RIGHT JOIN",
            "FULL JOIN",
            "GROUP BY",
            "ORDER BY",
            "HAVING",
            "SUM",
            "COUNT",
            "DISTINCT",
            "ASC",
            "DESC",
            "CASE WHEN THEN ELSE END"
        };
        private List<string> keywords_ru = new List<string>()
        {
            "ыудусе", // select
            "акщь", // from
            "цруку", // where
            "ещз", // top
            "щт", // on
            "фы", // as
            "шттук ощшт", // inner join
            "дуае ощшт", // left join
            "кшпре ощшт", // right join
            "агдд ощшт", // full join
            "пкщгз ин", // group by
            "щквук ин", // order by
            "рфмштп", // having
            "ыгь", // sum
            "сщгте", // count
            "вшыештсе", // distinct
            "фыс", // asc
            "вуыс", // desc
            "сфыу црут" // case when
        };
        private string MatchKeyword(string input)
        {
            CultureInfo culture = GetCultureInfo();

            for (int i =0; i< keywords_ru.Count;i++)
            {
                if (culture.CompareInfo.IsPrefix(keywords[i], input, CompareOptions.IgnoreCase)
                    || culture.CompareInfo.IsPrefix(keywords_ru[i], input, CompareOptions.IgnoreCase))
                {
                    return keywords[i];
                }
            }

            return null;
        }

        internal List<CompletionItem> GetCompletionItems(TSqlFragment fragment, int offset)
        {
            List<CompletionItem> list = new List<CompletionItem>();

            CompletionContext context = GetCompletionContext(fragment, offset);

            if (context.Keyword != null)
            {
                list.Add(new CompletionItem(context.Keyword, context.FragmentOffset, context.FragmentLength) { ItemType = "Keyword" });
            }

            if (context != null && context.SyntaxNode is TableNode table)
            {
                list.AddRange(GetTableCompletionItems(context, table.Name));
                return list;
            }
            else if (context != null && context.SyntaxNode is ColumnNode column)
            {
                list.AddRange(GetColumnCompletionItems(context, column.Name));
                return list;
            }

            if (fragment is TSqlScript script && script.ScriptTokenStream != null && script.ScriptTokenStream.Count > 0)
            {
                context = GetCompletionContextFromTokenStream(script, offset);

                if (context != null && context.SyntaxNode != null)
                {
                    list.AddRange(GetTableCompletionItems(context, ((TableNode)context.SyntaxNode).Name));
                    return list;
                }
            }

            return list;
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

            CompletionContext context = null;

            TSqlParserToken token = GetCurrentToken(script.ScriptTokenStream, offset);
            if (token != null)
            {
                context = new CompletionContext(offset, token.Offset, token.Text.Length);
                if (token.TokenType == TSqlTokenType.Identifier)
                {
                    context.Keyword = MatchKeyword(token.Text);
                }
            }

            SyntaxTreeBuilder builder = new SyntaxTreeBuilder()
            {
                CursorOffset = offset
            };
            builder.Build((TSqlScript)script, out _);

            if (context == null)
            {
                context = new CompletionContext(builder.CursorOffset, builder.FragmentOffset, builder.FragmentLength);
            }
            context.Fragment = builder.Fragment;
            context.SyntaxNode = builder.SyntaxNode;

            return context;
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

            int i = 0;
            while (i < tables.Count)
            {
                TableNode table = tables[i];

                ApplicationObject entity = ScriptingService.MatchApplicationObject(table.Name);
                if (entity != null)
                {
                    i++;
                    table.ApplicationObject = entity;
                }
                else
                {
                    tables.RemoveAt(i);
                }
            }

            if (tables.Count == 0)
            {
                return suggestions;
            }

            string[] names = columnIdentifier.Split('.', StringSplitOptions.RemoveEmptyEntries);
            string propertyName = names[names.Length - 1]; // last part of identifier

            foreach (TableNode table in tables)
            {
                List<MetadataProperty> properties = ScriptingService.MatchProperties(table.ApplicationObject, propertyName);

                foreach (MetadataProperty property in properties)
                {
                    CompletionItem completion = new CompletionItem(
                        (string.IsNullOrWhiteSpace(table.Alias)
                                        ? property.Name
                                        : table.Alias + "." + property.Name),
                        context.FragmentOffset, context.FragmentLength);

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

            CompletionContext context = new CompletionContext(offset, token.Offset, token.Text.Length);

            if (token.TokenType == TSqlTokenType.Identifier)
            {
                context.Keyword = MatchKeyword(token.Text);
            }

            TSqlParserToken keyword = TakeFirstKeywordToLeft(script.ScriptTokenStream, token);
            if (keyword != null && (keyword.TokenType == TSqlTokenType.From || keyword.TokenType == TSqlTokenType.Join))
            {
                string identifier = TryGetFullIdentifier(script.ScriptTokenStream, token);
                if (string.IsNullOrWhiteSpace(identifier)) // token.TokenType == TSqlTokenType.WhiteSpace
                {
                    context.FragmentOffset++;
                    context.FragmentLength = 0;
                }
                context.SyntaxNode = new TableNode() { Name = identifier };
            }

            return context;
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