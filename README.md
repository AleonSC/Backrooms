# Backrooms

An endless, procedurally generated Backrooms level with a first person controller, for Unity.

Everything is generated at runtime: the layout, the meshes and colliders, the
wallpaper, carpet and ceiling-tile textures, and the fluorescent hum and footstep
sounds. You don't need any art or audio assets.

## Getting started

Requires Unity 2021.3 or newer. The Built-in pipeline, URP and HDRP all work.

**New project:** open this folder with Unity Hub (*Add > Add project from disk*).
Unity generates `ProjectSettings/` the first time it opens the project.

**Existing project:** copy `Assets/Backrooms` into your project's `Assets` folder.

Then:

1. Choose **Backrooms > Create Backrooms Scene** from the menu bar. This saves
   `Assets/Backrooms/Backrooms.unity` and adds it to the build settings.
   You can also add a `BackroomsBootstrap` component to an empty GameObject in any scene.
2. Press **Play** and click the Game view to capture the mouse.

## Controls

| Key | Action |
| --- | --- |
| WASD / arrows | Move |
| Mouse | Look |
| Shift | Sprint (limited by stamina) |
| Ctrl / C | Crouch |
| Space | Jump |
| F | Flashlight |
| F3 | Debug overlay (FPS, position, zone, seed) |
| Esc | Release the cursor |

Input works with the legacy Input Manager and with the Input System package.

## How it works

| Script | Role |
| --- | --- |
| `BackroomsGenerator` | Pure, deterministic layout. Each grid cell (3 m) hashes `(seed, cell)` to decide its east and south walls, its corner pillar and its ceiling light. Low-frequency noise splits the world into **pillar halls**, **mazes** and **long corridors**, plus darker regions where most lights are dead. The 3×3 cells around spawn are always open. |
| `BackroomsWorld` | Streams 4×4-cell chunks within `viewRadius` of the player, a couple per frame, and unloads distant ones. It animates flickering fixtures and moves a pool of 8 real-time point lights onto the nearest working fixtures. |
| `ChunkMeshBuilder` | Builds one mesh per chunk with submeshes for walls, floor, ceiling, lit fixtures and dead fixtures, plus a `MeshCollider`. UVs are in world space, so the wallpaper runs continuously across walls. Wall segments end short of the corners, and corner posts fill the joints so no faces overlap and z-fight. |
| `FirstPersonController` | `CharacterController` movement with acceleration smoothing, air control, gravity, jumping, stamina-limited sprint, crouching with a headroom check, head bob, strafe roll, landing dip, footsteps, flashlight and an IMGUI HUD. |
| `ProceduralTextures` / `ProceduralAudio` | Generate the textures and sounds. |
| `MaterialFactory` | Picks Built-in, URP or HDRP shaders at runtime. |
| `BackroomsBootstrap` | Sets up fog and ambient light, disables cameras and directional lights already in the scene, then spawns the player and the world. |

Set `randomSeed` to false on `BackroomsBootstrap` to replay a specific `seed`.
The same seed always produces the same level.

## Player builds

The materials are created with `Shader.Find`. A player build only contains a
shader if something in the build references it. If walls render pink in a
build, do one of these:

- Add `Standard` and `Unlit/Color` (Built-in), or `Universal Render Pipeline/Lit`
  and `Universal Render Pipeline/Unlit` (URP), to
  *Project Settings > Graphics > Always Included Shaders*.
- Assign your own materials to the override slots on `BackroomsBootstrap`.
