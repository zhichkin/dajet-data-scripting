using DaJet.Metadata.Model;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace DaJet.Data.Scripting
{
    public interface IScriptingService
    {
        string ConnectionString { get; }
        void UseConnectionString(string connectionString);
        ///<summary>Default or initial InfoBase for executing queries and commands.</summary>
        InfoBase MainInfoBase { get; set; }
        ///<summary>Mapping database names to InfoBases.</summary>
        Dictionary<string, InfoBase> Databases { get; }

        string MapSchemaIdentifier(string schemaName);
        string MapTableIdentifier(string databaseName, string tableIdentifier);
        ApplicationObject GetApplicationObject(List<string> tableIdentifiers);
        ApplicationObject GetApplicationObject(string databaseName, string tableIdentifier);
        void FixPropertyTypeCode(ApplicationObject metaObject, MetadataProperty property);

        string PrepareScript(string script, out IList<ParseError> errors);
        string PrepareScript(string script, Dictionary<string, object> parameters, out IList<ParseError> errors);
        string ExecuteJson(string script, out IList<ParseError> errors);
        string ExecuteScript(string script, out IList<ParseError> errors);
        void ExecuteBatch(string script, out IList<ParseError> errors);

        TSqlFragment ParseScript(string script, out IList<ParseError> errors);
        TSqlFragment ParseScript(TextReader reader, out IList<ParseError> errors);
        TSqlFragment ParseScript(TextReader reader, out IList<ParseError> errors, int startOffset, int startLine, int startColumn);
        
        SyntaxNode BuildSyntaxTree(TextReader reader, out IList<ParserWarning> warnings);
        List<CompletionItem> RequestCompletion(TextReader reader, int offset, out IList<ParserWarning> warnings);
        List<ApplicationObject> MatchApplicationObjects(string identifier);
    }
    public sealed class ScriptingService : IScriptingService
    {
        private TSql150Parser Parser { get; }
        private Sql150ScriptGenerator Generator { get; }
        private IScriptExecutor ScriptExecutor { get; }
        private CompletionService CompletionService { get; }

        public ScriptingService()
        {
            Parser = new TSql150Parser(false, SqlEngineType.Standalone);

            Generator = new Sql150ScriptGenerator(new SqlScriptGeneratorOptions()
            {
                AlignClauseBodies = true
            });

            ScriptExecutor = new ScriptExecutor();

            CompletionService = new CompletionService(this);
        }

        public InfoBase MainInfoBase { get; set; }
        public string ConnectionString { get; private set; }
        public Dictionary<string, InfoBase> Databases { get; } = new Dictionary<string, InfoBase>();
        public void UseConnectionString(string connectionString)
        {
            ConnectionString = connectionString;
            ScriptExecutor.UseConnectionString(connectionString);
        }

        public string PrepareScript(string script, out IList<ParseError> errors)
        {
            //if (MetadataService.CurrentDatabase == null) throw new InvalidOperationException("Current database is not defined!");

            TSqlFragment fragment = Parser.Parse(new StringReader(script), out errors);
            if (errors.Count > 0)
            {
                return script;
            }

            //ScriptNode result = new ScriptNode() { Database = MetadataService.CurrentDatabase };
            ScriptNode result = new ScriptNode();
            SyntaxTreeVisitor visitor = new SyntaxTreeVisitor(this);
            visitor.Visit(fragment, result);

            Generator.GenerateScript(fragment, out string sql);
            return sql;
        }
        public string PrepareScript(string script, Dictionary<string, object> parameters, out IList<ParseError> errors)
        {
            //if (MetadataService.CurrentDatabase == null) throw new InvalidOperationException("Current database is not defined!");

            TSqlFragment fragment = Parser.Parse(new StringReader(script), out errors);
            if (errors.Count > 0)
            {
                return script;
            }

            DeclareVariableStatementVisitor parametersVisitor = new DeclareVariableStatementVisitor(parameters);
            fragment.Accept(parametersVisitor);

            //ScriptNode result = new ScriptNode() { Database = MetadataService.CurrentDatabase };
            ScriptNode result = new ScriptNode();
            SyntaxTreeVisitor visitor = new SyntaxTreeVisitor(this);
            visitor.Visit(fragment, result);

            Generator.GenerateScript(fragment, out string sql);
            return sql;
        }
        public TSqlFragment ParseScript(string script, out IList<ParseError> errors)
        {
            return Parser.Parse(new StringReader(script), out errors);
        }
        public TSqlFragment ParseScript(TextReader reader, out IList<ParseError> errors)
        {
            return Parser.Parse(reader, out errors);
        }
        public TSqlFragment ParseScript(TextReader reader, out IList<ParseError> errors, int startOffset, int startLine, int startColumn)
        {
            return Parser.Parse(reader, out errors, startOffset, startLine, startColumn);
        }

        public string ExecuteJson(string script, out IList<ParseError> errors)
        {
            errors = new ParseError[] { }; // TODO
            return ScriptExecutor.ExecuteJsonString(script);
        }
        public string ExecuteScript(string script, out IList<ParseError> errors)
        {
            errors = new ParseError[] { }; // TODO
            return ScriptExecutor.ExecuteJson(script);
        }
        public void ExecuteBatch(string script, out IList<ParseError> errors)
        {
            TSqlFragment syntaxTree = ParseScript(script, out errors);
            if (errors.Count > 0) { return; }

            ScriptExecutor.ExecuteScript((TSqlScript)syntaxTree);
        }

        private bool IsSpecialSchema(string schemaName)
        {
            return (schemaName == "Перечисление"
                || schemaName == "Справочник"
                || schemaName == "Документ"
                || schemaName == "ПланВидовХарактеристик"
                || schemaName == "ПланСчетов"
                || schemaName == "ПланОбмена"
                || schemaName == "РегистрСведений"
                || schemaName == "РегистрНакопления"
                || schemaName == "РегистрБухгалтерии");
        }
        public string MapSchemaIdentifier(string schemaName)
        {
            if (schemaName == "Перечисление"
                || schemaName == "Справочник"
                || schemaName == "Документ"
                || schemaName == "ПланВидовХарактеристик"
                || schemaName == "ПланСчетов"
                || schemaName == "ПланОбмена"
                || schemaName == "РегистрСведений"
                || schemaName == "РегистрНакопления"
                || schemaName == "РегистрБухгалтерии")
            {
                return string.Empty; // default schema name = dbo
            }
            return schemaName;
        }
        public string MapTableIdentifier(string databaseName, string tableIdentifier)
        {
            ApplicationObject @object = GetApplicationObject(databaseName, tableIdentifier);
            if (@object == null)
            {
                return tableIdentifier;
            }
            return @object.TableName;
        }
        private Dictionary<Guid, ApplicationObject> GetCollection(InfoBase infoBase, string identifier)
        {
            if (identifier == "Перечисление") return infoBase.Enumerations;
            else if (identifier == "Справочник") return infoBase.Catalogs;
            else if (identifier == "Документ") return infoBase.Documents;
            else if (identifier == "ПланВидовХарактеристик") return infoBase.Characteristics;
            else if (identifier == "ПланСчетов") return infoBase.Accounts;
            else if (identifier == "ПланОбмена") return infoBase.Publications;
            else if (identifier == "РегистрСведений") return infoBase.InformationRegisters;
            else if (identifier == "РегистрНакопления") return infoBase.AccumulationRegisters;
            else if (identifier == "РегистрБухгалтерии") return infoBase.AccountingRegisters;
            else return null;
        }
        public ApplicationObject GetApplicationObject(List<string> tableIdentifiers)
        {
            if (tableIdentifiers == null || tableIdentifiers.Count != 4) { return null; }

            string databaseName = null;
            string serverIdentifier = tableIdentifiers[0];
            string databaseIdentifier = tableIdentifiers[1];
            string schemaIdentifier = tableIdentifiers[2];
            string tableIdentifier = tableIdentifiers[3];

            if (serverIdentifier != null)
            {
                if (tableIdentifier.Contains('+')) // [server].[database].Документ.[ПоступлениеТоваровУслуг+Товары]
                {
                    databaseName = tableIdentifiers[1];
                    tableIdentifiers[3] = $"{schemaIdentifier}+{tableIdentifier}";
                    tableIdentifiers[2] = string.Empty; // dbo
                }
                else
                {
                    if (IsSpecialSchema(databaseIdentifier)) // [database].Документ.ПоступлениеТоваровУслуг.Товары
                    {
                        databaseName = tableIdentifiers[0];
                        tableIdentifiers[3] = $"{databaseIdentifier}+{schemaIdentifier}+{tableIdentifier}";
                        tableIdentifiers[2] = string.Empty; // dbo
                        tableIdentifiers[1] = serverIdentifier;
                        tableIdentifiers[0] = null;
                    }
                    else if (IsSpecialSchema(schemaIdentifier)) // [server].[database].Документ.ПоступлениеТоваровУслуг
                    {
                        databaseName = tableIdentifiers[1];
                        tableIdentifiers[3] = $"{schemaIdentifier}+{tableIdentifier}";
                        tableIdentifiers[2] = string.Empty; // dbo
                    }
                }
            }
            else if (databaseIdentifier != null)
            {
                if (IsSpecialSchema(databaseIdentifier)) // Документ.ПоступлениеТоваровУслуг.Товары
                {
                    databaseName = tableIdentifiers[1];
                    tableIdentifiers[3] = $"{databaseIdentifier}+{schemaIdentifier}+{tableIdentifier}";
                    tableIdentifiers[2] = null;
                    tableIdentifiers[1] = null;
                }
                else if (IsSpecialSchema(schemaIdentifier)) // [database].Документ.ПоступлениеТоваровУслуг
                {
                    databaseName = tableIdentifiers[1];
                    tableIdentifiers[3] = $"{schemaIdentifier}+{tableIdentifier}";
                    tableIdentifiers[2] = string.Empty; // dbo
                }
            }
            else if (schemaIdentifier != null)
            {
                if (IsSpecialSchema(schemaIdentifier)) // Документ.ПоступлениеТоваровУслуг
                {
                    tableIdentifiers[3] = $"{schemaIdentifier}+{tableIdentifier}";
                    tableIdentifiers[2] = null;
                }
            }
            else // ПоступлениеТоваровУслуг or some normal table
            {
                return null;
            }

            return GetApplicationObject(databaseName, tableIdentifiers[3]);
        }
        public ApplicationObject GetApplicationObject(string databaseName, string tableIdentifier) // $"[Документ+ПоступлениеТоваровУслуг+Товары]"
        {
            if (!tableIdentifier.Contains('+')) return null; // this is not special format, but schema object (table)

            InfoBase database;
            if (string.IsNullOrEmpty(databaseName))
            {
                database = MainInfoBase;
            }
            else if (!Databases.TryGetValue(databaseName, out database))
            {
                return null;
            }

            string tableName = tableIdentifier.TrimStart('[').TrimEnd(']');
            string[] identifiers = tableName.Split('+');

            Dictionary<Guid, ApplicationObject> bo = GetCollection(database, identifiers[0]);
            if (bo == null) return null;

            ApplicationObject @object = bo.Values.Where(mo => mo.Name == identifiers[1]).FirstOrDefault();
            if (@object == null) return null;

            if (identifiers.Length == 3)
            {
                @object = @object.TableParts.Where(mo => mo.Name == identifiers[2]).FirstOrDefault();
                if (@object == null) return null;
            }

            return @object;
        }

        public void FixPropertyTypeCode(ApplicationObject metaObject, MetadataProperty property)
        {
            if (property.PropertyType.ReferenceTypeCode != 0)
            {
                return;
            }

            // TODO: ReferenceTypeCode == 0 this should be fixed in DaJet.Metadata library !!!

            if (property.PropertyType.ReferenceTypeUuid == Guid.Empty
                && property.Purpose == PropertyPurpose.System
                && property.Name == "Ссылка")
            {
                property.PropertyType.ReferenceTypeCode = metaObject.TypeCode;
                return;
            }

            if (MainInfoBase.ReferenceTypeUuids.TryGetValue(property.PropertyType.ReferenceTypeUuid, out ApplicationObject propertyType))
            {
                property.PropertyType.ReferenceTypeCode = propertyType.TypeCode; // patch metadata
            }
            else if (property.Name == "Владелец")
            {
                if (metaObject is Catalog || metaObject is Characteristic)
                {
                    // TODO: this issue should be fixed in DaJet.Metadata library
                    // NOTE: file names lookup - Property.PropertyType.ReferenceTypeUuid for Owner property is a FileName, not metadata object Uuid !!!
                    if (MainInfoBase.Catalogs.TryGetValue(property.PropertyType.ReferenceTypeUuid, out ApplicationObject catalog))
                    {
                        property.PropertyType.ReferenceTypeCode = catalog.TypeCode; // patch metadata
                    }
                    else if (MainInfoBase.Characteristics.TryGetValue(property.PropertyType.ReferenceTypeUuid, out ApplicationObject characteristic))
                    {
                        property.PropertyType.ReferenceTypeCode = characteristic.TypeCode; // patch metadata
                    }
                }
            }
        }

        public List<CompletionItem> RequestCompletion(TextReader reader, int offset, out IList<ParserWarning> warnings)
        {
            warnings = new List<ParserWarning>();

            TSqlFragment parseTree = Parser.Parse(reader, out IList<ParseError> errors);

            foreach (ParseError error in errors)
            {
                warnings.Add(new ParserWarning(error.Number, error.Offset, error.Line, error.Column, error.Message));
            }

            if (parseTree == null || parseTree.ScriptTokenStream == null || parseTree.ScriptTokenStream.Count == 0)
            {
                return new List<CompletionItem>();
            }

            return CompletionService.GetCompletionItems(parseTree, offset);
        }

        public SyntaxNode BuildSyntaxTree(TextReader reader, out IList<ParserWarning> warnings)
        {
            warnings = new List<ParserWarning>();

            TSqlFragment parseTree = Parser.Parse(reader, out IList<ParseError> errors);

            foreach (ParseError error in errors)
            {
                warnings.Add(new ParserWarning(error.Number, error.Offset, error.Line, error.Column, error.Message));
            }

            if (!(parseTree is TSqlScript script))
            {
                return null;
            }

            if (script == null || script.ScriptTokenStream == null || script.ScriptTokenStream.Count == 0)
            {
                return null;
            }

            SyntaxTreeBuilder builder = new SyntaxTreeBuilder();
            builder.Build(script, out SyntaxNode root);
            return root;
        }
        public List<ApplicationObject> MatchApplicationObjects(string identifier)
        {
            List<ApplicationObject> list = new List<ApplicationObject>();

            string[] names = identifier.Split('.', StringSplitOptions.RemoveEmptyEntries);

            if (names.Length < 2)
            {
                return list;
            }

            Dictionary<Guid, ApplicationObject> collection = GetCollection(MainInfoBase, names[0]);
            if (collection == null)
            {
                return list;
            }

            if (names.Length == 2)
            {
                return MatchApplicationObjects(collection, names[1]);
            }

            ApplicationObject entity = collection.Values.Where(item => item.Name == names[1]).FirstOrDefault();
            if (entity == null)
            {
                return list;
            }

            return GetTableParts(entity);
        }
        private List<ApplicationObject> MatchApplicationObjects(Dictionary<Guid, ApplicationObject> collection, string pattern)
        {
            List<ApplicationObject> list = new List<ApplicationObject>();

            CultureInfo culture;
            try
            {
                culture = CultureInfo.GetCultureInfo("ru-RU");
            }
            catch (CultureNotFoundException)
            {
                culture = CultureInfo.CurrentUICulture;
            }

            foreach (ApplicationObject item in collection.Values)
            {
                //if (item.Name.StartsWith(pattern, true, culture))
                //{
                //    list.Add(item); // Поиск по первым символам
                //}

                if (culture.CompareInfo.IndexOf(item.Name, pattern, CompareOptions.IgnoreCase) >= 0)
                {
                    list.Add(item); // Поиск по частичному соответствию
                }
            }

            return list;
        }
        private List<ApplicationObject> GetTableParts(ApplicationObject entity)
        {
            List<ApplicationObject> list = new List<ApplicationObject>();

            foreach (TablePart item in entity.TableParts)
            {
                list.Add(item);
            }

            return list;
        }
    }
}