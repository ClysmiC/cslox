using System;
using System.Collections.Generic;

public class Scanner
{
    protected string            m_source;
    protected List<Token>       m_tokens        = new List<Token>();

    protected int               m_startIndex;
    protected int               m_currentIndex;
    protected int               m_currentLine;

    protected static Dictionary<string, TOKENK> m_reservedWords =
                                                   new Dictionary<string, TOKENK> {
                                                        { "and",    TOKENK.And },
                                                        { "break",  TOKENK.Break },
                                                        { "class",  TOKENK.Class },
                                                        { "continue", TOKENK.Continue},
                                                        { "else",   TOKENK.Else },
                                                        { "false",  TOKENK.False },
                                                        { "for",    TOKENK.For },
                                                        { "fun",    TOKENK.Fun },
                                                        { "if",     TOKENK.If },
                                                        { "nil",    TOKENK.Nil },
                                                        { "or",     TOKENK.Or },
                                                        { "print",  TOKENK.Print },
                                                        { "return", TOKENK.Return },
                                                        { "super",  TOKENK.Super },
                                                        { "this",   TOKENK.This },
                                                        { "true",   TOKENK.True },
                                                        { "var",    TOKENK.Var },
                                                        { "while",  TOKENK.While },
    };

    

    public Scanner (string source)
    {
        m_source = source;
    }

    public List<Token> ScanTokens()
    {
        bool isNumberBeginningWithDecimal = false;
        m_currentLine = 1;
        m_currentIndex = 0;

        while (true)
        {
            m_startIndex = m_currentIndex;

            char throwaway;
            char c = m_source[m_currentIndex];
            m_currentIndex++;

            switch (c)
            {
                case '(': AddToken(TOKENK.OpenParen, null); break;
                case ')': AddToken(TOKENK.CloseParen, null); break;
                case '{': AddToken(TOKENK.OpenBrace, null); break;
                case '}': AddToken(TOKENK.CloseBrace, null); break;
                case ',': AddToken(TOKENK.Comma, null); break;
                case '-': AddToken(TOKENK.Minus, null); break;
                case '+': AddToken(TOKENK.Plus, null); break;
                case ';': AddToken(TOKENK.Semicolon, null); break;
                case '*': AddToken(TOKENK.Star, null); break;

                case '.':
                {
                    if (TryPeekDigit(out throwaway))
                    {
                        isNumberBeginningWithDecimal = true;
                    }
                    else
                    {
                        AddToken(TOKENK.Dot, null);
                    }
                } break;

                case '/':
                {
                    if (TryMatch('/'))
                    {
                        // TODO: add /* */ block comments (and let them nest!)
                        
                        // Comment block. Advance to end of line.

                        while (true)
                        {
                            if (IsAtEnd()) break;

                            if (TryMatch('\n'))
                            {
                                m_currentLine++;
                                break;
                            }
                            else
                            {
                                m_currentIndex++;
                            }
                        }
                    }
                    else
                    {
                        AddToken(TOKENK.Slash, null);
                    }
                } break;


                case '!': AddToken(TryMatch('=') ? TOKENK.BangEqual : TOKENK.Bang, null); break;
                case '=': AddToken(TryMatch('=') ? TOKENK.EqualEqual : TOKENK.Equal, null); break;
                case '>': AddToken(TryMatch('=') ? TOKENK.GreaterEqual : TOKENK.Greater, null); break;
                case '<': AddToken(TryMatch('=') ? TOKENK.LesserEqual : TOKENK.Lesser, null); break;

                case '"':
                {
                    // Handle string literals.

                    bool hasNewLine = false;

                    while (true)
                    {
                        if (IsAtEnd())
                        {
                            Lox.Error(m_currentLine, "Unterminated string");
                        }
                        else if (TryMatch('"'))
                        {
                            // String close
                            // NOTE (andrews) If multi-line string, we still just pass it along to the parser.
                            //  We already complained about it, so no need to also have the parser complain.

                            AddToken(TOKENK.StringLiteral, m_source.Substring(m_startIndex + 1, m_currentIndex - 1 - (m_startIndex + 1)));
                            break;
                        }
                        else if (TryMatch('\n'))
                        {
                            // Multiline string attempt

                            m_currentLine++;
                            if (!hasNewLine)
                            {
                                Lox.Error(m_currentLine, "Multi-line strings are not supported");
                                hasNewLine = true;
                            }
                        }
                        else
                        {
                            m_currentIndex++;
                        }
                    }
                } break;

                case ' ':
                case '\r':
                case '\t':
                break;

                case '\n':
                {
                    m_currentLine++;
                } break;


                default:
                {
                    if (IsDigit(c))
                    {
                        // Number

                        bool hasDecimal = isNumberBeginningWithDecimal;

                        while (true)
                        {
                            if (TryMatch('.'))
                            {
                                if (hasDecimal)
                                {
                                    Lox.Error(m_currentLine, "Number cannot contain multiple decimal points");
                                }
                                else
                                {
                                    hasDecimal = true;
                                }
                            }
                            else if (!TryMatchDigit(out throwaway))
                            {
                                string lexeme = CurrentLexeme();

                                double value;
                                if (Double.TryParse(lexeme, out value))
                                {
                                    AddToken(TOKENK.NumberLiteral, value);
                                }
                                else
                                {
                                    Lox.Error(m_currentLine, "[Compiler Error] Gave Double.TryParse an unparsable value");
                                }

                                break;
                            }
                        }

                        isNumberBeginningWithDecimal = false;
                    }
                    else if (IsLetterOrUnderscore(c))
                    {
                        // Identifier / reserved word

                        while (TryMatchDigitOrLetterOrUnderscore(out throwaway))
                        {
                        }

                        string lexeme = CurrentLexeme();

                        TOKENK tokenk;
                        if (m_reservedWords.TryGetValue(lexeme, out tokenk))
                        {
                            object value = null;
                            if (tokenk == TOKENK.True) value = true;
                            else if (tokenk == TOKENK.False) value = false;

                            AddToken(tokenk, value);
                        }
                        else
                        {
                            AddToken(TOKENK.Identifier, lexeme);
                        }

                    }
                    else
                    {
                        Lox.Error(m_currentLine, "Unexpected character: " + c);
                    }
                } break;
            }

            if (IsAtEnd()) break;
        }

        m_tokens.Add(new Token(TOKENK.Eof, "(eof)", null, m_currentLine));
        return m_tokens;
    }

    protected string CurrentLexeme()
    {
        string lexeme = m_source.Substring(m_startIndex, m_currentIndex - m_startIndex);
        return lexeme;
    }

    protected void AddToken(TOKENK tokenk, object literal)
    {
        m_tokens.Add(new Token(tokenk, CurrentLexeme(), literal, m_currentLine));
    }

    protected bool TryMatch(char expected)
    {
        if (IsAtEnd()) return false;
        if (m_source[m_currentIndex] != expected) return false;

        m_currentIndex++;
        return true;
    }
    
    protected bool TryPeekDigit(out char charDigit)
    {
        return TryMatchDigit(out charDigit, false);
    }

    protected bool TryMatchDigit(out char charDigit, bool consumeOnMatch=true)
    {
        charDigit = '\0';

        if (IsAtEnd()) return false;
        if (IsDigit(m_source[m_currentIndex]))
        {
            charDigit = m_source[m_currentIndex];

            if (consumeOnMatch) m_currentIndex++;

            return true;
        }

        return false;
    }

    protected bool TryMatchDigitOrLetterOrUnderscore(out char charLetter, bool consumeOnMatch=true)
    {
        charLetter = '\0';

        if (IsAtEnd()) return false;
        if (IsDigitOrLetterOrUnderscore(m_source[m_currentIndex]))
        {
            charLetter = m_source[m_currentIndex];

            if (consumeOnMatch) m_currentIndex++;

            return true;
        }

        return false;        
    }

    protected bool IsAtEnd()
    {
        bool result = m_currentIndex >= m_source.Length;
        return result;
    }

    protected bool IsDigit(char c)
    {
        bool result = c >= '0' && c <= '9';
        return result;
    }

    protected bool IsDigitOrLetterOrUnderscore(char c)
    {
        return IsDigit(c) || IsLetterOrUnderscore(c);
    }

    protected bool IsLetterOrUnderscore(char c)
    {
        bool result =
                (c >= 'a' && c <= 'z') ||
                (c >= 'A' && c <= 'Z') ||
                (c == '_');

        return result;
    }
}
