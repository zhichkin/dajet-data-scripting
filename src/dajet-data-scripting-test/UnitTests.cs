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
            string script = "SELECT TOP 10 Ссылка AS [Ссылка] FROM Справочник.Номенклатура";

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
            script.AppendLine("ФЛ.Наименование AS ФИО,");
            script.AppendLine("БСК.НомерСчета AS НомерСчета");
            script.AppendLine("FROM");
            script.AppendLine("Справочник.ФизическиеЛица AS ФЛ");
            script.AppendLine("INNER JOIN Справочник.БанковскиеСчетаКонтрагентов AS БСК");
            script.AppendLine("ON ФЛ.Ссылка.uuid = БСК.Владелец.uuid");
            script.AppendLine("AND ФЛ.Ссылка.type = БСК.Владелец.type");
            script.AppendLine("AND ФЛ.Ссылка.TYPE = БСК.Владелец.TYPE");

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
            script.AppendLine("ФЛ.Наименование AS ФИО,");
            script.AppendLine("БСК.НомерСчета AS НомерСчета");
            script.AppendLine("FROM");
            script.AppendLine("Справочник.ФизическиеЛица AS ФЛ");
            script.AppendLine("INNER JOIN Справочник.БанковскиеСчетаКонтрагентов AS БСК");
            script.AppendLine("ON  ФЛ.Ссылка.uuid = БСК.Владелец.uuid");
            script.AppendLine("AND TYPEOF(Справочник.ФизическиеЛица) = БСК.Владелец.type");
            script.AppendLine("AND 0x08 = БСК.Владелец.TYPE");

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
            script.AppendLine("УТДок.Дата AS ТоргДата,");
            script.AppendLine("УТДок.Номер AS ТоргНомер,");
            script.AppendLine("УТЦН.Цена AS ТоргЦена,");
            script.AppendLine("БПДок.Дата AS БухДата,");
            script.AppendLine("БПДок.Номер AS БухНомер,");
            script.AppendLine("БПЦН.Цена AS БухЦена");
            script.AppendLine("FROM");
            script.AppendLine("trade_11_2_3_159_demo.РегистрСведений.ЦеныНоменклатуры AS УТЦН");
            script.AppendLine("LEFT JOIN accounting_3_0_72_72_demo.РегистрСведений.ЦеныНоменклатуры AS БПЦН");
            script.AppendLine("ON  УТЦН.Регистратор.uuid = БПЦН.Регистратор.uuid");
            script.AppendLine("AND УТЦН.Регистратор.type = TYPEOF(trade_11_2_3_159_demo.Документ.УстановкаЦенНоменклатуры)");
            script.AppendLine("AND БПЦН.Регистратор.type = TYPEOF(accounting_3_0_72_72_demo.Документ.УстановкаЦенНоменклатуры)");

            script.AppendLine("LEFT JOIN trade_11_2_3_159_demo.Документ.УстановкаЦенНоменклатуры AS УТДок");
            script.AppendLine("ON  УТЦН.Регистратор.uuid = УТДок.Ссылка.uuid");
            script.AppendLine("AND УТЦН.Регистратор.type = УТДок.Ссылка.type");

            script.AppendLine("LEFT JOIN accounting_3_0_72_72_demo.Документ.УстановкаЦенНоменклатуры AS БПДок");
            script.AppendLine("ON  БПЦН.Регистратор.uuid = БПДок.Ссылка.uuid");
            script.AppendLine("AND БПЦН.Регистратор.type = БПДок.Ссылка.type");

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
            string script = "SELECT TOP 10 Код AS [Код], Наименование AS [Наименование] FROM Справочник.Номенклатура";
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