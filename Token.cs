public enum TOKENK
{
    // TODO: change left/right to open/close

    // TODO: Add break/continue
    
    OpenParen,
    CloseParen,
    OpenBrace,
    CloseBrace,
    Comma,
    Dot,
    Minus,
    Plus,
    Semicolon,
    Slash,
    Star,

    Bang,
    BangEqual,
    Equal,
    EqualEqual,
    Greater,
    GreaterEqual,
    Lesser,
    LesserEqual,

    Identifier,
    StringLiteral,
    NumberLiteral,

    And,
    Break,
    Class,
    Continue,
    Else,
    False,
    For,
    Fun,
    If,
    Nil,
    Or,
    Print,
    Return,
    Super,
    This,
    True,
    Var,
    While,

    Eof,
}

public class Token
{
    public TOKENK       m_tokenk;
    public string       m_lexeme;
    public object       m_literal;
    public int          m_line;

    public Token(TOKENK tokenk, string lexeme, object literal, int line)
    {
        m_tokenk = tokenk;
        m_lexeme = lexeme;
        m_literal = literal;
        m_line = line;
    }

    public override string ToString()
    {
        string m_result = m_tokenk + " " + m_lexeme + " " + m_literal;
        return m_result;
    }
}
