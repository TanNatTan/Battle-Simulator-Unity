# W40K Battle Simulator — Unity Port

This branch is a Unity-native port of the browser simulator. It deliberately does not embed JavaScript in Unity: the deterministic simulation is plain C#, presentation is separate, and the original JSON catalogs remain the shared source of balance data.

## Run

Open the repository with Unity `6000.3.22f1` and press Play in `Assets/Scenes/SampleScene.unity`. `BattleBootstrap` creates the runtime before the scene loads, so no manual scene setup is required.

The default vertical slice is an autonomous 1920×1080 Space Marine-versus-Ork annihilation match. The top toolbar controls pause/resume and 1×, 2×, 4×, or 8× speed. Right/middle drag pans, the wheel zooms, and clicking the minimap moves the observer camera.

## Ported foundation

- Fixed-timestep simulation, independent rendering, capped catch-up, pause and speed controls.
- Central event bus and entity model for units, vehicles, aircraft, buildings, projectiles, resources, and environmental objects.
- Spatial hashing, same-layer separation, structure collision, air/ground separation, and stuck-unit recovery.
- Autonomous optical/auspex contact acquisition, contact-only withdrawal, territory capture by physical units, and fog-compatible target memory.
- Fifteen engine-level projectile classes with behavior flags, travel, collision, splash, suppression, damage, death, and corpse decay.
- Construction planning before builder assignment, multiple concurrent foundations, one repairer per damaged building, builder replacement, continuous unit queues, vehicles, and aircraft.
- Polygonal resource zones, physical capture, carrier gathering/delivery, warehouses, and territory requisition income.
- Phase 20 five-condition defeat: forces, production, reinforcement access, builders, and allied rescue are all checked.
- Lightweight observer UI: simple shapes, labels, FPS, show/hide terrain, show/hide clickable minimap, and battle statistics.
- Original browser JSON catalogs copied into `Assets/Resources/BattleSimulatorData/data` for Unity-side loaders.

## Architecture

`Core/` owns the clock, bootstrap, events, and deterministic utilities. `Simulation/` owns state and ordered systems. `Presentation/` only observes and draws the world. `Data/` loads the shared catalogs. This preserves the browser project's golden rule and gives later sprite, NavMesh, Jobs/Burst, DOTS, replay, map-editor, and save work clean extension points.

## Validation

`Tools/CompileCheck/BattleSimulator.CompileCheck.csproj` compiles the runtime against the exact Unity 6 managed assemblies without starting the editor:

```powershell
dotnet build Tools/CompileCheck/BattleSimulator.CompileCheck.csproj
```

`Tools/Smoke/SimulationSmoke.cs` is the deterministic 120-second headless smoke scenario used during the port. It asserts continued ticking, production, construction, physical territory growth, and projectile combat.

Unity batch validation can be run with:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.3.22f1\Editor\Unity.exe' -batchmode -nographics -quit -projectPath . -logFile unity-compile.log
```

If the Unity Licensing Client is unavailable, the editor command stops before script compilation; the standalone compile check still catches C# and Unity API type errors.
