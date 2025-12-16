# 📊 Vehicle Categories

## Supported Vehicle Types

| Category | Description | Pricing Model |
|----------|-------------|-----------------|
| **Small Car** | Compact vehicles | Day + km based |
| **Station Wagon** | Family vehicles | Day + km based |
| **Truck** | Commercial vehicles | Day-only pricing |

### Pricing Example:
- **Small Car**: `baseDayRate * days * 1.2 + baseKmPrice * km`
- **Truck**: `baseDayRate * days * 1.5` (no km charge)
