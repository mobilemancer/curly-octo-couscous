# Client-Server Architecture Extension

**Status**: Proposed  
**Date**: December 14, 2025

This document extends the Vehicle Rental Management System with a client-server architecture where pricing and vehicle type configuration is centrally managed.

---

## 1. Architecture Overview

### 1.1 Goals

- **Centralized Configuration**: Server holds authoritative vehicle type definitions and pricing formulas
- **Distributed Fleet Management**: Multiple clients (rental locations) manage their own vehicle fleets
- **Dynamic Updates**: Server can push vehicle type updates to all connected clients
- **Price Stability**: Active rentals preserve their pricing even when configuration changes
- **Client Authentication**: Only authorized clients can connect and receive configuration

### 1.2 Responsibilities

#### Server (`VehicleRental.Server`)
- Store and manage vehicle type definitions (configuration authority)
- Maintain registry of accepted clients
- Authenticate client connections
- Push configuration updates to connected clients
- Provide REST API for vehicle type CRUD operations

#### Client (`VehicleRental.CLI`)
- Manage local vehicle fleet (vehicles.json remains client-side)
- Maintain active rental state (in-memory or persistent)
- Connect to server on startup and authenticate
- Receive and apply vehicle type updates from server
- Perform checkout/return operations using local fleet + server-provided pricing

---

## 2. Solution Structure Changes

### 2.1 New Projects

Add to existing solution:

```
VehicleRental.Server (ASP.NET Core Web API, .NET 10)
├── Controllers/
│   ├── VehicleTypesController.cs      # REST API for vehicle types
│   └── ClientsController.cs            # Client registration endpoints
├── Hubs/
│   └── ConfigurationHub.cs             # SignalR hub for push updates
├── Services/
│   ├── ClientAuthenticationService.cs
│   └── VehicleTypeManagementService.cs
├── Models/
│   ├── ClientRegistration.cs
│   └── VehicleTypeUpdateNotification.cs
└── Program.cs                          # Server startup and DI

VehicleRental.Shared (class library, .NET 10)
├── Contracts/                           # Shared DTOs for client-server communication
│   ├── VehicleTypeDto.cs
│   ├── ClientAuthRequest.cs
│   └── ConfigurationUpdateDto.cs
└── Constants/
    └── ApiEndpoints.cs
```

### 2.2 Updated Project Dependencies

```
Core (no changes - remains independent)
  ↑
  ├── Infrastructure (no changes)
  ├── Server → Core
  ├── Shared (no Core dependency)
  └── CLI → Core + Infrastructure + Shared
```

---

## 3. Communication Protocol

### 3.1 Client Authentication

#### REST Endpoint
```
POST /api/clients/authenticate
Content-Type: application/json

{
  "clientId": "location-stockholm-001",
  "apiKey": "secure-key-here"
}

Response 200 OK:
{
  "authenticated": true,
  "clientName": "Stockholm Location",
  "accessToken": "jwt-token-here"
}

Response 401 Unauthorized:
{
  "authenticated": false,
  "reason": "Invalid credentials"
}
```

#### Client Registry (Server-side)
```json
// appsettings.json (Server)
{
  "AcceptedClients": [
    {
      "clientId": "location-stockholm-001",
      "clientName": "Stockholm Location",
      "apiKey": "secure-key-1",
      "enabled": true
    },
    {
      "clientId": "location-malmo-002",
      "clientName": "Malmö Location",
      "apiKey": "secure-key-2",
      "enabled": true
    }
  ]
}
```

### 3.2 Vehicle Type Synchronization

#### Initial Sync (Client Startup)
```
GET /api/vehicle-types
Authorization: Bearer {accessToken}

Response 200 OK:
{
  "vehicleTypes": [
    {
      "vehicleTypeId": "small-car",
      "displayName": "Small Car",
      "pricingFormula": "baseDayRate * days",
      "description": "Compact vehicles for city driving",
      "version": 1,
      "lastUpdated": "2025-12-14T10:00:00Z"
    },
    {
      "vehicleTypeId": "station-wagon",
      "displayName": "Station Wagon",
      "pricingFormula": "(baseDayRate * days * 1.3) + (baseKmPrice * km)",
      "version": 1,
      "lastUpdated": "2025-12-14T10:00:00Z"
    },
    {
      "vehicleTypeId": "truck",
      "displayName": "Truck",
      "pricingFormula": "(baseDayRate * days * 1.5) + (baseKmPrice * km * 1.5)",
      "version": 1,
      "lastUpdated": "2025-12-14T10:00:00Z"
    }
  ]
}
```

### 3.3 Real-time Updates (Push)

#### SignalR Hub
```csharp
// Server: ConfigurationHub
public class ConfigurationHub : Hub
{
    public async Task SubscribeToUpdates(string clientId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "AllClients");
    }
    
    // Server calls this to push updates
    public async Task NotifyVehicleTypesUpdated(VehicleTypeUpdateNotification notification)
    {
        await Clients.Group("AllClients").SendAsync("VehicleTypesUpdated", notification);
    }
}

// Client: Subscribe and handle
hubConnection.On<VehicleTypeUpdateNotification>("VehicleTypesUpdated", 
    async (notification) => 
    {
        await ReloadVehicleTypes();
    });
```

#### Update Notification Message
```json
{
  "updateType": "VehicleTypesChanged",
  "timestamp": "2025-12-14T15:30:00Z",
  "affectedTypes": ["small-car", "truck"],
  "message": "Pricing formulas updated for 2 vehicle types"
}
```

---

## 4. Core Abstractions Changes

### 4.1 Updated IVehicleTypeStore

The existing `IVehicleTypeStore` interface requires no changes, but implementations will differ:

```csharp
// Existing interface (unchanged)
public interface IVehicleTypeStore
{
    Task<VehicleTypeDefinition?> GetByIdAsync(string vehicleTypeId);
    Task<IReadOnlyList<VehicleTypeDefinition>> ListAllAsync();
}

// NEW: Server implementation (authoritative)
public class InMemoryVehicleTypeStore : IVehicleTypeStore
{
    // Can add/update/delete types via management API
    public Task AddOrUpdateAsync(VehicleTypeDefinition definition) { }
    public Task<bool> DeleteAsync(string vehicleTypeId) { }
}

// NEW: Client implementation (remote sync)
public class RemoteVehicleTypeStore : IVehicleTypeStore
{
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    
    public async Task SyncFromServerAsync()
    {
        var types = await _httpClient.GetFromJsonAsync<List<VehicleTypeDto>>(
            "/api/vehicle-types");
        UpdateCache(types);
    }
    
    // GetByIdAsync and ListAllAsync use cached data
}
```

### 4.2 New Client Configuration

```json
// appsettings.json (Client)
{
  "ServerConnection": {
    "serverUrl": "https://localhost:5001",
    "clientId": "location-stockholm-001",
    "apiKey": "secure-key-1",
    "enablePushUpdates": true,
    "fallbackMode": "UseLastKnownConfiguration"
  },
  "PricingParameters": {
    "baseDayRate": 100.0,
    "baseKmPrice": 0.5
  },
  "FilePaths": {
    "vehiclesJson": "Data/vehicles.json",
    "cachedVehicleTypesJson": "Data/cached-vehicle-types.json"
  }
}
```

---

## 5. Business Logic: Price Freezing

### 5.1 Requirement
**Active rentals MUST preserve their pricing even when vehicle type configuration changes.**

### 5.2 Implementation (Already Supported!)

The current implementation already satisfies this requirement:

```csharp
// Checkout captures VehicleTypeId at rental start
public class Rental
{
    public string BookingNumber { get; set; }
    public string VehicleTypeId { get; set; }  // Frozen at checkout!
    public DateTimeOffset CheckoutTimestamp { get; set; }
    public decimal CheckoutOdometer { get; set; }
    
    // Return calculates price using the frozen VehicleTypeId
    public DateTimeOffset? ReturnTimestamp { get; set; }
    public decimal? ReturnOdometer { get; set; }
    public decimal? RentalPrice { get; set; }
}

// ReturnService uses the frozen type
public async Task<Result<RegisterReturnResponse>> RegisterReturnAsync(
    RegisterReturnRequest request)
{
    var rental = await _rentalRepository.GetByBookingNumberAsync(request.BookingNumber);
    
    // Uses rental.VehicleTypeId captured at checkout
    // Even if server updated this type, the rental uses the old definition
    var price = await _pricingCalculator.CalculateAsync(
        rental.VehicleTypeId,  // Frozen value
        request.PricingParameters,
        days,
        km);
    
    rental.RentalPrice = Math.Ceiling(price);
    await _rentalRepository.UpdateAsync(rental);
}
```

### 5.3 Additional Safety: Version Tracking (Optional)

For audit purposes, you could optionally track the version:

```csharp
public class Rental
{
    public string VehicleTypeId { get; set; }
    public int VehicleTypeVersion { get; set; }  // Optional: snapshot version
    public string? PricingFormulaSnapshot { get; set; }  // Optional: snapshot formula
}
```

However, this is **not required** for Phase 1 since the frozen `VehicleTypeId` lookup will fail gracefully if the type is removed (explicit error handling in PricingCalculator).

---

## 6. Failure Modes & Resilience

### 6.1 Server Unavailable at Client Startup

**Options:**

1. **Fail Fast** (strict mode):
   ```csharp
   if (!await TryConnectToServerAsync())
       throw new InvalidOperationException("Cannot start without server connection");
   ```

2. **Use Cached Configuration** (resilient mode - recommended):
   ```csharp
   if (!await TryConnectToServerAsync())
   {
       Log.Warning("Server unavailable, using cached vehicle types");
       await LoadCachedVehicleTypes();
   }
   ```

**Recommendation**: Start with resilient mode, save last-known-good configuration to `cached-vehicle-types.json`.

### 6.2 Server Unavailable During Operation

- Client continues operating with cached vehicle type definitions
- New checkouts use cached types (may be stale but operational)
- Client logs warning and attempts reconnection periodically

### 6.3 Invalid Vehicle Type Update

- Client validates received formulas before applying
- If validation fails, reject update and log error
- Continue using previous valid configuration

---

## 7. Implementation Phases

### Phase 1: Basic Server + REST API
- [ ] Create VehicleRental.Server project (ASP.NET Core Web API)
- [ ] Implement VehicleTypesController (GET /api/vehicle-types)
- [ ] Implement server-side InMemoryVehicleTypeStore
- [ ] Implement ClientAuthenticationService with static client registry
- [ ] Add authentication middleware (API key or JWT)

### Phase 2: Client Connection
- [ ] Create VehicleRental.Shared project with contracts
- [ ] Update CLI to connect to server on startup
- [ ] Implement RemoteVehicleTypeStore in client
- [ ] Add client authentication flow
- [ ] Add cached vehicle types fallback

### Phase 3: Push Updates (SignalR)
- [ ] Add SignalR to server (ConfigurationHub)
- [ ] Implement server-to-client push notifications
- [ ] Update client to subscribe to SignalR hub
- [ ] Implement client-side reload on push notification
- [ ] Add server management API (POST/PUT/DELETE vehicle types)

### Phase 4: Testing & Resilience
- [ ] Test server unavailable scenarios
- [ ] Test concurrent clients receiving updates
- [ ] Test active rental price preservation
- [ ] Test invalid configuration rejection
- [ ] Load testing with multiple clients

---

## 8. API Reference

### 8.1 Server REST API

#### GET /api/vehicle-types
**Authorization**: Required (Bearer token)  
**Response**: List of all vehicle type definitions

#### GET /api/vehicle-types/{vehicleTypeId}
**Authorization**: Required  
**Response**: Single vehicle type definition or 404

#### POST /api/vehicle-types
**Authorization**: Required (Admin)  
**Body**: VehicleTypeDto  
**Response**: Created vehicle type (201) or validation errors (400)

#### PUT /api/vehicle-types/{vehicleTypeId}
**Authorization**: Required (Admin)  
**Body**: VehicleTypeDto  
**Response**: Updated vehicle type (200) or 404

#### DELETE /api/vehicle-types/{vehicleTypeId}
**Authorization**: Required (Admin)  
**Response**: 204 No Content or 404

#### POST /api/clients/authenticate
**Authorization**: None (this is the auth endpoint)  
**Body**: ClientAuthRequest  
**Response**: Authentication result with token

### 8.2 SignalR Hub

#### ConfigurationHub Methods

**Client → Server:**
- `SubscribeToUpdates(string clientId)`: Client subscribes to configuration updates

**Server → Client:**
- `VehicleTypesUpdated(VehicleTypeUpdateNotification notification)`: Server pushes update notification

---

## 9. Security Considerations

### 9.1 Authentication
- Client API keys stored securely (environment variables, Azure Key Vault)
- Server validates API key against accepted clients list
- Issue short-lived JWT tokens for session management

### 9.2 Authorization
- Regular clients: Read-only access to vehicle types
- Admin clients: Can create/update/delete vehicle types
- Implement role-based access control (RBAC)

### 9.3 Transport Security
- HTTPS required for all communications
- SignalR connections use secure WebSockets (wss://)
- No sensitive data in URLs or query parameters

### 9.4 Rate Limiting
- Implement rate limiting on server endpoints
- Prevent DoS attacks from misbehaving clients

---

## 10. Testing Strategy

### 10.1 Unit Tests
- Test RemoteVehicleTypeStore synchronization logic
- Test ClientAuthenticationService validation
- Test price freezing behavior (already covered)

### 10.2 Integration Tests
- Test full client-server authentication flow
- Test vehicle type sync on client startup
- Test SignalR push notifications
- Test fallback to cached configuration

### 10.3 End-to-End Tests
1. Start server, add vehicle types
2. Start client, verify sync
3. Perform checkout on client
4. Update vehicle type on server
5. Verify client receives update
6. Perform return on old rental
7. Verify old rental uses frozen pricing
8. Perform new checkout
9. Verify new rental uses updated pricing

---

## 11. Migration Path from Current System

### Step 1: Add Server Project (Non-Breaking)
- Create VehicleRental.Server
- Run server standalone
- Existing CLI continues working with local JSON

### Step 2: Add Shared Contracts
- Create VehicleRental.Shared
- Both Server and CLI reference Shared

### Step 3: Update CLI (Feature Flag)
- Add `ServerConnection:Enabled` config flag
- If false: use existing local JSON behavior
- If true: use RemoteVehicleTypeStore

### Step 4: Deploy & Test
- Deploy server
- Configure test client with server connection
- Validate both modes work

### Step 5: Enable for All Clients
- Update all client configurations
- Monitor connections and errors
- Remove local JSON files once stable

---

## 12. Open Questions & Decisions Needed

1. **Authentication Method**: API keys (simpler) or JWT (more secure)?
   - **Recommendation**: Start with API keys, upgrade to JWT in Phase 2

2. **Push Protocol**: SignalR (recommended) or polling?
   - **Recommendation**: SignalR for real-time, with polling fallback

3. **Server State**: In-memory (Phase 1) or database?
   - **Recommendation**: In-memory for Phase 1, add database in Phase 2

4. **Client Fleet Storage**: Keep local JSON or also sync to server?
   - **Recommendation**: Keep local - each client owns their fleet

5. **Pricing Parameters**: Server-managed or client-managed?
   - **Recommendation**: Move to server in Phase 2 (different rates per region)

6. **Active Rental Storage**: In-memory or persistent?
   - **Recommendation**: Add persistent storage (SQLite) in Phase 2 for production

---

## 13. Summary

This client-server architecture provides:

✅ Centralized configuration management  
✅ Multi-client support  
✅ Real-time updates via SignalR  
✅ Price stability for active rentals (already implemented)  
✅ Client authentication and authorization  
✅ Graceful degradation when server unavailable  
✅ Clean separation of concerns  
✅ Backward compatible migration path  

The existing clean architecture makes this extension straightforward - the Core remains unchanged, and we add new implementations of existing abstractions.
