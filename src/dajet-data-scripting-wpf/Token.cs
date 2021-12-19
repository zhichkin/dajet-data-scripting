using System;

namespace DaJet.SqlEditor
{
    public enum TokenType
    {
        Dot,
        Star,
        Plus,
        Minus,
        Slash,
        Comma,
        Semicolon,
        LeftBrace,
        RightBrace,
        LeftCurlyBrace,
        RightCurlyBrace,
        LeftSquareBrace,
        RightSquareBrace,
        Equal,
        NotEqual,
        Less,
        LessOrEqual,
        Greater,
        GreaterOrEqual,
        String,
        Number,
        Keyword,
        Operator,
        Identifier
    }
    readonly public struct Token
    {
        public Token(in TokenType type, int line, int start, int end)
        {
            Type = type;
            Line = line;
            Start = start;
            End = end;
        }
        public readonly int Line { get; }
        public readonly int Start { get; }
        public readonly int End { get; }
        public readonly TokenType Type { get; }
    }
}