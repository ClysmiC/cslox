using System.Collections.Generic;
using System.Diagnostics;

public delegate object FnPtr(Interpreter interpreter, List<object> args);

public interface IFunction
{
    int ArgCount();
    object Call(Interpreter interpreter, List<object> args);
}

public class BuiltInFunction
{
    public string m_name;
    protected int m_argCount;
    protected FnPtr m_function;

    public BuiltInFunction(string name, int argCount, FnPtr fnPtr)
    {
        m_name = name;
        m_argCount = argCount;
        m_function = fnPtr;
    }

    public int ArgCount()
    {
        return m_argCount;
    }

    public object Call(Interpreter interpreter, List<object> args)
    {
        return m_function(interpreter, args);
    }

    public override string ToString()
    {
        return "<fun " + m_name + ">";
    }
}

public class Function : IFunction
{
    protected AstFunDeclStmt m_funDecl;
    protected Environment m_closure;

    public Function(AstFunDeclStmt funDecl, Environment closure)
    {
        m_funDecl = funDecl;
        m_closure = closure;
    }

    public Function Bind(Instance instance)
    {
        Environment environment = new Environment(m_closure);
        environment.Define("this", instance);
        return new Function(m_funDecl, environment);
    }

    public int ArgCount()
    {
        return m_funDecl.m_params.Count;
    }

    public object Call(Interpreter interpreter, List<object> args)
    {
        Debug.Assert(args.Count == m_funDecl.m_params.Count);

        // Save prev environment and reset current environment to globals
        
        Environment prevEnvironment = interpreter.m_environment;
        interpreter.m_environment = m_closure;
        
        interpreter.PushEnvironment();

        // Assign argument values to their corresponding parameters
        
        for (int i = 0; i < args.Count; i++)
        {
            interpreter.m_environment.Define(m_funDecl.m_params[i], args[i]);
        }

        // Execute!
        
        object result = interpreter.ExecuteStmts(m_funDecl.m_body);

        interpreter.PopEnvironment();

        // Restore prev environment
        
        interpreter.m_environment = prevEnvironment;

        return result;
    }

    public override string ToString()
    {
        return "<fun " + m_funDecl.m_identifier + ">";
    }
}

