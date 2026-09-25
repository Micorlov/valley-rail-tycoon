# Unity Android Train Tycoon — Planning Task

I want to create a small Unity game for Android inspired by the train-management gameplay of OpenTTD.

This is NOT intended to be a full OpenTTD clone.

The first version should be a small, polished, playable MVP focused ONLY on trains.

## Important

DO NOT start implementing the game yet.

First analyze the project and create a detailed implementation plan.

The plan should be realistic for a small Unity Android game and should avoid unnecessary complexity.

---

# Game Concept

Create a simple isometric train-management / transport tycoon game.

The player should be able to:

* View an isometric map
* Move and zoom the camera using touch controls
* Build railway tracks
* Build train stations
* Buy trains
* Assign trains to routes
* Transport cargo between industries and towns
* Earn money from successful deliveries
* Expand the railway network

The core gameplay loop should be:

Build track → Build stations → Buy train → Create route → Transport cargo → Earn money → Expand network.

---

# Visual Direction

Use the attached concept image as the main visual reference.

The game should have:

* Isometric 3D camera
* Colorful low-poly graphics
* Green countryside
* Trees
* Rivers
* Small towns
* Train stations
* Railway tracks
* Bridges
* Tunnels if they are reasonably simple to implement
* Industries such as coal mines and factories

The UI should be designed for Android phones and tablets.

Large buttons and touch-friendly controls are important.

Do NOT copy OpenTTD graphics, assets, UI, names, maps, or copyrighted content.

Use OpenTTD only as inspiration for gameplay concepts.

---

# MVP Map

Start with ONE small handcrafted map.

Example:

Town A
Coal Mine
Power Plant
Town B

The map should be large enough to build several railway routes but small enough for an MVP.

Do not implement procedural world generation initially unless there is a strong technical reason.

---

# Track Building

The player should be able to enter a Track Build mode.

The player selects a starting point and drags/taps toward the destination.

Show a track preview.

Green = valid placement.

Red = invalid placement.

Display the construction cost before confirming.

Support initially:

* Straight tracks
* Curved tracks
* Junctions

Consider bridges and tunnels as a later milestone if they significantly increase MVP complexity.

---

# Stations

Players should be able to place stations next to tracks.

Stations should:

* Connect to railway tracks
* Detect nearby industries/towns
* Load available cargo
* Unload accepted cargo
* Show basic station information

Example:

Coal Mine Station

Waiting:
Coal: 75 tons

---

# Trains

The player should be able to purchase trains.

For the MVP, start with approximately three train types:

1. Small Freight Train
2. Fast Freight Train
3. Passenger Train

Each train should have basic properties:

* Purchase price
* Speed
* Capacity
* Running cost
* Cargo type

Trains must physically travel along the railway network.

---

# Routes

The player should be able to select a train and create a simple route.

Example:

Train #3

Coal Mine Station
↓
Rivermount Station
↓
Coal Mine Station

The train should automatically repeat its assigned route.

---

# Cargo

Start with only a few cargo types.

Recommended MVP:

Coal
Goods
Passengers

Example production chain:

Coal Mine
↓
Coal
↓
Power Plant
↓
Money

Another possible chain:

Town
↓
Passengers
↓
Town
↓
Money

Avoid complicated industry chains in the first version.

---

# Economy

The game should have a simple economy.

Player starts with money.

Money is spent on:

* Tracks
* Stations
* Trains

Money is earned by delivering cargo.

The UI should display:

Money
Income
Expenses
Number of trains

Avoid loans, stocks, company competition, inflation, complex maintenance systems, etc. in the MVP.

---

# Train Movement

This is one of the most important technical systems.

Design a reliable railway network representation.

The plan should explain how Unity should represent:

* Track segments
* Track connections
* Junctions
* Stations
* Train routes
* Pathfinding

Consider using a graph-based railway network.

Explain which pathfinding approach should be used, such as A*.

The architecture should make it possible to add signals later.

---

# Android Controls

Design everything for touch.

Camera:

One finger drag = move camera

Pinch = zoom

Optional:
Two-finger rotate

Building:

Tap = select

Drag = create track

Tap UI button = enter build mode

Tap object = open information panel

---

# Game Speed

Include:

Pause
1x
2x
4x

Simulation should remain deterministic and stable when game speed changes.

---

# Save System

The MVP should support:

Save game
Load game
Autosave

Store:

* Map state
* Money
* Tracks
* Stations
* Trains
* Routes
* Cargo
* Simulation state

Use an Android-compatible local save format.

---

# Unity Architecture

Before implementation, propose a clean architecture.

Possible systems:

GameManager
EconomyManager
SimulationManager
RailNetworkManager
TrackBuilder
PathfindingSystem
TrainManager
TrainController
StationManager
CargoManager
SaveManager
UIManager
CameraController

Do not automatically use these exact classes if a better architecture exists.

Explain the responsibility of each major system.

Avoid creating huge manager classes or tightly coupled systems.

---

# Performance

Target Android.

The game should run smoothly on typical mid-range Android devices.

Plan for:

* Object pooling where useful
* Efficient train updates
* Efficient pathfinding
* Limited physics usage
* Mobile-friendly shaders
* Reasonable polygon counts
* Minimal garbage collection during gameplay

Avoid using expensive Unity physics for systems that can be simulated more efficiently.

---

# Development Strategy

Break development into milestones.

Suggested structure:

Milestone 1
Map + camera

Milestone 2
Track placement

Milestone 3
Rail network representation

Milestone 4
Train movement

Milestone 5
Stations

Milestone 6
Cargo transportation

Milestone 7
Economy

Milestone 8
UI

Milestone 9
Save/load

Milestone 10
Android optimization and build

For every milestone specify:

* What needs to be implemented
* Important scripts/classes
* Data structures
* Unity GameObjects/prefabs
* How the feature should work
* Dependencies
* Potential technical risks
* How to test it

---

# Testing

Include a testing strategy.

Especially test:

* Track connections
* Junctions
* Pathfinding
* Train route calculation
* Trains reaching stations
* Cargo loading/unloading
* Economy calculations
* Save/load
* Touch controls
* Different Android screen sizes

Include Unity EditMode and PlayMode tests where appropriate.

---

# Deliverables

After analyzing the project, create:

PLAN.md

It should contain:

1. Game architecture
2. Core gameplay loop
3. Scene structure
4. Folder structure
5. Major scripts/classes
6. Data models
7. Railway network design
8. Train/pathfinding design
9. UI architecture
10. Android controls
11. Save architecture
12. Development milestones
13. Testing strategy
14. Performance considerations
15. Technical risks
16. MVP scope
17. Features explicitly postponed until after MVP

Also propose a recommended Unity project folder structure such as:

Assets/
Art/
Audio/
Materials/
Prefabs/
Trains/
Tracks/
Stations/
Buildings/
Scenes/
Scripts/
Core/
Rail/
Trains/
Stations/
Economy/
UI/
Save/
ScriptableObjects/
Tests/

Do not implement the game yet.

First produce PLAN.md so I can review and approve the architecture and MVP scope before implementation begins.
