using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class SafeExpressionEvaluatorTests
{
    [Theory]
    [InlineData("2+3*4", "14")]
    [InlineData("(2+3)*4", "20")]
    [InlineData("-5 + 2", "-3")]
    [InlineData("10 % 4", "2")]
    [InlineData("1 / 4", "0.25")]
    [InlineData("（3＋2）×4÷2", "10")]
    [InlineData("2 * -(3 + 4)", "-14")]
    public void TryEvaluate_UsesSafeArithmeticPrecedence(
        string expression,
        string expected)
    {
        Assert.True(
            SafeExpressionEvaluator
                .TryEvaluate(
                    expression,
                    out string result));
        Assert.Equal(
            expected,
            result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("42")]
    [InlineData("notepad")]
    [InlineData("C:\\Windows")]
    [InlineData("1/0")]
    [InlineData("1+")]
    [InlineData("2(3+4)")]
    [InlineData("1e3 + 2")]
    [InlineData("79228162514264337593543950335+1")]
    public void TryEvaluate_RejectsNonExpressionOrUnsafeInput(
        string expression)
    {
        Assert.False(
            SafeExpressionEvaluator
                .TryEvaluate(
                    expression,
                    out string result));
        Assert.Equal(
            string.Empty,
            result);
    }

    [Fact]
    public void TryEvaluate_RejectsExcessiveLengthAndDepth()
    {
        string longExpression =
            new string(
                '1',
                SafeExpressionEvaluator
                    .MaximumExpressionLength)
            + "+1";
        string deepExpression =
            new string('(', 17)
            + "1+1"
            + new string(')', 17);

        Assert.False(
            SafeExpressionEvaluator
                .TryEvaluate(
                    longExpression,
                    out _));
        Assert.False(
            SafeExpressionEvaluator
                .TryEvaluate(
                    deepExpression,
                    out _));
    }
}
