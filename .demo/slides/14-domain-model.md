# 📦 Domain Model

## Core Entities

### VehicleTypeDefinition
- `VehicleTypeId` - "small-car", "station-wagon", etc
- `DisplayName` - Human-readable name
- `PricingFormula` - Custom expression for pricing
- `Description` - Optional details

### Rental
- `BookingNumber` - Unique identifier
- `CustomerId` - Customer reference
- `Vehicle` - Vehicle being rented
- `CheckoutTime` - When rental started
- `FrozenPricing` - 🔒 Locked pricing at checkout!

**Key Feature:** Pricing is frozen at checkout, protecting against formula changes mid-rental
