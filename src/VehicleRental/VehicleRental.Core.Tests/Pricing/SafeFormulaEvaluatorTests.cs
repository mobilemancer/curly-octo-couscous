using VehicleRental.Core.Pricing;

namespace VehicleRental.Core.Tests.Pricing;

public class SafeFormulaEvaluatorTests
{
    private readonly SafeFormulaEvaluator _evaluator = new();

    [Theory]
    [InlineData("10 + 5", 15)]
    [InlineData("10 - 3", 7)]
    [InlineData("4 * 5", 20)]
    [InlineData("20 / 4", 5)]
    [InlineData("(10 + 5) * 2", 30)]
    [InlineData("10 + 5 * 2", 20)] // Order of operations
    [InlineData("(10 + 5) * (3 - 1)", 30)]
    public void Evaluate_BasicArithmetic_ReturnsCorrectResult(string formula, decimal expected)
    {
        // Arrange
        var variables = new Dictionary<string, decimal>();

        // Act
        var result = _evaluator.Evaluate(formula, variables);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Evaluate_WithVariables_ReturnsCorrectResult()
    {
        // Arrange
        var formula = "baseDayRate * days";
        var variables = new Dictionary<string, decimal>
        {
            ["baseDayRate"] = 100,
            ["days"] = 5
        };

        // Act
        var result = _evaluator.Evaluate(formula, variables);

        // Assert
        Assert.Equal(500, result);
    }

    [Fact]
    public void Evaluate_SmallCarFormula_ReturnsCorrectResult()
    {
        // Arrange
        var formula = "baseDayRate * days";
        var variables = new Dictionary<string, decimal>
        {
            ["baseDayRate"] = 100,
            ["baseKmPrice"] = 0.5m,
            ["days"] = 3,
            ["km"] = 150
        };

        // Act
        var result = _evaluator.Evaluate(formula, variables);

        // Assert
        Assert.Equal(300, result);
    }

    [Fact]
    public void Evaluate_StationWagonFormula_ReturnsCorrectResult()
    {
        // Arrange
        var formula = "(baseDayRate * days * 1.3) + (baseKmPrice * km)";
        var variables = new Dictionary<string, decimal>
        {
            ["baseDayRate"] = 100,
            ["baseKmPrice"] = 0.5m,
            ["days"] = 3,
            ["km"] = 150
        };

        // Act
        var result = _evaluator.Evaluate(formula, variables);

        // Assert
        Assert.Equal(465, result); // (100 * 3 * 1.3) + (0.5 * 150) = 390 + 75 = 465
    }

    [Fact]
    public void Evaluate_TruckFormula_ReturnsCorrectResult()
    {
        // Arrange
        var formula = "(baseDayRate * days * 1.5) + (baseKmPrice * km * 1.5)";
        var variables = new Dictionary<string, decimal>
        {
            ["baseDayRate"] = 100,
            ["baseKmPrice"] = 0.5m,
            ["days"] = 3,
            ["km"] = 150
        };

        // Act
        var result = _evaluator.Evaluate(formula, variables);

        // Assert
        Assert.Equal(562.5m, result); // (100 * 3 * 1.5) + (0.5 * 150 * 1.5) = 450 + 112.5 = 562.5
    }

    [Theory]
    [InlineData("System.Console.WriteLine()")]
    [InlineData("eval('malicious')")]
    [InlineData("exec('code')")]
    [InlineData("10; DROP TABLE")]
    [InlineData("baseDayRate; malicious")]
    public void Evaluate_InjectionAttempts_ThrowsArgumentException(string maliciousFormula)
    {
        // Arrange
        var variables = new Dictionary<string, decimal>
        {
            ["baseDayRate"] = 100,
            ["baseKmPrice"] = 0.5m,
            ["days"] = 3,
            ["km"] = 150
        };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _evaluator.Evaluate(maliciousFormula, variables));
    }

    [Fact]
    public void Evaluate_DivisionByZeroLiteral_ThrowsInvalidOperationException()
    {
        // Arrange
        var formula = "10 / 0";
        var variables = new Dictionary<string, decimal>();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => _evaluator.Evaluate(formula, variables));
    }

    [Fact]
    public void Evaluate_DivisionByZeroVariable_ThrowsInvalidOperationException()
    {
        // Arrange
        var formula = "baseDayRate / days";
        var variables = new Dictionary<string, decimal>
        {
            ["baseDayRate"] = 100,
            ["days"] = 0
        };

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => _evaluator.Evaluate(formula, variables));
    }

    [Fact]
    public void Evaluate_DeeplyNestedParentheses_ThrowsArgumentException()
    {
        // Arrange - create formula with > 50 levels of nesting
        var formula = new string('(', 51) + "1" + new string(')', 51);
        var variables = new Dictionary<string, decimal>();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _evaluator.Evaluate(formula, variables));
    }

    [Fact]
    public void Evaluate_ExceedsMaxLength_ThrowsArgumentException()
    {
        // Arrange - create formula longer than 500 chars
        var formula = new string('1', 501);
        var variables = new Dictionary<string, decimal>();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _evaluator.Evaluate(formula, variables));
    }

    [Fact]
    public void Evaluate_UnknownVariable_ThrowsArgumentException()
    {
        // Arrange
        var formula = "unknownVar * 10";
        var variables = new Dictionary<string, decimal>
        {
            ["baseDayRate"] = 100
        };

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => _evaluator.Evaluate(formula, variables));
        Assert.Contains("unknownVar", ex.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("( 1 * 10")]
    [InlineData("( 1 * 10(")]
    public void Evaluate_InvalidFormula_ThrowsArgumentException(string? formula)
    {
        // Arrange
        var variables = new Dictionary<string, decimal>();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _evaluator.Evaluate(formula!, variables));
    }

    [Fact]
    public void Evaluate_NegativeNumbers_HandlesCorrectly()
    {
        // Arrange
        var formula = "-10 + 5";
        var variables = new Dictionary<string, decimal>();

        // Act
        var result = _evaluator.Evaluate(formula, variables);

        // Assert
        Assert.Equal(-5, result);
    }

    [Fact]
    public void Evaluate_DecimalNumbers_HandlesCorrectly()
    {
        // Arrange
        var formula = "10.5 * 2.5";
        var variables = new Dictionary<string, decimal>();

        // Act
        var result = _evaluator.Evaluate(formula, variables);

        // Assert
        Assert.Equal(26.25m, result);
    }
}
