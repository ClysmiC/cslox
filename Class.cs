using System.Collections.Generic;

public class Class : IFunction
{
    public string                           m_identifier;
    public Dictionary<string, Function>     m_methods;

    public Class(string identifier, Dictionary<string, Function> methods)
    {
        m_identifier = identifier;
        m_methods = methods;
    }

    public int ArgCount()
    {
        Function init;
        if (TryGetMethod("init", out init))
        {
            return init.ArgCount();
        }

        return 0;
    }

    // Calling class name as if it were a function creates a new instance
    
    public object Call(Interpreter interpreter, List<object> args)
    {
        Instance instance = new Instance(this);
        Function init;
        if (TryGetMethod("init", out init))
        {
            init.Bind(instance).Call(interpreter, args);
        }

        return instance;
    }

    public bool TryGetMethod(string identifier, out Function value)
    {
        return m_methods.TryGetValue(identifier, out value);
    }

    public override string ToString()
    {
        return "<class " + m_identifier + ">";
    }
}

public class Instance
{
    public Class                        m_class;
    public Dictionary<string, object>   m_members = new Dictionary<string, object>();
    
    public Instance(Class class_)
    {
        m_class = class_;
    }

    public bool TryGetMember(string identifier, out object value)
    {
        if (m_members.TryGetValue(identifier, out value))
            return true;

        Function fnOut;
        if (m_class.TryGetMethod(identifier, out fnOut))
        {
            value = fnOut.Bind(this);
            return true;
        }
        
        return false;
    }

    public void SetMember(string identifier, object value)
    {
        m_members[identifier] = value;
    }
    
    public override string ToString()
    {
        return "<instance of " + m_class.ToString() + ">";
    }
}
