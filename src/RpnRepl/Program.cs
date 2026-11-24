using System.Text;
using MiniRepl.Rpn.Diagnostics;
using MiniRepl.Rpn.Engine;
using MiniRepl.Rpn.Memory;
using MiniRepl.Rpn.Types;

namespace MiniRepl.Rpn;

public static class Program
{
    public static int Main(string[] args)
    {
        TryConfigureUtf8Console();

        bool traceEnabled = args.Any(a => a is "--trace" or "-t");
        bool interactive = !Console.IsInputRedirected && !args.Contains("--batch");

        var evaluator = new RpnEvaluator();
        var stack = new EvaluationStack();
        var environment = new SymbolEnvironment();

        if (interactive)
        {
            PrintBanner(traceEnabled);
        }

        while (true)
        {
            if (interactive)
            {
                WritePrompt(environment.CurrentDepth);
            }

            string? line = Console.ReadLine();
            if (line is null)
            {
                break; // EOF
            }

            string trimmed = line.Trim().TrimStart('\uFEFF').Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            if (TryHandleReplCommand(trimmed, stack, ref environment, ref traceEnabled, out bool shouldExit))
            {
                if (shouldExit)
                {
                    break;
                }
                continue;
            }

            var options = new EvalOptions(CaptureTrace: traceEnabled, TransactionalRollback: true);
            EvalResult<EvaluationReport> outcome = evaluator.EvaluateLine(trimmed, stack, environment, in options);

            if (outcome.TryGetValue(out EvaluationReport? report, out RpnError? error))
            {
                RenderSuccess(report, stack, interactive);
            }
            else
            {
                RenderError(error, trimmed, interactive);
            }
        }

        if (interactive && environment.CurrentDepth > 0)
        {
            WriteColored(
                $"Aviso: sesión finalizada con {environment.CurrentDepth} bloque(s) 'begin' sin cerrar.",
                ConsoleColor.Yellow,
                interactive);
        }

        return 0;
    }

    private static bool TryHandleReplCommand(
        string command,
        EvaluationStack stack,
        ref SymbolEnvironment environment,
        ref bool traceEnabled,
        out bool shouldExit)
    {
        shouldExit = false;

        switch (command)
        {
            case "exit" or "quit" or ":exit" or ":quit":
                shouldExit = true;
                return true;

            case "help" or ":help" or "?":
                PrintHelp();
                return true;

            case "vars" or ":vars":
                PrintVariables(environment);
                return true;

            case "stack" or ":stack":
                PrintStack(stack);
                return true;

            case "clear" or ":clear":
                stack.Clear();
                Console.WriteLine("Pila vaciada.");
                return true;

            case "reset" or ":reset":
                stack.Clear();
                // Paso por referencia (ref): reasigna la referencia de 'environment' en el llamador
                environment = new SymbolEnvironment();
                Console.WriteLine("Sesión reiniciada: pila vacía y nuevo entorno global.");
                return true;

            case "trace" or ":trace":
                traceEnabled = !traceEnabled;
                Console.WriteLine($"Modo traza paso a paso: {(traceEnabled ? "ACTIVADO" : "DESACTIVADO")}.");
                return true;

            default:
                return false;
        }
    }

    private static void RenderSuccess(EvaluationReport report, EvaluationStack stack, bool useColor)
    {
        if (report.Trace.Count > 0)
        {
            WriteColored("  [Traza de ejecución]", ConsoleColor.DarkYellow, useColor);
            foreach (TraceStep step in report.Trace)
            {
                string stackText = step.StackSnapshot.Count == 0
                    ? "[]"
                    : $"[{string.Join(", ", step.StackSnapshot)}]";
                Console.WriteLine($"    #{step.Step,-2} token='{step.Lexeme,-6}' | nivel={step.ScopeDepth} | {step.Action,-34} | pila={stackText}");
            }
        }

        foreach (string evt in report.Events)
        {
            WriteColored($"  {evt}", ConsoleColor.Cyan, useColor);
        }

        if (report.ProducedStackOutput && report.TopValue is not null)
        {
            string stackHint = stack.Count > 1 ? $"  [pila: {stack.Count} elementos]" : string.Empty;
            WriteColored($"  => {report.TopValue}{stackHint}", ConsoleColor.Green, useColor);
        }
        else if (report.Events.Count == 0)
        {
            Console.WriteLine($"  [ok | pila={stack.Count}]");
        }
    }

    private static void RenderError(RpnError error, string sourceLine, bool useColor)
    {
        WriteColored($"  [!] {error.FormatMessage()}", ConsoleColor.Red, useColor);
        if (error.Span.Length > 0 && error.Span.Start >= 0 && error.Span.Start < sourceLine.Length)
        {
            string pointer = new string(' ', error.Span.Start + 4) + new string('^', Math.Max(1, error.Span.Length));
            WriteColored($"    {sourceLine}", ConsoleColor.DarkGray, useColor);
            WriteColored(pointer, ConsoleColor.Red, useColor);
        }
    }

    private static void PrintVariables(IScopeLookup lookup)
    {
        IReadOnlyList<ActiveVariableInfo> vars = lookup.GetActiveVariables();
        string scopeLabel = lookup.CurrentDepth == 0
            ? "global (nivel 0)"
            : $"bloque local (nivel {lookup.CurrentDepth})";

        Console.WriteLine($"Entorno activo [{scopeLabel}]:");
        if (vars.Count == 0)
        {
            Console.WriteLine("  (sin variables declaradas)");
            return;
        }

        foreach (ActiveVariableInfo v in vars)
        {
            string shadowNote = v.ShadowedDepth is not null
                ? $"  [sombrea a {v.Name} = {v.ShadowedValue} @ nivel {v.ShadowedDepth}]"
                : string.Empty;

            Console.WriteLine($"  {v.Name} = {v.Value}  (@ nivel {v.DefinedAtDepth}){shadowNote}");
        }
    }

    private static void PrintStack(EvaluationStack stack)
    {
        Console.WriteLine($"Pila de evaluación ({stack.Count} elemento(s)):");
        if (stack.IsEmpty)
        {
            Console.WriteLine("  (vacía)");
            return;
        }

        for (int i = 0; i < stack.Items.Count; i++)
        {
            string marker = i == stack.Items.Count - 1 ? "  <-- tope" : string.Empty;
            Console.WriteLine($"  [{i}] {stack.Items[i]}{marker}");
        }
    }

    private static void PrintBanner(bool traceEnabled)
    {
        Console.WriteLine("=================================================================");
        Console.WriteLine("  Mini-REPL en Notación Polaca Inversa (RPN) — C# 12 / .NET 8");
        Console.WriteLine("  UC FACYT · CAO604 Lenguajes de Programación");
        Console.WriteLine("=================================================================");
        Console.WriteLine($"  Traza paso a paso: {(traceEnabled ? "ON" : "OFF")}  |  Escriba 'help' para ver comandos.");
        Console.WriteLine();
    }

    private static void PrintHelp()
    {
        Console.WriteLine(
            """
            Comandos del REPL:
              vars          Lista las variables activas y muestra si hay shadowing
              stack         Muestra el contenido actual de la pila (fondo -> tope)
              clear         Vacía la pila de evaluación
              trace         Activa/desactiva la traza paso a paso de la pila
              begin / end   Abre o cierra un bloque de alcance local (pueden usarse en línea)
              help          Muestra esta ayuda
              exit / quit   Sale del intérprete

            Sintaxis RPN estricta (tokens separados por espacios):
              Literales:    Int (10, -3), Float (3.5, 1e-2), Bool (true, false)
              Aritmética:   +  -  *  /  %     (Int/Int -> Int; Int+Float -> Float; checked overflow)
              Comparación:  ==  !=  <  >  <=  >=
              Asignación:   x 10 =            (o x 5 2 + =)
              Evaluación:   x 5 + 2 *         => (x + 5) * 2
            """);
    }

    private static void WritePrompt(int depth)
    {
        string scopeTag = depth == 0 ? "global" : $"local:{depth}";
        Console.Write($"rpn[{scopeTag}]> ");
    }

    private static void WriteColored(string text, ConsoleColor color, bool useColor)
    {
        if (!useColor)
        {
            Console.WriteLine(text);
            return;
        }

        ConsoleColor prev = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.WriteLine(text);
        Console.ForegroundColor = prev;
    }

    private static void TryConfigureUtf8Console()
    {
        try
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding = Encoding.UTF8;
        }
        catch
        {
            // Ignorado si los flujos estándar están redirigidos sin consola física.
        }
    }
}
