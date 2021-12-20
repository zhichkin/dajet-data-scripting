using Microsoft.SqlServer.TransactSql.ScriptDom;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace DaJet.Data.Scripting
{
    public sealed class SyntaxTreeBuilder
    {
        public void Build(TSqlScript script, out SyntaxNode root)
        {
            root = new ScriptNode();
            Transform(script, root);
        }
        private void Transform(TSqlFragment fragment, SyntaxNode node)
        {
            Type type = fragment.GetType();

            // build children which resides in properties
            foreach (PropertyInfo property in type.GetProperties())
            {
                // ignore indexed properties
                if (property.GetIndexParameters().Length > 0) // property is an indexer
                {
                    // indexer property name is "Item" and it has parameters
                    continue;
                }

                // get type of property
                Type propertyType = property.PropertyType;
                bool isList = (propertyType.IsGenericType && propertyType.GetGenericTypeDefinition() == typeof(IList<>));
                if (isList) { propertyType = propertyType.GetGenericArguments()[0]; }

                // continue if child is not TSqlFragment
                if (!propertyType.IsSubclassOf(typeof(TSqlFragment))) { continue; }

                // check if property value is null
                object value = property.GetValue(fragment);
                if (value == null) { continue; }

                // build property or collection
                if (isList)
                {
                    IList list = (IList)value;
                    for (int i = 0; i < list.Count; i++)
                    {
                        TSqlFragment item = (TSqlFragment)list[i];
                        SyntaxNode sn = CreateSyntaxNode(item);
                        if (sn != null)
                        {
                            AttachSyntaxNode(node, sn, item);
                            Transform(item, sn);
                        }
                        else
                        {
                            Transform(item, node);
                        }
                    }
                }
                else
                {
                    SyntaxNode sn = CreateSyntaxNode((TSqlFragment)value);
                    if (sn != null)
                    {
                        AttachSyntaxNode(node, sn, (TSqlFragment)value);
                        Transform((TSqlFragment)value, sn);
                    }
                    else
                    {
                        Transform((TSqlFragment)value, node);
                    }
                }
            }
        }
        private SyntaxNode CreateSyntaxNode(TSqlFragment fragment)
        {
            SyntaxNode syntaxNode = null;

            if (fragment is SelectStatement)
            {
                syntaxNode = new StatementNode();
            }
            else if (fragment is QueryDerivedTable query) // has alias !
            {
                QueryNode node = new QueryNode();

                if (query.Alias != null)
                {
                    if (query.Alias.QuoteType == QuoteType.SquareBracket)
                    {
                        node.Alias = query.Alias.Value.TrimStart('[').TrimEnd(']');
                    }
                    else
                    {
                        node.Alias = query.Alias.Value;
                    }
                }

                syntaxNode = node;
            }
            else if (fragment is QuerySpecification)
            {
                // TODO: nested into WHERE clause queries =)
            }
            else if (fragment is WhereClause) // + ON = BooleanBinaryExpression
            {
                syntaxNode = new WhereNode();
            }
            else if (fragment is SelectScalarExpression select)
            {
                ColumnNode node = new ColumnNode();
                if (select.Expression is ColumnReferenceExpression column)
                {
                    foreach (Identifier identifier in column.MultiPartIdentifier.Identifiers)
                    {
                        if (!string.IsNullOrEmpty(node.Name)) { node.Name += "."; }
                        if (identifier.QuoteType == QuoteType.SquareBracket)
                        {
                            node.Name += identifier.Value.TrimStart('[').TrimEnd(']');
                        }
                        else
                        {
                            node.Name += identifier.Value;
                        }
                    }
                }
                if (select.ColumnName != null && select.ColumnName.Identifier != null)
                {
                    if (select.ColumnName.Identifier.QuoteType == QuoteType.SquareBracket)
                    {
                        node.Alias = select.ColumnName.Identifier.Value.TrimStart('[').TrimEnd(']');
                    }
                    else
                    {
                        node.Alias = select.ColumnName.Identifier.Value;
                    }
                }
                syntaxNode = node;
            }
            else if (fragment is NamedTableReference table)
            {
                TableNode node = new TableNode();
                if (table.Alias != null)
                {
                    if (table.Alias.QuoteType == QuoteType.SquareBracket)
                    {
                        node.Alias = table.Alias.Value.TrimStart('[').TrimEnd(']');
                    }
                    else
                    {
                        node.Alias = table.Alias.Value;
                    }
                }
                foreach (Identifier identifier in table.SchemaObject.Identifiers)
                {
                    if (!string.IsNullOrEmpty(node.Name)) { node.Name += "."; }
                    if (identifier.QuoteType == QuoteType.SquareBracket)
                    {
                        node.Name += identifier.Value.TrimStart('[').TrimEnd(']');
                    }
                    else
                    {
                        node.Name += identifier.Value;
                    }
                }
                syntaxNode = node;
            }
            else if (fragment is ColumnReferenceExpression column)
            {
                ColumnNode node = new ColumnNode();
                foreach (Identifier identifier in column.MultiPartIdentifier.Identifiers)
                {
                    if (!string.IsNullOrEmpty(node.Name)) { node.Name += "."; }
                    if (identifier.QuoteType == QuoteType.SquareBracket)
                    {
                        node.Name += identifier.Value.TrimStart('[').TrimEnd(']');
                    }
                    else
                    {
                        node.Name += identifier.Value;
                    }
                }
                syntaxNode = node;
            }

            return syntaxNode;
        }
        private void AttachSyntaxNode(SyntaxNode parent, SyntaxNode child, TSqlFragment fragment)
        {
            if (child is QueryNode _query)
            {
                if (parent is StatementNode statement)
                {
                    child.Parent = statement;
                    statement.Tables.Add(_query.Alias, _query);
                }
            }
            else if (child is StatementNode)
            {
                ScriptNode script = parent.SelfOrAncestor<ScriptNode>();
                if (script != null)
                {
                    child.Parent = script;
                    script.Statements.Add(child);
                }
            }
            else if (child is ColumnNode)
            {
                if (parent is WhereNode where)
                {
                    child.Parent = where;
                    where.Columns.Add(child);
                }
                else if (parent is QueryNode query)
                {
                    child.Parent = parent;
                    query.Columns.Add(child);
                }
                else if (parent is StatementNode statement)
                {
                    child.Parent = parent;
                    statement.Columns.Add(child);
                }
            }
            else if (child is TableNode table)
            {
                if (parent is StatementNode statement)
                {
                    child.Parent = statement;
                    if (string.IsNullOrEmpty(table.Alias))
                    {
                        statement.Tables.Add(table.Name, table);
                    }
                    else
                    {
                        statement.Tables.Add(table.Alias, table);
                    }
                }
                else if (parent is QueryNode query)
                {
                    child.Parent = query;
                    if (string.IsNullOrEmpty(table.Alias))
                    {
                        query.Tables.Add(table.Name, table);
                    }
                    else
                    {
                        query.Tables.Add(table.Alias, table);
                    }
                }
            }
            else if (child is WhereNode where)
            {
                if (parent is StatementNode statement)
                {
                    child.Parent = statement;
                    statement.Where = where;
                }
                else if (parent is QueryNode query)
                {
                    child.Parent = query;
                    query.Where = where;
                }
            }
        }
    }
}