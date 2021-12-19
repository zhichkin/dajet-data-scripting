namespace DaJet.Data.Scripting
{
    public sealed class CompletionItem
    {
        public CompletionItem(string value)
        {
            Value = value;
        }
        public string Value { get; private set; }
    }
}