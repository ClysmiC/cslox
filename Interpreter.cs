using System;
using System.Collections.Generic;
using System.Diagnostics;

public enum BLOCKEXITK
{
    Normal,
    Break,
    Continue,
}

public class Interpreter
{
    // NOTE (andrews) Every single evaluation/execution should perform a NOP if a runtime error has been detected.

    public bool             m_runtimeError = false;
    public Environment      m_globalEnvironment = new Environment();
    public Environment      m_environment;
    protected BLOCKEXITK    m_blockexitk = BLOCKEXITK.Normal;
    protected Stopwatch     m_stopwatch = new Stopwatch();

    protected bool             m_hasReturnValue = false;
    protected object           m_returnValue = null;

    public Interpreter()
    {
        m_environment = m_globalEnvironment;
        m_stopwatch.Start();

        string fnName = "clockms";
        m_globalEnvironment.Define(fnName, new BuiltInFunction(fnName, 0, (interp, args) => { return interp.m_stopwatch.ElapsedMilliseconds; }));
    }

    public object ExecuteStmts(List<AstStmt> stmts)
    {
        
        foreach (AstStmt stmt in stmts)
        {
            ExecuteStmt(stmt);

            if(HadErrorOrReturn()) break;
        }

        // Store the return value and then clean up before returning from function

        object returnValueForThisFunctionCall = m_returnValue;
        m_returnValue = null;
        m_hasReturnValue = false;

        if (m_runtimeError) return null;
        return returnValueForThisFunctionCall;
    }

    protected void ExecuteStmt(AstStmt stmt)
    {
        if (HadErrorOrReturn()) return;
        
        switch (stmt.m_stmtk)
        {
            case STMTK.Expr:
                ExecuteExprStmt((AstExprStmt)stmt); break;

            case STMTK.Print:
                ExecutePrintStmt((AstPrintStmt)stmt); break;

            case STMTK.VarDecl:
                ExecuteVarDeclStmt((AstVarDeclStmt)stmt); break;

            case STMTK.FunDecl:
                ExecuteFunDeclStmt((AstFunDeclStmt)stmt); break;

            case STMTK.ClassDecl:
                ExecuteClassDeclStmt((AstClassDeclStmt)stmt); break;

            case STMTK.Block:
                ExecuteBlockStmt((AstBlockStmt)stmt); break;

            case STMTK.If:
                ExecuteIfStmt((AstIfStmt)stmt); break;

            case STMTK.While:
                ExecuteWhileStmt((AstWhileStmt)stmt); break;

            case STMTK.For:
                ExecuteForStmt((AstForStmt)stmt); break;

            case STMTK.Break:
                ExecuteBreakStmt((AstBreakStmt)stmt); break;
                
            case STMTK.Continue:
                ExecuteContinueStmt((AstContinueStmt)stmt); break;

            case STMTK.Return:
                ExecuteReturnStmt((AstReturnStmt)stmt); break;

            default:
            {
                m_runtimeError = true;
                Lox.InternalError(stmt.m_startLine, "Unexpected statement kind " + stmt.m_stmtk);
            }
            break;
        }
    }

    protected void ExecuteVarDeclStmt(AstVarDeclStmt stmt)
    {
        if (HadErrorOrReturn()) return;
                
        object value = null;
        if (stmt.m_initExpr != null)
        {
            value = EvaluateExpr(stmt.m_initExpr);
        }
        
        bool success = m_environment.Define(stmt.m_identifier.m_identifier, value);
        if (!success)
        {
            // TODO: Print the line that the first definition was on? To do that we would have to store
            //  line numbers in the environment. Also, it wouldn't really make sense for repl mode so we would
            //  have to have a way to check if we are in repl mode.
            
            m_runtimeError = true;
            Lox.Error(stmt.m_startLine, "Redefinition of identifier " + stmt.m_identifier.m_identifier);
        }
    }

    protected Function ExecuteFunDeclStmt(AstFunDeclStmt stmt)
    {
        if (HadErrorOrReturn()) return null;

        Function fn = new Function(stmt, m_environment);

        bool success = m_environment.Define(stmt.m_identifier.m_identifier, fn);
        if (!success)
        {
            // @copypaste
            
            // TODO: Print the line that the first definition was on? To do that we would have to store
            //  line numbers in the environment. Also, it wouldn't really make sense for repl mode so we would
            //  have to have a way to check if we are in repl mode.

            m_runtimeError = true;
            Lox.Error(stmt.m_startLine, "Redifinition of identifier.m_identifier " + stmt.m_identifier.m_identifier);
            return null;
        }

        return fn;
    }

    protected void ExecuteClassDeclStmt(AstClassDeclStmt stmt)
    {
        if (HadErrorOrReturn()) return;

        // Define even though we aren't finished initializing it so that methods can
        //  reference the class.

        bool success = m_environment.Define(stmt.m_identifier.m_identifier, null);
        if (!success)
        {
            m_runtimeError = true;
            Lox.Error(stmt.m_startLine, "Redifinition of identifier " + stmt.m_identifier);
            return;
        }

        var methods = new Dictionary<string, Function>();
        foreach (AstFunDeclStmt funDecl in stmt.m_funDecls)
        {
            methods.Add(funDecl.m_identifier.m_identifier, new Function(funDecl, m_environment));
        }

        if (HadErrorOrReturn()) return;

        success = m_environment.Assign(stmt.m_identifier, new Class(stmt.m_identifier.m_identifier, methods));
    }

    protected void ExecuteExprStmt(AstExprStmt stmt)
    {
        if (HadErrorOrReturn()) return;
        
        EvaluateExpr(stmt.m_expr);
    }

    protected void ExecutePrintStmt(AstPrintStmt stmt)
    {
        if (HadErrorOrReturn()) return;
        
        object value = EvaluateExpr(stmt.m_expr);

        // TODO: this only lets us print w/ newline. Should we have a separate keyword for printing without newline?

        Console.WriteLine(Stringify(value));
    }

    // TODO: Add vardecl to if statement?
    
    protected void ExecuteIfStmt(AstIfStmt stmt)
    {
        if (HadErrorOrReturn()) return;
        
        object conditionalValue = EvaluateExpr(stmt.m_condition);
        
        if (IsTruthy(conditionalValue))
        {
            ExecuteStmt(stmt.m_body);
        }
        else if (stmt.m_else != null)
        {
            ExecuteStmt(stmt.m_else);
        }
    }

    // TODO: Add vardecl to while loop?
    //  It's pretty dumb but C++ allows it =/
    
    protected void ExecuteWhileStmt(AstWhileStmt stmt)
    {
        if (HadErrorOrReturn()) return;
        
        while (!m_runtimeError)
        {
            m_blockexitk = BLOCKEXITK.Normal;
            
            object conditionVal = EvaluateExpr(stmt.m_condition);

            if (!IsTruthy(conditionVal)) break;

            ExecuteStmt(stmt.m_body);

            if (m_blockexitk == BLOCKEXITK.Break) break;
        }
    }

    protected void ExecuteForStmt(AstForStmt stmt)
    {
        if (HadErrorOrReturn()) return;

        // NOTE (andrews) This pushes an environment before opening the scope, which is where any declared iterator lives

        bool isEnvPushed = false;
        if (stmt.m_preDecl != null)
        {
            Debug.Assert(stmt.m_preExpr == null);
            
            PushEnvironment();
            isEnvPushed = true;
            
            ExecuteVarDeclStmt(stmt.m_preDecl);
        }
        else if (stmt.m_preExpr != null) 
        {
            EvaluateExpr(stmt.m_preExpr);
        }

        while (!m_runtimeError)
        {
            m_blockexitk = BLOCKEXITK.Normal;

            object conditionVal = true;
            
            if (stmt.m_condition != null)
            {
                conditionVal = EvaluateExpr(stmt.m_condition);

                if (!IsTruthy(conditionVal)) break;

            }
            
            ExecuteStmt(stmt.m_body);
            
            if (m_blockexitk == BLOCKEXITK.Break) break;

            if (stmt.m_post != null)
            {
                EvaluateExpr(stmt.m_post);
            }
        }

        if (isEnvPushed)
        {
            PopEnvironment();
        }
    }

    protected void ExecuteBreakStmt(AstBreakStmt stmt)
    {
        if (HadErrorOrReturn()) return;
        m_blockexitk = BLOCKEXITK.Break;
    }

    protected void ExecuteContinueStmt(AstContinueStmt stmt)
    {
        if (HadErrorOrReturn()) return;
        m_blockexitk = BLOCKEXITK.Continue;
    }

    protected void ExecuteReturnStmt(AstReturnStmt stmt)
    {
        if (HadErrorOrReturn()) return;

        if (stmt.m_expr != null)
        {
            object returnValue = EvaluateExpr(stmt.m_expr);

            if (!m_runtimeError)
            {
                m_returnValue = returnValue;
                m_hasReturnValue = true;
            }
        }
        else
        {
            // TODO: is this really what I want to happen if you explicitly return nothing?
            //  I don't have any code to handle implicitly returning nothing so I don't think they
            //  will behave the same way. I think I would rather sidestep the issue by not allowing
            //  you to "use" a value returned by a function that doesn't return anything. This wolud
            //  require a semantic pass. Maybe I can cram it into the resolve pass?
            
            m_returnValue = null;
            m_hasReturnValue = true;
        }
    }

    protected void ExecuteBlockStmt(AstBlockStmt stmt)
    {
        if (HadErrorOrReturn()) return;

        PushEnvironment();
        
        foreach (AstStmt innerStmt in stmt.m_stmts)
        {
            ExecuteStmt(innerStmt);

            if (m_blockexitk == BLOCKEXITK.Break) break;
            if (m_blockexitk == BLOCKEXITK.Continue) break;
        }

        PopEnvironment();
    }

    protected object EvaluateExpr(AstExpr expr)
    {
        if (HadErrorOrReturn()) return null;
        
        switch (expr.m_exprk)
        {
            case EXPRK.Assignment:
                return EvaluateAssignmentExpr((AstAssignmentExpr)expr);
                
            case EXPRK.Unary:
                return EvaluateUnaryExpr((AstUnaryExpr)expr);
                
            case EXPRK.Binary:
                return EvaluateBinaryExpr((AstBinaryExpr)expr);
                
            case EXPRK.Literal:
                return EvaluateLiteralExpr((AstLiteralExpr)expr);
                
            case EXPRK.Group:
                return EvaluateGroupExpr((AstGroupExpr)expr);
                
            case EXPRK.Var:
                return EvaluateVarExpr((AstVarExpr)expr);

            case EXPRK.FunCall:
                return EvaluateFunCallExpr((AstFunCallExpr)expr);

            case EXPRK.This:
                return EvaluateThisExpr((AstThisExpr)expr);

            default:
            {
                m_runtimeError = true;
                Lox.InternalError(expr.m_startLine, "Unexpected expression kind " + expr.m_exprk);
            } break;
        }

        Debug.Assert(m_runtimeError);
        return null;
    }
    
    protected object EvaluateAssignmentExpr(AstAssignmentExpr expr)
    {
        if (HadErrorOrReturn()) return null;

        if (expr.m_lhs.m_instance != null)
        {
            object instanceObj = EvaluateExpr(expr.m_lhs.m_instance);
            Instance instance = instanceObj as Instance;

            if (instance != null)
            {
                object value = EvaluateExpr(expr.m_rhs);
                if (HadErrorOrReturn()) return null;
                
                instance.SetMember(expr.m_lhs.m_identifier.m_identifier, value);
                return value;
            }
            else
            {
                m_runtimeError = true;
                Lox.Error(expr.m_startLine, "Only class instances can access properties with '.'");
                return null;
            }
        }
        else
        {
            object value = EvaluateExpr(expr.m_rhs);
            if (HadErrorOrReturn()) return null;
            
            bool success = m_environment.Assign(expr.m_lhs.m_identifier, value);

            if (success)
            {
                return value;
            }
            else
            {
                m_runtimeError = true;
                Lox.Error(expr.m_startLine, "Undefined variable \"" + expr.m_lhs.m_identifier + "\"");
                return null;
            }
        }
    }

    protected object EvaluateFunCallExpr(AstFunCallExpr expr)
    {
        if (HadErrorOrReturn()) return null;

        object calleeObj = EvaluateExpr(expr.m_callee);
        IFunction callee = calleeObj as IFunction;

        if (callee != null)
        {
            if (expr.m_args.Count != callee.ArgCount())
            {
                // TODO: print function name in the error
            
                m_runtimeError = true;
                Lox.Error(expr.m_startLine, "Function expected " + callee.ArgCount()+ " arguments, but was passed " + expr.m_args.Count);
                return null;
            }
        
            List<object> args = new List<object>();
            foreach (AstExpr argExpr in expr.m_args)
            {
                args.Add(EvaluateExpr(argExpr));
            }

            return callee.Call(this, args);
        }
        else
        {
            // TODO: Distinguish between someUnknownFunction() and (3 + 1)() ?
            //  It would be nice to be able to print the name of the function in the former case
            
            m_runtimeError = true;
            Lox.Error(expr.m_startLine, "Invalid function call");
            return null;
        }
    }

    protected object EvaluateThisExpr(AstThisExpr expr)
    {
        Debug.Assert(expr.m_identifier.m_identifier == "this");

        object result = null;
        bool success = m_environment.Get(expr.m_identifier, out result);

        if (!success)
        {
            m_runtimeError = true;
            Lox.Error(expr.m_startLine, "Undeclared identifier \"" + expr.m_identifier.m_identifier + "\"");
            return null;
        }

        return result;
    }

    protected object EvaluateUnaryExpr(AstUnaryExpr expr)
    {
        if (HadErrorOrReturn()) return null;
            
        object value = EvaluateExpr(expr.m_expr);
        switch (expr.m_tokenkOp)
        {
            case TOKENK.Bang:
            {
                return !IsTruthy(value);
            } break;

            case TOKENK.Minus:
            {
                double? numVal = value as double?;

                if (numVal.HasValue)
                    return -numVal.Value;

                m_runtimeError = true;
                Lox.Error(expr.m_startLine, "Unary - operator can only be applied to numbers");
            } break;

            default:
            {
                m_runtimeError = true;
                Lox.InternalError(expr.m_startLine, "Unary expression has unexpected operator: " + expr.m_tokenkOp);
            } break;
        }

        Debug.Assert(m_runtimeError);
        return null;
    }

    protected object EvaluateBinaryExpr(AstBinaryExpr expr)
    {
        if (HadErrorOrReturn()) return null;
        
        object lValue = EvaluateExpr(expr.m_leftExpr);

        // Test the logical operators first so we can short circuit w/o evaluating rhs

        switch (expr.m_tokenkOp)
        {
            case TOKENK.Or:
            {
                if (IsTruthy(lValue)) return lValue;
                
                object rVal = EvaluateExpr(expr.m_rightExpr);
                return rVal;
            } break;

            case TOKENK.And:
            {
                if (!IsTruthy(lValue)) return lValue;
                
                object rVal = EvaluateExpr(expr.m_rightExpr);
                return rVal;
            } break;
        }
        
        object rValue = EvaluateExpr(expr.m_rightExpr);

        double? lNum = lValue as double?;
        double? rNum = rValue as double?;
                
        switch (expr.m_tokenkOp)
        {
            case TOKENK.Minus:
            {
                if (lNum.HasValue && rNum.HasValue)
                    return lNum.Value - rNum.Value;

                m_runtimeError = true;
                Lox.Error(expr.m_startLine, "'-' only supported for numbers");
            } break;

            case TOKENK.Slash:
            {
                if (lNum.HasValue && rNum.HasValue)
                {
                    if (rNum.Value == 0)
                    {
                        m_runtimeError = true;
                        Lox.Error(expr.m_startLine, "Divide by 0 error");
                        return null;
                    }
                    
                    return lNum.Value / rNum.Value;
                }

                m_runtimeError = true;
                Lox.Error(expr.m_startLine, "'/' only supported for numbers");
            } break;

            case TOKENK.Star:
            {
                if (lNum.HasValue && rNum.HasValue)
                    return lNum.Value * rNum.Value;

                m_runtimeError = true;
                Lox.Error(expr.m_startLine, "'*' only supported for numbers");
            } break;

            case TOKENK.Plus:
            {
                string lStr = lValue as string;
                string rStr = rValue as string;

                if ((lStr == null && !lNum.HasValue) || (rStr == null && !rNum.HasValue))
                {
                    m_runtimeError = true;
                    Lox.Error(expr.m_startLine, "'+' only supported for numbers and strings");
                }
                else
                {
                    if (lNum.HasValue && rNum.HasValue)
                        return lNum.Value + rNum.Value;

                    if (lStr != null)
                    {
                        if (rStr != null)
                            return lStr + rStr;

                        Debug.Assert(rNum.HasValue);
                    
                        return lStr + rNum.Value; 
                    }

                    Debug.Assert(rStr != null);
                    Debug.Assert(lNum.HasValue);

                    return lNum.Value + rStr;
                }
            } break;

            case TOKENK.EqualEqual:
            {
                return IsEqual(lValue, rValue);
            } break;

            case TOKENK.BangEqual:
            {
                return IsEqual(lValue, rValue);
            } break;

            // TODO: support lexically comparing strings?

            case TOKENK.Lesser:
            {
                if (lNum.HasValue && rNum.HasValue)
                    return lNum.Value < rNum.Value;

                m_runtimeError = true;
                Lox.Error(expr.m_startLine, "'<' only supported for numbers");
            } break;

            case TOKENK.Greater:
            {
                if (lNum.HasValue && rNum.HasValue)
                    return lNum.Value > rNum.Value;

                m_runtimeError = true;
                Lox.Error(expr.m_startLine, "'>' only supported for numbers");
            } break;

            case TOKENK.LesserEqual:
            {
                if (lNum.HasValue && rNum.HasValue)
                    return lNum.Value <= rNum.Value;

                m_runtimeError = true;
                Lox.Error(expr.m_startLine, "'<=' only supported for numbers");
            } break;

            case TOKENK.GreaterEqual:
            {
                if (lNum.HasValue && rNum.HasValue)
                    return lNum.Value >= rNum.Value;

                m_runtimeError = true;
                Lox.Error(expr.m_startLine, "'>=' only supported for numbers");
            } break;

            default:
            {
                m_runtimeError = true;
                Lox.InternalError(expr.m_startLine, "Binary expression has unexpected operator: " + expr.m_tokenkOp);
            } break;
        }

        Debug.Assert(m_runtimeError);
        return null;
    }
    
    protected object EvaluateVarExpr(AstVarExpr expr)
    {
        if (HadErrorOrReturn()) return null;

        if (expr.m_instance == null)
        {
            object result = null;
            bool success = m_environment.Get(expr.m_identifier, out result);

            if (!success)
            {
                m_runtimeError = true;
                Lox.Error(expr.m_startLine, "Undeclared identifier \"" + expr.m_identifier.m_identifier + "\"");
                return null;
            }

            return result;
        }
        else
        {
            object instanceObj = EvaluateExpr(expr.m_instance);
            Instance instance = instanceObj as Instance;

            if (instance != null)
            {
                object value;
                bool success = instance.TryGetMember(expr.m_identifier.m_identifier, out value);

                if (success) return value;

                m_runtimeError = true;
                Lox.Error(expr.m_startLine, "Undefined member \"" + expr.m_identifier + "\"");
                return null;
            }
            else
            {
                m_runtimeError = true;
                Lox.Error(expr.m_startLine, "Only class instances can access properties with '.'");
                return null;
            }
        }
    }
    
    protected object EvaluateLiteralExpr(AstLiteralExpr expr)
    {
        if (HadErrorOrReturn()) return null;
        return expr.m_value;
    }

    protected object EvaluateGroupExpr(AstGroupExpr expr)
    {
        if (HadErrorOrReturn()) return null;
        return EvaluateExpr(expr.m_expr);
    }


    // Non eval/exec functions
    
    public void PushEnvironment()
    {
        m_environment = new Environment(m_environment);
    }

    public void PopEnvironment()
    {
        m_environment = m_environment.m_parent;
    }

    protected string Stringify(object obj)
    {
        if (obj == null) return "nil";

        return obj.ToString();
    }

    protected bool IsEqual(object a, object b)
    {
        if (a == null && b == null) return true;
        if (a == null) return false;

        return a.Equals(b);
    }

    protected bool IsTruthy(object obj)
    {
        bool? boolVal;
        double? numVal;
        
        if (obj == null) return false;
        if ((boolVal = obj as bool?).HasValue) return boolVal.Value;
        if ((numVal = obj as double?).HasValue) return numVal.Value != 0;

        return true;
    }

    protected bool HadErrorOrReturn()
    {
        return m_hasReturnValue || m_runtimeError;
    }
}
