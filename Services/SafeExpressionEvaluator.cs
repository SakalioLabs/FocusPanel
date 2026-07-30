using System;
using System.Globalization;

namespace FocusPanel.Services;

internal static class SafeExpressionEvaluator
{
    internal const int MaximumExpressionLength =
        128;
    private const int MaximumParenthesisDepth =
        16;

    internal static bool TryEvaluate(
        string? expression,
        out string result)
    {
        result = string.Empty;
        if (string.IsNullOrWhiteSpace(
                expression)
            || expression.Length
            > MaximumExpressionLength)
        {
            return false;
        }

        try
        {
            var parser =
                new Parser(
                    Normalize(expression));
            decimal value =
                parser.Parse();
            if (!parser.HasBinaryOperator)
                return false;

            result =
                value.ToString(
                    "0.############################",
                    CultureInfo.InvariantCulture);
            return true;
        }
        catch (
            Exception ex)
            when (ex
                is FormatException
                or OverflowException
                or DivideByZeroException)
        {
            return false;
        }
    }

    private static string Normalize(
        string expression) =>
        expression
            .Replace('×', '*')
            .Replace('÷', '/')
            .Replace('−', '-')
            .Replace('＋', '+')
            .Replace('（', '(')
            .Replace('）', ')');

    private sealed class Parser
    {
        private readonly string _text;
        private int _index;
        private int _depth;

        internal Parser(string text)
        {
            _text = text;
        }

        internal bool HasBinaryOperator
        {
            get;
            private set;
        }

        internal decimal Parse()
        {
            decimal value =
                ParseExpression();
            SkipWhitespace();
            if (_index != _text.Length)
                throw new FormatException();
            return value;
        }

        private decimal ParseExpression()
        {
            decimal value =
                ParseTerm();
            while (true)
            {
                SkipWhitespace();
                if (TryConsume('+'))
                {
                    HasBinaryOperator = true;
                    value = checked(
                        value
                        + ParseTerm());
                    continue;
                }
                if (TryConsume('-'))
                {
                    HasBinaryOperator = true;
                    value = checked(
                        value
                        - ParseTerm());
                    continue;
                }
                return value;
            }
        }

        private decimal ParseTerm()
        {
            decimal value =
                ParseUnary();
            while (true)
            {
                SkipWhitespace();
                if (TryConsume('*'))
                {
                    HasBinaryOperator = true;
                    value = checked(
                        value
                        * ParseUnary());
                    continue;
                }
                if (TryConsume('/'))
                {
                    HasBinaryOperator = true;
                    decimal divisor =
                        ParseUnary();
                    if (divisor == 0)
                    {
                        throw new
                            DivideByZeroException();
                    }
                    value =
                        value
                        / divisor;
                    continue;
                }
                if (TryConsume('%'))
                {
                    HasBinaryOperator = true;
                    decimal divisor =
                        ParseUnary();
                    if (divisor == 0)
                    {
                        throw new
                            DivideByZeroException();
                    }
                    value =
                        value
                        % divisor;
                    continue;
                }
                return value;
            }
        }

        private decimal ParseUnary()
        {
            SkipWhitespace();
            if (TryConsume('+'))
                return ParseUnary();
            if (TryConsume('-'))
            {
                return checked(
                    -ParseUnary());
            }
            return ParsePrimary();
        }

        private decimal ParsePrimary()
        {
            SkipWhitespace();
            if (TryConsume('('))
            {
                _depth++;
                if (_depth
                    > MaximumParenthesisDepth)
                {
                    throw new FormatException();
                }

                decimal value =
                    ParseExpression();
                SkipWhitespace();
                if (!TryConsume(')'))
                    throw new FormatException();
                _depth--;
                return value;
            }

            return ParseNumber();
        }

        private decimal ParseNumber()
        {
            SkipWhitespace();
            int start = _index;
            bool hasDigit = false;
            bool hasDecimalPoint = false;
            while (_index < _text.Length)
            {
                char character =
                    _text[_index];
                if (char.IsDigit(character))
                {
                    hasDigit = true;
                    _index++;
                    continue;
                }
                if (character == '.'
                    && !hasDecimalPoint)
                {
                    hasDecimalPoint = true;
                    _index++;
                    continue;
                }
                break;
            }

            if (!hasDigit
                || !decimal.TryParse(
                    _text[start.._index],
                    NumberStyles
                        .AllowDecimalPoint,
                    CultureInfo
                        .InvariantCulture,
                    out decimal value))
            {
                throw new FormatException();
            }

            return value;
        }

        private void SkipWhitespace()
        {
            while (_index < _text.Length
                   && char.IsWhiteSpace(
                       _text[_index]))
            {
                _index++;
            }
        }

        private bool TryConsume(
            char expected)
        {
            if (_index >= _text.Length
                || _text[_index]
                != expected)
            {
                return false;
            }

            _index++;
            return true;
        }
    }
}
