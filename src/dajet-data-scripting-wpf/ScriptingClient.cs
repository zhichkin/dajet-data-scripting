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
using System.Xml;

namespace DaJet.Data.Scripting.Wpf
{
    public sealed class ScriptingClient
    {
        private ToolTip toolTip = new ToolTip();
        private CompletionWindow completionWindow;

        private readonly IScriptingService ScriptingService;
        public ScriptingClient(IScriptingService service)
        {
            ScriptingService = service;
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

        public void TextArea_TextEnteredHandler(object sender, TextCompositionEventArgs e)
        {
            if (!(sender is TextArea textArea)) return;

            int line = textArea.Caret.Line;
            int offset = textArea.Caret.Offset;

            completionWindow = new CompletionWindow(textArea)
            {
                MinWidth = 300D,
                MinHeight = 100D
            };
            IList<ICompletionData> data = completionWindow.CompletionList.CompletionData;
            data.Clear();

            TextReader reader = textArea.Document.CreateReader();
            List<CompletionItem> completions = ScriptingService.RequestCompletion(reader, offset, out IList<ParserWarning> warnings);
            foreach (CompletionItem item in completions)
            {
                data.Add(new CompletionData(item.Value));
            }
            if (warnings.Count > 0)
            {
                data.Add(new CompletionData("Warnings:"));
                foreach (ParserWarning warning in warnings)
                {
                    data.Add(new CompletionData(warning.Message));
                }
            }

            completionWindow.Show();
            completionWindow.Closed += delegate { completionWindow = null; };
        }
        public void TextArea_TextEnteringHandler(object sender, TextCompositionEventArgs e)
        {
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

        public SyntaxNode BuildSyntaxTree(TextReader source, out IList<ParserWarning> warnings)
        {
            return ScriptingService.BuildSyntaxTree(source, out warnings);
        }
    }
}