using ICSharpCode.AvalonEdit.Document;
using System;
using System.Collections.Generic;

namespace DaJet.SqlEditor
{
    public enum SearchDirection { Left, Right }
    public static class Tokenizer
    {
        public static ReadOnlySpan<char> GetCurrent(TextDocument document, int offset)
        {
            int left = SearchEdge(document, offset, SearchDirection.Left);
            int right = SearchEdge(document, offset, SearchDirection.Right);
            return document.Text.AsSpan(left, right - left);
        }
        private static int SearchEdge(TextDocument document, int offset, SearchDirection direction)
        {
            int index;
            char chr;

            if (direction == SearchDirection.Left)
            {
                if (offset == 0)
                {
                    return offset;
                }

                index = offset - 1; // индекс символа слева от курсора
                chr = document.GetCharAt(index); // символ слева от курсора

                while (index > 0 && !(chr == ' ' || chr == '\n' || chr == '\r'))
                {
                    index--;
                    chr = document.GetCharAt(index);
                }

                return (index == 0 ? index : ++index);
            }
            else
            {
                if (offset == document.Text.Length)
                {
                    return offset;
                }

                index = offset; // индекс символа справа от курсора

                do
                {
                    chr = document.GetCharAt(index); // символ справа от курсора

                    if (chr == ' ' || chr == '\r' || chr == '\n')
                    {
                        break;
                    }

                    index++;
                }
                while (index < document.Text.Length);

                return index;
            }
        }

        private static void AddToken(TokenType type, int line, int start, int end)
        {
            _tokens.Add(new Token(type, line, start, end));
        }

        private static bool EndOfSource()
        {
            return (_offset == _end);
        }
        private static char LookNext()
        {
            if (EndOfSource())
            {
                return char.MinValue;
            }
            return _source.GetCharAt(_offset);
        }
        private static char LookSecond()
        {
            if (_offset + 1 >= _end)
            {
                return char.MinValue;
            }
            return _source.GetCharAt(_offset + 1);
        }
        private static char MoveNext()
        {
            if (EndOfSource())
            {
                return char.MinValue;
            }

            return _source.GetCharAt(_offset++); // amazing !!!
        }
        private static bool MoveIfMatch(char expected)
        {
            if (EndOfSource())
            {
                return false;
            }

            if (_source.GetCharAt(_offset) != expected)
            {
                return false;
            }

            _offset++;
            return true;
        }
        private static bool Ignore(char chr)
        {
            return (chr == ' ' || chr == '\t' || chr == '\r');
        }
        private static bool IsNewLine(char chr)
        {
            return (chr == '\n');
        }
        private static bool IsIdentifier(char chr)
        {
            return (char.IsLetter(chr) || chr == '_' || chr == '[' || chr == ']');
        }

        private static void GetString()
        {
            while (!EndOfSource() && LookNext() != '"') // closing bracket
            {
                if (IsNewLine(LookNext())) // error !?
                {
                    _line++;
                }
                MoveNext();
            }

            if (EndOfSource())
            {
                // unterminated string - keyboard input might not be completed yet
                //throw new ArgumentOutOfRangeException($"Unterminated string.");
            }
            else
            {
                // opening and closing brackets inclusive
                _tokens.Add(new Token(TokenType.String, _line, _start, _offset));

                MoveNext(); // closing bracket
            }
        }
        private static void GetNumber()
        {
            while (char.IsDigit(LookNext()))
            {
                MoveNext();
            }

            if (LookNext() == '.' && char.IsDigit(LookSecond()))
            {
                MoveNext(); // consume the '.'

                while (char.IsDigit(LookNext()))
                {
                    MoveNext();
                }
            }

            // here all digits have been consumed - the offset points to the next after that character
            _tokens.Add(new Token(TokenType.Number, _line, _start, _offset - 1));
        }
        private static void GetIdentifier()
        {
            while (IsIdentifier(LookNext()) || char.IsDigit(LookNext()))
            {
                MoveNext();
            }

            if (keywords.TryGetValue(_source.GetText(_start, _offset - _start), out _))
            {
                _tokens.Add(new Token(TokenType.Keyword, _line, _start, _offset - 1));
            }
            else
            {
                _tokens.Add(new Token(TokenType.Identifier, _line, _start, _offset - 1));
            }
        }

        private static Dictionary<string, TokenType> keywords = new Dictionary<string, TokenType>()
        {
            { "SELECT", TokenType.Keyword },
            { "TOP", TokenType.Keyword },
            { "DISTINCT", TokenType.Keyword },
            { "FROM", TokenType.Keyword },
            { "WHERE", TokenType.Keyword },
            { "AND", TokenType.Keyword },
            { "OR", TokenType.Keyword },
            { "NOT", TokenType.Keyword },
            { "NULL", TokenType.Keyword },
            { "ORDER BY", TokenType.Keyword },
            { "ASC", TokenType.Keyword },
            { "DESC", TokenType.Keyword },
            { "INNER JOIN", TokenType.Keyword },
            { "LEFT JOIN", TokenType.Keyword },
            { "RIGHT JOIN", TokenType.Keyword },
            { "FULL JOIN", TokenType.Keyword },
            { "UNION", TokenType.Keyword },
            { "ON", TokenType.Keyword }
        };

        private static TextDocument _source;
        private static int _start;
        private static int _offset;
        private static int _end;
        private static int _line;
        private static char _current;
        private static List<Token> _tokens;

        public static List<Token> Scan(in TextDocument source)
        {
            _line = 1;
            _start = 0;
            _offset = 0;
            _source = source;
            _end = source.TextLength;
            _tokens = new List<Token>();

            while (!EndOfSource())
            {
                _start = _offset;
                _current = MoveNext();

                if (Ignore(_current)) { /* do nothing and go to the beginning of the loop */ }
                else if (IsNewLine(_current)) { _line++; }
                else if (_current == '.') { AddToken(TokenType.Dot, _line, _start, _start); }
                else if (_current == '*') { AddToken(TokenType.Star, _line, _start, _start); }
                else if (_current == '+') { AddToken(TokenType.Plus, _line, _start, _start); }
                else if (_current == '-') { AddToken(TokenType.Minus, _line, _start, _start); }
                else if (_current == '/') { AddToken(TokenType.Slash, _line, _start, _start); }
                else if (_current == ',') { AddToken(TokenType.Comma, _line, _start, _start); }
                else if (_current == ';') { AddToken(TokenType.Semicolon, _line, _start, _start); }
                else if (_current == '(') { AddToken(TokenType.LeftBrace, _line, _start, _start); }
                else if (_current == ')') { AddToken(TokenType.RightBrace, _line, _start, _start); }
                else if (_current == '{') { AddToken(TokenType.LeftCurlyBrace, _line, _start, _start); }
                else if (_current == '}') { AddToken(TokenType.RightCurlyBrace, _line, _start, _start); }
                //else if (_current == '[') { AddToken(TokenType.LeftSquareBrace, _line, _start, _start); }
                //else if (_current == ']') { AddToken(TokenType.RightSquareBrace, _line, _start, _start); }
                else if (_current == '=') { AddToken(TokenType.Equal, _line, _start, _start); }
                else if (_current == '>')
                {
                    if (MoveIfMatch('='))
                    {
                        AddToken(TokenType.GreaterOrEqual, _line, _start, _start + 1);
                    }
                    else
                    {
                        AddToken(TokenType.Greater, _line, _start, _start);
                    }
                }
                else if (_current == '<')
                {
                    if (MoveIfMatch('='))
                    {
                        AddToken(TokenType.LessOrEqual, _line, _start, _start + 1);
                    }
                    else if (MoveIfMatch('>'))
                    {
                        AddToken(TokenType.NotEqual, _line, _start, _start + 1);
                    }
                    else
                    {
                        AddToken(TokenType.Less, _line, _start, _start);
                    }
                }
                // TODO: else if (_current == '[') { GetIdentifier(); }
                else if (_current == '"') { GetString(); }
                else if (char.IsDigit(_current)) { GetNumber(); }
                else if (IsIdentifier(_current)) { GetIdentifier(); }
                else
                {
                    // TODO: process ()[],; and others ...
                    //throw new ArgumentOutOfRangeException($"Unexpected character '{_current}' at offset {_offset}.");
                }
            }

            return _tokens;
        }
    }
}