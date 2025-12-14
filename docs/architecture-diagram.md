# Client-Server Architecture Diagram

```mermaid
┌─────────────────────────────────────────────────────────────────────────┐
│                        VehicleRental.Server                              │
│                     (ASP.NET Core Web API)                               │
│                                                                           │
│  ┌────────────────────────────────────────────────────────────────────┐ │
│  │                    REST API Controllers                             │ │
│  │                                                                      │ │
│  │  ┌──────────────────┐         ┌──────────────────────────────┐    │ │
│  │  │ ClientsController │         │ VehicleTypesController       │    │ │
│  │  │                   │         │                               │    │ │
│  │  │ POST /authenticate│         │ GET    /api/vehicle-types    │    │ │
│  │  │                   │         │ POST   /api/vehicle-types    │    │ │
│  │  │ Returns: JWT      │         │ PUT    /api/vehicle-types/:id│    │ │
│  │  └──────────────────┘         │ DELETE /api/vehicle-types/:id│    │ │
│  │                                 └──────────────────────────────┘    │ │
│  └────────────────────────────────────────────────────────────────────┘ │
│                                                                           │
│  ┌────────────────────────────────────────────────────────────────────┐ │
│  │                    SignalR Hub (Real-time Push)                     │ │
│  │                                                                      │ │
│  │  ┌────────────────────────────────────────────────────────────┐   │ │
│  │  │              ConfigurationHub                               │   │ │
│  │  │                                                              │   │ │
│  │  │  Methods:                                                   │   │ │
│  │  │  • SubscribeToUpdates(clientId)                            │   │ │
│  │  │  • NotifyVehicleTypesUpdated(notification)                 │   │ │
│  │  └────────────────────────────────────────────────────────────┘   │ │
│  └────────────────────────────────────────────────────────────────────┘ │
│                                                                           │
│  ┌────────────────────────────────────────────────────────────────────┐ │
│  │                         Services Layer                              │ │
│  │                                                                      │ │
│  │  ┌───────────────────────┐      ┌─────────────────────────────┐   │ │
│  │  │ ClientAuthentication  │      │ VehicleTypeManagement       │   │ │
│  │  │ Service               │      │ Service                      │   │ │
│  │  │                       │      │                              │   │ │
│  │  │ • ValidateClient()    │      │ • Add/Update/Delete Types   │   │ │
│  │  │ • IssueJWT()          │      │ • ValidateFormula()         │   │ │
│  │  └───────────────────────┘      │ • NotifyClients()           │   │ │
│  │                                  └─────────────────────────────┘   │ │
│  └────────────────────────────────────────────────────────────────────┘ │
│                                                                           │
│  ┌────────────────────────────────────────────────────────────────────┐ │
│  │                    Data Store (Authoritative)                       │ │
│  │                                                                      │ │
│  │  ┌────────────────────────────────────────────────────────────┐   │ │
│  │  │  InMemoryVehicleTypeStore (Server)                         │   │ │
│  │  │                                                              │   │ │
│  │  │  • small-car:    baseDayRate * days                        │   │ │
│  │  │  • station-wagon: (baseDayRate * days * 1.3) + ...        │   │ │
│  │  │  • truck:        (baseDayRate * days * 1.5) + ...         │   │ │
│  │  └────────────────────────────────────────────────────────────┘   │ │
│  │                                                                      │ │
│  │  ┌────────────────────────────────────────────────────────────┐   │ │
│  │  │  Accepted Clients Registry                                  │   │ │
│  │  │                                                              │   │ │
│  │  │  • location-stockholm-001  → API Key 1                     │   │ │
│  │  │  • location-malmo-002      → API Key 2                     │   │ │
│  │  │  • location-goteborg-003   → API Key 3                     │   │ │
│  │  └────────────────────────────────────────────────────────────┘   │ │
│  └────────────────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────────────────┘
                                    ▲
                                    │
                    ┌───────────────┼───────────────┐
                    │ HTTPS/WSS     │ HTTPS/WSS     │ HTTPS/WSS
                    │               │               │
                    ▼               ▼               ▼
┌──────────────────────┐  ┌──────────────────────┐  ┌──────────────────────┐
│   Client 1           │  │   Client 2           │  │   Client 3           │
│   (Stockholm)        │  │   (Malmö)            │  │   (Göteborg)         │
│                      │  │                      │  │                      │
│  VehicleRental.CLI   │  │  VehicleRental.CLI   │  │  VehicleRental.CLI   │
│                      │  │                      │  │                      │
│ ┌──────────────────┐ │  │ ┌──────────────────┐ │  │ ┌──────────────────┐ │
│ │ Remote           │ │  │ │ Remote           │ │  │ │ Remote           │ │
│ │ VehicleTypeStore │ │  │ │ VehicleTypeStore │ │  │ │ VehicleTypeStore │ │
│ │                  │ │  │ │                  │ │  │ │                  │ │
│ │ • HTTP Client    │ │  │ │ • HTTP Client    │ │  │ │ • HTTP Client    │ │
│ │ • SignalR Client │ │  │ │ • SignalR Client │ │  │ │ • SignalR Client │ │
│ │ • Memory Cache   │ │  │ │ • Memory Cache   │ │  │ │ • Memory Cache   │ │
│ │ • Disk Cache     │ │  │ │ • Disk Cache     │ │  │ │ • Disk Cache     │ │
│ └──────────────────┘ │  │ └──────────────────┘ │  │ └──────────────────┘ │
│                      │  │                      │  │                      │
│ ┌──────────────────┐ │  │ ┌──────────────────┐ │  │ ┌──────────────────┐ │
│ │ Local Fleet      │ │  │ │ Local Fleet      │ │  │ │ Local Fleet      │ │
│ │ (vehicles.json)  │ │  │ │ (vehicles.json)  │ │  │ │ (vehicles.json)  │ │
│ │                  │ │  │ │                  │ │  │ │                  │ │
│ │ ABC123 small-car │ │  │ │ DEF456 truck     │ │  │ │ GHI789 wagon     │ │
│ │ ABC124 small-car │ │  │ │ DEF457 truck     │ │  │ │ GHI790 wagon     │ │
│ │ ABC125 truck     │ │  │ │ DEF458 small-car │ │  │ │ GHI791 small-car │ │
│ └──────────────────┘ │  │ └──────────────────┘ │  │ └──────────────────┘ │
│                      │  │                      │  │                      │
│ ┌──────────────────┐ │  │ ┌──────────────────┐ │  │ ┌──────────────────┐ │
│ │ Active Rentals   │ │  │ │ Active Rentals   │ │  │ │ Active Rentals   │ │
│ │ (In-Memory)      │ │  │ │ (In-Memory)      │ │  │ │ (In-Memory)      │ │
│ │                  │ │  │ │                  │ │  │ │                  │ │
│ │ BK001: ABC123    │ │  │ │ BK501: DEF456    │ │  │ │ BK801: GHI789    │ │
│ │   Type: small-car│ │  │ │   Type: truck    │ │  │ │   Type: wagon    │ │
│ │   Price: FROZEN  │ │  │ │   Price: FROZEN  │ │  │ │   Price: FROZEN  │ │
│ └──────────────────┘ │  │ └──────────────────┘ │  │ └──────────────────┘ │
└──────────────────────┘  └──────────────────────┘  └──────────────────────┘


═══════════════════════════════════════════════════════════════════════════
                            Communication Flows
═══════════════════════════════════════════════════════════════════════════

1. CLIENT STARTUP (Initial Sync)
   ─────────────────────────────
   Client                              Server
     │                                   │
     ├─► POST /api/clients/authenticate  │
     │   { clientId, apiKey }            │
     │                                   │
     │   ◄─ 200 OK { jwt }              │
     │                                   │
     ├─► GET /api/vehicle-types          │
     │   Authorization: Bearer {jwt}     │
     │                                   │
     │   ◄─ 200 OK { vehicleTypes[] }   │
     │                                   │
     ├─► Cache to disk & memory          │
     │                                   │
     ├─► Connect to SignalR Hub          │
     │   Authorization: Bearer {jwt}     │
     │                                   │
     │   ◄─ Connection accepted          │
     │                                   │
     └─► Ready for operations            │


2. VEHICLE TYPE UPDATE (Server-Initiated Push)
   ────────────────────────────────────────────
   Admin                Server                      All Clients
     │                    │                              │
     ├─► PUT /vehicle-    │                              │
     │   types/small-car  │                              │
     │   { new formula }  │                              │
     │                    │                              │
     │   ◄─ 200 OK        │                              │
     │                    │                              │
     │                    ├─► Update storage             │
     │                    │                              │
     │                    ├─► SignalR: VehicleTypes─────►│
     │                    │   Updated({ changes })       │
     │                    │                              │
     │                    │                    Client 1 ─┤ Reload types
     │                    │                              │
     │                    │                    Client 2 ─┤ Reload types
     │                    │                              │
     │                    │                    Client 3 ─┤ Reload types


3. PRICE FREEZING (Active Rental Unaffected by Update)
   ────────────────────────────────────────────────────
   Client State Before Update:
   ┌──────────────────────────────────────┐
   │ Active Rental: BK001                 │
   │   VehicleTypeId: "small-car"         │
   │   Checkout: 2025-12-10               │
   │   Type Definition at Checkout:       │
   │     Formula: "baseDayRate * days"    │
   │     baseDayRate: 100                 │
   └──────────────────────────────────────┘

   Server Updates small-car:
   ┌──────────────────────────────────────┐
   │ New Formula: "baseDayRate * days * 2"│
   │ baseDayRate: 100 (unchanged)         │
   └──────────────────────────────────────┘

   Client State After Update:
   ┌──────────────────────────────────────┐
   │ Active Rental: BK001                 │
   │   VehicleTypeId: "small-car"         │
   │   Return: 2025-12-13                 │
   │   Days: 3                            │
   │   Price Calculation:                 │
   │     Uses: "baseDayRate * days"       │
   │     Result: 100 * 3 = 300            │
   │   ✓ OLD PRICING PRESERVED            │
   └──────────────────────────────────────┘

   New Rental After Update:
   ┌──────────────────────────────────────┐
   │ New Rental: BK002                    │
   │   VehicleTypeId: "small-car"         │
   │   Checkout: 2025-12-14               │
   │   Price Calculation:                 │
   │     Uses: "baseDayRate * days * 2"   │
   │     Result: 100 * 3 * 2 = 600        │
   │   ✓ NEW PRICING APPLIED              │
   └──────────────────────────────────────┘


4. OFFLINE OPERATION (Server Unavailable)
   ────────────────────────────────────────
   Client                              Server
     │                                   │
     ├─► POST /api/clients/authenticate  │
     │                                   X (timeout)
     │                                   
     ├─► Load cached-vehicle-types.json
     │   
     ├─► Log: "Operating in offline mode"
     │   
     └─► Continue operations with cached types
         (Can still checkout/return using local data)


═══════════════════════════════════════════════════════════════════════════
                            Data Ownership Model
═══════════════════════════════════════════════════════════════════════════

Server Owns:
  ✓ Vehicle Type Definitions (small-car, truck, etc.)
  ✓ Pricing Formulas
  ✓ Client Registry & API Keys
  ✓ Configuration Authority

Client Owns:
  ✓ Local Vehicle Fleet (vehicles.json)
  ✓ Active Rentals (in-memory)
  ✓ Pricing Parameters (baseDayRate, baseKmPrice)
  ✓ Operational State

Cached on Client:
  ⟳ Vehicle Type Definitions (synced from server)
  ⟳ Last-known-good configuration (for offline mode)


═══════════════════════════════════════════════════════════════════════════
                            Security Model
═══════════════════════════════════════════════════════════════════════════

┌────────────────────────────────────────────────────────────────────────┐
│                         Authentication Flow                             │
└────────────────────────────────────────────────────────────────────────┘

1. Static API Keys (Phase 1)
   ─────────────────────────
   • Each client has unique clientId + apiKey pair
   • API keys stored in server appsettings.json
   • Client sends credentials on authentication
   • Server issues short-lived JWT token
   • Client uses JWT for all subsequent requests

2. Authorization Levels
   ────────────────────
   ┌──────────────────┬────────────────┬─────────────────┐
   │ Role             │ Can Read Types │ Can Write Types │
   ├──────────────────┼────────────────┼─────────────────┤
   │ Regular Client   │      ✓         │       ✗         │
   │ Admin Client     │      ✓         │       ✓         │
   └──────────────────┴────────────────┴─────────────────┘

3. Transport Security
   ──────────────────
   • HTTPS required for all REST API calls
   • WSS (secure WebSockets) for SignalR
   • TLS 1.2+ minimum
   • Certificate validation enforced


═══════════════════════════════════════════════════════════════════════════
                         Implementation Priority
═══════════════════════════════════════════════════════════════════════════

Phase 1: Basic Server + REST API (1-2 weeks)
  1. Create Server project
  2. Implement authentication
  3. Implement vehicle types CRUD API
  4. Add Swagger documentation

Phase 2: Client Connection (1 week)
  1. Create RemoteVehicleTypeStore
  2. Add HTTP client with auth
  3. Implement caching (memory + disk)
  4. Add fallback to cached data

Phase 3: Real-time Updates (1 week)
  1. Add SignalR to server
  2. Add SignalR client to CLI
  3. Implement push notifications
  4. Test concurrent clients

Phase 4: Production Readiness (1 week)
  1. Add comprehensive error handling
  2. Add logging and monitoring
  3. Performance testing
  4. Security hardening
  5. Documentation
```
