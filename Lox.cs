using System;
using System.IO;
using System.Collections.Generic;

public class Lox
{
    public static bool          s_isRepl = false;
    public static int           s_errorCount = 0;
    
    protected static bool       s_hadError = false;
    

    
    public static void Run(string sourceCode)
    {
        Scanner scanner = new Scanner(sourceCode);
        List<Token> tokens = scanner.ScanTokens();
        Parser parser = new Parser(tokens);
        List<AstStmt> statements = parser.Parse();

        if (!s_hadError)
        {
            new ResolvePass().ResolveStmts(statements);
        }

        if (!s_hadError)
        {
            new Interpreter().ExecuteStmts(statements);
        }
    }

    public static int RunPrompt()
    {
        s_isRepl = true;

        while (true)
        {
            Console.Write("> ");
            string line = Console.ReadLine();

            if (line == "exit")
                return 0;

            Run(line);

            s_hadError = false;
        }
    }

    public static int RunFile(string filename)
    {
        string allLines = File.ReadAllText(filename);
        
        Run(allLines);

#if DEBUG
        Console.WriteLine("Press any key to continue . . .");
        Console.ReadKey();
#endif

        if (s_hadError)
            return 1;

        return 0;
    }

    public static void InternalError(int line, string message)
    {
        ConsoleColor colorPrev = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Red;

        Report(line, "Internal Lox Error", message);

        Console.ForegroundColor = colorPrev;
    }

    public static void Error(int line, string message)
    {
        Report(line, "Error", message);
    }

    public static void Report(int line, string errorType, string message)
    {
        Console.WriteLine(
            (!s_isRepl ? ("[line " + line + "] ") : ("")) +
            errorType + ": " + message);
        s_hadError = true;
        s_errorCount++;
    }

    public static int Main(string[] args)
    {
        int exitCode = 0;
        
        if (args.Length == 0)
        {
            exitCode = RunPrompt();
        }
        else if (args.Length == 1)
        {
            exitCode = RunFile(args[0]);
        }
        else
        {
            Console.WriteLine("Usage: cslox [script]");
            exitCode = 1;
        }

        return exitCode;
    }
}
