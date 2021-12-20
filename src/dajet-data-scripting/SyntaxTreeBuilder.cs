using Microsoft.SqlServer.TransactSql.ScriptDom;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace DaJet.Data.Scripting
{
    public sealed class SyntaxTreeBuilder
    {
        public int CursorOffset { get; set; }
        public int FragmentOffset { get; set; }
        public int FragmentLength { get; set; }
        public TSqlFragment Fragment { get; set; }
        public SyntaxNode SyntaxNode { get; set; }
        public void Build(TSqlScript script, out SyntaxNode root)
        {
            root = new ScriptNode();
            BuildSyntaxNode(script, root);
        }
        private bool IsEditingFragment(TSqlFragment fragment)
        {
            return (CursorOffset > fragment.StartOffset)
                && ((fragment.StartOffset + fragment.FragmentLength) >= CursorOffset);
        }
        private void BuildSyntaxNode(TSqlFragment fragment, SyntaxNode parent)
        {
            if (IsEditingFragment(fragment))
            {
                FragmentOffset = fragment.StartOffset;
                FragmentLength = fragment.FragmentLength;
                Fragment = fragment;
                SyntaxNode = parent;
            }

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
                        SyntaxNode node = CreateSyntaxNode(item, parent);
                        BuildSyntaxNode(item, (node == null ? parent : node));
                    }
                }
                else
                {
                    SyntaxNode node = CreateSyntaxNode((TSqlFragment)value, parent);
                    BuildSyntaxNode((TSqlFragment)value, (node == null ? parent : node));
                }
            }
        }
        private SyntaxNode CreateSyntaxNode(TSqlFragment fragment, SyntaxNode parent)
        {
            if (fragment is SelectStatement)
            {
                return CreateSelectNode(fragment, parent);
            }
            else if (fragment is QueryDerivedTable)
            {
                return CreateQueryNode(fragment, parent);
            }
            else if (fragment is QualifiedJoin)
            {
                return CreateJoinNode(fragment, parent);
            }
            else if (fragment is QuerySpecification)
            {
                // TODO: nested into WHERE clause queries =)
                // + ScalarSubqueryExpression in SELECT clause
            }
            else if (fragment is NamedTableReference)
            {
                return CreateTableNode(fragment, parent);
            }
            else if (fragment is SelectScalarExpression)
            {
                return CreateColumnNode(fragment, parent);
            }
            else if (fragment is ColumnReferenceExpression)
            {
                return CreateColumnNode(fragment, parent);
            }
            else if (fragment is WhereClause)
            {
                return CreateWhereNode(fragment, parent);
            }
            return null;
        }

        private string GetIdentifierValue(Identifier identifier)
        {
            if (identifier == null)
            {
                return string.Empty;
            }

            if (identifier.QuoteType == QuoteType.SquareBracket)
            {
                return identifier.Value.TrimStart('[').TrimEnd(']');
            }
            else
            {
                return identifier.Value;
            }
        }
        private string GetIdentifierValue(MultiPartIdentifier identifiers)
        {
            string value = string.Empty;

            foreach (Identifier identifier in identifiers.Identifiers)
            {
                if (!string.IsNullOrEmpty(value))
                {
                    value += ".";
                }

                value += GetIdentifierValue(identifier);
            }

            return value;
        }
        private string GetIdentifierValue(IdentifierOrValueExpression identifier)
        {
            if (identifier == null)
            {
                return string.Empty;
            }

            return GetIdentifierValue(identifier.Identifier);
        }

        private SyntaxNode CreateSelectNode(TSqlFragment fragment, SyntaxNode parent)
        {
            if (!(fragment is SelectStatement))
            {
                return null;
            }

            ScriptNode script = parent.SelfOrAncestor<ScriptNode>();
            if (script == null)
            {
                return null;
            }

            SelectNode node = new SelectNode()
            {
                Parent = script
            };
            script.Statements.Add(node);

            return node;
        }
        private SyntaxNode CreateQueryNode(TSqlFragment fragment, SyntaxNode parent)
        {
            if (!(fragment is QueryDerivedTable query))
            {
                return null;
            }

            ITableScopeProvider scope = parent.TableScopeProvider();
            if (scope == null)
            {
                return null;
            }

            QueryNode node = new QueryNode()
            {
                Parent = parent,
                Alias = GetIdentifierValue(query.Alias)
            };
            scope.Tables.Add(node);

            return node;
        }
        private SyntaxNode CreateJoinNode(TSqlFragment fragment, SyntaxNode parent)
        {
            if (!(fragment is QualifiedJoin join))
            {
                return null;
            }

            ITableScopeProvider scope = parent.TableScopeProvider();
            if (scope == null)
            {
                return null;
            }
            
            JoinNode node = new JoinNode()
            {
                Parent = parent,
                JoinType = (JoinType)join.QualifiedJoinType
            };
            scope.Tables.Add(node);

            return node;
        }
        private SyntaxNode CreateTableNode(TSqlFragment fragment, SyntaxNode parent)
        {
            if (!(fragment is NamedTableReference table))
            {
                return null;
            }

            ITableScopeProvider scope = parent.TableScopeProvider();
            if (scope == null)
            {
                return null;
            }

            TableNode node = new TableNode()
            {
                Parent = parent,
                Name = GetIdentifierValue(table.SchemaObject),
                Alias = GetIdentifierValue(table.Alias)
            };
            scope.Tables.Add(node);

            return node;
        }
        private SyntaxNode CreateColumnNode(TSqlFragment fragment, SyntaxNode parent)
        {
            if (fragment is SelectScalarExpression)
            {
                return CreateSelectScalarNode(fragment, parent);
            }
            else if (fragment is ColumnReferenceExpression)
            {
                return CreateColumnReferenceNode(fragment, parent);
            }
            return null;
        }
        private SyntaxNode CreateSelectScalarNode(TSqlFragment fragment, SyntaxNode parent)
        {
            if (!(fragment is SelectScalarExpression scalar))
            {
                return null;
            }

            SelectNode select = parent.SelfOrAncestor<SelectNode>();
            if (select == null)
            {
                return null;
            }

            ColumnNode node = new ColumnNode()
            {
                Parent = parent,
                Alias = GetIdentifierValue(scalar.ColumnName)
            };
            select.Columns.Add(node);

            return node;
        }
        private SyntaxNode CreateColumnReferenceNode(TSqlFragment fragment, SyntaxNode parent)
        {
            if (!(fragment is ColumnReferenceExpression column))
            {
                return null;
            }

            if (parent is ColumnNode scalar)
            {
                scalar.Name = GetIdentifierValue(column.MultiPartIdentifier);
                return scalar;
            }

            ITableScopeProvider scope = parent.TableScopeProvider();
            if (scope == null)
            {
                return null;
            }

            ColumnNode node = new ColumnNode()
            {
                Parent = scope.Where,
                Name = GetIdentifierValue(column.MultiPartIdentifier)
            };
            scope.Where.Columns.Add(node);

            return node;
        }
        private SyntaxNode CreateWhereNode(TSqlFragment fragment, SyntaxNode parent)
        {
            if (!(fragment is WhereClause))
            {
                return null;
            }

            ITableScopeProvider scope = parent.TableScopeProvider();
            if (scope == null)
            {
                return null;
            }

            return scope.Where;
        }
    }
}