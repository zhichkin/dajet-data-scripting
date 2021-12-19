using DaJet.Metadata;
using DaJet.Metadata.Model;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Text;

namespace DaJet.Data.Scripting.Test
{
    [TestClass] public class UnitTests
    {
        private readonly InfoBase Trade;
        private readonly InfoBase Accounting;
        private readonly IMetadataService MetadataService;
        private readonly IScriptingService ScriptingService;

        public UnitTests()
        {
            MetadataService = new MetadataService()
                .UseDatabaseProvider(DatabaseProvider.SQLServer)
                .UseConnectionString("Data Source=ZHICHKIN;Initial Catalog=trade_11_2_3_159_demo;Integrated Security=True");

            Trade = MetadataService.OpenInfoBase(); // trade_11_2_3_159_demo

            MetadataService = new MetadataService()
                .UseDatabaseProvider(DatabaseProvider.SQLServer)
                .UseConnectionString("Data Source=ZHICHKIN;Initial Catalog=accounting_3_0_72_72_demo;Integrated Security=True");

            Accounting = MetadataService.OpenInfoBase(); // accounting_3_0_72_72_demo

            ScriptingService = new ScriptingService();
            ScriptingService.UseConnectionString(MetadataService.ConnectionString);

            ScriptingService.MainInfoBase = Trade;
            
            //ScriptingService.Databases.Add("trade_11_2_3_159_demo", Trade);
            //ScriptingService.Databases.Add("accounting_3_0_72_72_demo", Accounting);
        }

        [TestMethod] public void Prepare_Simple_Script()
        {
            string script = "SELECT TOP 10 ������ AS [������] FROM ����������.������������";

            string sql = ScriptingService.PrepareScript(script, out IList<ParseError> errors);
            
            string errorMessage = string.Empty;
            foreach (ParseError error in errors)
            {
                errorMessage += error.Message + Environment.NewLine;
            }

            Console.WriteLine(sql);
            Console.WriteLine();
            Console.WriteLine(errorMessage);
        }
        [TestMethod] public void Prepare_Reference_Script()
        {
            StringBuilder script = new StringBuilder();
            script.AppendLine("SELECT TOP 1");
            script.AppendLine("��.������������ AS ���,");
            script.AppendLine("���.���������� AS ����������");
            script.AppendLine("FROM");
            script.AppendLine("����������.�������������� AS ��");
            script.AppendLine("INNER JOIN ����������.��������������������������� AS ���");
            script.AppendLine("ON ��.������.uuid = ���.��������.uuid");
            script.AppendLine("AND ��.������.type = ���.��������.type");
            script.AppendLine("AND ��.������.TYPE = ���.��������.TYPE");

            string sql = ScriptingService.PrepareScript(script.ToString(), out IList<ParseError> errors);

            string errorMessage = string.Empty;
            foreach (ParseError error in errors)
            {
                errorMessage += error.Message + Environment.NewLine;
            }

            Console.WriteLine(sql);
            Console.WriteLine();
            Console.WriteLine(errorMessage);
        }
        [TestMethod] public void Prepare_TYPEOF_Script()
        {
            StringBuilder script = new StringBuilder();
            script.AppendLine("SELECT TOP 1");
            script.AppendLine("��.������������ AS ���,");
            script.AppendLine("���.���������� AS ����������");
            script.AppendLine("FROM");
            script.AppendLine("����������.�������������� AS ��");
            script.AppendLine("INNER JOIN ����������.��������������������������� AS ���");
            script.AppendLine("ON  ��.������.uuid = ���.��������.uuid");
            script.AppendLine("AND TYPEOF(����������.��������������) = ���.��������.type");
            script.AppendLine("AND 0x08 = ���.��������.TYPE");

            string sql = ScriptingService.PrepareScript(script.ToString(), out IList<ParseError> errors);

            string errorMessage = string.Empty;
            foreach (ParseError error in errors)
            {
                errorMessage += error.Message + Environment.NewLine;
            }

            Console.WriteLine(sql);
            Console.WriteLine();
            Console.WriteLine(errorMessage);
        }
        [TestMethod] public void Prepare_TwoDatabases_Script()
        {
            StringBuilder script = new StringBuilder();
            script.AppendLine("SELECT");
            script.AppendLine("�����.���� AS ��������,");
            script.AppendLine("�����.����� AS ���������,");
            script.AppendLine("����.���� AS ��������,");
            script.AppendLine("�����.���� AS �������,");
            script.AppendLine("�����.����� AS ��������,");
            script.AppendLine("����.���� AS �������");
            script.AppendLine("FROM");
            script.AppendLine("trade_11_2_3_159_demo.���������������.���������������� AS ����");
            script.AppendLine("LEFT JOIN accounting_3_0_72_72_demo.���������������.���������������� AS ����");
            script.AppendLine("ON  ����.�����������.uuid = ����.�����������.uuid");
            script.AppendLine("AND ����.�����������.type = TYPEOF(trade_11_2_3_159_demo.��������.������������������������)");
            script.AppendLine("AND ����.�����������.type = TYPEOF(accounting_3_0_72_72_demo.��������.������������������������)");

            script.AppendLine("LEFT JOIN trade_11_2_3_159_demo.��������.������������������������ AS �����");
            script.AppendLine("ON  ����.�����������.uuid = �����.������.uuid");
            script.AppendLine("AND ����.�����������.type = �����.������.type");

            script.AppendLine("LEFT JOIN accounting_3_0_72_72_demo.��������.������������������������ AS �����");
            script.AppendLine("ON  ����.�����������.uuid = �����.������.uuid");
            script.AppendLine("AND ����.�����������.type = �����.������.type");

            string sql = ScriptingService.PrepareScript(script.ToString(), out IList<ParseError> errors);

            string errorMessage = string.Empty;
            foreach (ParseError error in errors)
            {
                errorMessage += error.Message + Environment.NewLine;
            }

            Console.WriteLine(sql);
            Console.WriteLine();
            Console.WriteLine(errorMessage);
        }

        [TestMethod] public void Execute_Simple_Script()
        {
            string script = "SELECT TOP 10 ��� AS [���], ������������ AS [������������] FROM ����������.������������";
            Console.WriteLine(script);

            string sql = ScriptingService.PrepareScript(script, out IList<ParseError> errors);

            string errorMessage = string.Empty;
            foreach (ParseError error in errors)
            {
                errorMessage += error.Message + Environment.NewLine;
            }

            if (!string.IsNullOrEmpty(errorMessage))
            {
                Console.WriteLine();
                Console.WriteLine(errorMessage);
                return;
            }

            Console.WriteLine();
            Console.WriteLine(sql);

            string json = ScriptingService.ExecuteJson(sql, out errors);
            errorMessage = string.Empty;
            foreach (ParseError error in errors)
            {
                errorMessage += error.Message + Environment.NewLine;
            }

            if (!string.IsNullOrEmpty(errorMessage))
            {
                Console.WriteLine();
                Console.WriteLine(errorMessage);
                return;
            }

            Console.WriteLine();
            Console.WriteLine(json);
        }

        [TestMethod] public void Parse_Script()
        {
            string script = "SELECT  FROM ; INSERT [table1] (name) SELECT @name;";

            TSqlFragment sql = ScriptingService.ParseScript(script, out IList<ParseError> errors);

            string errorMessage = string.Empty;
            foreach (ParseError error in errors)
            {
                errorMessage += error.Message + Environment.NewLine;
            }

            //Console.WriteLine(sql);
            //Console.WriteLine();
            Console.WriteLine(errorMessage);
        }
    }
}