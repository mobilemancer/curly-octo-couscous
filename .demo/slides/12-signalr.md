# 📡 SignalR Real-Time Updates

## ConfigurationHub

### Server-to-Client Events:
- `VehicleTypesUpdated` - Pricing formula changes
- `VehicleUpdated` - Fleet changes at location

### How It Works:
1. Admin updates pricing via DevTools
2. Server updates internal store
3. SignalR broadcasts to all subscribed clients
4. Clients update local cache instantly

**Result:** Sub-second synchronization across all locations
