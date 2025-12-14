# Changes Required to Existing Documentation

**For**: Client-Server Architecture Extension  
**Date**: December 14, 2025

This document outlines specific changes needed to `implementation-spec.md` and `implementation-plan.md` to support the client-server architecture.

---

## 1. Changes to implementation-spec.md

### Section 3: Solution Structure

**ADD** after existing projects:

```markdown
- `VehicleRental.Server` (ASP.NET Core Web API)
  - REST API for vehicle type management
  - Client authentication and registry
  - SignalR hub for push notifications
  - Vehicle type storage (authoritative)

- `VehicleRental.Shared` (class library)
  - Shared DTOs for client-server communication
  - API contracts and constants
  - No dependencies on other projects
```

**UPDATE** dependency direction:

```markdown
Dependency direction:
- `Core` has no reference to other projects
- `Infrastructure` references `Core`
- `Server` references `Core` + `Shared`
- `CLI` references `Core` + `Infrastructure` + `Shared`
- `Tests` reference `Core` (and optionally `Infrastructure` for integration tests)
```

### Section 4: Architecture Overview

**ADD** new subsection 4.3:

```markdown
### 4.3 Client-Server Pattern

- **Server** acts as configuration authority for vehicle types and pricing
- **Clients** (CLI instances) manage their own vehicle fleets and rentals
- **Communication** via REST API (initial sync) + SignalR (push updates)
- **Resilience** via cached configurations when server unavailable
- **Security** via client authentication (API keys initially, JWT later)
```

### Section 11: Infrastructure Implementation

**ADD** new subsection 11.4:

```markdown
### 11.4 Remote Vehicle Type Store (Client-side)

`RemoteVehicleTypeStore` should:

- Connect to server on startup and authenticate
- Download vehicle type definitions via REST API
- Cache definitions locally (both in-memory and on disk)
- Subscribe to SignalR hub for real-time updates
- Reload definitions when server pushes updates
- Fall back to cached definitions when server unavailable

Implementation notes:
- Use `HttpClient` with authentication headers
- Use `IMemoryCache` for fast lookups
- Use JSON file (`cached-vehicle-types.json`) for persistence
- Validate formulas before applying updates
```

**ADD** new section 13:

```markdown
## 13. Client-Server Communication

### 13.1 Authentication

Clients authenticate using:
- `clientId`: Unique identifier for rental location
- `apiKey`: Shared secret for initial authentication
- Bearer token: JWT issued by server after authentication

### 13.2 REST API Endpoints

- `POST /api/clients/authenticate` - Client authentication
- `GET /api/vehicle-types` - Fetch all vehicle type definitions
- `GET /api/vehicle-types/{id}` - Fetch single vehicle type
- `POST /api/vehicle-types` - Create new type (admin only)
- `PUT /api/vehicle-types/{id}` - Update type (admin only)
- `DELETE /api/vehicle-types/{id}` - Delete type (admin only)

### 13.3 SignalR Hub

- Hub name: `ConfigurationHub`
- Client subscribes: `SubscribeToUpdates(clientId)`
- Server pushes: `VehicleTypesUpdated(notification)`

### 13.4 Failure Handling

- Server unavailable at startup: Use cached configuration + log warning
- Server unavailable during operation: Continue with current config
- Invalid update received: Reject update, keep current config, log error
- Network timeout: Retry with exponential backoff
```

---

## 2. Changes to implementation-plan.md

### Section 0: Guardrails

**UPDATE** to add server project:

```markdown
- [x] Confirm dependency direction is enforced: 
  - Core → (none)
  - Infrastructure → Core
  - Server → Core + Shared
  - CLI → Core + Infrastructure + Shared
  - Tests → Core (optionally Infrastructure)
```

### Section 3: Infrastructure

**ADD** new subsection 3.4:

```markdown
### 3.4 Remote Vehicle Type Store (Client Implementation)

- [ ] Create `RemoteVehicleTypeStore` that implements `IVehicleTypeStore`
- [ ] Implement HTTP client for REST API calls
  - [ ] Add authentication headers (Bearer token)
  - [ ] Add timeout and retry logic
- [ ] Implement caching strategy
  - [ ] In-memory cache (IMemoryCache) for fast lookups
  - [ ] Disk cache (cached-vehicle-types.json) for persistence
- [ ] Implement SignalR client connection
  - [ ] Connect to ConfigurationHub on startup
  - [ ] Subscribe to VehicleTypesUpdated events
  - [ ] Handle reconnection on disconnect
- [ ] Implement validation of received vehicle types
  - [ ] Validate formulas before caching
  - [ ] Reject invalid updates
- [ ] Implement fallback behavior
  - [ ] Load from disk cache if server unavailable
  - [ ] Log warnings for degraded mode

Exit criteria:
- [ ] Client can authenticate and sync vehicle types from server
- [ ] Client receives and applies push updates
- [ ] Client operates correctly when server unavailable
- [ ] All edge cases tested (network errors, invalid data, etc.)
```

### ADD New Section 9: Server Implementation

```markdown
## 9. Server: REST API + SignalR Hub

Project: `VehicleRental.Server` (ASP.NET Core Web API)

### 9.1 Project Setup

- [ ] Create ASP.NET Core Web API project targeting .NET 10
- [ ] Add references to Core and Shared projects
- [ ] Configure HTTPS and CORS
- [ ] Add Swagger/OpenAPI for API documentation

### 9.2 Client Authentication

- [ ] Create `ClientAuthenticationService`
  - [ ] Load accepted clients from appsettings.json
  - [ ] Validate clientId + apiKey combinations
  - [ ] Issue JWT tokens with appropriate claims
- [ ] Create `ClientsController` with `/api/clients/authenticate` endpoint
- [ ] Add authentication middleware to API pipeline
- [ ] Configure JWT authentication settings

### 9.3 Vehicle Type Management API

- [ ] Create server-side `InMemoryVehicleTypeStore` (authoritative)
  - [ ] Support CRUD operations (Add, Update, Delete, Get, List)
  - [ ] Thread-safe with ConcurrentDictionary
  - [ ] Seed with initial 3 vehicle types
- [ ] Create `VehicleTypeManagementService`
  - [ ] Validate formulas before accepting
  - [ ] Enforce unique vehicleTypeId (case-insensitive)
  - [ ] Notify connected clients on changes
- [ ] Create `VehicleTypesController`
  - [ ] GET /api/vehicle-types - List all (requires auth)
  - [ ] GET /api/vehicle-types/{id} - Get single (requires auth)
  - [ ] POST /api/vehicle-types - Create (requires admin)
  - [ ] PUT /api/vehicle-types/{id} - Update (requires admin)
  - [ ] DELETE /api/vehicle-types/{id} - Delete (requires admin)

### 9.4 SignalR Push Notifications

- [ ] Add SignalR to server (Microsoft.AspNetCore.SignalR)
- [ ] Create `ConfigurationHub`
  - [ ] Implement SubscribeToUpdates method
  - [ ] Use groups for broadcasting
- [ ] Integrate hub with VehicleTypeManagementService
  - [ ] Trigger push notification on vehicle type changes
  - [ ] Include change details in notification
- [ ] Configure SignalR in Program.cs

### 9.5 Server Configuration

- [ ] Define accepted clients in appsettings.json
  ```json
  {
    "AcceptedClients": [
      {
        "clientId": "location-stockholm-001",
        "clientName": "Stockholm",
        "apiKey": "key1",
        "enabled": true
      }
    ],
    "Jwt": {
      "secretKey": "your-secret-key-min-32-chars",
      "issuer": "VehicleRental.Server",
      "audience": "VehicleRental.Clients",
      "expirationMinutes": 60
    }
  }
  ```

Exit criteria:
- [ ] Server starts and loads initial vehicle types
- [ ] Authentication endpoint works (returns JWT for valid clients)
- [ ] Vehicle types API endpoints work (GET/POST/PUT/DELETE)
- [ ] SignalR hub accepts connections and broadcasts updates
- [ ] Swagger UI documents all endpoints
```

### ADD New Section 10: Client-Server Integration

```markdown
## 10. Client-Server Integration

### 10.1 Update CLI Configuration

- [ ] Add server connection settings to appsettings.json
  ```json
  {
    "ServerConnection": {
      "serverUrl": "https://localhost:5001",
      "clientId": "location-stockholm-001",
      "apiKey": "key1",
      "enablePushUpdates": true,
      "connectionTimeout": 30,
      "retryAttempts": 3
    }
  }
  ```
- [ ] Add cached vehicle types file path

### 10.2 Update CLI Dependency Injection

- [ ] Add `RemoteVehicleTypeStore` registration
- [ ] Add HttpClient with authentication
- [ ] Add SignalR client connection
- [ ] Add feature flag to toggle local vs remote mode

### 10.3 Update CLI Startup Flow

- [ ] Authenticate with server on startup
- [ ] Sync vehicle types from server
- [ ] Save to local cache
- [ ] Subscribe to SignalR updates
- [ ] Handle authentication failures gracefully
- [ ] Continue with cached data if server unavailable

### 10.4 Implement Real-time Updates

- [ ] Listen for VehicleTypesUpdated SignalR events
- [ ] Reload vehicle types when notification received
- [ ] Log update details (what changed, when)
- [ ] Handle reload failures (keep old configuration)

Exit criteria:
- [ ] CLI connects to server and syncs vehicle types
- [ ] CLI receives real-time updates when types change
- [ ] CLI operates with cached data when server unavailable
- [ ] All integration scenarios tested
```

### UPDATE Section 8: Definition of Done

**ADD** to subsection 8.1 Testing:

```markdown
- [ ] Client-server authentication works
- [ ] Vehicle type sync works (initial + push updates)
- [ ] Price freezing works (active rentals unaffected by updates)
- [ ] Fallback to cached data works when server unavailable
- [ ] Multiple clients can connect simultaneously
- [ ] Server handles concurrent vehicle type modifications safely
```

**ADD** to subsection 8.2 Data & Operations:

```markdown
- [ ] Server stores vehicle types and serves them to clients
- [ ] Clients authenticate successfully
- [ ] Clients sync vehicle types on startup
- [ ] Server pushes updates to all connected clients
- [ ] Updates are applied without restarting clients
- [ ] Active rentals preserve their pricing when config changes
```

---

## 3. Testing Strategy Updates

### New Test Categories

**Server Tests:**
```markdown
VehicleRental.Server.Tests/
├── Authentication/
│   └── ClientAuthenticationServiceTests.cs
├── VehicleTypes/
│   ├── VehicleTypeManagementServiceTests.cs
│   └── VehicleTypesControllerTests.cs
└── Hubs/
    └── ConfigurationHubTests.cs
```

**Integration Tests:**
```markdown
VehicleRental.Integration.Tests/
├── ClientServerAuthenticationTests.cs
├── VehicleTypeSyncTests.cs
├── PushNotificationTests.cs
├── FallbackBehaviorTests.cs
└── PriceFreezingTests.cs
```

**Test Scenarios:**

1. **Authentication**:
   - Valid client authenticates successfully
   - Invalid clientId rejected
   - Invalid apiKey rejected
   - Disabled client rejected
   - JWT token expires and must be refreshed

2. **Vehicle Type Sync**:
   - Client downloads types on first startup
   - Client uses cached types on subsequent startups
   - Client reloads when server pushes update
   - Client validates formulas before accepting
   - Client rejects invalid updates

3. **Price Freezing**:
   - Create rental with small-car at 100/day
   - Update small-car pricing to 150/day on server
   - Verify push notification received by client
   - Complete old rental → verify still uses 100/day
   - Create new rental → verify uses 150/day

4. **Resilience**:
   - Server unavailable at client startup → uses cache
   - Server goes down during operation → continues working
   - Server comes back up → client reconnects
   - Network timeout → client retries with backoff
   - Invalid data received → client rejects and logs

---

## 4. Key Implementation Notes

### Price Freezing (Already Implemented!)

The current code **already supports** price freezing because:

1. `Rental` captures `VehicleTypeId` at checkout
2. `ReturnService` uses the captured `VehicleTypeId` to calculate price
3. Even if that type's formula changes, the rental still references it by ID

**No code changes needed** for price freezing - it's a natural consequence of the existing design.

### What Actually Needs Changes

1. **Add Server project** - new code
2. **Add Shared project** - new DTOs
3. **Add RemoteVehicleTypeStore** - new implementation of existing interface
4. **Update CLI startup** - add server connection logic
5. **Add SignalR client** - new dependency
6. **Add caching logic** - new fallback behavior

### Migration Strategy

You can implement this **incrementally**:

1. ✅ Phase 0: Keep current local JSON system working
2. 🆕 Phase 1: Add server + REST API (clients don't use it yet)
3. 🆕 Phase 2: Add RemoteVehicleTypeStore (feature-flagged)
4. 🆕 Phase 3: Add SignalR push (optional enhancement)
5. 🆕 Phase 4: Enable for all clients

---

## 5. Summary

Your proposed architecture is **excellent** and the changes needed are:

### To implementation-spec.md:
- Add Server and Shared projects to solution structure
- Add client-server communication section
- Document REST API and SignalR protocols
- Document authentication and security

### To implementation-plan.md:
- Add Section 9: Server Implementation (REST API + SignalR)
- Add Section 10: Client-Server Integration
- Add Section 3.4: Remote Vehicle Type Store implementation
- Update Section 8: Definition of Done with client-server criteria

### Key Insight:
**Price freezing already works** - no changes needed to Core! The `VehicleTypeId` captured at checkout naturally preserves pricing even when formulas change.

The clean architecture makes this extension straightforward - Core remains unchanged, we just add new implementations of existing abstractions (`IVehicleTypeStore`).
