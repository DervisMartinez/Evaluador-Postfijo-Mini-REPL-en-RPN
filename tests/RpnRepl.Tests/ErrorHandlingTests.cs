using MiniRepl.Rpn.Diagnostics;
using MiniRepl.Rpn.Engine;
using MiniRepl.Rpn.Memory;
using MiniRepl.Rpn.Types;
using Xunit;

namespace RpnRepl.Tests;

public class ErrorHandlingTests
{
    private static readonly EvalOptions DefaultOpts = new(CaptureTrace: true, TransactionalRollback: true);

    [Theory]
    [InlineData("10 0 /")]
    [InlineData("10 0 %")]
    [InlineData("10.0 0.0 /")]
    [InlineData("10 0.0 /")]
    public void Evaluate_DivisionByZero_ReturnsDivisionByZeroError(string expr)
    {
        var evaluator = new RpnEvaluator();
        var stack = new EvaluationStack();
        var env = new SymbolEnvironment();

        EvalResult<EvaluationReport> res = evaluator.EvaluateLine(expr, stack, env, in DefaultOpts);

        Assert.True(res.IsFailure);
        Assert.True(res.Match(_ => false, err => err is RpnError.DivisionByZero));
    }

    [Theory]
    [InlineData("+")]
    [InlineData("5 +")]
    [InlineData("==")]
    [InlineData("=")]
    [InlineData("x =")]
    public void Evaluate_InsufficientStack_ReturnsStackUnderflow(string expr)
    {
        var evaluator = new RpnEvaluator();
        var stack = new EvaluationStack();
        var env = new SymbolEnvironment();

        EvalResult<EvaluationReport> res = evaluator.EvaluateLine(expr, stack, env, in DefaultOpts);

        Assert.True(res.IsFailure);
        Assert.True(res.Match(_ => false, err => err is RpnError.StackUnderflow));
    }

    [Fact]
    public void Evaluate_Int64Overflow_ReturnsArithmeticOverflow()
    {
        var evaluator = new RpnEvaluator();
        var stack = new EvaluationStack();
        var env = new SymbolEnvironment();

        EvalResult<EvaluationReport> res = evaluator.EvaluateLine("9223372036854775807 1 +", stack, env, in DefaultOpts);

        Assert.True(res.IsFailure);
        Assert.True(res.Match(_ => false, err => err is RpnError.ArithmeticOverflow));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("missing 5 +")]
    [InlineData("x missing =")]
    public void Evaluate_UndefinedVariable_ReturnsUndefinedVariableError(string expr)
    {
        var evaluator = new RpnEvaluator();
        var stack = new EvaluationStack();
        var env = new SymbolEnvironment();

        EvalResult<EvaluationReport> res = evaluator.EvaluateLine(expr, stack, env, in DefaultOpts);

        Assert.True(res.IsFailure);
        Assert.True(res.Match(_ => false, err => err is RpnError.UndefinedVariable));
    }

    [Theory]
    [InlineData("5 10 =")]
    [InlineData("true 1 =")]
    public void Evaluate_LiteralAsAssignmentTarget_ReturnsInvalidAssignmentTarget(string expr)
    {
        var evaluator = new RpnEvaluator();
        var stack = new EvaluationStack();
        var env = new SymbolEnvironment();

        EvalResult<EvaluationReport> res = evaluator.EvaluateLine(expr, stack, env, in DefaultOpts);

        Assert.True(res.IsFailure);
        Assert.True(res.Match(_ => false, err => err is RpnError.InvalidAssignmentTarget));
    }

    [Fact]
    public void Evaluate_ErrorMidLine_RollsBackStackAndScopeTransactionally()
    {
        var evaluator = new RpnEvaluator();
        var stack = new EvaluationStack();
        var env = new SymbolEnvironment();

        Assert.True(evaluator.EvaluateLine("42", stack, env, in DefaultOpts).IsSuccess);
        Assert.Single(stack.Items);

        // Esta línea entra a un bloque, define tmp y luego falla con TypeMismatch
        EvalResult<EvaluationReport> failRes = evaluator.EvaluateLine("begin tmp 10 = 5 true +", stack, env, in DefaultOpts);
        Assert.True(failRes.IsFailure);

        // Tanto la pila como la profundidad de alcance y variables deben quedar intactas
        Assert.Single(stack.Items);
        var preserved = Assert.IsType<StackSlot.ValueSlot>(stack.Items[0]);
        Assert.Equal(new RpnValue.IntValue(42), preserved.Value);
        Assert.Equal(0, env.CurrentDepth);
        Assert.False(env.TryLookup("tmp", out _, out _));
    }
}
