# Unity Netcode Multiplayer Demo

Technical demonstration of multiplayer systems using Unity Netcode for GameObjects.

Built with primitives to focus on networking implementation rather than visuals.

## What This Is

A prototype showcasing core multiplayer architecture:
- Server-authoritative design
- Network state synchronization
- AI behavior in networked environment
- Component-based structure

This is not a game - it's a technical reference for multiplayer implementation patterns.

## Tech Stack

- Unity 6
- Unity Netcode for GameObjects
- NavMesh AI
- Unity Input System
- C# (51%)

## Key Systems

### Multiplayer Architecture

Server authority model where gameplay logic runs on server, clients handle input and visualization.

Players connect via host/client setup. Server validates all actions, clients interpolate for smooth visuals.

### AI System

Component-based enemies with three states: patrol, chase, attack. NavMesh pathfinding with player detection.

Two enemy types:
- Melee (close range)
- Ranged (projectile attacks)

Server controls AI decisions, state synced to clients via NetworkVariables.

### Player System

Third-person camera with collision detection. Movement uses CharacterController with server validation and client-side prediction.

Input sent via ServerRpc, position synced with NetworkVariables, interpolation handles latency.

## Project Structure

```
Scripts/
├── Enemies/
│   ├── BaseEnemy.cs
│   ├── MeleeEnemy.cs
│   ├── RangedEnemy.cs
│   └── Components/
│       ├── EnemyMovement.cs
│       └── EnemyTargeting.cs
├── Player/
│   ├── PlayerInput.cs
│   ├── PlayerMovement.cs
│   └── ThirdPersonCamera.cs
└── Networking/
    └── PlayerSpawnManager.cs
```

## Testing

1. Build project
2. First instance: Host
3. Second instance: Client (127.0.0.1)
4. Test synchronized gameplay

## Technical Highlights

**Server Authority**
All gameplay validation on server prevents cheating.

**Client Prediction**
Local movement prediction reduces perceived latency.

**Component Design**
Separated concerns for movement, targeting, and attack logic.

**Network Optimization**
NetworkVariables for state, ServerRpc for commands, interpolation for smooth visuals.

## What I Learned

- Server-authoritative multiplayer patterns
- Network state synchronization strategies
- Client-side prediction implementation
- NavMesh integration with networking
- Component-based architecture for networked games

## Requirements

- Unity 6+
- Netcode for GameObjects package
- NavMesh components
