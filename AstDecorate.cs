using System.Diagnostics;

public class ResolvedIdent
{
    public const int    s_globalScopeOrUnresolved = -1;
    
    public string       m_identifier;
    public int          m_hops = s_globalScopeOrUnresolved;

    public override string ToString()
    {
        Debug.Assert(false, "You probably wanted to step through to the underlying string...");
        return m_identifier;
    }
}
