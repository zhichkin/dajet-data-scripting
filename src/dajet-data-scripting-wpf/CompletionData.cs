using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using System;
using System.Windows.Media;

namespace DaJet.Data.Scripting.Wpf
{
    public sealed class CompletionData : ICompletionData
    {
        public CompletionData(string text, int fragmentOffset, int fragmentLength)
        {
            Text = text;
            FragmentOffset = fragmentOffset;
            FragmentLength = fragmentLength;
        }
        public CompletionData(string text, int fragmentOffset, int fragmentLength, string description)
            : this(text, fragmentOffset, fragmentLength)
        {
            Description = description;
        }
        public CompletionData(string text, int fragmentOffset, int fragmentLength, ImageSource image)
            : this(text, fragmentOffset, fragmentLength)
        {
            Image = image;
        }

        public ImageSource Image { get; }

        public string Text { get; private set; }

        // Use this property if you want to show a fancy UIElement in the list.
        public object Content
        {
            get { return Text; }
        }

        public object Description { get; }

        public double Priority { get; }

        public int FragmentOffset { get; }
        public int FragmentLength { get; }

        public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
        {
            TextSegment segment = new TextSegment()
            {
                StartOffset = FragmentOffset,
                EndOffset = FragmentOffset + FragmentLength,
                Length = FragmentLength
            };
            textArea.Document.Replace(segment, Text);
        }
    }
}
