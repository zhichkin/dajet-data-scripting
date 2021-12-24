﻿using DaJet.Data.Scripting.SyntaxTree;
using DaJet.Metadata.Model;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ScriptDom = Microsoft.SqlServer.TransactSql.ScriptDom;

namespace DaJet.Data.Scripting
{
    public sealed class ColumnReferenceExpressionVisitor : ISyntaxTreeVisitor
    {
        private IScriptingService ScriptingService { get; }
        internal ColumnReferenceExpressionVisitor(IScriptingService scriptingService)
        {
            ScriptingService = scriptingService ?? throw new ArgumentNullException(nameof(scriptingService));
        }
        public IList<string> PriorityProperties { get { return null; } }
        public ISyntaxNode Visit(TSqlFragment node, TSqlFragment parent, string sourceProperty, ISyntaxNode result)
        {
            ColumnReferenceExpression columnReference = node as ColumnReferenceExpression;
            if (columnReference == null) return result;

            StatementNode statement = result as StatementNode;
            if (statement == null) return result;
            if (columnReference.ColumnType != ColumnType.Regular) return result;
            if (statement.Tables == null || statement.Tables.Count == 0) return result;

            Identifier identifier = null;
            string propertyFieldName = null;
            IList<string> identifiers = new List<string>();
            for (int i = 0; i < columnReference.MultiPartIdentifier.Identifiers.Count; i++)
            {
                identifiers.Add(columnReference.MultiPartIdentifier.Identifiers[i].Value);
            }

            if (identifiers.Count == 1) // no table alias - just column name
            {
                identifier = columnReference.MultiPartIdentifier.Identifiers[0];
            }
            else if (columnReference.MultiPartIdentifier.Identifiers.Count == 2)
            {
                if (IsSpecialField(identifiers[1])) // no table alias - just column name
                {
                    propertyFieldName = identifiers[1];
                    identifier = columnReference.MultiPartIdentifier.Identifiers[0];
                }
                else
                {
                    identifier = columnReference.MultiPartIdentifier.Identifiers[1];
                }
            }
            else // columnReference.MultiPartIdentifier.Identifiers.Count == 3
            {
                propertyFieldName = identifiers[2];
                identifier = columnReference.MultiPartIdentifier.Identifiers[1];
            }

            MetadataProperty property = GetProperty(identifiers, statement);
            if (property == null) return result;
            if (property.Fields.Count == 0) return result;

            if (property.PropertyType.CanBeReference || (property.Purpose == PropertyPurpose.System && property.Name == "Ссылка"))
            {
                if (propertyFieldName == null) // Т.Ссылка | Т.Владелец
                {
                    VisitReferenceTypeColumn(columnReference, parent, sourceProperty, identifier, property);
                }
                else // uuid | type | TYPE
                {
                    VisitReferenceTypeColumn(columnReference, parent, sourceProperty, columnReference.MultiPartIdentifier.Identifiers, identifier, property, propertyFieldName);
                }
            }
            else // Т.Наименование
            {
                VisitValueTypeColumn(identifier, property);
            }

            return result;
        }
        private bool IsSpecialField(string fieldName)
        {
            return (fieldName == "uuid" || fieldName == "type" || fieldName == "TYPE");
        }
        private MetadataProperty GetProperty(IList<string> identifiers, StatementNode statement)
        {
            if (identifiers.Count == 1)
            {
                return GetPropertyWithoutTableAlias(identifiers, statement);
            }
            else if (identifiers.Count == 2)
            {
                if (IsSpecialField(identifiers[1]))
                {
                    return GetPropertyWithoutTableAlias(identifiers, statement);
                }
                else
                {
                    return GetPropertyWithTableAlias(identifiers, statement);
                }
            }
            else
            {
                return GetPropertyWithTableAlias(identifiers, statement);
            }
        }
        private MetadataProperty GetPropertyWithTableAlias(IList<string> identifiers, StatementNode statement)
        {
            TableNode table = null;
            MetadataProperty property = null;
            string alias = identifiers[0];
            string propertyName = identifiers[1];

            if (statement.Tables.TryGetValue(alias, out ISyntaxNode tableNode))
            {
                if (tableNode is TableNode)
                {
                    table = (TableNode)tableNode;
                    if (table.ApplicationObject != null)
                    {
                        property = table.ApplicationObject.Properties.Where(p => p.Name == propertyName).FirstOrDefault();
                        if (property != null && property.PropertyType.ReferenceTypeCode == 0)
                        {
                            ScriptingService.FixPropertyTypeCode(table.ApplicationObject, property);
                        }
                    }
                }
                else if (tableNode is StatementNode)
                {
                    return property; // TODO: query derived table ... tunneling ... value type inference ... !?
                }
            }
            return property;
        }
        private MetadataProperty GetPropertyWithoutTableAlias(IList<string> identifiers, StatementNode statement)
        {
            TableNode table = null;
            MetadataProperty property = null;
            string propertyName = identifiers[0];

            foreach (ISyntaxNode tableNode in statement.Tables.Values)
            {
                if (tableNode is TableNode)
                {
                    table = (TableNode)tableNode;
                    if (table.Alias == null)
                    {
                        if (table.ApplicationObject != null)
                        {
                            property = table.ApplicationObject.Properties.Where(p => p.Name == propertyName).FirstOrDefault();
                            if (property != null)
                            {
                                if (property.PropertyType.ReferenceTypeCode == 0)
                                {
                                    ScriptingService.FixPropertyTypeCode(table.ApplicationObject, property);
                                }
                                return property; //TODO: check if property name is unique for all tables having no alias
                            }
                        }
                    }
                }
                else if (tableNode is StatementNode)
                {
                    return property; // TODO: query derived table ... tunneling ... value type inference ... !?
                }
            }
            return property;
        }
        private void VisitValueTypeColumn(Identifier identifier, MetadataProperty property)
        {
            if (property.Fields.Count == 1)
            {
                identifier.Value = property.Fields[0].Name;
            }
            else
            {
                // TODO: error !? compound type properties is not supported for multivalued columns !
            }
        }
        private void VisitReferenceTypeColumn(ColumnReferenceExpression node, TSqlFragment parent, string sourceProperty, IList<Identifier> identifiers, Identifier identifier, MetadataProperty property, string fieldName)
        {
            DatabaseField field = null;
            BinaryLiteral binaryLiteral = null;
            
            if (fieldName == "uuid")
            {
                if (property.Fields.Count == 1)
                {
                    field = property.Fields[0];
                }
                else
                {
                    field = property.Fields.Where(f => f.Purpose == FieldPurpose.Object).FirstOrDefault();
                }
            }
            else if (fieldName == "type")
            {
                if (property.Fields.Count == 1)
                {
                    string HexTypeCode = $"0x{property.PropertyType.ReferenceTypeCode.ToString("X").PadLeft(8, '0')}";
                    binaryLiteral = new BinaryLiteral() { Value = HexTypeCode };
                }
                else
                {
                    field = property.Fields.Where(f => f.Purpose == FieldPurpose.TypeCode).FirstOrDefault();
                }
            }
            else if (fieldName == "TYPE")
            {
                if (property.Fields.Count == 1)
                {
                    binaryLiteral = new BinaryLiteral() { Value = "0x08" };
                }
                else
                {
                    field = property.Fields.Where(f => f.Purpose == FieldPurpose.Discriminator).FirstOrDefault();
                }
            }
            // TODO: throw new MissingMemberException !? if nonexistent field referenced

            if (binaryLiteral == null)
            {
                identifier.Value = field.Name; // change object property identifier to SQL table column identifier
                identifiers.RemoveAt(identifiers.Count - 1); // uuid | type | TYPE
            }
            else
            {
                TransformToBinaryLiteral(node, parent, sourceProperty, binaryLiteral);
            }
        }
        private void TransformToBinaryLiteral(ColumnReferenceExpression node, TSqlFragment parent, string sourceProperty, TSqlFragment expression)
        {
            PropertyInfo property = parent.GetType().GetProperty(sourceProperty);
            bool isList = (property.PropertyType.IsGenericType
                && property.PropertyType.GetGenericTypeDefinition() == typeof(IList<>));
            if (isList)
            {
                IList list = (IList)property.GetValue(parent);
                int index = list.IndexOf(node);
                list[index] = expression;
            }
            else
            {
                property.SetValue(parent, expression);
            }
        }
        private void VisitReferenceTypeColumn(ColumnReferenceExpression node, TSqlFragment parent, string sourceProperty, Identifier identifier, MetadataProperty property)
        {
            if (property.Fields.Count == 1) // Т.Ссылка (не составной тип)
            {
                string HexTypeCode = $"0x{property.PropertyType.ReferenceTypeCode.ToString("X").PadLeft(8, '0')}";
                identifier.Value = property.Fields[0].Name;
                ParenthesisExpression expression = new ParenthesisExpression()
                {
                    Expression = new ScriptDom.BinaryExpression()
                    {
                        BinaryExpressionType = BinaryExpressionType.Add,
                        FirstExpression = new BinaryLiteral() { Value = HexTypeCode },
                        SecondExpression = node
                    }
                };
                PropertyInfo pi = parent.GetType().GetProperty(sourceProperty);
                bool isList = (pi.PropertyType.IsGenericType && pi.PropertyType.GetGenericTypeDefinition() == typeof(IList<>));
                if (isList)
                {
                    IList list = (IList)pi.GetValue(parent);
                    int index = list.IndexOf(node);
                    list[index] = expression;
                }
                else
                {
                    pi.SetValue(parent, expression);
                }
            }
            else // Т.Владелец (составной тип)
            {
                DatabaseField typeCode = property.Fields.Where(f => f.Purpose == FieldPurpose.TypeCode).FirstOrDefault();
                DatabaseField reference = property.Fields.Where(f => f.Purpose == FieldPurpose.Object).FirstOrDefault();
                identifier.Value = reference.Name;

                MultiPartIdentifier mpi = new MultiPartIdentifier();
                foreach (var id in node.MultiPartIdentifier.Identifiers)
                {
                    mpi.Identifiers.Add(new Identifier() { Value = id.Value });
                }
                mpi.Identifiers[mpi.Count - 1].Value = typeCode.Name;
                ParenthesisExpression expression = new ParenthesisExpression()
                {
                    Expression = new ScriptDom.BinaryExpression()
                    {
                        BinaryExpressionType = BinaryExpressionType.Add,
                        FirstExpression = new ColumnReferenceExpression()
                        {
                            ColumnType = ColumnType.Regular,
                            MultiPartIdentifier = mpi
                        },
                        SecondExpression = node
                    }
                };

                PropertyInfo pi = parent.GetType().GetProperty(sourceProperty);
                bool isList = (pi.PropertyType.IsGenericType && pi.PropertyType.GetGenericTypeDefinition() == typeof(IList<>));
                if (isList)
                {
                    IList list = (IList)pi.GetValue(parent);
                    int index = list.IndexOf(node);
                    list[index] = expression;
                }
                else
                {
                    pi.SetValue(parent, expression);
                }
            }
        }
    }
}