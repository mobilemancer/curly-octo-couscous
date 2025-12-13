namespace VehicleRental.Core.Ports;

/// <summary>
/// Evaluates pricing formulas with provided variable values.
/// </summary>
public interface IPriceFormulaEvaluator
{
    /// <summary>
    /// Evaluates a pricing formula with the given variable values.
    /// </summary>
    /// <param name="formula">The formula expression to evaluate.</param>
    /// <param name="variables">Dictionary of variable names and their values.</param>
    /// <returns>The calculated result.</returns>
    /// <exception cref="ArgumentException">Thrown when the formula is invalid or contains disallowed operations.</exception>
    /// <exception cref="InvalidOperationException">Thrown when evaluation fails (e.g., divide by zero).</exception>
    decimal Evaluate(string formula, IReadOnlyDictionary<string, decimal> variables);
}
