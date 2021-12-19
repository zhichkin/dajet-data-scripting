namespace DaJet.Data.Scripting
{
    public sealed class ParserWarning
    {
        public ParserWarning(int number, int offset, int line, int column, string message)
        {
            Line = line;
            Column = column;
            Offset = offset;
            Number = number;
            Message = message;
        }
        public int Line { get; }
        public int Column { get; }
        public int Offset { get; }
        public int Number { get; }
        public string Message { get; }
    }
}