# Client-Server Architecture Diagram

## System Overview

```mermaid
graph TB
    subgraph Server["VehicleRental.Server (ASP.NET Core Web API)"]
        subgraph Controllers["REST API Controllers"]
            CC[ClientsController<br/>POST /api/clients/authenticate]
            VTC[VehicleTypesController<br/>GET/POST/DELETE /api/vehicle-types]
            VC[VehiclesController<br/>POST/DELETE /api/vehicles]
        end
        
        subgraph Hub["SignalR Hub"]
            CH[ConfigurationHub<br/>/hubs/configuration]
        end
        
        subgraph Services["Services Layer"]
            CAS[ClientAuthenticationService<br/>• Authenticate<br/>• IssueJWT]
            SVTS[ServerVehicleTypeStore<br/>• AddOrUpdate<br/>• Delete<br/>• NotifyClients]
        end
        
        subgraph DataStore["Data Store (In-Memory)"]
            VTD[(Vehicle Type Definitions<br/>• small-car<br/>• station-wagon<br/>• truck)]
            ACR[(Accepted Clients Registry<br/>• location-xxx-001<br/>• admin-001)]
        end
        
        CC --> CAS
        VTC --> SVTS
        VC --> CH
        SVTS --> CH
        CAS --> ACR
        SVTS --> VTD
    end
    
    subgraph Admin["VehicleRental.DevTools.CLI (Admin)"]
        AdminHTTP[HTTP Client<br/>JWT Auth]
    end
    
    subgraph Client1["VehicleRental.CLI (Location Client)"]
        subgraph RemoteStore1["RemoteVehicleTypeStore"]
            HTTP1[HTTP Client]
            SR1[SignalR Client]
            Cache1[Memory Cache]
        end
        
        subgraph LocalData1["Local Data"]
            Fleet1[(vehicles.json)]
            Rentals1[(Active Rentals<br/>In-Memory)]
        end
        
        subgraph CoreServices1["Core Services"]
            Checkout1[CheckoutService]
            Return1[ReturnService]
            Pricing1[PricingCalculator]
        end
    end
    
    AdminHTTP -->|HTTPS| CC
    AdminHTTP -->|HTTPS| VTC
    AdminHTTP -->|HTTPS| VC
    
    HTTP1 -->|HTTPS| CC
    HTTP1 -->|HTTPS| VTC
    SR1 -->|WSS| CH
    
    CH -->|VehicleTypesUpdated| SR1
    CH -->|VehicleUpdated| SR1
```

## Project Structure

```mermaid
graph LR
    subgraph Projects
        Server[VehicleRental.Server]
        CLI[VehicleRental.CLI]
        DevTools[VehicleRental.DevTools.CLI]
        Core[VehicleRental.Core]
        Infra[VehicleRental.Infrastructure]
        Shared[VehicleRental.Shared]
    end
    
    Server --> Core
    Server --> Infra
    Server --> Shared
    
    CLI --> Core
    CLI --> Infra
    CLI --> Shared
    
    DevTools --> Shared
    
    Infra --> Core
```

## REST API Endpoints

```mermaid
graph LR
    subgraph Authentication
        A1["POST /api/clients/authenticate<br/>Body: {clientId, apiKey}<br/>Returns: {accessToken, clientName}"]
    end
    
    subgraph VehicleTypes["Vehicle Types (Requires JWT)"]
        B1["GET /api/vehicle-types<br/>Returns all types"]
        B2["GET /api/vehicle-types/{id}<br/>Returns specific type"]
        B3["POST /api/vehicle-types<br/>Create/Update (Admin only)"]
        B4["DELETE /api/vehicle-types/{id}<br/>Delete (Admin only)"]
        B5["GET /api/vehicle-types/version<br/>Returns current version"]
    end
    
    subgraph Vehicles["Vehicles Relay (Admin only, Requires JWT)"]
        C1["POST /api/vehicles<br/>Relay add to location"]
        C2["DELETE /api/vehicles/{regNum}?location=xxx<br/>Relay remove from location"]
    end
```

---

## Communication Flows

### 1. Client Startup (Initial Sync)

```mermaid
sequenceDiagram
    participant CLI as VehicleRental.CLI
    participant Server as VehicleRental.Server
    
    CLI->>Server: POST /api/clients/authenticate<br/>{clientId, apiKey}
    Server-->>CLI: 200 OK {accessToken, clientName}
    
    CLI->>Server: GET /api/vehicle-types<br/>Authorization: Bearer {jwt}
    Server-->>CLI: 200 OK [{vehicleTypes}]
    
    CLI->>CLI: Cache types to memory
    
    CLI->>Server: Connect to /hubs/configuration<br/>access_token={jwt}
    Server-->>CLI: Connection established
    
    CLI->>Server: SubscribeToUpdates()
    Server-->>CLI: Subscribed
    
    Note over CLI: Ready for checkout/return operations
```

### 2. Vehicle Type Update (Admin Push to All Clients)

```mermaid
sequenceDiagram
    participant Admin as DevTools.CLI (Admin)
    participant Server as VehicleRental.Server
    participant Hub as ConfigurationHub
    participant C1 as CLI (Location 1)
    participant C2 as CLI (Location 2)
    
    Admin->>Server: POST /api/vehicle-types<br/>{vehicleTypeId: "small-car", formula: "..."}
    Server->>Server: Validate formula
    Server->>Server: Update ServerVehicleTypeStore
    Server-->>Admin: 200 OK {updated type}
    
    Server->>Hub: NotifyVehicleTypesUpdated
    Hub-->>C1: VehicleTypesUpdated({type, version})
    Hub-->>C2: VehicleTypesUpdated({type, version})
    
    C1->>C1: Update local cache
    C2->>C2: Update local cache
```

### 3. Vehicle Relay to Location

```mermaid
sequenceDiagram
    participant Admin as DevTools.CLI (Admin)
    participant Server as VehicleRental.Server
    participant Hub as ConfigurationHub
    participant CLI as CLI (Target Location)
    
    Admin->>Server: POST /api/vehicles<br/>{regNum, typeId, location}
    Server->>Server: Validate vehicle type exists
    Server->>Hub: Notify Location:{location}
    Hub-->>CLI: VehicleUpdated({type: Added, vehicle})
    Server-->>Admin: 202 Accepted
    
    CLI->>CLI: Add vehicle to local catalog
```

### 4. Price Freezing at Checkout

```mermaid
sequenceDiagram
    participant CLI as VehicleRental.CLI
    participant CheckoutSvc as CheckoutService
    participant VTStore as VehicleTypeStore
    participant RentalRepo as RentalRepository
    
    CLI->>CheckoutSvc: RegisterCheckoutAsync(request)
    CheckoutSvc->>VTStore: GetByIdAsync(vehicleTypeId)
    VTStore-->>CheckoutSvc: VehicleTypeDefinition (with formula)
    
    CheckoutSvc->>CheckoutSvc: Create Rental with frozen VehicleTypeDefinition
    CheckoutSvc->>RentalRepo: SaveAsync(rental)
    
    Note over RentalRepo: Rental stores snapshot of<br/>VehicleTypeDefinition at checkout time
    
    CheckoutSvc-->>CLI: Success {bookingNumber, frozenType}
```

### 5. Return with Frozen Pricing

```mermaid
sequenceDiagram
    participant CLI as VehicleRental.CLI
    participant ReturnSvc as ReturnService
    participant RentalRepo as RentalRepository
    participant PricingCalc as PricingCalculator
    
    CLI->>ReturnSvc: RegisterReturnAsync(request)
    ReturnSvc->>RentalRepo: GetByBookingNumberAsync(bookingNumber)
    RentalRepo-->>ReturnSvc: Rental (with frozen VehicleTypeDefinition)
    
    ReturnSvc->>PricingCalc: CalculatePrice(rental, pricingParams)
    
    Note over PricingCalc: Uses frozen formula from<br/>checkout time, NOT current server formula
    
    PricingCalc-->>ReturnSvc: calculatedPrice
    ReturnSvc->>RentalRepo: Update rental as completed
    ReturnSvc-->>CLI: Success {price, days, km}
```

### 6. Offline Operation

```mermaid
sequenceDiagram
    participant CLI as VehicleRental.CLI
    participant Remote as RemoteVehicleTypeStore
    participant Server as VehicleRental.Server
    
    CLI->>Remote: Initialize
    Remote->>Server: POST /api/clients/authenticate
    Server--xRemote: Timeout/Error
    
    Remote->>Remote: Load cached vehicle types
    Remote->>Remote: Start background reconnection task
    
    Note over CLI: Operating in offline mode<br/>with cached vehicle types
    
    loop Every 10 seconds
        Remote->>Server: Retry authentication
        alt Server Available
            Server-->>Remote: 200 OK {jwt}
            Remote->>Server: GET /api/vehicle-types
            Server-->>Remote: 200 OK [{types}]
            Remote->>Remote: Update cache
        else Server Unavailable
            Remote->>Remote: Continue with cached data
        end
    end
```

---

## Data Ownership Model

```mermaid
graph TB
    subgraph ServerOwns["Server Owns (Authoritative)"]
        VT[Vehicle Type Definitions<br/>• small-car<br/>• station-wagon<br/>• truck]
        PF[Pricing Formulas]
        CR[Client Registry & API Keys]
    end
    
    subgraph ClientOwns["Client Owns (Local)"]
        LF[Local Vehicle Fleet<br/>vehicles.json]
        AR[Active Rentals<br/>In-Memory with frozen pricing]
        PP[Pricing Parameters<br/>baseDayRate, baseKmPrice]
    end
    
    subgraph ClientCaches["Client Caches (Synced)"]
        CVT[Vehicle Type Definitions<br/>cached from server]
    end
    
    ServerOwns -->|Push via SignalR| ClientCaches
    ClientCaches -->|Used for new checkouts| ClientOwns
```

---

## Security Model

### Authentication Flow

```mermaid
sequenceDiagram
    participant Client as CLI/DevTools
    participant Server as VehicleRental.Server
    participant Auth as ClientAuthenticationService
    participant JWT as JWT Token
    
    Client->>Server: POST /api/clients/authenticate<br/>{clientId, apiKey}
    Server->>Auth: Validate credentials
    
    alt Valid Credentials
        Auth->>Auth: Lookup client in AcceptedClients
        Auth->>JWT: Generate JWT with claims<br/>(client_id, role, exp)
        Auth-->>Server: AuthResponse with token
        Server-->>Client: 200 OK {accessToken, clientName}
        
        Note over Client: Use token for all subsequent requests
        Client->>Server: GET /api/vehicle-types<br/>Authorization: Bearer {token}
    else Invalid Credentials
        Auth-->>Server: Authentication failed
        Server-->>Client: 401 Unauthorized
    end
```

### Authorization Levels

```mermaid
graph LR
    subgraph Roles
        Regular[Regular Client<br/>role: Client]
        Admin[Admin Client<br/>role: Admin]
    end
    
    subgraph Permissions
        ReadTypes[Read Vehicle Types ✓]
        WriteTypes[Write Vehicle Types]
        RelayVehicles[Relay Vehicles to Locations]
    end
    
    Regular --> ReadTypes
    Admin --> ReadTypes
    Admin --> WriteTypes
    Admin --> RelayVehicles
```

### Transport Security

```mermaid
graph TB
    subgraph Security["Transport Security"]
        HTTPS[HTTPS for REST API]
        WSS[WSS for SignalR]
        TLS[TLS 1.2+ minimum]
        JWT[JWT Bearer Authentication]
    end
```
