# W40K Battle Simulator - Unity C# Port

This branch is a Unity-native behavioral port of the browser simulator. It does not embed or execute JavaScript: runtime state, AI, combat, construction, economy, objectives, movement, and presentation are C#. The authored JSON catalogs remain the shared source of faction, subfaction, weapon, projectile, objective, workforce, production-plan, and economic-map data.

## Run

Open the repository with Unity `6000.3.22f1`, open `Assets/Scenes/SampleScene.unity`, and press Play. `BattleBootstrap` creates the runtime before the scene loads; no scene wiring is required.

The default is an autonomous 1920x1080 match. The top toolbar controls pause/resume and 1x, 2x, 4x, or 8x speed. Right/middle drag pans, the wheel zooms, and clicking the minimap moves the camera. Click `Battle setup` at the lower-left to configure 2-12 players, map size, seed, team, faction, subfaction, battle objective, 160px-compatible spawn radius, and four identity colors, then start a new battle.

## Runtime parity foundation

- Fixed 30 Hz deterministic simulation separated from rendering, capped catch-up, pause/speed controls, and per-system cadences.
- Central event bus and entity state for units, squads, vehicles, aircraft, buildings, projectiles, resource zones, authored economic nodes, and territory polygons.
- All eight factions, 68 subfactions, full faction rosters, subfaction production plans, and bounded faction-specific builder policies.
- Irregular Voronoi-style territory polygons. Only physical combat-capable units capture; buildings anchor territory until destroyed; Space Marines capture at 3x the baseline rate.
- Directional optical sight, auspex contact, camouflage, remembered contacts, battlefield-wide vox sharing, and player-specific observer/minimap intelligence.
- Thirteen squad roles, objective/economy-driven missions, combat formations, commander attachment, and condition-aware finish targeting.
- Fifteen projectile classes plus behavior flags, physical travel/collision, armor penetration, splash/suppression, magazines, reloads, heat, incapacitation, death, and corpse cleanup.
- Specialist rules including Devastator heavy-bolter modifiers, Chaplain buffs, Apothecary gene-seed recovery, Assault jump packs, and Captain/Chapter Master Iron Halo shields.
- Field healing, stabilization, building/vehicle repair, Necron reanimation, Tyranid recovery, and one dedicated building repairer while construction stays first priority.
- Base planning before workforce demand, concurrent foundations, unique-building progression before duplication, builder replacement/growth ceilings, continuous full-roster infantry and vehicle queues, and faction building labels.
- Transport capacity with physical embark/travel/disembark, faster carriers, air/ground collision separation, and spatial stuck recovery.
- Manually authored resource polygons, physical ownership, builder/carrier gathering, warehouse delivery, landmark import/export/capture stock, and authored-only trade routes.
- Ork Waaagh momentum and priority banners, kill-driven Ork/Chaos/Tyranid economies, and shared-core faction/subfaction strategic weights.
- All 18 battle-objective metrics plus the strict Phase-20 annihilation rule (forces, production, reinforcement access, builders, and allied rescue).
- Lightweight observer rendering, FPS display, four-color player identity, show/hide terrain, show/hide interactive minimap, and player-intelligence view switching.

## Architecture

`Core/` owns time, bootstrap, events, and deterministic utilities. `Configuration/` defines battle setup. `Data/` parses the shared catalogs into typed C# definitions. `Simulation/` owns state and ordered/cadenced systems. `Presentation/` observes and configures the world without making simulation decisions.

## Validation

Compile the runtime against the installed Unity 6 managed assemblies:

```powershell
dotnet build Tools/CompileCheck/BattleSimulator.CompileCheck.csproj --no-restore
```

Run the deterministic 12-player 1920x1080 stress/parity scenario:

```powershell
dotnet run --project Tools/Smoke/SimulationSmoke.csproj --no-restore
```

The smoke test asserts exact catalog counts (18 objectives, 68 production plans, 8 workforce policies, 15 projectile classes), continued ticking, physical territory, construction, continuous production, combat, contacts, and a 4x-responsive wall-time budget.

Unity batch validation can be run with:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.3.22f1\Editor\Unity.exe' -batchmode -nographics -quit -projectPath . -logFile Logs\codex-validation.log
```

If the local Unity Licensing Client is unavailable, the editor can stall before project validation. The compile and headless tests remain independent of that machine-level service.

## Parity boundary

The port targets equivalent authored data and simulation outcomes, not byte-for-byte JavaScript execution or pixel-identical Canvas rendering. Unity floating-point, update scheduling, and presentation differ by engine. Browser-only editor/replay authoring surfaces can continue to be ported on top of the typed C# runtime without weakening this separation.
