using System.Collections.Generic;
using System.Diagnostics;

public class ResolvePass
{
    // TODO (andrews) Global scope lookup is done at runtime to achieve order independence. A big TODO is to make
    //  it resolved statically, but still order-independent.

    // Also, since global variables are resolved at runtime that has the ugly side-effect of failing to catch a user redefining
    //  a global variable in this pass. Below code is a runtime error, but should be static error that we catch here!

    /*
        var a = 2;
    
        fun printA()
        {
            print a;
        }

        var a = "hello";    // runtime error

        printA();
     */
    
    public List<Dictionary<string, bool>> m_scopes = new List<Dictionary<string, bool>>();

    public bool m_error = false;
    
    public ResolvePass()
    {
    }

    public void ResolveStmts(List<AstStmt> stmts)
    {
        // TODO: Do I need to check for errors here?
        
        foreach (AstStmt stmt in stmts)
        {
            ResolveStmt(stmt);
        }
    }
    
    protected void ResolveStmt(AstStmt stmt)
    {
        // todo when error?
        switch (stmt.m_stmtk)
        {
            case STMTK.Expr:
                ResolveExprStmt((AstExprStmt)stmt); break;

            case STMTK.Print:
                ResolvePrintStmt((AstPrintStmt)stmt); break;

            case STMTK.VarDecl:
                ResolveVarDeclStmt((AstVarDeclStmt)stmt); break;

            case STMTK.FunDecl:
                ResolveFunDeclStmt((AstFunDeclStmt)stmt); break;

            case STMTK.ClassDecl:
                ResolveClassDeclStmt((AstClassDeclStmt)stmt); break;

            case STMTK.Block:
                ResolveBlockStmt((AstBlockStmt)stmt); break;

            case STMTK.If:
                ResolveIfStmt((AstIfStmt)stmt); break;

            case STMTK.While:
                ResolveWhileStmt((AstWhileStmt)stmt); break;

            case STMTK.For:
                ResolveForStmt((AstForStmt)stmt); break;

            case STMTK.Return:
                ResolveReturnStmt((AstReturnStmt)stmt); break;

            // NOP

            case STMTK.Break:
            case STMTK.Continue:
            break;

            default:
            {
                m_error = true;
                Lox.InternalError(stmt.m_startLine, "Unexpected statement kind in resolver " + stmt.m_stmtk);
            }
            break;
        }
    }

    protected void ResolveExpr(AstExpr expr)
    {
        // todo when error?
        
        switch (expr.m_exprk)
        {
            case EXPRK.Assignment:
                ResolveAssignmentExpr((AstAssignmentExpr)expr); break;
                
            case EXPRK.Unary:
                ResolveUnaryExpr((AstUnaryExpr)expr); break;
                
            case EXPRK.Binary:
                ResolveBinaryExpr((AstBinaryExpr)expr); break;
                
            case EXPRK.Group:
                ResolveGroupExpr((AstGroupExpr)expr); break;
                
            case EXPRK.Var:
                ResolveVarExpr((AstVarExpr)expr); break;

            case EXPRK.FunCall:
                ResolveFunCallExpr((AstFunCallExpr)expr); break;

            case EXPRK.This:
                ResolveThisExpr((AstThisExpr)expr); break;

            // NOP
                
            case EXPRK.Literal:
                break;

            default:
            {
                m_error = true;
                Lox.InternalError(expr.m_startLine, "Unexpected expression kind in resolver " + expr.m_exprk);
            } break;
        }
    }

    protected void ResolveExprStmt(AstExprStmt stmt)
    {
        ResolveExpr(stmt.m_expr);
    }

    protected void ResolvePrintStmt(AstPrintStmt stmt)
    {
        ResolveExpr(stmt.m_expr);
    }

    protected void ResolveVarDeclStmt(AstVarDeclStmt stmt)
    {
        var scope = Scope();
        if (scope == null) return;  // TODO: Better global handling
        
        if (scope.ContainsKey(stmt.m_identifier.m_identifier))
        {
            m_error = true;
            Lox.Error(stmt.m_startLine, "Redefinition of identifier \"" + stmt.m_identifier.m_identifier + "\" in same scope");
            return;
        }
        
        // Declared but not yet initialized
            
        scope[stmt.m_identifier.m_identifier] = false;

        if (stmt.m_initExpr != null)
        {
            ResolveExpr(stmt.m_initExpr);
        }

        // Declared and initialized
            
        scope[stmt.m_identifier.m_identifier] = true;
    }

    protected void ResolveFunDeclStmt(AstFunDeclStmt stmt)
    {
        var scope = Scope();
        if (scope != null)  // TODO: Better global handling
        {
            if (scope.ContainsKey(stmt.m_identifier.m_identifier))
            {
                m_error = true;
                Lox.Error(stmt.m_startLine, "Redefinition of identifier \"" + stmt.m_identifier.m_identifier + "\"");
                return;
            }

            scope[stmt.m_identifier.m_identifier] = true;
        }

        scope = PushScope();                                       
        foreach (string param in stmt.m_params)
        {
            scope[param] = true;
        }
        
        ResolveStmts(stmt.m_body);
        PopScope();
    }

    protected void ResolveClassDeclStmt(AstClassDeclStmt stmt)
    {
        var scope = Scope();
        if (scope != null)      // TODO: Better global handling
        {
            if (scope.ContainsKey(stmt.m_identifier.m_identifier))
            {
                m_error = true;
                Lox.Error(stmt.m_startLine, "Redefinition of identifier \"" + stmt.m_identifier.m_identifier + "\"");
                return;
            }

            scope[stmt.m_identifier.m_identifier] = true;
        }

        scope = PushScope();
        scope["this"] = true;
        
        foreach (AstFunDeclStmt funDecl in stmt.m_funDecls)
        {
            ResolveFunDeclStmt(funDecl);
        }

        PopScope();
    }

    protected void ResolveBlockStmt(AstBlockStmt stmt)
    {
        PushScope();
        ResolveStmts(stmt.m_stmts);
        PopScope();
    }

    protected void ResolveIfStmt(AstIfStmt stmt)
    {
        ResolveExpr(stmt.m_condition);
        ResolveStmt(stmt.m_body);

        if (stmt.m_else != null) ResolveStmt(stmt.m_else);
    }

    protected void ResolveWhileStmt(AstWhileStmt stmt)
    {
        ResolveExpr(stmt.m_condition);
        ResolveStmt(stmt.m_body);
    }

    protected void ResolveForStmt(AstForStmt stmt)
    {
        bool scopePushed = false;
        if (stmt.m_preDecl != null)
        {
            PushScope();
            scopePushed = true;
            ResolveVarDeclStmt(stmt.m_preDecl);
        }
        else if (stmt.m_preExpr != null)
        {
            ResolveExpr(stmt.m_preExpr);
        }

        if (stmt.m_condition != null)
        {
            ResolveExpr(stmt.m_condition);
        }

        if (stmt.m_post != null)
        {
            ResolveExpr(stmt.m_post);
        }

        ResolveStmt(stmt.m_body);

        if (scopePushed)
        {
            PopScope();
        }
    }

    protected void ResolveReturnStmt(AstReturnStmt stmt)
    {
        if (stmt.m_expr != null)
        {
            ResolveExpr(stmt.m_expr);
        }
    }

    protected void ResolveAssignmentExpr(AstAssignmentExpr expr)
    {
        ResolveExpr(expr.m_rhs);
        ResolveVarExpr(expr.m_lhs);
    }

    protected void ResolveUnaryExpr(AstUnaryExpr expr)
    {
        ResolveExpr(expr.m_expr);
    }

    protected void ResolveBinaryExpr(AstBinaryExpr expr)
    {
        ResolveExpr(expr.m_leftExpr);
        ResolveExpr(expr.m_rightExpr);
    }

    protected void ResolveGroupExpr(AstGroupExpr expr)
    {
        ResolveExpr(expr.m_expr);
    }
    
    protected void ResolveVarExpr(AstVarExpr expr)
    {
        var scope = Scope();
        if (scope == null) return;  // TODO: Better global handling

        if (expr.m_instance != null)
        {
            // Can't resolve properties (i.e., right-hand side of .), but we still want to resolve the instance (i.e., left-hand-side of .)

            ResolveExpr(expr.m_instance);
        }
        else
        {
            if (scope.ContainsKey(expr.m_identifier.m_identifier) && !scope[expr.m_identifier.m_identifier])
            {
                m_error = true;
                Lox.Error(expr.m_startLine, "Cannot use identifier \"" + expr.m_identifier.m_identifier + "\" in it's own initialization.");
                return;
            }

            ResolveLocal(expr.m_identifier, expr.m_startLine);
        }
    }

    protected void ResolveFunCallExpr(AstFunCallExpr expr)
    {
        ResolveExpr(expr.m_callee);
        foreach (AstExpr arg in expr.m_args)
        {
            ResolveExpr(arg);
        }
    }

    protected void ResolveThisExpr(AstThisExpr expr)
    {
        ResolveLocal(expr.m_identifier, expr.m_startLine);
    }

    protected void ResolveLocal(ResolvedIdent identifier, int lineNumber)
    {
        Debug.Assert(identifier.m_hops == ResolvedIdent.s_globalScopeOrUnresolved);
        
        for (int i = 0; i < m_scopes.Count; i++)
        {
            var scope = m_scopes[m_scopes.Count - 1 - i];
            if (scope.ContainsKey(identifier.m_identifier))
            {
                identifier.m_hops = i;
                return;
            }
        }

        // BB (andrews) Can't distinguish between unresolveable lookups and global variables, since
        //  global resolves are done at runtime
    }

    protected Dictionary<string, bool> Scope()
    {
        if (m_scopes.Count == 0) return null;   // TODO: Better global handling

        return m_scopes[m_scopes.Count - 1]; ;
    }
    
    protected Dictionary<string, bool> PushScope()
    {
        var result = new Dictionary<string, bool>();
        m_scopes.Add(result);
        return result;
    }

    protected void PopScope()
    {
        m_scopes.RemoveAt(m_scopes.Count - 1);
    }
}
