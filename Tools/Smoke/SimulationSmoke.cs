using System;
using System.Diagnostics;
using System.IO;
using BattleSimulator.Configuration;
using BattleSimulator.Core;
using BattleSimulator.Data;
using BattleSimulator.Simulation;
using BattleSimulator.Simulation.Systems;

internal static class SimulationSmoke
{
    private static int Main()
    {
        try
        {
            string dataRoot = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "Resources", "BattleSimulatorData", "data");
            BattleDataRepository data = BattleDataRepository.LoadFromJson(path => File.ReadAllText(Path.Combine(dataRoot, path.Replace('/', Path.DirectorySeparatorChar) + ".json")));
            Require(data.Objectives.Count == 18, $"expected 18 objectives, got {data.Objectives.Count}");
            Require(data.ProductionPlans.Count == 68, $"expected 68 production plans, got {data.ProductionPlans.Count}");
            Require(data.BuilderPolicies.Count == 8, $"expected 8 workforce policies, got {data.BuilderPolicies.Count}");
            Require(data.Projectiles.Count == 15, $"expected 15 projectile classes, got {data.Projectiles.Count}");

            BattleSetup setup = BattleSetup.CreateDefault(12);
            setup.Width = 1920f; setup.Height = 1080f; setup.Seed = 42040;
            BattleWorld world = BattleScenarioFactory.Create(setup, data);
            var simulation = CreateSimulation(world, data);
            int produced = 0;
            world.Events.Published += battleEvent => { if (battleEvent.Type == BattleEventType.UnitCreated) produced++; };
            int initialUnits = world.Units.Count, initialBuildings = world.Buildings.Count;
            int peakProjectiles = 0;
            var watch = Stopwatch.StartNew();
            const int maximumTicks = 1200;
            for (int tick = 1; tick <= maximumTicks && !world.BattleEnded; tick++)
            {
                simulation.Step(new SimulationStep((ulong)tick, 1f / 30f, tick / 30d));
                if (world.Projectiles.Count > peakProjectiles) peakProjectiles = world.Projectiles.Count;
            }
            watch.Stop();

            int captured = 0, activeUnits = 0, completedBuildings = 0, contacts = 0;
            for (int i = 0; i < world.TerritoryCells.Count; i++) if (world.TerritoryCells[i].OwnerId != 0) captured++;
            for (int i = 0; i < world.Units.Count; i++) if (world.Units[i].Active && !world.Units[i].IsDead) activeUnits++;
            for (int i = 0; i < world.Buildings.Count; i++) if (world.Buildings[i].Active && world.Buildings[i].Operational) completedBuildings++;
            for (int i = 0; i < world.Players.Count; i++) contacts += world.Players[i].IntelContacts.Count;
            Require(world.Tick > 0, "simulation did not advance");
            Require(world.Buildings.Count > initialBuildings, "AI did not create foundations");
            Require(produced > 0, "AI did not continuously produce units");
            Require(captured >= world.Players.Count, "physical territory ownership did not persist");
            Require(completedBuildings >= world.Players.Count, "headquarters were not operational");
            // Four simulated seconds per wall second is already enough to keep a 4x match responsive in this headless worst-case pass.
            Require(watch.Elapsed.TotalSeconds < 10d, $"12-player stress pass too slow: {watch.Elapsed.TotalSeconds:0.00}s");
            Console.WriteLine($"catalogs=18/68/8/15 ticks={world.Tick} units={world.Units.Count} active={activeUnits} produced={produced} buildings={world.Buildings.Count} completed={completedBuildings} captured={captured} contacts={contacts} peakProjectiles={peakProjectiles} wall={watch.Elapsed.TotalSeconds:0.000}s");
            simulation.Dispose();
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static BattleSimulation CreateSimulation(BattleWorld world, BattleDataRepository data)
    {
        var simulation = new BattleSimulation(world);
        simulation.AddSystem(new SpatialIndexSystem());
        simulation.AddSystem(new PerceptionIntelSystem());
        simulation.AddSystem(new StrategicCommandSystem(data));
        simulation.AddSystem(new SquadFormationSystem());
        simulation.AddSystem(new AutonomousAISystem());
        simulation.AddSystem(new VehicleDeploymentSystem());
        simulation.AddSystem(new MovementSystem());
        simulation.AddSystem(new CombatSystem());
        simulation.AddSystem(new ProjectileSystem());
        simulation.AddSystem(new EconomyTerritorySystem());
        simulation.AddSystem(new LandmarkEconomySystem());
        simulation.AddSystem(new SustainmentSystem());
        simulation.AddSystem(new FactionIdentitySystem());
        simulation.AddSystem(new ConstructionProductionSystem(data));
        simulation.AddSystem(new VictorySystem(data));
        simulation.AddSystem(new CleanupSystem());
        return simulation;
    }

    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
