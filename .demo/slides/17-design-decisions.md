# 📋 Design Decisions

## Key Architectural Choices

### 1. **Distributed Data Ownership**
- Server: Vehicle types only
- Clients: Local fleet + rentals
- Why: Franchise model = local ownership

### 2. **Price Freezing at Checkout**
- Captured: Formula + base rates
- Why: Customer protection from mid-rental changes

### 3. **SignalR over Polling**
- Real-time push vs periodic fetch
- Why: Instant updates, lower bandwidth

### 4. **Expression-Based Pricing**
- String formulas vs compiled code
- Why: Flexibility + security (no code injection)
