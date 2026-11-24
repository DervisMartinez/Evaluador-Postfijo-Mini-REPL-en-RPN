using MiniRepl.Rpn.Diagnostics;
using MiniRepl.Rpn.Engine;
using MiniRepl.Rpn.Memory;
using MiniRepl.Rpn.Types;
using Xunit;

namespace RpnRepl.Tests;

public class ArithmeticAndTypeTests
{
    private static readonly EvalOptions DefaultOpts = new(CaptureTrace: false);

    [Theory]
    [InlineData("5 3 +", 8L)]
    [InlineData("10 4 -", 6L)]
    [InlineData("2 10 -", -8L)]
    [InlineData("10 2 /", 5L)]
    [InlineData("2 10 /", 0L)]
    [InlineData("6 7 *", 42L)]
    [InlineData("7 2 /", 3L)]
    [InlineData("10 3 %", 1L)]
    [InlineData("3 4 2 * +", 11L)]
    [InlineData("5 1 2 + 4 * + 3 -", 14L)]
    [InlineData("15 7 1 1 + - / 3 * 2 1 1 + + -", 5L)]
    public void Evaluate_IntArithmetic_ProducesIntValue(string expr, long expected)
    {
        var evaluator = new RpnEvaluator();
        var stack = new EvaluationStack();
        var env = new SymbolEnvironment();

        EvalResult<EvaluationReport> res = evaluator.EvaluateLine(expr, stack, env, in DefaultOpts);

        Assert.True(res.TryGetValue(out EvaluationReport? report, out _));
        var intVal = Assert.IsType<RpnValue.IntValue>(report.TopValue);
        Assert.Equal(expected, intVal.Value);
    }

    [Theory]
    [InlineData("5 2.5 +", 7.5)]
    [InlineData("7.0 2 /", 3.5)]
    [InlineData("2.5 4 *", 10.0)]
    [InlineData("10.5 3.5 -", 7.0)]
    public void Evaluate_MixedOrFloatArithmetic_PromotesToFloat(string expr, double expected)
    {
        var evaluator = new RpnEvaluator();
        var stack = new EvaluationStack();
        var env = new SymbolEnvironment();

        EvalResult<EvaluationReport> res = evaluator.EvaluateLine(expr, stack, env, in DefaultOpts);

        Assert.True(res.TryGetValue(out EvaluationReport? report, out _));
        var floatVal = Assert.IsType<RpnValue.FloatValue>(report.TopValue);
        Assert.Equal(expected, floatVal.Value, precision: 10);
    }

    [Theory]
    [InlineData("5 5 ==", true)]
    [InlineData("5 5.0 ==", true)]
    [InlineData("5 6 !=", true)]
    [InlineData("3 7 <", true)]
    [InlineData("3 7 >", false)]
    [InlineData("true true ==", true)]
    [InlineData("true false !=", true)]
    [InlineData("5 5 <=", true)]
    [InlineData("7 5 >=", true)]
    public void Evaluate_Comparisons_ProduceBoolValue(string expr, bool expected)
    {
        var evaluator = new RpnEvaluator();
        var stack = new EvaluationStack();
        var env = new SymbolEnvironment();

        EvalResult<EvaluationReport> res = evaluator.EvaluateLine(expr, stack, env, in DefaultOpts);

        Assert.True(res.TryGetValue(out EvaluationReport? report, out _));
        var boolVal = Assert.IsType<RpnValue.BoolValue>(report.TopValue);
        Assert.Equal(expected, boolVal.Value);
    }

    [Theory]
    [InlineData("5 true +")]
    [InlineData("true 5 *")]
    [InlineData("true false +")]
    [InlineData("true false <")]
    [InlineData("5 true ==")]
    [InlineData("5.0 false !=")]
    public void Evaluate_IncompatibleOperandTypes_ReturnsTypeMismatch(string expr)
    {
        var evaluator = new RpnEvaluator();
        var stack = new EvaluationStack();
        var env = new SymbolEnvironment();

        EvalResult<EvaluationReport> res = evaluator.EvaluateLine(expr, stack, env, in DefaultOpts);

        Assert.True(res.IsFailure);
        Assert.True(res.Match(_ => false, err => err is RpnError.TypeMismatch));
    }

    [Fact]
    public void Evaluate_AssignmentExampleFromSpec_EvaluatesCorrectly()
    {
        var evaluator = new RpnEvaluator();
        var stack = new EvaluationStack();
        var env = new SymbolEnvironment();

        Assert.True(evaluator.EvaluateLine("x 10 =", stack, env, in DefaultOpts).IsSuccess);
        EvalResult<EvaluationReport> res = evaluator.EvaluateLine("x 5 + 2 *", stack, env, in DefaultOpts);

        Assert.True(res.TryGetValue(out EvaluationReport? report, out _));
        Assert.Equal(new RpnValue.IntValue(30), report.TopValue);
    }
}
