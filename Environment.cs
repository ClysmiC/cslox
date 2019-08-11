using System.Collections.Generic;
using System.Diagnostics;

public class Environment
{
    // TODO: Instead of pointing back to parent, can we just make the environments a dynamic array where the "parent"
    //  is just the previous element in the array?

    public Environment m_globalRoot = null;
    public Environment m_parent = null;
    public Dictionary<string, object> m_values = new Dictionary<string, object>();

    public Environment()
    {
        m_globalRoot = this;
    }
    
    public Environment(Environment parent)
    {
        m_parent = parent;
        m_globalRoot = m_parent.m_globalRoot;
    }

    // TODO: should error reporting be delegated to the environment? Right now, the interp reads the
    //  false value and reports the error.
    
    public bool Define(string identifier, object value)
    {
        if (m_values.ContainsKey(identifier))
            return false;
        
        m_values[identifier] = value;
        return true;
    }

    public bool Assign(ResolvedIdent identifier, object value)
    {
        Environment target = TargetEnvironment(identifier.m_hops);
        return target.AssignInternal(identifier.m_identifier, value);
    }

    public bool Get(ResolvedIdent identifier, out object value)
    {
        Environment target = TargetEnvironment(identifier.m_hops);
        return target.GetInternal(identifier.m_identifier, out value);
    }

    protected bool AssignInternal(string identifier, object value)
    {
        if (!m_values.ContainsKey(identifier))
        {
            Debug.Assert(IsGlobalScope());
            return false;
        }

        m_values[identifier] = value;
        return true;
    }

    protected bool GetInternal(string identifier, out object value)
    {
        // TODO: For now this is a runtime error for globals, but a static semantic error for non-globals.
        //  Ideally, it should be a static error for everything, but I don't want to deal with handling
        //  this for global variables that are referred to lexically before they are defined. For example,
        //  I want below to work:
        //
        //  fun printSomeGlobal() { print theGlobal; }
        //
        //  var theGlobal = 3;
        //
        //  The easiest way to make that work is a runtime check (what I am doing now). But it would be better
        //  for the static resolver to just smart enough to sort that stuff out!
        
        value = null;
        if (!m_values.ContainsKey(identifier))
        {
            Debug.Assert(IsGlobalScope());
            return false;
        }

        value = m_values[identifier];
        return true;
    }

    protected Environment TargetEnvironment(int hops)
    {
        Environment target;
        if (hops == ResolvedIdent.s_globalScopeOrUnresolved)
        {
            target = m_globalRoot;
        }
        else
        {
            Debug.Assert(hops >= 0);
            
            target = this;
            for (int i = 0; i < hops; i++)
            {
                Debug.Assert(target.m_parent != null);
                target = target.m_parent;
            }
        }

        return target;
    }

    protected bool IsGlobalScope()
    {
        return this == m_globalRoot;
    }
}
