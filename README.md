# Neon Highway

An endless 3D night driving game made in Unity 6 (6000.0.85f1, URP) for the Game Design course (HW2).

Controls: A / D or Left / Right arrows to change lane, Esc or P to pause, Enter to start or restart.

## Running it

Open the folder with Unity Hub. After the import finishes, open `Assets/Scenes/Game.unity` and press Play.

If the scene is missing or broken, use the menu `Neon > Build Everything`. It regenerates the textures,
sounds, materials, prefabs, post processing and the scene. `Neon > Build Executables` makes the macOS and Windows builds.

## Scripts (`Assets/Scripts`)

- `GameManager`: singleton, game states (Menu / Playing / Paused / GameOver), speed, level, score, high score (PlayerPrefs)
- `PlayerCar`: lane change (New Input System), 3 lives, fuel, hit feedback, neon underglow
- `Interactable` (abstract): base for everything on the road. `Obstacle` (abstract) -> `StaticCar`, `MovingCar`. `Collectible` (abstract) -> `Coin`, `FuelCan`
- `PoolManager`, `PooledObject`, `PooledFx`: object pooling, nothing is created or destroyed while playing
- `RoadGenerator`, `RoadSegment`: endless procedural road
- `ObstacleSpawner`: rows of cars, coin lines, fuel cans, harder over time
- `FireworksManager`, `AudioManager`, `CameraFollow`, `UIManager`
- `AutoDriver`: test helper, only active when the `NEON_AUTODRIVE` environment variable is set
- `Editor/`: generators used by the `Neon` menu
