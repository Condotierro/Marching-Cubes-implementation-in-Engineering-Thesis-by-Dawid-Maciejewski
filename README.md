
# 📘 Project Documentation

### Procedural Voxel Tunnel System with Gameplay Integration

## 1. Overview

This project implements a **procedural voxel-based world** using chunked terrain and dynamic tunnel generation. It combines:

* **Voxel terrain (3D grid of blocks)**
* **Procedural tunnel carving**
* **Runtime chunk streaming**
* **Player interaction (movement, shooting, destruction)**
* **Mesh generation and collision updates**

The system is designed for a forward-moving game loop, where terrain is generated ahead of the player and removed behind them.

## 2. Core Architecture

### Main Systems

| System           | Responsibility                             |
| ---------------- | ------------------------------------------ |
| `World`          | Chunk streaming and world management       |
| `Chunk`          | Voxel data + mesh generation               |
| `TunnelPath`     | Procedural tunnel path generation          |
| `ShipController` | Player movement & gameplay                 |
| `Projectile`     | Terrain interaction (destruction/painting) |
| `PlayerHealth`   | Health system                              |
| `Collectible`    | Score/health pickups                       |

## 3. Voxel System

### Block Types
```csharp
public enum BlockType
```

Defines the material of each voxel:

| Type                | Description             |
| ------------------- | ----------------------- |
| Air                 | Empty space             |
| Dirt / Grass / Sand | Surface materials       |
| Stone / DeepStone   | Underground layers      |
| Burned              | Modified by projectiles |
| Obsol               | Reserved/unused         |

### Chunk Structure (`Chunk.cs`)

Each chunk is a **3D voxel grid**:

```csharp
16 x 64 x 16 (X, Y, Z)
```

* Stored as:
```csharp
BlockType[,,] blocks;
```

### Chunk Responsibilities

#### 1. Block Generation

```csharp
GenerateBlocks()
```

* Fills voxel grid
* Carves tunnels using `TunnelPath`
* Assigns materials based on height layers:
  * Bottom → DeepStone
  * Middle → Stone
  * Top → Dirt

#### 2. Mesh Generation

```csharp
GenerateMesh()
```

* Converts voxels → mesh
* Only renders **visible faces**
* Uses:
  * Vertices
  * UVs (texture atlas)
  * Triangles

Optimization:
* Faces between solid blocks are **not generated**

#### 3. Collider Handling

```csharp
UpdateCollider()
```
* Rebuilds `MeshCollider`
* Called after terrain modification

#### 4. Dynamic Content
* **Tunnel connections** (geysers)
* **Collectibles**
* Stored in:

```csharp
List<GameObject> registered;
```

Used for cleanup when chunk unloads.

## 4. Procedural Tunnel Generation

### `TunnelPath.cs`

This system generates **continuous, branching paths** in 2D (X,Z plane).

### Key Concepts

#### 1. Main Path
* Starts at origin
* Moves forward step-by-step
* Slight random turning:

```csharp
ApplyRandomTurnWithClamping()
```

* Direction constrained by:
```csharp
maxDeviation
```

#### 2. Branching System

Branches are created probabilistically:

```csharp
branchChance
branchAccumulator
```

Branch lifecycle:
1. Spawn from main path
2. Diverge away
3. Travel minimum length
4. Curve back
5. Rejoin main path

#### 3. Spatial Optimization

Uses grid-based spatial binning:

```csharp
Dictionary<Vector2Int, List<Vector2>> nodeBuckets;
```

Purpose:
* Fast distance queries
* Avoid checking entire path

#### 4. Distance Query

```csharp
DistanceSqToPath(Vector2 point)
```

* Computes shortest distance from point to path
* Used by `Chunk` to carve tunnels

### Tunnel Carving

In `Chunk.GenerateBlocks()`:

```csharp
bool carve = distSq < tunnelRadiusSq;
```

If true then block becomes `Air`

## 5. World Streaming System

### `World.cs`

Handles **dynamic chunk loading/unloading**.

### Key Logic

#### 1. Determine Player Chunk

```csharp
playerChunkX = floor(player.position.x / chunkSize)
```

#### 2. Load Nearby Chunks

Within:

```csharp
renderDistance
```

#### 3. Unload Distant Chunks

* Destroy chunk GameObject
* Destroy registered objects (collectibles, effects)

#### 4. Ensure Tunnel Continuity

```csharp
layerX.EnsureLengthUpTo()
```

Guarantees tunnels exist ahead of player.

## 6. Player System

### `ShipController.cs`

Handles all player gameplay.

### Movement

* Physics-based (`Rigidbody`)
* Forces applied:

```csharp
rb.AddForce()
```

* Speed clamped to `maxSpeed`

### Vertical Layer System

Player moves between 3 fixed heights:
| Layer  | Height |
| ------ | ------ |
| Top    | 55     |
| Medium | 33     |
| Bottom | 11     |

Controlled by:
* `P` → go up (if near geyser)
* `O` → go down (if space available)

### Rotation

* Yaw: `A / D`
* Tilt: visual banking effect

### Shooting

```csharp
HandleShooting()
```

* Left mouse button
* Spawns projectile prefab
* Applies forward velocity

Fire modes:

* Default
* Marker
* Large
* Creative

### Collision with Terrain

```csharp
HandleVoxelCollision()
```

* Detects impacted voxel
* Removes spherical region
* Spawns debris
* Updates mesh + collider


### Scoring System
```csharp
Score = distance + destroyedBlocks - crashes + collectibles
```

## 7. Projectile System

### `Projectile.cs`

Handles terrain interaction.

### Modes

| Mode | Effect                     |
| ---- | -------------------------- |
| 0    | Small destruction          |
| 1    | Destruction + burned shell |
| 2    | Paint (Sand blocks)        |

### Destruction Algorithm

For a sphere:
```csharp
x² + y² + z² <= r²
```

* Iterates through voxel cube
* Applies modification

### Effects
* Explosion prefab
* Sound
* Physics impulse

## 8. Health System

### `PlayerHealth.cs`

* Continuous health drain:
```csharp
TakeDamage(Time.deltaTime)
```

* Damage sources:
  * Shooting
  * Collisions
    
* Healing:
  * Collectibles

## 9. Collectibles

### `Collectible.cs`

Types:
* Score bonus
* Health boost

Behavior:
* Rotates continuously
* On trigger:
  * Adds score OR heals player
  * Destroys itself

## 10. Camera System

### `CameraBehaviour.cs`

* Smooth follow using:
```csharp
Vector3.Lerp()
```

* Maintains:
  * Height offset
  * Z offset

## 11. Performance Considerations

### Implemented Optimizations
* Chunk-based world streaming
* Face culling (only visible faces rendered)
* Spatial hashing for tunnel queries
* Coroutine-based chunk generation
* Non-alloc physics queries (`OverlapSphereNonAlloc`)

### Profiling Hooks
```csharp
RuntimeMetrics.Record(...)
```

Tracks:

* Mesh generation time
* Collider updates
* Memory usage (commented)

## 12. Gameplay Loop

1. Player moves forward
2. World streams new chunks
3. Tunnels generated ahead
4. Player:
   * Navigates tunnels
   * Shoots terrain
   * Collects items
5. Terrain updates in real-time
6. Score increases over distance

## 13. Key Design Features

### Procedural Continuity
* Infinite tunnel generation
* Smooth branching and merging

### Fully Destructible Terrain
* Real-time voxel editing
* Immediate mesh rebuild

### Layered Gameplay
* Multi-height navigation system

### Hybrid System
* Combines:
  * Procedural generation
  * Physics gameplay
  * Mesh-based rendering

## 14. Possible Improvements

* Replace cube meshing with Marching Cubes algorithm for higher visual fidelity of the terrain.
* Implement:
  * LOD system
  * GPU mesh generation
* Add:
  * Biomes
  * Noise-based terrain variation
* Optimize collider updates (partial updates instead of full rebuild)

## 15. Summary

This project demonstrates a complete pipeline for:
* Procedural world generation
* Real-time voxel manipulation
* Dynamic streaming environments
* Integrated gameplay systems

It may be utilized as a strong foundation for:
* Voxel games
* Endless runners
* Destructible environments

## 16. Credits
Special thanks to Dr hab. sztuki inż. arch. Rafał Szrajber, prof. uczelni for both providing the idea for the game concept, and guiding the creation of the thesis, to which this project is attached.
