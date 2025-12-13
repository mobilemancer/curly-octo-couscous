# Product Requirements Document: Car Rental Management System

## 1. Product Overview

### 1.1 Purpose

Build a flexible car rental management system that handles the rental lifecycle from vehicle checkout to return, with automated pricing calculations. The system must be adaptable to multiple customers with varying data storage and UI requirements.

### 1.2 Objectives

- Implement core business logic for car rental operations
- Support multiple vehicle categories with extensible pricing models
- Provide accurate automated pricing calculations
- Enable easy integration with different storage backends and user interfaces

## 2. Scope

### 2.1 In Scope - Phase 1

- Business logic implementation for vehicle checkout and return
- Pricing calculation engine for three vehicle categories
- Test coverage for all business logic
- Extensible architecture for future vehicle categories

### 2.2 Out of Scope - Phase 1

- User interface implementation
- Database/storage implementation
  - We still need to implement the abstraction
  - We need inital in-memory store working
  - We need an inital json-file holding vehicle objects available for rental
  - Load initial json in memory on startup, then use the in memory store during application runtime
- Customer management features
- Payment processing
- Vehicle inventory management
- Reservation/booking system

## 3. Target Users

### 3.1 Primary Users

- **Car Rental Agents**: Staff who process vehicle checkouts and returns at rental locations

### 3.2 System Integrators

- Development teams integrating this business logic with various storage solutions and UIs

## 4. Vehicle Categories & Pricing

### 4.1 Vehicle Categories

The system supports three initial vehicle categories, with architecture allowing future additions:

1. **Small Car** (Småbil)
2. **Station Wagon** (Kombi)
3. **Truck** (Lastbil)

### 4.2 Pricing Parameters

All pricing calculations use two configurable parameters:

- **baseDayRate**: Base daily rental rate
- **baseKmPrice**: Base price per kilometer

### 4.3 Pricing Formulas

#### Small Car

```
Price = baseDayRate × numberOfDays
```

#### Station Wagon

```
Price = (baseDayRate × numberOfDays × 1.3) + (baseKmPrice × numberOfKm)
```

#### Truck

```
Price = (baseDayRate × numberOfDays × 1.5) + (baseKmPrice × numberOfKm × 1.5)
```

#### Additional vehicles

Assume that additional vehicles can have a price calculation pattern that differes from the first ones we are going to implement.

## 5. Functional Requirements

### 5.1 Use Case 1: Register Vehicle Checkout

**Description**: When a rental agent hands over a vehicle to a customer, the checkout must be registered in the system.

**Required Data Fields**:

- Booking number (unique identifier for the rental)
- Vehicle registration number
- Customer personal identification number (personnummer or equivalent)
- Vehicle category (Small Car, Station Wagon, or Truck)
- Checkout date and time
- Current odometer reading (km)

**Business Rules**:

- Booking number must be unique per rental instance
- One rental relates to one vehicle at one time
- All fields are mandatory
- Odometer reading must be a non negative number

**Success Criteria**:

- Checkout is successfully recorded with all required information
- System associates the checkout with the correct vehicle category for future pricing calculation

### 5.2 Use Case 2: Register Vehicle Return

**Description**: When a customer returns a vehicle, the rental agent registers the return, triggering automatic price calculation.

**Required Data Fields**:

- Booking number
- Return date and time
- Current odometer reading (km)

**Business Rules**:

- Booking number must match an existing checkout
- Return odometer reading must be greater than or equal to checkout reading
- Number of days calculated from checkout to return timestamps
- Number of kilometers = return odometer - checkout odometer
- Price automatically calculated using the appropriate formula based on vehicle category

**Success Criteria**:

- Return is successfully recorded
- Rental duration (days) is accurately calculated
- Distance traveled (km) is accurately calculated
- Final rental price is calculated according to the correct pricing formula
- Calculated price is available for retrieval

## 6. Non-Functional Requirements

### 6.1 Architecture

- **Modularity**: Business logic must be decoupled from data storage and UI layers
- **Extensibility**: Architecture must support adding new vehicle categories without easily
- **Testability**: All business logic must be unit testable

### 6.2 Code Quality

- Comprehensive test coverage for all use cases
- Clean, maintainable code following established design patterns
- Clear separation of concerns

### 6.3 Flexibility

- Support for dependency injection to enable different storage implementations
- No hard dependencies on specific frameworks for data access or UI

## 7. Technical Considerations

### 7.1 Design Patterns (Recommended)

- Strategy Pattern for pricing calculations (different strategies per vehicle category)
- Repository Pattern for data access abstraction
- Dependency Injection for loose coupling

### 7.2 Calculation Precision

- Prices should be calculated with decimal precision
- Rounding rules for final price presentation should allways round up to nearest integer value

### 7.3 Date/Time Handling

- No partial days
- Minimum amount of days a car can be rented is 1
- If a customer return a car the the same day they rent it, we charge for 1 day
- Time zone considerations if system will be used across regions

## 8. Test Requirements

### 8.1 Unit Tests Required

- Pricing calculation for each vehicle category
- Checkout registration validation
- Return registration validation
- Duration calculation (various scenarios: same day, multiple days, edge cases)
- Distance calculation
- End-to-end rental flow (checkout → return → price calculation)

### 8.2 Test Scenarios

- Normal rental flow for each vehicle category
- Same-day returns
- Multi-day rentals
- Zero kilometer rentals (vehicle not moved)
- Edge cases: minimum values, large numbers

### 8.3 Validation Tests

- Invalid booking numbers
- Missing required fields
- Invalid odometer readings (negative, return < checkout)
- Duplicate booking numbers

## 9. Success Metrics

- 100% of core business logic covered by passing tests
- Pricing calculations accurate to specification for all vehicle categories
- Zero critical bugs in rental flow
- Clean architecture enabling easy integration with multiple storage/UI implementations

## 10. Future Considerations

### 10.1 Potential Enhancements

- Additional vehicle categories
- Dynamic pricing based on season, demand, or other factors
- Multi-day discounts or promotional pricing
- Damage reporting and fee calculation
- Fuel level tracking and refueling charges
- Late return penalties
- Insurance options and pricing

### 10.2 Integration Points

- Customer management system
- Payment processing system
- Vehicle inventory system
- Reservation/booking system
- Reporting and analytics

## 11. Assumptions & Constraints

### 11.1 Assumptions

- Rental agents have accurate odometer readings
- Booking numbers are generated by an external system
- Base pricing parameters (baseDayRate, baseKmPrice) are provided by configuration
- One vehicle can only have one active rental at a time

### 11.2 Constraints

- Must remain storage-agnostic
- Must remain UI-agnostic
- Focus on business logic only in Phase 1

## 12. Acceptance Criteria

A successful implementation must:

1. Implement both use cases (checkout and return) with all required data fields
2. Include accurate pricing calculation for all three vehicle categories
3. Provide comprehensive test suite with all tests passing
4. Demonstrate extensibility for future vehicle categories
5. Show clear separation between business logic and infrastructure concerns
6. Include appropriate error handling and validation

---

**Document Version**: 1.1
**Last Updated**: December 2025
**Status**: Ready for Implementation
