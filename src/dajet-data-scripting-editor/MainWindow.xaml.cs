using DaJet.Data.Scripting.SyntaxTree;
using DaJet.Data.Scripting.Wpf;
using DaJet.Metadata;
using DaJet.Metadata.Model;
using ICSharpCode.AvalonEdit.Folding;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;

namespace DaJet.Data.Scripting.Editor
{
    public partial class MainWindow : Window, IParserErrorHandler
    {
        private FoldingManager foldingManager;
        private readonly ScriptingClient EditorService;
        private readonly SelectStatementFoldingStrategy foldingStrategy = new SelectStatementFoldingStrategy();

        public MainWindow()
        {
            InitializeComponent();

            IMetadataService metadata = new MetadataService();

            if (!metadata
                .UseDatabaseProvider(DatabaseProvider.SQLServer)
                .UseConnectionString("Data Source=ZHICHKIN;Initial Catalog=trade_11_2_3_159_demo;Integrated Security=True")
                .TryOpenInfoBase(out InfoBase infoBase, out string error))
            {
                textEditor.Text = error;
                return;
            }

            IScriptingService scripting = new ScriptingService();
            scripting.UseConnectionString(metadata.ConnectionString);
            scripting.MainInfoBase = infoBase;

            EditorService = new ScriptingClient(scripting, this);

            textEditor.TextArea.KeyDown += EditorService.TextArea_KeyDown;
            textEditor.TextArea.TextEntered += EditorService.TextArea_TextEnteredHandler;
            textEditor.TextArea.TextEntering += EditorService.TextArea_TextEnteringHandler;
            DataObject.AddPastingHandler(textEditor.TextArea, EditorService.TextArea_TextPasteHandler);
            //textEditor.TextArea.TextView.MouseHover += EditorService.TextView_MouseHoverHandler;
            //textEditor.TextArea.TextView.MouseHoverStopped += EditorService.TextView_MouseHoverStoppedHandler;
        }

        public void HandleError(string message)
        {
            warningsBlock.Text = message;
        }
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            textEditor.SyntaxHighlighting = EditorService.GetSyntaxHighlightingDefinition();

            foldingManager = FoldingManager.Install(textEditor.TextArea);
        }
        private void Fold_Button_Click(object sender, RoutedEventArgs e)
        {
            foldingStrategy.UpdateFoldings(foldingManager, textEditor.Document);
        }
        private void Open_Button_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog()
            {
                Filter = "sql files (*.sql)|*.sql|all files (*.*)|*.*"
            };

            if (!dialog.ShowDialog().Value)
            {
                return;
            }

            textEditor.Load(dialog.FileName);
        }

        private void Build_Button_Click(object sender, RoutedEventArgs e)
        {
            TextReader reader = textEditor.TextArea.Document.CreateReader();
            SyntaxNode root = EditorService.BuildSyntaxTree(reader, out IList<ParserWarning> warnings);

            ShowParserWarnings(warnings);

            ShowScriptInfo(root);
        }
        private void ShowParserWarnings(IList<ParserWarning> warnings)
        {
            warningsBlock.Text = string.Empty;

            foreach (ParserWarning warning in warnings)
            {
                string message = GetWarningMessage(warning);

                if (string.IsNullOrEmpty(warningsBlock.Text))
                {
                    warningsBlock.Text = message;
                }
                else
                {
                    warningsBlock.Text += Environment.NewLine + message;
                }
            }
        }
        private string GetWarningMessage(ParserWarning warning)
        {
            return warning.Message + ", line " + warning.Line.ToString() + ", column " + warning.Column.ToString();
        }
        private void ShowScriptInfo(SyntaxNode root)
        {
            if (!(root is ScriptNode script))
            {
                return;
            }

            string info = string.Empty;

            foreach (SelectNode select in script.Statements)
            {
                int indent = 1;
                ShowTables(select, ref info, indent);
            }

            warningsBlock.Text += info;
        }
        private void ShowTables(ITableScopeProvider scope, ref string info, int indent)
        {
            info += Environment.NewLine + "".PadLeft(indent, '-')
                + scope.GetType().ToString()
                + " - " + (scope is QueryNode q ? q.Alias : string.Empty)
                + " : " + (scope is JoinNode join ? join.JoinType.ToString() : string.Empty);

            SelectNode select = scope as SelectNode;

            if (select != null)
            {
                info += Environment.NewLine + "".PadLeft(indent, '-') + "SELECT";
                foreach (ColumnNode column in select.Columns)
                {
                    info += Environment.NewLine + "".PadLeft(indent, '-')
                        + column.Name + (string.IsNullOrEmpty(column.Alias) ? string.Empty : " AS [" + column.Alias + "]");
                }
                info += Environment.NewLine + "".PadLeft(indent, '-') + "FROM";
            }

            foreach (SyntaxNode node in scope.Tables)
            {
                if (node is TableNode table)
                {
                    string alias = string.Empty;
                    if (table.Parent is QueryNode query)
                    {
                        alias = query.Alias;
                    }
                    else
                    {
                        alias = table.Alias;
                    }
                    info += Environment.NewLine + "".PadLeft(++indent, '-')
                        + table.Name
                        + ((string.IsNullOrEmpty(alias) ? string.Empty : " AS [" + alias + "]")
                        + " - (" + table.Parent.GetType().ToString() + ") + " + alias);
                }
                else if (node is ITableScopeProvider tsp)
                {
                    ShowTables(tsp, ref info, ++indent);
                }
            }

            if (scope.Where.Columns.Count > 0)
            {
                info += Environment.NewLine + "".PadLeft(indent, '-') + "WHERE";
                foreach (ColumnNode column in scope.Where.Columns)
                {
                    info += Environment.NewLine + "".PadLeft(indent, '-')
                        + column.Name + (string.IsNullOrEmpty(column.Alias) ? string.Empty : " AS [" + column.Alias + "] ")
                        + scope.GetType().ToString()
                        + " - " + (scope is QueryNode q1 ? q1.Alias : string.Empty)
                        + " : " + (scope is JoinNode join1 ? join1.JoinType.ToString() : string.Empty);
                }
            }
        }
    }
}
//SELECT f1 FROM z
//INNER JOIN
//  (SELECT f2 FROM a
//     LEFT JOIN b ON a.f = b.f
//     RIGHT JOIN (SELECT f3 FROM c WHERE c.f5 = 123) AS a ON a.f = c1.f
//     FULL JOIN (SELECT f4 FROM d) AS dt ON c.f = d.f
//   WHERE a.f6 IS NOT NULL) AS x
//ON z.f = x.f
//WHERE z.f7 = "abc"