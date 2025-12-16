# 🔐 Authentication Flow

## JWT-Based Authentication

1. Client sends credentials: `{clientId, apiKey}`
2. Server validates and issues JWT token
3. Client uses token for subsequent requests
4. SignalR authenticated via access token in query param

### Security Features:
- JWT tokens with configurable expiry
- Role-based access (Admin vs Location)
- SignalR authenticated via query param
