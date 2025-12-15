using Microsoft.Extensions.Logging;
using VehicleRental.Core.Domain;
using VehicleRental.Core.Helpers;
using VehicleRental.Core.Ports;

namespace VehicleRental.Core.Pricing;

/// <summary>
/// Calculates rental prices using vehicle type definitions and pricing formulas.
/// </summary>
public class PricingCalculator(
    IVehicleTypeStore vehicleTypeStore,
    IPriceFormulaEvaluator formulaEvaluator,
    ILogger<PricingCalculator> logger)
{
    private readonly IVehicleTypeStore _vehicleTypeStore = vehicleTypeStore ?? throw new ArgumentNullException(nameof(vehicleTypeStore));
    private readonly IPriceFormulaEvaluator _formulaEvaluator = formulaEvaluator ?? throw new ArgumentNullException(nameof(formulaEvaluator));
    private readonly ILogger<PricingCalculator> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <summary>
    /// Calculates the rental price for the given parameters.
    /// </summary>
    /// <param name="vehicleTypeId">The vehicle type identifier.</param>
    /// <param name="parameters">The pricing parameters (base rates).</param>
    /// <param name="days">Number of rental days.</param>
    /// <param name="km">Distance traveled in kilometers.</param>
    /// <returns>The calculated raw price before final rounding.</returns>
    /// <exception cref="InvalidOperationException">Thrown when vehicle type is not found or formula evaluation fails.</exception>
    public async Task<decimal> CalculateAsync(
        string vehicleTypeId,
        PricingParameters parameters,
        int days,
        decimal km)
    {
        var normalizedTypeId = VehicleTypeIdNormalizer.Normalize(vehicleTypeId);

        var vehicleType = await _vehicleTypeStore.GetByIdAsync(normalizedTypeId);
        if (vehicleType is null)
        {
            _logger.LogError("Vehicle type not found: {VehicleTypeId}", normalizedTypeId);
            throw new InvalidOperationException($"Vehicle type not found: {normalizedTypeId}");
        }

        _logger.LogInformation("Calculating price for vehicle type {VehicleTypeId} with formula: {Formula}",
            normalizedTypeId, vehicleType.PricingFormula);

        var variables = new Dictionary<string, decimal>
        {
            ["baseDayRate"] = parameters.BaseDayRate,
            ["baseKmPrice"] = parameters.BaseKmPrice,
            ["days"] = days,
            ["km"] = km
        };

        try
        {
            var rawPrice = _formulaEvaluator.Evaluate(vehicleType.PricingFormula, variables);

            _logger.LogInformation("Calculated raw price: {RawPrice} for {Days} days and {Km} km",
                rawPrice, days, km);

            return rawPrice;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to evaluate pricing formula for vehicle type {VehicleTypeId}", normalizedTypeId);
            throw new InvalidOperationException($"Failed to evaluate pricing formula for vehicle type {normalizedTypeId}", ex);
        }
    }
}
