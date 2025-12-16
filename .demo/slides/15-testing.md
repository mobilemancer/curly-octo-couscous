# 🧪 Testing Strategy

## Unit Testing with xUnit

### Test Categories:
- **Application** - Service integration tests
- **Pricing** - Formula evaluation tests
- **Helpers** - Utility function tests

### Test Organization:
```
VehicleRental.Core.Tests/
├── Application/   - CheckoutService, ReturnService
├── Pricing/       - SafeFormulaEvaluator
└── Helpers/       - RentalCalculations, timezone handling
```

### Running Tests:
```bash
dotnet test
```
