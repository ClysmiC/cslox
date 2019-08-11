using System.Collections.Generic;
using System.Diagnostics;

// TODO: Better strategy for handling invalid parses of sub-expressions than just constantly checking for null

public class Parser
{
    protected List<Token> m_tokens;
    protected int m_currentIndex = 0;

    protected int m_nestedLoopCount = 0;
    protected int m_nestedFunCount = 0;
    protected int m_nestedClassCount = 0;

    public Parser(List<Token> tokens)
    {
        m_tokens = tokens;
    }

    public List<AstStmt> Parse()
    {
        m_currentIndex = 0;

        List<AstStmt> statements = new List<AstStmt>();

        while (!IsAtEnd())
        {
            AstStmt stmt = ParseDeclStmt();

            if (stmt != null)
            {
                statements.Add(stmt);
            }
            else
            {
                // TODO: error recovery

                return statements;
            }
        }

        return statements;
    }

    protected AstExpr ParseExpr()
    {
        return ParseAssignmentExpr();
    }

    protected AstExpr ParseAssignmentExpr()
    {
        AstExpr lhs = ParseOrExpr();
        if (lhs == null) return EmptyErrorExpr();

        Token token;
        if ((token = TryMatch(TOKENK.Equal)) != null)
        {
            AstExpr rhs = ParseExpr();
            if (rhs == null) return EmptyErrorExpr();

            if (lhs.m_exprk == EXPRK.Var)
            {
                return new AstAssignmentExpr((AstVarExpr)lhs, rhs);
            }
            else
            {
                return ErrorExpr(lhs.m_startLine, "Unable to assign to non-variable expression");
            }
        }
        else
        {
            return lhs;
        }
    }

    protected AstExpr ParseOrExpr()
    {
        AstExpr lhs = ParseAndExpr();

        Token token;
        if ((token = TryMatch(TOKENK.Or)) != null)
        {
            AstExpr rhs = ParseAndExpr();
            if (rhs == null) return EmptyErrorExpr();

            return new AstBinaryExpr(lhs, TOKENK.Or, rhs);
        }

        return lhs;
    }

    protected AstExpr ParseAndExpr()
    {
        AstExpr lhs = ParseEqualityExpr();
        if (lhs == null) return EmptyErrorExpr();

        Token token;
        if ((token = TryMatch(TOKENK.And)) != null)
        {
            AstExpr rhs = ParseEqualityExpr();
            if (rhs == null) return EmptyErrorExpr();

            return new AstBinaryExpr(lhs, TOKENK.And, rhs);
        }

        return lhs;
    }

    protected AstExpr ParseEqualityExpr()
    {
        AstExpr expr = ParseComparisonExpr();
        if (expr == null) return EmptyErrorExpr();

        Token token;
        while ((token = TryMatch(TOKENK.BangEqual, TOKENK.EqualEqual)) != null)
        {
            AstExpr exprRight = ParseComparisonExpr();
            if (exprRight == null) return EmptyErrorExpr();

            expr = new AstBinaryExpr(expr, token.m_tokenk, exprRight);
        }

        return expr;
    }

    protected AstExpr ParseComparisonExpr()
    {
        AstExpr expr = ParseAdditionExpr();
        if (expr == null) return EmptyErrorExpr();

        Token token;
        while ((token = TryMatch(TOKENK.Greater, TOKENK.GreaterEqual, TOKENK.Lesser, TOKENK.LesserEqual)) != null)
        {
            AstExpr exprRight = ParseAdditionExpr();
            if (exprRight == null) return EmptyErrorExpr();

            expr = new AstBinaryExpr(expr, token.m_tokenk, exprRight);
        }

        return expr;
    }

    protected AstExpr ParseAdditionExpr()
    {
        AstExpr expr = ParseMultiplicationExpr();
        if (expr == null) return EmptyErrorExpr();

        Token token;
        while ((token = TryMatch(TOKENK.Plus, TOKENK.Minus)) != null)
        {
            AstExpr exprRight = ParseMultiplicationExpr();
            if (exprRight == null) return EmptyErrorExpr();

            expr = new AstBinaryExpr(expr, token.m_tokenk, exprRight);
        }

        return expr;
    }

    protected AstExpr ParseMultiplicationExpr()
    {
        AstExpr expr = ParseUnaryExpr();
        if (expr == null) return EmptyErrorExpr();

        Token token;        
        while ((token = TryMatch(TOKENK.Star, TOKENK.Slash)) != null)
        {
            AstExpr exprRight = ParseUnaryExpr();
            if (exprRight == null) return EmptyErrorExpr();

            expr = new AstBinaryExpr(expr, token.m_tokenk, exprRight);
        }

        return expr;
    }

    protected AstExpr ParseUnaryExpr()
    {
        Token token;
        if ((token = TryMatch(TOKENK.Bang, TOKENK.Minus)) != null)
        {
            AstExpr exprRight = ParseUnaryExpr();
            if (exprRight == null) return EmptyErrorExpr();

            return new AstUnaryExpr(token.m_tokenk, exprRight);
        }
        else
        {
            return ParseFunCallExpr();
        }
    }

    protected AstExpr ParseFunCallExpr()
    {
        AstExpr expr = ParsePrimaryExpr();
        if (expr == null) return EmptyErrorExpr();
        
        Token token;

        // NOTE (andrews) While loop handles chained function calls and member access like this:
        //  some.member.functionThatReturnsFunction()();
        
        while(true)
        {
            if ((token = TryMatch(TOKENK.OpenParen)) != null)
            {
                bool isFirstArg = true;
                List<AstExpr> args = new List<AstExpr>();
                while ((token = TryMatch(TOKENK.CloseParen)) == null)
                {
                    if (!isFirstArg)
                    {
                        AstExpr prevArg = args[args.Count - 1];

                        if ((token = TryMatch(TOKENK.Comma)) == null)
                            return ErrorExpr(prevArg.m_startLine, "Expected ',' in argument list");

                        if (args.Count == AstFunCallExpr.s_maxArgCount)
                        {
                            // NOTE (andrews) This isn't a parse error so we don't return ErrorExpr like we do for other parse errors.
                            //  This lets us report it while continuing the parse in case we find other errors.

                            Lox.Error(prevArg.m_startLine, "Cannot exceed " + AstFunCallExpr.s_maxArgCount + " arguments");
                        }
                    }

                    AstExpr arg = ParseExpr();
                    if (arg == null) return EmptyErrorExpr();

                    args.Add(arg);
                    isFirstArg = false;
                }

                expr = new AstFunCallExpr(expr, args);
            }
            else if ((token = TryMatch(TOKENK.Dot)) != null)
            {
                if ((token = TryMatch(TOKENK.Identifier)) != null)
                {
                    expr = new AstVarExpr(expr, token);
                }
            }
            else
            {
                return expr;
            }
        }
    }

    protected AstExpr ParsePrimaryExpr()
    {
        Token token;
        if ((token = TryMatch(TOKENK.OpenParen)) != null)
        {
            int lParenLine = token.m_line;
            AstExpr expr = ParseExpr();
            if (expr == null) return EmptyErrorExpr();

            if ((token = TryMatch(TOKENK.CloseParen)) == null)
                return ErrorExpr(lParenLine, "Expected )");
            
            return new AstGroupExpr(expr);
        }
        else if ((token = TryMatch(TOKENK.NumberLiteral, TOKENK.StringLiteral, TOKENK.False, TOKENK.True, TOKENK.Nil)) != null)
        {
            return new AstLiteralExpr(token.m_literal, token.m_line);
        }
        else if ((token = TryMatch(TOKENK.Identifier)) != null)
        {
            return new AstVarExpr(token);
        }
        else if ((token = TryMatch(TOKENK.This)) != null)
        {
            if (m_nestedClassCount <= 0)
                return ErrorExpr(token.m_line, "\"this\" only valid inside of class method");

            Debug.Assert(m_nestedFunCount > 0);

            return new AstThisExpr(token.m_line);
        }
        else
        {
            Token nextToken = Peek();
            if (nextToken == null)
            {
                return ErrorExpr(m_tokens[m_tokens.Count - 1].m_line, "Unexpected end of file");
            }
            else
            {
                return ErrorExpr(nextToken.m_line, "Expected expression");
            }
        }
    }

    protected AstStmt ParseDeclStmt()
    {
        Token token;
        if ((token = TryMatch(TOKENK.Var)) != null)
        {
            return ParseVarDeclStmt();
        }
        else if ((token = TryMatch(TOKENK.Fun)) != null)
        {
            return ParseFunDeclStmt();
        }
        else if ((token = TryMatch(TOKENK.Class)) != null)
        {
            return ParseClassDeclStmt();
        }
        else
        {
            return ParseStmt();
        }
    }

    protected AstStmt ParseVarDeclStmt()
    {
        Token var_ = Previous();
        Debug.Assert(var_.m_tokenk == TOKENK.Var);

        Token ident;
        AstExpr initExpr = null;
        if ((ident = TryMatch(TOKENK.Identifier)) != null)
        {
            Token equals;
            if ((equals = TryMatch(TOKENK.Equal)) != null)
            {
                initExpr = ParseExpr();
                if (initExpr == null) return EmptyErrorStmt();
            }

            Token semicolon;
            if ((semicolon = TryMatch(TOKENK.Semicolon)) == null)
            {
                return ErrorStmt(var_.m_line, "Expected ';'");

                // NOTE (andrews) In the future, remove this null return. Ideally, we would still return the declstmt
                //  if there is a missing semicolon to prevent a bunch of "undeclared identifier" errors from cascading
                //  from this. But as things are right now, undeclared identifier checks happen at run-time, not compile
                //  time. If I want to rework it to happen at compile time, I need to remove the return null here and
                //  just lean on the rest of the error handling to not blow up since we are missing a semi-colon.
            }
        }
        else
        {
            return ErrorStmt(var_.m_line, "Expected identifier after \"var\"");
        }
        
        
        return new AstVarDeclStmt(ident, initExpr);
    }

    protected AstStmt ParseFunDeclStmt()
    {
        PushFun();
        try
        {
            // BB (andrews) Many of the error lines are only "close enough"
        
            Token fun = Previous();
            Debug.Assert(fun.m_tokenk == TOKENK.Fun);

            Token ident;
            if ((ident = TryMatch(TOKENK.Identifier)) == null)
                return ErrorStmt(fun.m_line, "Expected identifier after \"fun\"");

            Token token;
            if ((token = TryMatch(TOKENK.OpenParen)) == null)
                return ErrorStmt(fun.m_line, "Expected '(' after function name");

            List<string> params_ = new List<string>();
            bool isFirstParam = true;
        
            while ((token = TryMatch(TOKENK.CloseParen)) == null)
            {
                if (!isFirstParam)
                {                    
                    if ((token = TryMatch(TOKENK.Comma)) == null)
                        return ErrorStmt(fun.m_line, "Expected ',' in parameter list");
                    
                    if (params_.Count == AstFunCallExpr.s_maxArgCount)
                    {
                        // NOTE (andrews) This isn't a parse error so we don't return ErrorExpr like we do for other parse errors.
                        //  This lets us report it while continuing the parse in case we find other errors.
                        
                        Lox.Error(fun.m_line, "Cannot exceed " + AstFunCallExpr.s_maxArgCount + " parameters");
                    }
                }

                if ((token = TryMatch(TOKENK.Identifier)) == null)
                {
                
                    return ErrorStmt(ident.m_line, "Expected parameter identifier");
                }

                params_.Add(token.m_lexeme);
                isFirstParam = false;
            }

            if ((token = TryMatch(TOKENK.OpenBrace)) == null)
                return ErrorStmt(fun.m_line, "Expected '{'");

            AstStmt block = ParseBlockStmt();
            if (block == null) return EmptyErrorStmt();

            Debug.Assert(block.m_stmtk == STMTK.Block);

            return new AstFunDeclStmt(ident, params_, ((AstBlockStmt)block).m_stmts);
        }
        finally
        {
            PopFun();
        }
    }

    protected AstStmt ParseClassDeclStmt()
    {
        PushClass();
        try
        {
            Token class_ = Previous();
            Debug.Assert(class_.m_tokenk == TOKENK.Class);

            Token name;
            if ((name = TryMatch(TOKENK.Identifier)) == null)
                return ErrorStmt(class_.m_line, "Expected class name identifier");

            Token token;
            if ((token = TryMatch(TOKENK.OpenBrace)) == null)
                return ErrorStmt(class_.m_line, "Expected '{' after class name \"" + name.m_lexeme + "\"");

            var funDecls = new List<AstFunDeclStmt>();

            while (true)
            {
                if (IsAtEnd())
                {
                    return ErrorStmt(m_tokens[m_tokens.Count - 1].m_line, "Expected '}'");
                }
                else if ((token = TryMatch(TOKENK.CloseBrace)) != null)
                {
                    return new AstClassDeclStmt(name, funDecls);
                }
                else if ((token = TryMatch(TOKENK.Fun)) == null)
                {
                    // NOTE (andrews) This differs from the ebook in that it requires method declaration to also use "fun"

                    return ErrorStmt(Peek().m_line, "Expected function declaration beginning with \"fun\"");
                }

                var funDecl = ParseFunDeclStmt();
                if (funDecl == null) return EmptyErrorStmt(); ;

                Debug.Assert(funDecl.m_stmtk == STMTK.FunDecl);
                funDecls.Add((AstFunDeclStmt)funDecl);
            }
        }
        finally
        {
            PopClass();
        }
    }

    protected AstStmt ParseStmt()
    {
        Token token;
        if ((token = TryMatch(TOKENK.Print)) != null)
        {
            return ParsePrintStmt();
        }
        else if ((token = TryMatch(TOKENK.OpenBrace)) != null)
        {
            return ParseBlockStmt();
        }
        else if ((token = TryMatch(TOKENK.If)) != null)
        {
            return ParseIfStmt();
        }
        else if ((token = TryMatch(TOKENK.While)) != null)
        {
            return ParseWhileStmt();
        }
        else if ((token = TryMatch(TOKENK.For)) != null)
        {
            return ParseForStmt();
        }
        else if ((token = TryMatch(TOKENK.Break)) != null)
        {
            return ParseBreakStmt();
        }
        else if ((token = TryMatch(TOKENK.Continue)) != null)
        {
            return ParseContinueStmt();
        }
        else if ((token = TryMatch(TOKENK.Return)) != null)
        {
            return ParseReturnStmt();
        }
        else
        {
            return ParseExprStmt();
        }
    }

    protected AstStmt ParseBlockStmt()
    {
        Token openBrace = Previous();
        Debug.Assert(openBrace.m_tokenk == TOKENK.OpenBrace);

        Token closeBrace;
        List<AstStmt> stmts = new List<AstStmt>();

        while (true)
        {
            if (IsAtEnd())
            {
                return ErrorStmt(m_tokens[m_tokens.Count - 1].m_line, "Expected '}'");
            }
            else if ((closeBrace = TryMatch(TOKENK.CloseBrace)) != null)
            {
                return new AstBlockStmt(openBrace.m_line, stmts);
            }

            var declStmt = ParseDeclStmt();
            if (declStmt == null) return EmptyErrorStmt();
            
            stmts.Add(declStmt);
        }
    }

    // TODO: Consider not using parentheses around the conditional clause? The ebook says that a delimiter after the
    //  conditional is required, but it could be ), then, or {
    //  I think Jai doesn't require parentheses or a delimiter though? I wonder how it handles some of these ambiguous
    //  cases or if they are just kind of ignored since most of them are pretty nonsense:
    //  https://softwareengineering.stackexchange.com/questions/335504/why-do-languages-require-parenthesis-around-expressions-when-used-with-if-and
    
    protected AstStmt ParseIfStmt()
    {
        Token ifToken = Previous();
        Debug.Assert(ifToken.m_tokenk == TOKENK.If);

        Token openParen;
        Token closeParen;

        AstExpr condition;
        AstStmt body;
        
        // TODO: A lot of these errors after getting null expr/stmt are probably reported as errors at the spots
        //  where they are parsed and returned as null. It might be redundant to also report them here. Do an audit
        //  of what kinds of malformed if's lead to these errors and see if the additional message actually provides
        //  any useful information. It *might*, since the other errors aren't reported in the context of an "if"
        //  statement but these ones are. Do the audit to find out!!
        
        if ((openParen = TryMatch(TOKENK.OpenParen)) == null)
            return ErrorStmt(ifToken.m_line, "Expected '(' after \"if\"");
        
        condition = ParseExpr();
        if (condition == null)
            return EmptyErrorStmt();

        if ((closeParen = TryMatch(TOKENK.CloseParen)) == null)
            return ErrorStmt(condition.m_startLine, "Expected ')' after \"if\" conditional");
                    
        body = ParseStmt();
        if (body == null)
            return EmptyErrorStmt();
                        
        Token elseToken;
        AstStmt elseStmt = null;
        if ((elseToken = TryMatch(TOKENK.Else)) != null)
        {
            // TODO: Right now we assign a dangling else to the nearest "if". I would prefer if
            //  we could *detect* the ambiguous dangling else case and simply make it an error.
            //  This would require some special bookkeeping, but I don't see why it wouldn't be possible.

            elseStmt = ParseStmt();
            if (elseStmt == null)
            {
                return ErrorStmt(elseToken.m_line, "Expected statement after \"else\"");
            }
        }

        return new AstIfStmt(condition, body, elseStmt);
    }

    protected AstStmt ParseWhileStmt()
    {
        PushLoop();     // Ugh... really wish there was some sort a nicer way to defer PopLoop
        try
        {
            Token whileToken = Previous();
            Debug.Assert(whileToken.m_tokenk == TOKENK.While);

            Token openParen;
            Token closeParen;

            if ((openParen = TryMatch(TOKENK.OpenParen)) == null)
            {
                return ErrorStmt(whileToken.m_line, "Expected '(' after \"while\"");
            }
        
            AstExpr condition = ParseExpr();
            if (condition == null) return EmptyErrorStmt();

            if ((closeParen = TryMatch(TOKENK.CloseParen)) == null)
            {
                return ErrorStmt(condition.m_startLine, "Expected ')' after \"while\" conditional");
            }
            
            AstStmt stmt = ParseStmt();
            if (stmt == null) return EmptyErrorStmt();

            return new AstWhileStmt(condition, stmt);
        }
        finally
        {
            PopLoop();
        }
    }

    protected AstStmt ParseForStmt()
    {
        PushLoop();
        try
        {
        
            Token forToken = Previous();
            Debug.Assert(forToken.m_tokenk == TOKENK.For);

            Token openParen;
            Token closeParen;
        
            AstVarDeclStmt preDecl = null;
            AstExpr preExpr = null;
            AstExpr condition = null;
            AstExpr post = null;
        
            Token semicolon;

            if ((openParen = TryMatch(TOKENK.OpenParen)) == null)
            {
                return ErrorStmt(forToken.m_line, "Expected '(' after \"for\"");
            }
        
            if ((semicolon = TryMatch(TOKENK.Semicolon)) == null)
            {
                Token varToken;
                if ((varToken = TryMatch(TOKENK.Var)) != null)
                {
                    AstStmt stmt = ParseVarDeclStmt();
                    if (stmt == null) return EmptyErrorStmt();

                    Debug.Assert(stmt.m_stmtk == STMTK.VarDecl);
                    preDecl = (AstVarDeclStmt)stmt;
                }
                else
                {
                    AstStmt stmt = ParseExprStmt();
                    if (stmt == null) return EmptyErrorStmt();

                    Debug.Assert(stmt.m_stmtk == STMTK.Expr);
                    preExpr = ((AstExprStmt)stmt).m_expr;
                }
            }

            if ((semicolon = TryMatch(TOKENK.Semicolon)) == null)
            {
                AstStmt stmt = ParseExprStmt();
                if (stmt == null) return EmptyErrorStmt();

                Debug.Assert(stmt.m_stmtk == STMTK.Expr);
                condition = ((AstExprStmt)stmt).m_expr;
            }

            if ((closeParen = TryMatch(TOKENK.CloseParen)) == null)
            {
                post = ParseExpr();
                if (post == null) return EmptyErrorStmt();

                if ((closeParen = TryMatch(TOKENK.CloseParen)) == null)
                {
                    return ErrorStmt(forToken.m_line, "Expected ')' at end of \"for\"");
                }
            }

            AstStmt body = ParseStmt();
            if (body == null) return EmptyErrorStmt();

            if (preDecl != null)    return new AstForStmt(forToken.m_line, preDecl, condition, post, body);
            else                    return new AstForStmt(forToken.m_line, preExpr, condition, post, body);
        }
        finally
        {
            PopLoop();
        }
    }

    protected AstStmt ParseBreakStmt()
    {
        Token breakToken = Previous();
        Debug.Assert(breakToken.m_tokenk == TOKENK.Break);

        if (m_nestedLoopCount == 0)
            return ErrorStmt(breakToken.m_line, "\"break\" not valid outside of loop");

        Token token;
        if ((token = TryMatch(TOKENK.Semicolon)) == null)
            return ErrorStmt(breakToken.m_line, "Expected ';'");

        return new AstBreakStmt(breakToken.m_line);
    }

    protected AstStmt ParseContinueStmt()
    {
        Token continueToken = Previous();
        Debug.Assert(continueToken.m_tokenk == TOKENK.Continue);

        if (m_nestedLoopCount == 0)
            return ErrorStmt(continueToken.m_line, "\"continue\" not valid outside of loop");

        Token token;
        if ((token = TryMatch(TOKENK.Semicolon)) == null)
            return ErrorStmt(continueToken.m_line, "Expected ';'");

        return new AstContinueStmt(continueToken.m_line);
    }

    protected AstStmt ParseReturnStmt()
    {
        Token returnToken = Previous();
        Debug.Assert(returnToken.m_tokenk == TOKENK.Return);

        if (m_nestedFunCount <= 0)
        {
            return ErrorStmt(returnToken.m_line, "\"return\" only valid inside of function");
        }

        Token semicolon;
        if ((semicolon = TryMatch(TOKENK.Semicolon)) != null)
        {
            // return void
            
            return new AstReturnStmt(returnToken.m_line);
        }
        
        AstExpr expr = ParseExpr();
        if (expr == null) return EmptyErrorStmt();

        if ((semicolon = TryMatch(TOKENK.Semicolon)) == null)
            return ErrorStmt(expr.m_startLine, "Expected ';'");

        return new AstReturnStmt(returnToken.m_line, expr);
    }

    protected AstStmt ParsePrintStmt()
    {
        AstExpr expr = ParseExpr();
        if (expr == null) return EmptyErrorStmt();

        Token token;
        if ((token = TryMatch(TOKENK.Semicolon)) == null)
            return ErrorStmt(expr.m_startLine, "Expected ';'");

        return new AstPrintStmt(expr);
    }

    protected AstStmt ParseExprStmt()
    {
        AstExpr expr = ParseExpr();
        if (expr == null) return EmptyErrorStmt();
        
        Token token;
        if ((token = TryMatch(TOKENK.Semicolon)) == null)
        {
            return ErrorStmt(expr.m_startLine, "Expected ';'");
        }

        return new AstExprStmt(expr);
    }

    protected Token TryMatch(params TOKENK[] aryTokenkMatch)
    {
        if (IsAtEnd()) return null;

        Token token = m_tokens[m_currentIndex];
        for (int i = 0; i < aryTokenkMatch.Length; i++)
        {
            TOKENK tokenkMatch = aryTokenkMatch[i];

            if (token.m_tokenk == tokenkMatch)
            {
                m_currentIndex++;   // Consume
                return token;
            }
        }

        return null;
    }

    protected Token Previous()
    {
        if (m_currentIndex > 0 && m_tokens.Count != 0)
            return m_tokens[m_currentIndex - 1];

        return null;
    }

    protected Token Peek()
    {
        if (IsAtEnd()) return null;
        return m_tokens[m_currentIndex];
    }

    protected bool IsAtEnd()
    {
        bool result = m_currentIndex >= m_tokens.Count - 1;

        if (!result)
        {
            Debug.Assert(m_tokens[m_tokens.Count - 1].m_tokenk == TOKENK.Eof);
        }

        return result;
    }

    protected void PushLoop()
    {
        m_nestedLoopCount++;
    }

    protected void PopLoop()
    {
        m_nestedLoopCount--;
        Debug.Assert(m_nestedLoopCount >= 0);
    }

    protected void PushFun()
    {
        m_nestedFunCount++;
    }

    protected void PopFun()
    {
        m_nestedFunCount--;
        Debug.Assert(m_nestedFunCount >= 0);
    }

    protected void PushClass()
    {
        m_nestedClassCount++;
    }

    protected void PopClass()
    {
        m_nestedClassCount--;
        Debug.Assert(m_nestedClassCount >= 0);
    }

    protected AstStmt EmptyErrorStmt()
    {
        Debug.Assert(Lox.s_errorCount > 0);
        return null;
    }

    protected AstStmt ErrorStmt(int line, string error)
    {
        Lox.Error(line, error);
        return null;
    }

    protected AstExpr EmptyErrorExpr()
    {
        Debug.Assert(Lox.s_errorCount > 0);
        return null;
    }

    protected AstExpr ErrorExpr(int line, string error)
    {
        Lox.Error(line, error);
        return null;
    }
}
