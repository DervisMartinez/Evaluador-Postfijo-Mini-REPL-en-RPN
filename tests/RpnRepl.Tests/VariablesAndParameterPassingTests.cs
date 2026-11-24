using MiniRepl.Rpn.Diagnostics;
using MiniRepl.Rpn.Engine;
using MiniRepl.Rpn.Memory;
using MiniRepl.Rpn.Types;
using Xunit;

namespace RpnRepl.Tests;

public class VariablesAndParameterPassingTests
{
    private static readonly EvalOptions TraceOpts = new(CaptureTrace: true, TransactionalRollback: true);

    [Fact]
    public void Evaluate_DeclareUpdateAndSelfReferenceVariable_WorksInRpn()
    {
        var evaluator = new RpnEvaluator();
        var stack = new EvaluationStack();
        var env = new SymbolEnvironment();

        // Declaración compuesta: x 5 2 + =  -> x = 7
        Assert.True(evaluator.EvaluateLine("x 5 2 + =", stack, env, in TraceOpts).IsSuccess);
        Assert.True(env.TryLookup("x", out RpnValue? val1, out int depth1));
        Assert.Equal(new RpnValue.IntValue(7), val1);
        Assert.Equal(0, depth1);

        // Actualización autorreferenciada: x x 3 * =  -> x = 21
        Assert.True(evaluator.EvaluateLine("x x 3 * =", stack, env, in TraceOpts).IsSuccess);
        Assert.True(env.TryLookup("x", out RpnValue? val2, out _));
        Assert.Equal(new RpnValue.IntValue(21), val2);

        // Copia de valor entre variables: y x =
        Assert.True(evaluator.EvaluateLine("y x =", stack, env, in TraceOpts).IsSuccess);
        Assert.True(env.TryLookup("y", out RpnValue? yVal, out _));
        Assert.Equal(new RpnValue.IntValue(21), yVal);
    }

    [Fact]
    public void ValueTypeVsReferenceType_DemonstratesCopyVsSharedMutation()
    {
        // Struct por valor: copiar EvalOptions no altera el original
        var optsOriginal = new EvalOptions(CaptureTrace: false, TransactionalRollback: true);
        EvalOptions optsCopy = optsOriginal with { CaptureTrace = true };
        Assert.False(optsOriginal.CaptureTrace);
        Assert.True(optsCopy.CaptureTrace);

        // Objeto de referencia pasado por valor: mutar stack dentro de EvaluateLine persiste en el llamador
        var stack = new EvaluationStack();
        var env = new SymbolEnvironment();
        var evaluator = new RpnEvaluator();

        EvalResult<EvaluationReport> res = evaluator.EvaluateLine("10 20 +", stack, env, in optsCopy);
        Assert.True(res.TryGetValue(out EvaluationReport? report, out _));
        Assert.Equal(3, report.Trace.Count);
        Assert.Equal(1, stack.Count);
    }
}
