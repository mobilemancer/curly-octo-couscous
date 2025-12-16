# 💵 Dynamic Pricing Engine

## SafeFormulaEvaluator

### Supported Formula Variables:
- `baseDayRate` - Daily rental rate
- `baseKmPrice` - Per-kilometer charge
- `days` - Rental duration
- `km` - Distance traveled

### Example Formulas:
```
Small car:  baseDayRate * days * 1.2 + baseKmPrice * km
Truck:      baseDayRate * days * 1.5
```

### Safety:
- Expression parsing (no code execution)
- Validated operators: + - * / ()
- Decimal precision maintained
