# Implementation Specification: Vehicle Rental Management System (.NET 10)

**Status**: Draft (implementation-ready)

This document translates the PRD into an implementation-ready specification: architecture, interfaces, business rules, data contracts, and a recommended build order.

Related docs:

- PRD: `docs/vehicle-rental-management-system-prd.md`
- Assumptions: `docs/assumptions.md`

---

## 1. Goals (Phase 1)

- Implement checkout and return flows as business logic (use cases).
- Calculate pricing for the three initial vehicle types.
- Keep code storage-agnostic and UI-agnostic (console is only an entrypoint).
- Provide an initial in-memory store seeded from a JSON file at startup.
- Provide comprehensive unit tests covering all business rules and calculations.

## 2. Non-Goals (Phase 1)

- No interactive UI beyond a minimal console runner.
- No real database or external persistence; only in-memory repositories.
- No reservation/booking system.
- No payment processing.
- No customer management beyond capturing the provided customer identifier.

---

## 3. Solution Structure (Projects & Responsibilities)

Target framework: `.NET 10`.

- `VehicleRental.Core` (class library)
  - Domain model (entities/value objects).
  - Use cases (application services).
  - Pricing strategies (Strategy pattern).
  - Validation and error contracts.
  - Abstractions/ports (repository interfaces, clock/timezone provider if needed).

- `VehicleRental.Infrastructure` (class library)
  - In-memory repositories.
  - JSON seed loader for initial vehicles.
  - Any runtime adapters that implement Core abstractions.

- `VehicleRental.CLI` (console app)
  - Composition root: DI setup, configuration loading.
  - Demonstration entrypoint (can run a simple scripted flow).
  - No business rules here.

- `VehicleRental.Core.Tests` (xUnit)
  - Unit tests for Core.
  - End-to-end flow tests using in-memory repositories.

Dependency direction must be one-way:

- `Core` has no reference to `Infrastructure` or `CLI`.
- `Infrastructure` references `Core`.
- `CLI` references `Core` + `Infrastructure`.
- `Tests` reference `Core` and may use `Infrastructure` only when testing the in-memory adapters (keep most tests pure Core).

---

## 4. Architecture Overview

### 4.1 Layers

- **Domain (Core)**: Entities/value objects and rules that must always hold.
- **Application (Core)**: Use cases orchestrating domain rules and repositories.
- **Infrastructure**: Concrete storage implementations (in-memory) and seed loading.
- **Presentation (CLI)**: Startup, DI wiring, minimal IO.

### 4.2 Key Patterns

- **Strategy** for pricing per vehicle type.
- **Repository** interfaces in Core, implementations in Infrastructure.
- **Dependency Injection** to assemble strategies and repositories.
- Optional: **Result**-style errors (recommended) or domain exceptions (acceptable). Choose one and apply consistently.

Optional (recommended for operational flexibility):

- **Data-driven pricing**: vehicle types and pricing formulas defined in storage (schema), not in code.
- **Event-driven refresh**: the running app reloads vehicle types when it receives a “vehicle types updated” signal.

---

## 5. Domain Model (Core)

### 5.1 Concepts

#### Vehicle Type (Dynamic)

- Vehicle types are not hard-coded (no enum).
- A vehicle references a `VehicleTypeId` (string) such as `small-car`, `station-wagon`, `truck`.
- A vehicle type becomes "available" when the system can price it.
  - Preferred approach: the type is present in the vehicle type store and contains a valid pricing formula.
  - Optional escape hatch: a corresponding pricing plugin is discovered at runtime (see section 12).

#### VehicleTypeDefinition

Represents a vehicle type that can be priced.

- Fields:
  - `VehicleTypeId` (string, required, canonical lower-case)
  - `DisplayName` (string, required)
  - `PricingFormula` (string, required)
  - Optional: `Description` (string)

The formula defines how to compute the raw price from variables such as `days`, `km`, `baseDayRate`, `baseKmPrice`.

#### Vehicle

- Fields:
  - `RegistrationNumber` (string, required)
  - `VehicleTypeId` (string, required)

#### Rental (RentalContract)

- Identified by `BookingNumber` (string, required, unique)
- Fields:
  - `BookingNumber`
  - `VehicleRegistrationNumber`
  - `CustomerPersonalId`
  - `VehicleTypeId` (captured at checkout to lock pricing type)
  - `CheckoutAt` (timestamp)
  - `CheckoutOdometerKm` (non-negative integer)
  - `ReturnedAt` (timestamp, nullable until returned)
  - `ReturnOdometerKm` (non-negative integer, nullable until returned)
  - Derived:
    - `DistanceKm` (return - checkout)
    - `Days` (see duration rules)
    - `FinalPrice` (decimal, nullable until returned)

Rationale: capturing the type id at checkout avoids future changes to vehicle catalog impacting the rental price type.

### 5.2 Types

- Use `DateTimeOffset` for timestamps to preserve offset/time zone information.
- Use `decimal` for all money calculations.
- Use integers for odometer and distance in kilometers.

---

## 6. Business Rules & Validation

### 6.1 Checkout Validation

Input fields required:

- Booking number
- Vehicle registration number
- Customer personal identification number
- Vehicle type id (or vehicle registration number that resolves to a vehicle type)
- Checkout date/time
- Checkout odometer (km)

Rules:

1. Booking number must be unique (no existing rental with same booking number).
2. A vehicle may have at most one active rental at a time.
3. All fields must be present (non-null/empty strings where applicable).
4. Checkout odometer must be a **non-negative** number.
5. Vehicle must exist in the seeded vehicle catalog at startup (Phase 1 assumption).
   - If you want to allow “unknown vehicles”, explicitly relax this rule; otherwise enforce it.
6. Vehicle type must be resolvable and supported:

  If the checkout request provides a `VehicleTypeId`, it must exist in the runtime registry of loaded vehicle types. If the checkout request omits `VehicleTypeId`, the system resolves it from the vehicle catalog by registration number.

### 6.2 Return Validation

Input fields required:

- Booking number
- Return date/time
- Return odometer (km)

Rules:

1. Booking number must match an existing checkout.
2. Return timestamp must be >= checkout timestamp after normalizing to instants (UTC).
3. Return odometer must be >= checkout odometer.
4. Distance (km) = return odometer - checkout odometer.
5. Days calculation: see section 7.
6. Price calculated using `VehicleTypeId` captured at checkout.

---

## 7. Time Zone Handling & Duration Calculation

### 7.1 Time Zone Rules

From PRD + assumptions:

- A driver can return a vehicle in a different time zone.
- If user inputs a date/time without a time zone, assume it is in the **user local time zone**.
- User time zone is assumed to be the same as system time zone and is fetched at application startup.

Implementation approach:

- **Core** should operate on `DateTimeOffset` and treat values as absolute instants.
- **CLI** should parse inputs:
  - If an ISO-8601 value contains an offset (e.g., `2025-12-13T10:00:00+01:00`), parse to `DateTimeOffset` directly.
  - If the input lacks an offset (e.g., `2025-12-13T10:00:00`), interpret it in the configured user time zone and convert to `DateTimeOffset`.

Note: .NET parsing of “no-offset” timestamps yields `DateTime` or `DateTimeOffset` with unspecified semantics. Be explicit in CLI parsing.

### 7.2 Days Calculation (No Partial Days)

Assumption: minimum rental period is **1 full day**.

Define:

- `duration = ReturnedAtUtc - CheckoutAtUtc` (a `TimeSpan`)
- `days = max(1, ceil(duration.TotalDays))`

Examples:

- 2 hours → 1 day
- 23 hours → 1 day
- 25 hours → 2 days
- 48 hours exactly → 2 days

This matches:

- “No partial days”
- “If returned the same day, charge 1 day”
- “Minimum amount of days a vehicle can be rented is 1”

---

## 8. Pricing & Money Rules

### 8.1 Pricing Parameters

Configuration values (from config in CLI):

- `baseDayRate` (decimal)
- `baseKmPrice` (decimal)

### 8.2 Formulas

The initial vehicle types must be available at runtime. Preferred: define them as data-driven vehicle type definitions (schema/JSON) using the following equivalent formulas:

- `small-car`: `baseDayRate * days`
- `station-wagon`: `(baseDayRate * days * 1.3m) + (baseKmPrice * km)`
- `truck`: `(baseDayRate * days * 1.5m) + (baseKmPrice * km * 1.5m)`

Notes:

- These are raw formulas. Final rounding is still applied in Core (ceiling to nearest integer) after computing the raw price.
- If a future vehicle type cannot be expressed using the formula language, a plugin can be used as an escape hatch (section 12).

### 8.4 Formula Language (Phase 1)

To allow non-developer updates, pricing is driven by formulas stored in the vehicle type definition.

Phase 1 formula requirements:

- Supported operators: `+`, `-`, `*`, `/`, parentheses.
- Supported literals: decimal constants.
- Supported variables (case-insensitive):
  - `days` (int)
  - `km` (int)
  - `baseDayRate` (decimal)
  - `baseKmPrice` (decimal)
- Output: decimal.

Safety requirements:

- No reflection, method calls, property access, or arbitrary code execution.
- Reject formulas that contain disallowed characters/tokens.
- Validate formula at load time and fail fast (do not accept invalid vehicle types).

Implementation guidance:

- Prefer a small dedicated expression parser/evaluator in Core (or Infrastructure) that only supports the above subset.
- Cache compiled/parsed expressions per `VehicleTypeId`.

### 8.3 Rounding

Assumption: final price presentation rounds **up** to nearest integer.

Rule:

- Compute price as `decimal`.
- FinalPrice = `Ceiling(price)` (decimal ceiling to whole currency units).

Clarification:

- Only round once at the end of calculation (avoid rounding intermediate parts).

---

## 9. Core Abstractions (Ports)

### 9.1 Rental Repository

Interface in Core:

- `IRentalRepository`
  - `Task<bool> BookingNumberExistsAsync(string bookingNumber)`
  - `Task<Rental?> GetByBookingNumberAsync(string bookingNumber)`
  - `Task<bool> HasActiveRentalForVehicleAsync(string registrationNumber)`
  - `Task AddAsync(Rental rental)`
  - `Task UpdateAsync(Rental rental)`

Notes:

- Keep methods minimal and aligned with use cases.
- “Active rental” means checkout registered and return not yet registered.

### 9.2 Vehicle Catalog / Vehicle Repository

Interface in Core:

- `IVehicleCatalog`
  - `Task<Vehicle?> GetByRegistrationNumberAsync(string registrationNumber)`
  - Optional: `Task<IReadOnlyList<Vehicle>> ListAllAsync()` (only if needed)

Seed data requirement:

- Vehicles are loaded from JSON at startup into an in-memory catalog.

### 9.3 Pricing Strategy

Interface in Core:

- `IVehiclePricingStrategy`
  - `string VehicleTypeId { get; }`
  - `decimal CalculateRawPrice(PricingParameters parameters, int days, int km)`

Rules:

- `VehicleTypeId` must be globally unique across loaded strategies.
- A `PricingCalculator` (or `PricingService`) selects the implementation by `VehicleTypeId` from an injected collection (`IEnumerable<IVehiclePricingStrategy>`).
- If no strategy exists for a vehicle type id, return an explicit error (do not default silently).

Recommended implementation strategy:

- Provide a built-in, data-driven strategy (or calculator) that reads `VehicleTypeDefinition.PricingFormula` and evaluates it.
- Keep the interface as the integration point so you can still plug in custom strategies later.

#### 9.3.1 Vehicle Type Store (Schema)

Interface in Core:

- `IVehicleTypeStore`
  - `Task<VehicleTypeDefinition?> GetByIdAsync(string vehicleTypeId)`
  - `Task<IReadOnlyList<VehicleTypeDefinition>> ListAllAsync()`

Phase 1 implementation: an in-memory store loaded from JSON at startup (acts as a stand-in for a database schema).

#### 9.3.2 Formula Evaluator

Interface in Core:

- `IPriceFormulaEvaluator`
  - `decimal Evaluate(string formula, PricingParameters parameters, int days, int km)`

Rules:

- Must enforce the formula language and safety constraints from section 8.4.
- Must produce deterministic results.
- Should support caching/compilation to avoid repeated parsing.

---

## 10. Use Cases (Application Services)

### 10.1 Register Checkout

Service in Core: `CheckoutService`

Input DTO (Core):

- `RegisterCheckoutRequest`
  - bookingNumber
  - vehicleRegistrationNumber
  - customerPersonalId
  - vehicleTypeId (optional if catalog resolves it)
  - checkoutAt (DateTimeOffset)
  - checkoutOdometerKm

Behavior:

1. Validate required fields.
1. Ensure booking number unique.
1. Ensure vehicle exists in catalog.
1. Ensure no active rental for the vehicle.
1. Resolve `VehicleTypeId`.

   Prefer request `vehicleTypeId` if provided; otherwise resolve from catalog. Validate a pricing strategy exists for the resolved type id.
1. Create `Rental` with return fields empty.
1. Save via `IRentalRepository.AddAsync`.

Output:

- Recommended: `Result<RegisterCheckoutResponse>`
  - Contains booking number and stored checkout snapshot.

### 10.2 Register Return

Service in Core: `ReturnService`

Input DTO (Core):

- `RegisterReturnRequest`
  - bookingNumber
  - returnedAt (DateTimeOffset)
  - returnOdometerKm

Behavior:

1. Load rental by booking number; error if not found.
2. Validate return timestamp >= checkout timestamp.
3. Validate return odometer >= checkout odometer.
4. Calculate `km` and `days`.
5. Calculate raw price via pricing strategy using `rental.VehicleTypeId`.
6. Apply rounding (ceiling) to get final price.
7. Update rental with return details + final price.
8. Save via `IRentalRepository.UpdateAsync`.

Output:

- Recommended: `Result<RegisterReturnResponse>`
  - booking number, days, km, final price.

---

## 11. Infrastructure Implementation

### 11.1 In-Memory Rental Repository

`InMemoryRentalRepository` should:

- Use thread-safe structures (e.g., `ConcurrentDictionary`) or a simple lock.
- Enforce uniqueness at repository level (defensive), but primary rule enforcement remains in use cases.

Indexes to support required operations efficiently:

- By booking number.
- By vehicle registration number for “active rental check”.

### 11.2 In-Memory Vehicle Catalog

`InMemoryVehicleCatalog` should:

- Load a JSON file at startup and create a lookup by registration number.

JSON file requirements:

- Location: `VehicleRental.CLI` should include it as content (copied to output).
- Format (suggested):

```json
[
  { "registrationNumber": "ABC123", "vehicleTypeId": "small-car" },
  { "registrationNumber": "XYZ999", "vehicleTypeId": "truck" }
]
```

Vehicle type id parsing must be strict and case-insensitive, normalized to a canonical form (e.g., lower-case).

### 11.3 Vehicle Type Store (JSON-backed, In-Memory)

Phase 1 uses a JSON file to represent “database schema” for vehicle types.

Suggested file `vehicle-types.json`:

```json
[
  {
    "vehicleTypeId": "small-car",
    "displayName": "Small Car",
    "pricingFormula": "baseDayRate * days"
  },
  {
    "vehicleTypeId": "station-wagon",
    "displayName": "Station Wagon",
    "pricingFormula": "(baseDayRate * days * 1.3) + (baseKmPrice * km)"
  }
]
```

Parsing requirements:

- `vehicleTypeId` must be unique (case-insensitive) and normalized.
- `pricingFormula` must validate at load time (section 8.4).

---

## 12. CLI Composition & Configuration

### 12.1 Configuration Sources

Use `Microsoft.Extensions.Configuration` with:

- `appsettings.json` (baseDayRate, baseKmPrice, seed file path)
- Environment overrides if desired.

### 12.2 Dependency Injection

Use `Microsoft.Extensions.DependencyInjection` to register:

- Repositories (in-memory)
- Vehicle catalog (in-memory)
- Pricing strategies
- Use case services

Vehicle type discovery (runtime loading):

- The CLI loads vehicle type pricing plugins from a configured folder (e.g., `./plugins`).
- Each plugin assembly is loaded and scanned for `IVehiclePricingStrategy` implementations.
- Discovered strategies are registered into DI as `IEnumerable<IVehiclePricingStrategy>`.

Safety notes (recommended):

- Only load plugins from a trusted path.
- Fail fast on duplicate `VehicleTypeId`.
- Consider strong-name signing / allowlist of assemblies if you expect external plugins.

#### 12.2.0 Preferred Approach: Data-driven Vehicle Types

To allow adding new vehicle types without deploying code:

- The CLI loads `vehicle-types.json` (or later: database table) into an in-memory `IVehicleTypeStore`.
- The pricing calculation uses the `PricingFormula` from `VehicleTypeDefinition` via `IPriceFormulaEvaluator`.

This is simpler than plugins and is the recommended Phase 1 path.

#### 12.2.1 Plugin Contract (Vehicle Type Pricing)

This section defines the contract that external assemblies must follow to contribute new vehicle types at runtime.

##### Core contract

- Plugins must reference `VehicleRental.Core` and implement the Core interface `IVehiclePricingStrategy`.
- Plugins must not reference `VehicleRental.CLI` or `VehicleRental.Infrastructure`.

##### Identity & naming

- `VehicleTypeId` is a stable string identifier (recommended: lower-case kebab-case), for example:
  - `small-car`
  - `truck`
  - `premium-suv`
- `VehicleTypeId` is case-insensitive in input, but must be normalized to a canonical form for lookups (recommendation: store/compare as lower-case).
- `VehicleTypeId` must be unique across all loaded plugins; duplicates must fail startup.

##### Runtime discovery rules

- The application loads plugin assemblies (DLLs) from a configured plugin folder (for example `./plugins`).
- A plugin assembly is considered valid if it contains at least one public, non-abstract class that implements `IVehiclePricingStrategy`.
- The application registers all discovered strategies in DI and uses `VehicleTypeId` for strategy selection.

##### Compatibility rules

- Plugins must target the same runtime (or a compatible one) as the host app. Recommended: `net10.0`.
- Breaking changes to `IVehiclePricingStrategy` are a breaking change to the plugin ecosystem. If you need to evolve the contract later, introduce a versioned interface (e.g., `IVehiclePricingStrategyV2`) rather than modifying the existing one.

##### Error handling contract

- Strategy implementations should validate their inputs (`days >= 1`, `km >= 0`) and either:
  - throw a domain exception used consistently by Core, or
  - return a value and let Core handle validation before calling the strategy.

Recommendation: keep validation in Core use cases and keep strategies pure (assume valid inputs).

##### Packaging

- Each plugin can be its own class library project, producing a DLL that is copied into the host’s plugin folder.
- Vehicles in the seed JSON can reference plugin-provided types simply by setting `vehicleTypeId` to the plugin’s `VehicleTypeId`.

#### 12.2.2 Example Plugin: premium-suv

This example adds a new vehicle type `premium-suv` whose pricing is:

- daily price is `baseDayRate * days * 2.0`
- kilometer price is `baseKmPrice * km * 1.2`
- final rounding still happens in Core (ceiling to nearest integer) after the raw price is returned

Example implementation (in a plugin assembly):

```csharp
using VehicleRental.Core.Pricing;

namespace VehicleRental.Plugins.PremiumSuv;

public sealed class PremiumSuvPricingStrategy : IVehiclePricingStrategy
{
    public string VehicleTypeId => "premium-suv";

    public decimal CalculateRawPrice(PricingParameters parameters, int days, int km)
    {
        // Core should ensure days >= 1 and km >= 0.
        var dayComponent = parameters.BaseDayRate * days * 2.0m;
        var kmComponent = parameters.BaseKmPrice * km * 1.2m;
        return dayComponent + kmComponent;
    }
}
```

Seed JSON example using this plugin:

```json
[
  { "registrationNumber": "SUV777", "vehicleTypeId": "premium-suv" }
]
```

#### 12.2.3 CLI Plugin Loader (Implementation)

This section specifies the required CLI implementation to load pricing plugins at runtime.

##### Configuration

Add these configuration keys (e.g., in `appsettings.json`):

- `Plugins:Path` (string): plugin folder path, relative to the CLI working directory or absolute.
- `Plugins:Enabled` (bool): allows disabling plugin loading (useful for tests / debugging).

Recommended default:

- `Plugins:Enabled = true`
- `Plugins:Path = "plugins"`

##### Loading algorithm

At CLI startup (before building the service provider), perform:

1. Resolve `pluginsPath` to a full path.
2. If plugins are disabled, skip loading.
3. If the folder does not exist, treat as empty (or fail fast; choose one policy and keep it consistent).
4. Enumerate `*.dll` files in the folder (non-recursive is sufficient for Phase 1).
5. For each DLL:
   - Load the assembly.
   - Find all public, non-abstract types implementing `IVehiclePricingStrategy`.
6. Instantiate discovered strategies (constructor must be parameterless for Phase 1).
7. Validate:
   - `VehicleTypeId` is non-empty.
   - `VehicleTypeId` is unique across all loaded strategies (case-insensitive).
8. Register each strategy instance into DI as `IVehiclePricingStrategy`.
9. Log the loaded vehicle types (or at least count) for diagnostics.

##### Assembly load mechanism

Use `AssemblyLoadContext` so dependency resolution works predictably:

- Use a dedicated load context with an `AssemblyDependencyResolver` rooted at the plugin DLL path.
- Load plugin dependencies from the same folder as the plugin.

##### Example (host-side) implementation

This example shows one acceptable approach (exact file/namespace can differ):

```csharp
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VehicleRental.Core.Pricing;

internal static class PluginLoader
{
  public static IReadOnlyList<IVehiclePricingStrategy> LoadStrategies(IConfiguration configuration)
  {
    var enabled = configuration.GetValue("Plugins:Enabled", true);
    if (!enabled)
    {
      return Array.Empty<IVehiclePricingStrategy>();
    }

    var pluginsPath = configuration.GetValue<string>("Plugins:Path") ?? "plugins";
    var fullPath = Path.GetFullPath(pluginsPath);
    if (!Directory.Exists(fullPath))
    {
      return Array.Empty<IVehiclePricingStrategy>();
    }

    var result = new List<IVehiclePricingStrategy>();

    foreach (var dllPath in Directory.EnumerateFiles(fullPath, "*.dll"))
    {
      var loadContext = new PluginLoadContext(dllPath);
      var assembly = loadContext.LoadFromAssemblyPath(dllPath);

      var types = assembly
        .GetExportedTypes()
        .Where(t => !t.IsAbstract && typeof(IVehiclePricingStrategy).IsAssignableFrom(t));

      foreach (var type in types)
      {
        if (Activator.CreateInstance(type) is IVehiclePricingStrategy strategy)
        {
          result.Add(strategy);
        }
      }
    }

    // Enforce uniqueness (case-insensitive)
    var duplicates = result
      .GroupBy(s => (s.VehicleTypeId ?? string.Empty).Trim().ToLowerInvariant())
      .Where(g => g.Key.Length == 0 || g.Count() > 1)
      .ToList();

    if (duplicates.Count > 0)
    {
      throw new InvalidOperationException("Invalid or duplicate VehicleTypeId detected in plugins.");
    }

    return result;
  }

  public static IServiceCollection AddPricingStrategiesFromPlugins(this IServiceCollection services, IConfiguration configuration)
  {
    foreach (var strategy in LoadStrategies(configuration))
    {
      services.AddSingleton(typeof(IVehiclePricingStrategy), strategy);
    }

    return services;
  }

  private sealed class PluginLoadContext : AssemblyLoadContext
  {
    private readonly AssemblyDependencyResolver _resolver;

    public PluginLoadContext(string pluginPath)
    {
      _resolver = new AssemblyDependencyResolver(pluginPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
      var path = _resolver.ResolveAssemblyToPath(assemblyName);
      return path is null ? null : LoadFromAssemblyPath(path);
    }
  }
}
```

Notes:

- Phase 1 assumes parameterless constructors for strategies. If you later want DI inside plugins, you’ll need a richer activation model.
- Keep plugin loading in CLI; Core should remain unaware of assemblies and reflection.

#### 12.2.4 Vehicle Types Refresh (Event / Reload)

To support new vehicle types without restarting the app, the system needs a refresh signal.

Phase 1 options (choose one):

1. **File change reload (simplest):** The CLI watches `vehicle-types.json` with `FileSystemWatcher`. On change, it reloads the file, validates it, and swaps the in-memory store (atomic replace).

1. **Explicit reload command (simplest to implement & test):** The CLI exposes a command such as `reload-vehicle-types`. Running it triggers a reload of the vehicle type store.

1. **Mock event (for future message bus):** Define an internal event `VehicleTypesUpdated`. CLI subscribes and triggers reload when the event is received.

Recommendation for Phase 1: start with (2), optionally add (1) once stable.

##### Optional: Mock CLI that emits a VehicleTypesUpdated event

Separate tool to simulate “database updated” + “event fired”, implement a second console app  `VehicleRental.DevTools.CLI` that:

- Updates the backing store (Phase 1: writes `vehicle-types.json`, later: writes to the DB).
- Emits a signal that the main app can detect.

Simplest signalling mechanism without introducing a message broker:

- The updater writes a small marker file (e.g., `vehicle-types.updated`) or touches the JSON file.
- The main CLI either watches for file changes (option 1) or checks the marker when executing operations.

This provides the event-driven behavior while staying within Phase 1 “no infrastructure” constraints.

### 12.3 Minimal Console Behavior (Phase 1)

Keep CLI minimal and deterministic (best for early development):

- Load config + seed vehicles.
- Execute a scripted sample flow OR accept simple args.

The CLI is not a product requirement; it is a runnable entrypoint for wiring and demos.

---

## 13. Testing Specification (xUnit)

### 13.1 Pricing Strategy Unit Tests

For each vehicle type:

- days=1, km=0
- days=2, km=100
- verify raw price and final rounded price.

### 13.2 Duration Calculation Tests

- 2 hours → 1 day
- 23 hours → 1 day
- 25 hours → 2 days
- Cross-time-zone scenarios using `DateTimeOffset` with different offsets.

### 13.3 Validation Tests

Checkout:

- missing fields
- duplicate booking number
- negative odometer
- vehicle not found in catalog
- vehicle already in active rental

Return:

- booking not found
- return < checkout (after UTC normalization)
- return odometer < checkout odometer

### 13.4 End-to-End Use Case Tests

- Checkout → Return → price computed for each vehicle type.
- Zero km rental.

---

## 14. Implementation Order (Recommended)

Implement in thin vertical slices to keep tests green throughout.

### Milestone 1 — Core primitives

1. Define `PricingParameters` and vehicle type identifier conventions.
2. Define `Vehicle` and `Rental` model.
3. Implement duration calculation helper.
4. Implement rounding rule helper.

### Milestone 2 — Pricing engine

1. Define `IVehiclePricingStrategy`.
2. Implement the initial 3 strategies as first-party plugins (`small-car`, `station-wagon`, `truck`).
3. Implement `PricingCalculator` that selects strategy from `IEnumerable<IVehiclePricingStrategy>` by `VehicleTypeId`.
4. Unit test pricing + rounding.

### Milestone 3 — Ports & in-memory infrastructure

1. Define `IRentalRepository` and `IVehicleCatalog` interfaces (Core).
2. Implement `InMemoryVehicleCatalog` + JSON loader (Infrastructure).
3. Implement `InMemoryRentalRepository` (Infrastructure).
4. Add tests for repository behaviors only if needed (keep most tests at use-case level).

### Milestone 4 — Use cases

1. Implement `CheckoutService` with validations.
2. Implement `ReturnService` with validations + calculations.
3. Add full suite of validation tests.
4. Add end-to-end flow tests.

### Milestone 5 — CLI wiring

1. Add `appsettings.json` and seed vehicles JSON file.
2. Wire DI in CLI.
3. Run a deterministic demo flow or simple args.

### Milestone 6 — Hardening

1. Review error handling consistency (Result vs exceptions).
2. Add test coverage for edge cases and large values.
3. Ensure no Core dependency on Infrastructure.

---

## 15. Definition of Done (Phase 1)

- All acceptance criteria in the PRD are met.
- Unit tests pass and cover pricing/rounding, duration/time zone scenarios, checkout/return validations, and end-to-end flows.
- Seed vehicles are loaded from JSON at startup.
- Architecture boundaries are respected (Core independent).
