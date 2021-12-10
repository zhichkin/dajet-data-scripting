﻿using Microsoft.SqlServer.TransactSql.ScriptDom;
using System;
using System.Collections.Generic;

namespace DaJet.Data.Scripting
{
    public sealed class CreateFunctionStatementVisitor : TSqlFragmentVisitor
    {
        public string FunctionName { get; private set; } = string.Empty;
        private List<string> Parameters { get; } = new List<string>();
        private List<string> Declarations { get; } = new List<string>();
        public override void ExplicitVisit(CreateFunctionStatement node)
        {
            FunctionName = node.Name.BaseIdentifier.Value;
            ParseParameters(node.Parameters);
            base.ExplicitVisit(node);
        }
        public override void ExplicitVisit(CreateOrAlterFunctionStatement node)
        {
            FunctionName = node.Name.BaseIdentifier.Value;
            ParseParameters(node.Parameters);
            base.ExplicitVisit(node);
        }
        private void ParseParameters(IList<ProcedureParameter> parameters)
        {
            Parameters.Clear();
            Declarations.Clear();

            foreach (ProcedureParameter parameter in parameters)
            {
                DeclareVariableStatement statement = CreateDeclareVariableStatement(
                    parameter.VariableName,
                    parameter.DataType,
                    parameter.Value);
                string sql = statement.ToSqlString();
                if (!sql.EndsWith(';')) { sql += ";"; }
                Declarations.Add(sql);

                Parameters.Add(parameter.VariableName.Value);
            }
        }
        private DeclareVariableStatement CreateDeclareVariableStatement(Identifier name, DataTypeReference type, ScalarExpression value)
        {
            DeclareVariableStatement statement = new DeclareVariableStatement();
            statement.Declarations.Add(new DeclareVariableElement()
            {
                Value = value,
                DataType = type,
                VariableName = name
            });
            return statement;
        }
        public string GenerateExecuteFunctionCode()
        {
            string scriptCode = string.Empty;
            foreach (string declaration in Declarations)
            {
                scriptCode += declaration + Environment.NewLine;
            }
            scriptCode += $"SELECT [dbo].[{FunctionName}](";
            foreach (string parameter in Parameters)
            {
                scriptCode += parameter + ",";
            }
            scriptCode = scriptCode.TrimEnd(',') + ");";
            return scriptCode;
        }
    }
}