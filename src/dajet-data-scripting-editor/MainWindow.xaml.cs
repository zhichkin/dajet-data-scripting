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
    public partial class MainWindow : Window
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

            EditorService = new ScriptingClient(scripting);

            //textEditor.TextArea.TextEntered += EditorService.TextArea_TextEnteredHandler;
            //textEditor.TextArea.TextEntering += EditorService.TextArea_TextEnteringHandler;
            //DataObject.AddPastingHandler(textEditor.TextArea, EditorService.TextArea_TextPasteHandler);
            //textEditor.TextArea.TextView.MouseHover += EditorService.TextView_MouseHoverHandler;
            //textEditor.TextArea.TextView.MouseHoverStopped += EditorService.TextView_MouseHoverStoppedHandler;
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

            ShowScriptInfo(root);
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

            foreach (StatementNode statement in script.Statements)
            {
                info += Environment.NewLine + "SELECT";
                foreach (ColumnNode column in statement.Columns)
                {
                    info += Environment.NewLine + column.Name + (string.IsNullOrEmpty(column.Alias) ? string.Empty : " AS [" + column.Alias + "]");
                }
                info += Environment.NewLine + "FROM";
                foreach (SyntaxNode node in statement.Tables.Values)
                {
                    if (node is TableNode table)
                    {
                        info += Environment.NewLine + table.Name + (string.IsNullOrEmpty(table.Alias) ? string.Empty : " AS [" + table.Alias + "]");
                    }
                    else if (node is QueryNode query)
                    {
                        info += Environment.NewLine + (string.IsNullOrEmpty(query.Alias) ? string.Empty : " AS [" + query.Alias + "]");
                    }
                }
                info += Environment.NewLine + "WHERE";
                if (statement.Where != null)
                {
                    foreach (ColumnNode column in statement.Where.Columns)
                    {
                        info += Environment.NewLine + column.Name + (string.IsNullOrEmpty(column.Alias) ? string.Empty : " AS [" + column.Alias + "]");
                    }
                }
            }

            warningsBlock.Text += info;
        }
    }
}