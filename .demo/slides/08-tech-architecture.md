# 🏗️ Architecture Overview

## System Architecture

```
┌─────────────────────────────────────────────────────────┐
│                VehicleRental.Server                      │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐  │
│  │ REST API     │  │ SignalR Hub  │  │ Auth Service │  │
│  │ Controllers  │  │ (Real-time)  │  │ (JWT)        │  │
│  └──────────────┘  └──────────────┘  └──────────────┘  │
└─────────────────────────────────────────────────────────┘
           │                   │                   
           ▼                   ▼                   
┌─────────────────┐   ┌─────────────────┐   ┌─────────────────┐
│ CLI (Location 1)│   │ CLI (Location 2)│   │ DevTools (Admin)│
│ Local Fleet     │   │ Local Fleet     │   │ Management      │
└─────────────────┘   └─────────────────┘   └─────────────────┘
```
