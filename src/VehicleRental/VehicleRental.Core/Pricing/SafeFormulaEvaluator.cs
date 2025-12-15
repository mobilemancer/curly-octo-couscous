using VehicleRental.Core.Ports;

namespace VehicleRental.Core.Pricing;

/// <summary>
/// Simple recursive descent parser for safe formula evaluation.
/// Supports: + - * / ( ) decimal literals and named variables.
/// </summary>
public partial class SafeFormulaEvaluator : IPriceFormulaEvaluator
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
                string varName = formula[start..i];

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
}
