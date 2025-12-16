# 📁 Project Structure

## Solution Organization

```
VehicleRental/
├── VehicleRental.Server/       # ASP.NET Core Web API + SignalR
├── VehicleRental.CLI/          # Franchise location client
├── VehicleRental.DevTools.CLI/ # Admin management tool
├── VehicleRental.Core/         # Domain & business logic
├── VehicleRental.Infrastructure/ # Data access & stores
├── VehicleRental.Shared/       # Contracts & DTOs
└── VehicleRental.Core.Tests/   # Unit tests (xUnit)
```

### Dependency Flow:
`Server/CLI/DevTools` → `Core` → `Infrastructure` → `Shared`
