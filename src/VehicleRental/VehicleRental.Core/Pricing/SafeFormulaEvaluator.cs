using System.Globalization;
using VehicleRental.Core.Ports;

namespace VehicleRental.Core.Pricing;

/// <summary>
/// Simple recursive descent parser for safe formula evaluation.
/// Supports: + - * / ( ) decimal literals and named variables.
/// </summary>
public class SafeFormulaEvaluator : IPriceFormulaEvaluator
{
    private const int MaxFormulaLength = 500;
    private static readonly char[] AllowedOperators = ['+', '-', '*', '/', '(', ')', '.', ' '];

    public decimal Evaluate(string formula, IReadOnlyDictionary<string, decimal> variables)
    {
        if (string.IsNullOrWhiteSpace(formula))
        {
            throw new ArgumentException("Formula cannot be null or whitespace.", nameof(formula));
        }

        if (formula.Length > MaxFormulaLength)
        {
            throw new ArgumentException($"Formula exceeds maximum length of {MaxFormulaLength} characters.", nameof(formula));
        }

        // Security: validate allowed characters
        ValidateFormulaSafety(formula, variables);

        var parser = new FormulaParser(formula, variables);
        return parser.Parse();
    }

    private static void ValidateFormulaSafety(string formula, IReadOnlyDictionary<string, decimal> variables)
    {
        var allowedVariableNames = variables.Keys.ToHashSet();

        for (int i = 0; i < formula.Length; i++)
        {
            char c = formula[i];

            if (char.IsDigit(c) || AllowedOperators.Contains(c))
            {
                continue;
            }

            if (char.IsLetter(c))
            {
                // Extract variable name
                int start = i;
                while (i < formula.Length && char.IsLetterOrDigit(formula[i]))
                {
                    i++;
                }
                string varName = formula.Substring(start, i - start);

                if (!allowedVariableNames.Contains(varName))
                {
                    throw new ArgumentException($"Formula contains unknown variable: '{varName}'. Allowed variables: {string.Join(", ", allowedVariableNames)}");
                }

                i--; // Adjust for loop increment
                continue;
            }

            throw new ArgumentException($"Formula contains disallowed character at position {i}: '{c}'");
        }
    }

    private class FormulaParser
    {
        private readonly string _formula;
        private readonly IReadOnlyDictionary<string, decimal> _variables;
        private int _position;
        private int _depth;
        private const int MaxDepth = 50; // Prevent stack overflow from deeply nested parentheses

        public FormulaParser(string formula, IReadOnlyDictionary<string, decimal> variables)
        {
            _formula = formula;
            _variables = variables;
            _position = 0;
            _depth = 0;
        }

        public decimal Parse()
        {
            var result = ParseExpression();

            SkipWhitespace();
            if (_position < _formula.Length)
            {
                throw new ArgumentException($"Unexpected character at position {_position}: '{_formula[_position]}'");
            }

            return result;
        }

        private decimal ParseExpression()
        {
            var left = ParseTerm();

            while (true)
            {
                SkipWhitespace();
                if (_position >= _formula.Length)
                    break;

                char op = _formula[_position];
                if (op != '+' && op != '-')
                    break;

                _position++;
                var right = ParseTerm();

                left = op == '+' ? left + right : left - right;
            }

            return left;
        }

        private decimal ParseTerm()
        {
            var left = ParseFactor();

            while (true)
            {
                SkipWhitespace();
                if (_position >= _formula.Length)
                    break;

                char op = _formula[_position];
                if (op != '*' && op != '/')
                    break;

                _position++;
                var right = ParseFactor();

                if (op == '*')
                {
                    left *= right;
                }
                else
                {
                    if (right == 0)
                    {
                        throw new InvalidOperationException("Division by zero in formula evaluation.");
                    }
                    left /= right;
                }
            }

            return left;
        }

        private decimal ParseFactor()
        {
            SkipWhitespace();

            if (_position >= _formula.Length)
            {
                throw new ArgumentException("Unexpected end of formula.");
            }

            // Handle parentheses
            if (_formula[_position] == '(')
            {
                _depth++;
                if (_depth > MaxDepth)
                {
                    throw new ArgumentException($"Formula exceeds maximum nesting depth of {MaxDepth}.");
                }

                _position++;
                var result = ParseExpression();
                SkipWhitespace();

                if (_position >= _formula.Length || _formula[_position] != ')')
                {
                    throw new ArgumentException("Missing closing parenthesis.");
                }

                _position++;
                _depth--;
                return result;
            }

            // Handle negative numbers
            if (_formula[_position] == '-')
            {
                _position++;
                return -ParseFactor();
            }

            // Handle numbers
            if (char.IsDigit(_formula[_position]) || _formula[_position] == '.')
            {
                return ParseNumber();
            }

            // Handle variables
            if (char.IsLetter(_formula[_position]))
            {
                return ParseVariable();
            }

            throw new ArgumentException($"Unexpected character at position {_position}: '{_formula[_position]}'");
        }

        private decimal ParseNumber()
        {
            int start = _position;

            while (_position < _formula.Length &&
                   (char.IsDigit(_formula[_position]) || _formula[_position] == '.'))
            {
                _position++;
            }

            string numberStr = _formula.Substring(start, _position - start);

            if (!decimal.TryParse(numberStr, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal result))
            {
                throw new ArgumentException($"Invalid number format: '{numberStr}'");
            }

            return result;
        }

        private decimal ParseVariable()
        {
            int start = _position;

            while (_position < _formula.Length && char.IsLetterOrDigit(_formula[_position]))
            {
                _position++;
            }

            string varName = _formula.Substring(start, _position - start);

            if (!_variables.TryGetValue(varName, out decimal value))
            {
                throw new ArgumentException($"Unknown variable: '{varName}'");
            }

            return value;
        }

        private void SkipWhitespace()
        {
            while (_position < _formula.Length && char.IsWhiteSpace(_formula[_position]))
            {
                _position++;
            }
        }
    }
}
