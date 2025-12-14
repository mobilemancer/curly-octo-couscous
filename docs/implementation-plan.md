# Implementation Plan (Phase 1) — Vehicle Rental Management System (.NET 10)

This is an execution checklist derived from `docs/implementation-spec.md`. Items are ordered to keep the build/test loop tight and avoid cross-layer churn.

---

## 0. Guardrails (Do First)

- [x] Confirm dependency direction is enforced: Core → (none), Infrastructure → Core, CLI → Core+Infrastructure, Tests → Core (optionally Infrastructure)
- [x] Define folder structure: Core/{Domain,Application,Ports}, Infrastructure/{Repositories,Stores,JsonLoaders}, CLI/{Commands,Configuration}
- [ ] Decide one error-handling style and apply consistently across Core (recommended: Result-style errors)
- [ ] Define error/Result type structure for consistent validation messages across all use cases
- [x] Decide refresh approach for vehicle types (recommended Phase 1: explicit command `reload-vehicle-types`; optional: file watcher)
- [x] Commit to test-first approach: write failing test → implement feature → test passes (TDD)

---

## 1. Core: Foundations (Domain + Contracts)

### 1.1 Domain types

- [x] Create `VehicleTypeDefinition` model (`VehicleTypeId`, `DisplayName`, `PricingFormula`, `Description?`)
- [x] Create `Vehicle` model (`RegistrationNumber`, `VehicleTypeId`)
- [x] Create `Rental` model (checkout/return fields + derived fields concept)
- [x] Ensure timestamps use `DateTimeOffset` and money uses `decimal`

### 1.2 Core configuration types

- [x] Create `PricingParameters` (`BaseDayRate`, `BaseKmPrice`)
- [x] Define canonicalization rules for `VehicleTypeId` (trim + lower-case; reject empty)

### 1.3 Ports (interfaces)

- [x] Define `IRentalRepository` (exists/get/add/update + active-rental check)
- [x] Define `IVehicleCatalog` (lookup by registration number)
- [x] Define `IVehicleTypeStore` (get by id, list all)
- [x] Define `IPriceFormulaEvaluator` (evaluate formula with variables)
- [x] Use `Microsoft.Extensions.Logging.ILogger<T>` for logging (inject into use cases and stores as needed)

### 1.4 Cross-cutting helpers

- [x] Implement duration/day calculation helper: `days = max(1, ceil((returnUtc - checkoutUtc).TotalDays))`
- [x] Implement distance helper: `km = returnOdometer - checkoutOdometer`
- [x] Implement rounding helper: final price is `Ceiling(rawPrice)` to whole currency units

- Exit criteria:

- [x] Core compiles
- [x] Unit tests exist for day calculation and rounding (happy path + edge cases)

---

## 2. Core: Pricing Engine (Data-driven First)

### 2.1 Formula language and safety

- [x] Choose formula evaluator approach: custom recursive descent parser (full control) vs. sandboxed library (NCalc/DynamicExpresso with restricted mode)
- [x] Implement `IPriceFormulaEvaluator` supporting only: `+ - * / ( )`, decimal literals, variables: `days`, `km`, `baseDayRate`, `baseKmPrice`
- [x] Add strict validation at load/evaluate time: reject disallowed characters/tokens, enforce max length (e.g., 500 chars)
- [ ] Add caching of parsed/compiled formulas by `VehicleTypeId` (performance + determinism)
- [x] Add formula security tests: injection attempts (semicolons, method calls), divide-by-zero, deeply nested parentheses (stack overflow protection)

### 2.2 Pricing selection

- [x] Define pricing selection API (recommended): `PricingCalculator.Calculate(vehicleTypeId, parameters, days, km)`
- [x] Implement selection using `IVehicleTypeStore` + `IPriceFormulaEvaluator`
  - [x] Missing type id → explicit error
  - [x] Invalid formula → explicit error

### 2.3 Pricing tests

- [x] Add test vectors for the three initial types:
  - [x] `small-car`: `baseDayRate * days`
  - [x] `station-wagon`: `(baseDayRate * days * 1.3) + (baseKmPrice * km)`
  - [x] `truck`: `(baseDayRate * days * 1.5) + (baseKmPrice * km * 1.5)`
- [ ] Verify rounding is applied only once at the end (ceil)

- Exit criteria:

- [x] Pricing formula evaluation is deterministic and safe
- [x] All pricing tests pass
- [x] Security tests confirm formulas cannot execute arbitrary code

---

## 2.5. Core: Time Zone Handling

### 2.5.1 CLI timestamp parsing

- [x] Implement timestamp parser in CLI that detects offset presence in input strings
- [x] Apply system local timezone to offset-less timestamps (e.g., `2025-12-13T10:00:00` → `DateTimeOffset` with local offset)
- [x] Parse ISO-8601 timestamps with explicit offsets directly (e.g., `2025-12-13T10:00:00+01:00`)
- [ ] Make sure the user can enter very relaxed times (eg 10:00, 22, 23:42)

### 2.5.2 Time zone tests

- [x] Test: same absolute instant expressed in different time zones produces same rental days
- [x] Test: parsing offset-less timestamp applies system timezone
- [x] Test: invalid offset values are rejected with clear error
- [x] Test: DST transitions (if applicable to system timezone)
- [x] Test: Leap year tests, combined with renting and turning in from different timezones

- Exit criteria:

- [x] All timestamp inputs are normalized to `DateTimeOffset` before reaching Core
- [x] Core always operates on UTC instants for duration calculations

---

## 3. Infrastructure: In-Memory Stores + JSON Loading

### 3.1 JSON schemas and seed data

- [x] Define `vehicles.json` format: `[ { registrationNumber, vehicleTypeId } ]`
- [x] Define `vehicle-types.json` format: `[ { vehicleTypeId, displayName, pricingFormula, description? } ]`
- [x] Create initial `vehicle-types.json` with the three required types:
  - [x] `small-car`: formula `baseDayRate * days`
  - [x] `station-wagon`: formula `(baseDayRate * days * 1.3) + (baseKmPrice * km)`
  - [x] `truck`: formula `(baseDayRate * days * 1.5) + (baseKmPrice * km * 1.5)`
- [x] Create initial `vehicles.json` with at least 15 vehicles (one per type) for testing

### 3.2 Implement stores

- [x] Implement `InMemoryVehicleCatalog` loading `vehicles.json` (lookup by registration number)
- [x] Implement `InMemoryVehicleTypeStore` loading `vehicle-types.json`
  - [x] Enforce unique `vehicleTypeId` (case-insensitive)
  - [x] Validate formula at load time (fail fast)
- [x] Implement `InMemoryRentalRepository`
  - [x] Unique booking number enforcement (defensive)
  - [x] "Active rental per vehicle" check

---

## 4. Core: Use Cases (Checkout + Return)

### 4.1 DTOs

- [x] Create `RegisterCheckoutRequest`
- [x] Create `RegisterReturnRequest`
- [x] Create `RegisterCheckoutResponse` and `RegisterReturnResponse` (or equivalent)

### 4.2 Checkout service

- [x] Implement `CheckoutService`:
  - [x] Validate required fields (non-empty strings, non-negative odometer)
  - [x] Validate booking number uniqueness
  - [x] Validate vehicle exists in `IVehicleCatalog`
  - [x] Validate vehicle not already in active rental
  - [x] Resolve `VehicleTypeId` (prefer request value; otherwise from vehicle catalog)
  - [x] Validate vehicle type exists in `IVehicleTypeStore`
  - [x] Persist rental

### 4.3 Return service

- [x] Implement `ReturnService`:
  - [x] Load rental by booking number
  - [x] Validate return timestamp >= checkout timestamp (UTC instants)
  - [x] Validate return odometer >= checkout odometer
  - [x] Compute `days` and `km`
  - [x] Calculate raw price via `PricingCalculator`
  - [x] Apply final rounding (ceiling)
  - [x] Persist updated rental

### 4.4 Use case tests

- [x] Validation tests (checkout): missing fields, negative odometer, duplicate booking number, missing vehicle, active rental exists
- [x] Validation tests (return): booking not found, return < checkout, return odometer < checkout
- [x] End-to-end tests (in-memory): checkout → return → expected days/km/price per initial type
- [x] Time zone test: checkout and return in different offsets but correct UTC ordering
- [x] Zero km rental test

- Exit criteria:

- [x] All use-case tests pass
- [x] No Core dependency on Infrastructure/CLI

---

## 5. CLI: Wiring + Commands

### 5.1 Configuration

- [x] Add `appsettings.json` with `baseDayRate`, `baseKmPrice`, and paths to `vehicles.json` + `vehicle-types.json`

### 5.2 DI composition

- [x] Register Infrastructure implementations for:
  - [x] `IVehicleCatalog`
  - [x] `IVehicleTypeStore`
  - [x] `IRentalRepository`
- [x] Register Core pricing + formula evaluator
- [x] Register Core use case services
- [x] Configure logging using `Microsoft.Extensions.Logging` (AddConsole for Phase 1)

### 5.3 Minimal commands (Phase 1)

- [x] Implement `checkout` command (inputs align to `RegisterCheckoutRequest`)
- [x] Implement `return` command (inputs align to `RegisterReturnRequest`)
- [ ] Implement `reload-vehicle-types` command with atomic validation:
  - [ ] Fully validate new `vehicle-types.json` before swapping
  - [ ] Rollback to old types if new file is invalid (keep previous store intact)
  - [ ] Log reload success/failure with diagnostic details
- [x] Add clear console output (booking number, days, km, final price)

### 5.4 Optional refresh mechanisms

- [ ] (Optional) Add `FileSystemWatcher` for `vehicle-types.json` and swap store atomically
- [ ] (Optional) Test concurrent reload + active pricing calculation (no race conditions or data corruption)

- Exit criteria:

- [x] Running CLI supports checkout/return with seeded vehicles and types
- [ ] Reload command updates vehicle types without restart

---

## 6. Dev Tooling: Vehicle Type Updater CLI (Optional)

Project name: `VehicleRental.DevTools.CLI`

- [ ] Create command `add-vehicle-type` that appends/updates an entry in `vehicle-types.json`
  - [ ] Validate formula before writing (prevent file corruption)
  - [ ] Ensure unique `vehicleTypeId` (case-insensitive check)
- [ ] Create command `add-vehicle` that appends/updates an entry in `vehicles.json`
  - [ ] Validate `vehicleTypeId` exists in `vehicle-types.json`
  - [ ] Ensure unique `registrationNumber`
- [ ] Emit a refresh signal:
  - [ ] Simplest: touch `vehicle-types.json` (works with file watcher)
  - [ ] Alternative: write marker file `vehicle-types.updated` (CLI can poll or watch)

- Exit criteria:

- [ ] A developer can add a new type (e.g., `premium-suv`) without rebuilding the main app
- [ ] Main CLI can reload and price a vehicle using the new type

---

## 7. Plugin Escape Hatch (Only If Needed)

Only implement this if you have pricing rules that cannot be expressed in the formula language.

- [ ] Keep plugin interface `IVehiclePricingStrategy` as an optional extension point
- [ ] Implement CLI plugin loader (scan `./plugins`, load DLLs, validate unique type ids, register strategies)
- [ ] Define precedence rules if both data-driven and plugin exist for same `vehicleTypeId` (recommended: plugin wins, but forbid duplicates in Phase 1)

---

## 8. Definition of Done (Phase 1)

### 8.1 Testing

- [ ] All acceptance criteria from the PRD are met
- [ ] Tests pass: pricing, formula safety, duration/time zones, checkout/return validations, end-to-end flows
- [ ] Run full end-to-end smoke test: CLI start → checkout vehicle → return vehicle → verify exact calculated price matches expected

### 8.2 Data & Operations

- [ ] Seeded vehicles and vehicle types load successfully
- [ ] Adding a new `vehicleTypeId` in `vehicle-types.json` and reloading makes it available immediately
- [ ] Invalid `vehicle-types.json` during reload preserves old types (rollback works)

### 8.3 Documentation

- [ ] Update README with:
  - [ ] Architecture overview (layers, dependency direction)
  - [ ] Quick start guide (build, run, example checkout/return)
  - [ ] Formula language reference (operators, variables, examples)
- [ ] Verify all public APIs in Core have XML doc comments
- [ ] Add inline comments for complex logic (especially formula evaluator and time zone handling)
