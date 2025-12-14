# Vehicle Rental System Instructions

## TLDR; Starting the system

### Visual Studio Code

Prerequiste installations:

- C# extension
- .Net 10 SDK

Start the project with F5.

### Visual Studio

Open `VehicleRental.slnx`.
Start the project with F5.

## What happens when you run the project

Three projects are started in parallel

- VehicleRental.Server
- VehicleRental.CLI
- VehicleRental.DevTools.CLI

### Purpose of the three projects

#### VehicleRental.Server

Clients connec tto the server. The server validates that the clients should have access. If the clients have access they can download vehicleTypes.

#### VehicleRental.CLI

This is the client used at assumed VehicleRental endpoints all over the world.
The client connects to the server on startup and recieves VehicleTypes.
The client cna also receive updated VehicleType information.

#### VehicleRental.DevTools.CLI

Used to administer the VehicleRental system.
Enables CRUD operations over VehicleTypes.



dir -Recurse -Include *.cs,*.md | Get-Content | Measure-Object -Line

truck
GHI791
(enter)
2025-1
2025-12-14T17:00
1

GHI791
2025-12-14T17:00
500
30
