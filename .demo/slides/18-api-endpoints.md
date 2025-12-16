# 🔍 REST API Endpoints

## API Surface

### Authentication
- `POST /api/clients/authenticate` - Get JWT token

### Vehicle Types (JWT Required)
- `GET /api/vehicle-types` - List all types
- `GET /api/vehicle-types/{id}` - Get specific type
- `POST /api/vehicle-types` - Create/update (Admin)
- `DELETE /api/vehicle-types/{id}` - Delete (Admin)

### Vehicle Relay (Admin Only)
- `POST /api/vehicles` - Add to location
- `DELETE /api/vehicles/{regNum}` - Remove from location

### SignalR
- `GET /hubs/configuration` - Real-time events
