using MiniRepl.Rpn.Diagnostics;
using MiniRepl.Rpn.Engine;
using MiniRepl.Rpn.Memory;
using MiniRepl.Rpn.Types;
using Xunit;

namespace RpnRepl.Tests;

public class ScopingAndShadowingTests
{
    private static readonly EvalOptions DefaultOpts = new(CaptureTrace: false);

    [Fact]
    public void BeginEnd_LocalShadowing_DoesNotOverwriteGlobalVariableAfterEnd()
    {
        var evaluator = new RpnEvaluator();
        var stack = new EvaluationStack();
        var env = new SymbolEnvironment();

        Assert.True(evaluator.EvaluateLine("x 10 =", stack, env, in DefaultOpts).IsSuccess);
        Assert.True(evaluator.EvaluateLine("y 5 =", stack, env, in DefaultOpts).IsSuccess);

        Assert.True(evaluator.EvaluateLine("begin", stack, env, in DefaultOpts).IsSuccess);
        Assert.Equal(1, env.CurrentDepth);

        // Modificación / shadowing local de x, y creación de variable puramente local z
        Assert.True(evaluator.EvaluateLine("x 99 =", stack, env, in DefaultOpts).IsSuccess);
        Assert.True(evaluator.EvaluateLine("z y 3 * =", stack, env, in DefaultOpts).IsSuccess);

        // Dentro del bloque: x vale 99 (sombreando al 10 global), y vale 5, z vale 15
        EvalResult<EvaluationReport> insideX = evaluator.EvaluateLine("x", stack, env, in DefaultOpts);
        Assert.True(insideX.TryGetValue(out EvaluationReport? repX, out _));
        Assert.Equal(new RpnValue.IntValue(99), repX.TopValue);

        ActiveVariableInfo xInfo = env.GetActiveVariables().Single(v => v.Name == "x");
        Assert.Equal(1, xInfo.DefinedAtDepth);
        Assert.Equal(0, xInfo.ShadowedDepth);
        Assert.Equal(new RpnValue.IntValue(10), xInfo.ShadowedValue);

        // Cerrar bloque
        Assert.True(evaluator.EvaluateLine("end", stack, env, in DefaultOpts).IsSuccess);
        Assert.Equal(0, env.CurrentDepth);

        // Fuera del bloque: x conserva su valor global 10 y z ya no existe
        Assert.True(env.TryLookup("x", out RpnValue? xGlobal, out int xDepth));
        Assert.Equal(new RpnValue.IntValue(10), xGlobal);
        Assert.Equal(0, xDepth);
        Assert.False(env.TryLookup("z", out _, out _));
    }

    [Fact]
    public void BeginEnd_NestedMultiLevelAndInline_RestoresEachLevelProperly()
    {
        var evaluator = new RpnEvaluator();
        var stack = new EvaluationStack();
        var env = new SymbolEnvironment();

        // Todo en una línea: global x=10; nivel 1 x=20; nivel 2 x=30 (empuja 30); sale a nivel 1 (empuja 20); sale a global (empuja 10); suma todo
        EvalResult<EvaluationReport> res = evaluator.EvaluateLine(
            "x 10 = begin x 20 = begin x 30 = x end x + end x +",
            stack,
            env,
            in DefaultOpts);

        Assert.True(res.TryGetValue(out EvaluationReport? report, out _));
        Assert.Equal(0, report.ScopeDepth);
        Assert.Equal(new RpnValue.IntValue(60), report.TopValue); // 30 + 20 + 10
        Assert.True(env.TryLookup("x", out RpnValue? finalX, out int finalDepth));
        Assert.Equal(new RpnValue.IntValue(10), finalX);
        Assert.Equal(0, finalDepth);
    }

    [Fact]
    public void EndWithoutBegin_ReturnsUnmatchedEndBlockError()
    {
        var evaluator = new RpnEvaluator();
        var stack = new EvaluationStack();
        var env = new SymbolEnvironment();

        EvalResult<EvaluationReport> res = evaluator.EvaluateLine("end", stack, env, in DefaultOpts);

        Assert.True(res.IsFailure);
        Assert.True(res.Match(_ => false, err => err is RpnError.UnmatchedEndBlock));
    }

    [Fact]
    public void BeginEnd_SelfReferencingReassignment_ShadowsWithoutMutatingGlobal()
    {
        var evaluator = new RpnEvaluator();
        var stack = new EvaluationStack();
        var env = new SymbolEnvironment();

        Assert.True(evaluator.EvaluateLine("x 10 =", stack, env, in DefaultOpts).IsSuccess);
        Assert.True(evaluator.EvaluateLine("begin", stack, env, in DefaultOpts).IsSuccess);

        // Lee el valor global x (10), suma 5 y lo vincula en el alcance local (15)
        Assert.True(evaluator.EvaluateLine("x x 5 + =", stack, env, in DefaultOpts).IsSuccess);
        Assert.True(env.TryLookup("x", out RpnValue? localX, out int localDepth));
        Assert.Equal(new RpnValue.IntValue(15), localX);
        Assert.Equal(1, localDepth);

        Assert.True(evaluator.EvaluateLine("end", stack, env, in DefaultOpts).IsSuccess);
        Assert.True(env.TryLookup("x", out RpnValue? globalX, out int globalDepth));
        Assert.Equal(new RpnValue.IntValue(10), globalX);
        Assert.Equal(0, globalDepth);
    }
}
