namespace DaJet.Data.Scripting
{
    public sealed class CompletionItem
    {
        public CompletionItem(string value, int offset, int length)
        {
            Value = value;
            Offset = offset;
            Length = length;
        }
        public int Offset { get; }
        public int Length { get; }
        public string Value { get; }
        public string Description { get; set; }
        public string ItemType { get; set; }
    }
}