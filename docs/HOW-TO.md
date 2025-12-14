# Vehicle Rental System Instructions

## TLDR; Starting the system

### Visual Studio Code

Prerequisite installations:

- C# extension
- .NET 10 SDK

Start the project with F5 (launches "Launch All" compound configuration which starts Server + CLI + DevTools).

### Visual Studio

Open `VehicleRental.slnx`.
Start the project with F5.

## What happens when you run the project

Three projects are started in parallel:

- VehicleRental.Server
- VehicleRental.CLI
- VehicleRental.DevTools.CLI

### Purpose of the three projects

#### VehicleRental.Server

The server relays information but is not the master of all data. It stores and manages VehicleTypeDefinitions (vehicle types with pricing formulas).
Clients connect to the server and are authenticated based on settings in `appsettings.json`. If the client's connection is successful, a SignalR connection is established to push real-time updates to VehicleRental.CLI and VehicleRental.DevTools.CLI.

#### VehicleRental.CLI

This is the client used at franchise VehicleRental locations.
The client connects to the server on startup and receives VehicleTypeDefinitions.
The client can also receive updated VehicleTypeDefinition information via SignalR push notifications.

#### VehicleRental.DevTools.CLI

Used to administer the VehicleRental system.
Enables CRUD operations over VehicleTypeDefinitions.
Enables Add/Remove operations for Vehicles (relayed to specific location clients via SignalR).

## System Design Backstory

The system is implemented for a chain of franchise vehicle rental offices!
This means that

- the local offices own their car fleet, thus storage of data is their responsibility
- the server helps relay changes or additions to the vehicle fleets
- there is a devtool cli that helps admins manage vehicles
- each franchise location has their own store front, implemented in the CLI project
- the main business wants the vehicle types to be available to all franchices
  - even if in some cases all locations don't have vehicles of a specific type

There are no real third party Auth, or Data storage - they are all mocked in this version.

## System Design Structure

See `architecture-diagram.md` for relevant mermaid diagrams describing the system, communication patterns and data ownership and boundaries.
