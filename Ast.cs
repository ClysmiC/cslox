using System.Collections.Generic;
using System.Diagnostics;

// TODO: Lots of the constructors infer the start line from the expressions they are passed. But some nodes get started
//  by key words/tokens like "if" or "{" which don't get passed into the ctor. Requiring a startLine argument for just those
//  ctors but not the other ones feels weird. Maybe I should just require a start line ctor for every ast node, and for the
//  ones that can also infer I will just add an assert that the supplied start line == the infered one.


public enum ASTNODEK
{
    Expr,
    Stmt,

    Nil = -1
}

public enum EXPRK
{
    Assignment,
    Unary,
    Binary,     // NOTE (andrews) This encompasses logical operators too (and and or) with special execution logic to do short-circuit eval.
                //  This differs from the ebook which creates a separate logical expression kind
    Literal,
    Group,
    Var,
    FunCall,
    This,

    Nil = -1
}

public enum STMTK
{
    Expr,
    Print,
    VarDecl,
    FunDecl,
    ClassDecl,
    Block,
    If,
    While,
    For,        // NOTE (andrews) The ebook just de-sugars for syntax into a while node. I decided to just make a for node.
    Break,
    Continue,
    Return,
    
    Nil = -1
}

public class AstNode
{
    public readonly ASTNODEK    m_astnodek;
    public readonly int         m_startLine;

    public AstNode(ASTNODEK astnodek, int startLine)
    {
        m_astnodek = astnodek;
        m_startLine = startLine;
    }
}

public class AstExpr : AstNode
{
    public readonly EXPRK m_exprk;
    
    public AstExpr(EXPRK exprk, int startLine)
        : base(ASTNODEK.Expr, startLine)
    {
        m_exprk = exprk;
    }
}

public class AstStmt : AstNode
{
    public readonly STMTK m_stmtk;
    
    public AstStmt(STMTK stmtk, int startLine)
        : base(ASTNODEK.Stmt, startLine)
    {
        m_stmtk = stmtk;
    }
}

public class AstAssignmentExpr : AstExpr
{
    public AstVarExpr       m_lhs;
    public AstExpr          m_rhs;

    public AstAssignmentExpr(AstVarExpr lhs, AstExpr rhs)
        : base(EXPRK.Assignment, lhs.m_startLine)
    {
        m_lhs = lhs;
        m_rhs = rhs;
    }
}

public class AstBinaryExpr : AstExpr
{
    public AstExpr      m_leftExpr;
    public AstExpr      m_rightExpr;
    public TOKENK       m_tokenkOp;
    
    public AstBinaryExpr(AstExpr leftExpr, TOKENK tokenkOp, AstExpr rightExpr)
        : base(EXPRK.Binary, leftExpr.m_startLine)
    {
        m_leftExpr = leftExpr;
        m_rightExpr = rightExpr;
        m_tokenkOp = tokenkOp;
    }
}

public class AstGroupExpr : AstExpr
{
    public AstExpr      m_expr;

    public AstGroupExpr(AstExpr expr)
        : base(EXPRK.Group, expr.m_startLine)
    {
        m_expr = expr;
    }
}

public class AstLiteralExpr : AstExpr
{
    public object       m_value;

    // NOTE (andrews) We could just pass in the token and extract
    //  the line and value from that.
    
    public AstLiteralExpr(object value, int line)
        : base(EXPRK.Literal, line)
    {
        m_value = value;
    }
}

public class AstVarExpr : AstExpr
{
    public AstExpr          m_instance;
    public ResolvedIdent    m_identifier = new ResolvedIdent();

    public AstVarExpr(AstExpr instance, Token identifier)
        : base(EXPRK.Var, identifier.m_line)
    {
        m_instance = instance;
        m_identifier.m_identifier = identifier.m_lexeme;
    }

    public AstVarExpr(Token identifier)
        : this(null, identifier)
    {}
}

public class AstUnaryExpr : AstExpr
{
    public AstExpr      m_expr;
    public TOKENK       m_tokenkOp;

    // NOTE (andrews) technically the start line should be the line of the operator.
    //  That info *is* stored on Token's but we only really need the tokenk here
    //  So we just use the expression.

    public AstUnaryExpr(TOKENK tokenkOp, AstExpr expr)
        : base(EXPRK.Unary, expr.m_startLine)
    {
        m_tokenkOp = tokenkOp;
        m_expr = expr;
    }
}

public class AstFunCallExpr : AstExpr
{
    public AstExpr          m_callee;
    public List<AstExpr>    m_args;

    public const int        s_maxArgCount = 255;

    public AstFunCallExpr(AstExpr callee, List<AstExpr> args)
        : base(EXPRK.FunCall, callee.m_startLine)
    {
        m_callee = callee;
        m_args = args;
    }
}

public class AstThisExpr : AstExpr
{
    public ResolvedIdent    m_identifier;
    
    public AstThisExpr(int line)
        : base(EXPRK.This, line)
    {
        m_identifier = new ResolvedIdent();
        m_identifier.m_identifier = "this";
    }
}

public class AstExprStmt : AstStmt
{
    public AstExpr      m_expr;

    public AstExprStmt(AstExpr expr)
        : base(STMTK.Expr, expr.m_startLine)
    {
        m_expr = expr;
    }
}

public class AstPrintStmt : AstStmt
{
    public AstExpr      m_expr;

    public AstPrintStmt(AstExpr expr)
        : base(STMTK.Print, expr.m_startLine) 
    {
        m_expr = expr;
    }
}

public class AstVarDeclStmt : AstStmt
{
    public ResolvedIdent    m_identifier = new ResolvedIdent();
    public AstExpr          m_initExpr;
    
    public AstVarDeclStmt(Token identifier, AstExpr initExpr)
        : base(STMTK.VarDecl, identifier.m_line)
    {
        m_identifier.m_identifier = identifier.m_lexeme;
        m_initExpr = initExpr;
    }
}

public class AstFunDeclStmt : AstStmt
{
    public ResolvedIdent    m_identifier = new ResolvedIdent();
    public List<string>     m_params;
    public List<AstStmt>    m_body;

    public AstFunDeclStmt(Token identifier, List<string> params_, List<AstStmt> body)
        : base(STMTK.FunDecl, identifier.m_line)
    {
        m_identifier.m_identifier = identifier.m_lexeme;
        m_params = params_;
        m_body = body;
    }
}

public class AstClassDeclStmt : AstStmt
{
    public ResolvedIdent            m_identifier = new ResolvedIdent();
    public List<AstFunDeclStmt>     m_funDecls;

    public AstClassDeclStmt(Token identifier, List<AstFunDeclStmt> funDecls)
    : base(STMTK.ClassDecl, identifier.m_line)
    {
        m_identifier.m_identifier = identifier.m_lexeme;
        m_funDecls = funDecls;
    }
}

public class AstBlockStmt : AstStmt
{
    public List<AstStmt>   m_stmts;

    // BB (andrews) Explicitly requiring the open brace line is kinda meh.
    //  Other Ast ctors don't have anything like that since they can infer
    //  their start line from the expressions passed in, but people rarely
    //  start a block on the same line as the first statement in it!
    
    public AstBlockStmt(int openBraceLine, List<AstStmt> stmts)
        : base(STMTK.Block, openBraceLine)
    {
        m_stmts = stmts;
    }
}

public class AstIfStmt : AstStmt
{
    public AstExpr      m_condition;
    public AstStmt      m_body;
    public AstStmt      m_else;

    // BB (andrews): Start line should be provided from the if token
    
    public AstIfStmt(AstExpr condition, AstStmt body, AstStmt else_=null)
        : base(STMTK.If, condition.m_startLine)
    {
        m_condition = condition;
        m_body = body;
        m_else = else_;
    }
}

public class AstWhileStmt : AstStmt
{
    public AstExpr      m_condition;
    public AstStmt      m_body;

    // BB (andrews): Start line should be provided from the while token

    public AstWhileStmt(AstExpr condition, AstStmt body)
        : base(STMTK.While, condition.m_startLine)
    {
        m_condition = condition;
        m_body = body;
    }
}

public class AstForStmt : AstStmt
{
    // NOTE (andrews) pre is EITHER a vardecl OR an expr (or both null)
    
    public AstVarDeclStmt   m_preDecl;
    public AstExpr          m_preExpr;
    
    public AstExpr          m_condition;
    public AstExpr          m_post;
    public AstStmt          m_body;

    public AstForStmt(int startLine, AstVarDeclStmt pre, AstExpr condition, AstExpr post, AstStmt body)
        : base(STMTK.For, startLine)
    {
        m_preDecl = pre;
        m_condition = condition;
        m_post = post;
        m_body = body;
    }
    
    public AstForStmt(int startLine, AstExpr pre, AstExpr condition, AstExpr post, AstStmt body)
        : base(STMTK.For, startLine)
    {
        m_preExpr = pre;
        m_condition = condition;
        m_post = post;
        m_body = body;
    }
}

public class AstBreakStmt : AstStmt
{
    public AstBreakStmt(int line)
        : base(STMTK.Break, line)
    {}
}

public class AstContinueStmt : AstStmt
{
    public AstContinueStmt(int line)
        : base(STMTK.Continue, line)
    {}
}

public class AstReturnStmt : AstStmt
{
    public AstExpr      m_expr;

    public AstReturnStmt(int line)
        : this(line, null)
    {
    }
    
    public AstReturnStmt(int line, AstExpr expr)
        : base(STMTK.Return, line)
    {
        m_expr = expr;
    }
}

public static class AstPrinter
{
    public static string PrintExpr(AstExpr astRoot)
    {
        switch (astRoot.m_exprk)
        {
            case EXPRK.Binary:
            {
                AstBinaryExpr expr = (AstBinaryExpr)astRoot;
                return "( " + expr.m_tokenkOp.ToString() + " " + PrintExpr(expr.m_leftExpr) + " " + PrintExpr(expr.m_rightExpr) + " )";
            } break;

            case EXPRK.Group:
            {
                AstGroupExpr group = (AstGroupExpr)astRoot;
                return "( " + PrintExpr(group.m_expr) + " )";
            } break;

            case EXPRK.Literal:
            {
                AstLiteralExpr expr = (AstLiteralExpr)astRoot;
                return expr.m_value.ToString();
            } break;

            case EXPRK.Unary:
            {
                AstUnaryExpr expr = (AstUnaryExpr)astRoot;
                return "( " + expr.m_tokenkOp.ToString() + " " + PrintExpr(expr.m_expr) + " )";                
            } break;

            default:
            {
                Debug.Fail("Print not implemented for Ast Node type: " + astRoot.m_astnodek);
                return "<error>";
            } break;
        }
    }
}
