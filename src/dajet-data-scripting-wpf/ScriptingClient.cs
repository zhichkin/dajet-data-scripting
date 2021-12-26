using DaJet.Data.Scripting.SyntaxTree;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using ICSharpCode.AvalonEdit.Rendering;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Xml;

namespace DaJet.Data.Scripting.Wpf
{
    public sealed class ScriptingClient
    {
        private ToolTip toolTip = new ToolTip();
        private CompletionWindow completionWindow;

        private readonly IParserErrorHandler ErrorHandler;
        private readonly IScriptingService ScriptingService;

        #region "Icons"

        private const string CATALOG_ICON_PATH = "pack://application:,,,/DaJet.Data.Scripting.Wpf;component/images/Справочник.png";
        private const string DOCUMENT_ICON_PATH = "pack://application:,,,/DaJet.Data.Scripting.Wpf;component/images/Документ.png";
        private const string INFOREG_ICON_PATH = "pack://application:,,,/DaJet.Data.Scripting.Wpf;component/images/РегистрСведений.png";
        private const string ACCUMREG_ICON_PATH = "pack://application:,,,/DaJet.Data.Scripting.Wpf;component/images/РегистрНакопления.png";
        private const string EXCHANGE_ICON_PATH = "pack://application:,,,/DaJet.Data.Scripting.Wpf;component/images/ПланОбмена.png";
        private const string DEFAULT_ICON_PATH = "pack://application:,,,/DaJet.Data.Scripting.Wpf;component/images/ВложеннаяТаблица.png";
        private const string MEASURE_ICON_PATH = "pack://application:,,,/DaJet.Data.Scripting.Wpf;component/images/Ресурс.png";
        private const string PROPERTY_ICON_PATH = "pack://application:,,,/DaJet.Data.Scripting.Wpf;component/images/Реквизит.png";
        private const string DIMENSION_ICON_PATH = "pack://application:,,,/DaJet.Data.Scripting.Wpf;component/images/Измерение.png";
        private const string KEYWORD_ICON_PATH = "pack://application:,,,/DaJet.Data.Scripting.Wpf;component/images/KeywordIntellisense.png";

        private readonly BitmapImage CATALOG_ICON = new BitmapImage(new Uri(CATALOG_ICON_PATH));
        private readonly BitmapImage DOCUMENT_ICON = new BitmapImage(new Uri(DOCUMENT_ICON_PATH));
        private readonly BitmapImage INFOREG_ICON = new BitmapImage(new Uri(INFOREG_ICON_PATH));
        private readonly BitmapImage ACCUMREG_ICON = new BitmapImage(new Uri(ACCUMREG_ICON_PATH));
        private readonly BitmapImage EXCHANGE_ICON = new BitmapImage(new Uri(EXCHANGE_ICON_PATH));
        private readonly BitmapImage DEFAULT_ICON = new BitmapImage(new Uri(DEFAULT_ICON_PATH));
        private readonly BitmapImage MEASURE_ICON = new BitmapImage(new Uri(MEASURE_ICON_PATH));
        private readonly BitmapImage PROPERTY_ICON = new BitmapImage(new Uri(PROPERTY_ICON_PATH));
        private readonly BitmapImage DIMENSION_ICON = new BitmapImage(new Uri(DIMENSION_ICON_PATH));
        private readonly BitmapImage KEYWORD_ICON = new BitmapImage(new Uri(KEYWORD_ICON_PATH));

        #endregion

        public ScriptingClient(IScriptingService service, IParserErrorHandler errorHandler)
        {
            ScriptingService = service;
            ErrorHandler = errorHandler;
        }

        public IHighlightingDefinition GetSyntaxHighlightingDefinition()
        {
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("DaJet.Data.Scripting.Wpf.resources.TSQL.xshd"))
            {
                using (var reader = new XmlTextReader(stream))
                {
                    return HighlightingLoader.Load(reader, HighlightingManager.Instance);
                }
            }
        }

        private bool IsAutoCompletionRequested = false; // CTRL+SPACE

        public void TextArea_TextEnteredHandler(object sender, TextCompositionEventArgs e)
        {
            if (!(sender is TextArea textArea)) return;

            int line = textArea.Caret.Line;
            int offset = textArea.Caret.Offset;

            TextReader reader = textArea.Document.CreateReader();
            List<CompletionItem> completions = ScriptingService.RequestCompletion(reader, offset, out IList<ParserWarning> warnings);

            ReportParserWarnings(warnings);

            if (completions == null || completions.Count == 0)
            {
                return;
            }

            completionWindow = new CompletionWindow(textArea)
            {
                MinWidth = 300D,
                MinHeight = 100D
            };
            IList<ICompletionData> data = completionWindow.CompletionList.CompletionData;
            data.Clear();

            foreach (CompletionItem item in completions)
            {
                BitmapImage icon = GetIconByItemType(item.ItemType);
                data.Add(new CompletionData(item.Value, item.Offset, item.Length, icon));
            }

            completionWindow.Show();
            completionWindow.Closed += delegate { completionWindow = null; };
        }
        private BitmapImage GetIconByItemType(string itemType)
        {
            if (itemType == "Справочник") return CATALOG_ICON;
            else if (itemType == "Документ") return DOCUMENT_ICON;
            else if (itemType == "РегистрСведений") return INFOREG_ICON;
            else if (itemType == "РегистрНакопления") return ACCUMREG_ICON;
            else if (itemType == "ПланОбмена") return EXCHANGE_ICON;
            else if (itemType == "Measure") return MEASURE_ICON;
            else if (itemType == "Property") return PROPERTY_ICON;
            else if (itemType == "Dimension") return DIMENSION_ICON;
            else if (itemType == "System") return PROPERTY_ICON;
            else if (itemType == "Hierarchy") return PROPERTY_ICON;
            else if (itemType == "Keyword") return KEYWORD_ICON;
            return DEFAULT_ICON;
        }
        public void TextArea_TextEnteringHandler(object sender, TextCompositionEventArgs e)
        {
            if (IsAutoCompletionRequested) // CTRL+SPACE
            {
                e.Handled = true; // prevent inserting the character that was typed
                IsAutoCompletionRequested = false;
                TextArea_TextEnteredHandler(sender, e);
                return;
            }

            if (e.Text.Length > 0 && completionWindow != null)
            {
                char c = e.Text[0];

                if (!(char.IsLetterOrDigit(c) && c == '_'))
                {
                    // Whenever a non-letter is typed while the completion window is open,
                    // insert the currently selected element.
                    // This will call the Complete method of corresponding ComplitionData class.
                    completionWindow.CompletionList.RequestInsertion(e);
                }
                // Do not set e.Handled=true.
                // We still want to insert the character that was typed.
            }
        }
        public void TextArea_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyboardDevice.Modifiers == ModifierKeys.Control && e.Key == Key.Space)
            {
                IsAutoCompletionRequested = true;
            }
        }

        public void TextArea_TextPasteHandler(object sender, DataObjectPastingEventArgs e)
        {
            string text = (string)e.DataObject.GetData(typeof(string));
            DataObject d = new DataObject();
            d.SetData(DataFormats.Text, text.Replace(Environment.NewLine, " "));
            e.DataObject = d;
        }
        
        public void TextView_MouseHoverHandler(object sender, MouseEventArgs e)
        {
            if (!(sender is TextView textView)) return;
            TextViewPosition? pos = textView.GetPosition(e.GetPosition(textView));
            if (pos != null)
            {
                toolTip.PlacementTarget = textView; // required for property inheritance
                toolTip.Content = pos.ToString();
                toolTip.IsOpen = true;
                e.Handled = true;
            }
        }
        public void TextView_MouseHoverStoppedHandler(object sender, MouseEventArgs e)
        {
            toolTip.IsOpen = false;
        }

        private void ReportParserWarnings(IList<ParserWarning> warnings)
        {
            string message = string.Empty;

            foreach (ParserWarning warning in warnings)
            {
                if (string.IsNullOrEmpty(message))
                {
                    message = GetWarningMessage(warning);
                }
                else
                {
                    message += Environment.NewLine + GetWarningMessage(warning);
                }
            }

            ErrorHandler.HandleError(message);
        }
        private string GetWarningMessage(ParserWarning warning)
        {
            return warning.Message + ", line " + warning.Line.ToString() + ", column " + warning.Column.ToString();
        }

        public SyntaxNode BuildSyntaxTree(TextReader source, out IList<ParserWarning> warnings)
        {
            return ScriptingService.BuildSyntaxTree(source, out warnings);
        }
    }
}