using DaJet.Data.Scripting.SyntaxTree;
using DaJet.Metadata.Model;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace DaJet.Data.Scripting
{
    public sealed class FunctionCallVisitor : ISyntaxTreeVisitor
    {
        private IScriptingService ScriptingService { get; }
        internal FunctionCallVisitor(IScriptingService scriptingService)
        {
            ScriptingService = scriptingService ?? throw new ArgumentNullException(nameof(scriptingService));
        }
        public IList<string> PriorityProperties { get { return null; } }
        public ISyntaxNode Visit(TSqlFragment node, TSqlFragment parent, string sourceProperty, ISyntaxNode result)
        {
            FunctionCall functionCall = node as FunctionCall;
            if (functionCall == null) return result;
            if (functionCall.CallTarget != null) return result;
            if (functionCall.FunctionName.Value != "TYPEOF") return result;
            if (functionCall.Parameters == null || functionCall.Parameters.Count != 1) return result;
            if (!(functionCall.Parameters[0] is ColumnReferenceExpression columnReference)) return result;
            if (columnReference.ColumnType != ColumnType.Regular) return result;

            ApplicationObject table = GetApplicationObject(columnReference.MultiPartIdentifier.Identifiers);
            if (table == null) return result;

            Transform(parent, sourceProperty, functionCall, table.TypeCode);

            return result;
        }
        private ApplicationObject GetApplicationObject(IList<Identifier> identifiers)
        {
            List<string> tableIdentifiers = new List<string>();
            int count = identifiers.Count;
            for (int i = 0; i < (4 - count); i++)
            {
                tableIdentifiers.Add(null);
            }
            for (int i = 0; i < count; i++)
            {
                tableIdentifiers.Add(identifiers[i].Value);
            }
            return ScriptingService.GetApplicationObject(tableIdentifiers);
        }
        private void Transform(TSqlFragment parent, string sourceProperty, FunctionCall functionCall, int typeCode)
        {
            string HexTypeCode = $"0x{typeCode.ToString("X").PadLeft(8, '0')}";
            BinaryLiteral binaryLiteral = new BinaryLiteral() { Value = HexTypeCode };
            
            PropertyInfo pi = parent.GetType().GetProperty(sourceProperty);
            bool isList = (pi.PropertyType.IsGenericType && pi.PropertyType.GetGenericTypeDefinition() == typeof(IList<>));
            if (isList)
            {
                IList list = (IList)pi.GetValue(parent);
                int index = list.IndexOf(functionCall);
                list[index] = binaryLiteral;
            }
            else
            {
                pi.SetValue(parent, binaryLiteral);
            }
        }
    }
}