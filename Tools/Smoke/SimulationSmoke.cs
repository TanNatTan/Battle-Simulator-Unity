using System;
using BattleSimulator.Core;
using BattleSimulator.Simulation;
using BattleSimulator.Simulation.Systems;

internal static class SimulationSmoke
{
    private static int Main()
    {
        try
        {
            BattleWorld world = BattleScenarioFactory.CreateAutonomousBattle(42040);
            var simulation = new BattleSimulation(world);
            simulation.AddSystem(new SpatialIndexSystem());
            simulation.AddSystem(new AutonomousAISystem());
            simulation.AddSystem(new MovementSystem());
            simulation.AddSystem(new CombatSystem());
            simulation.AddSystem(new ProjectileSystem());
            simulation.AddSystem(new EconomyTerritorySystem());
            simulation.AddSystem(new ConstructionProductionSystem());
            simulation.AddSystem(new VictorySystem());
            simulation.AddSystem(new CleanupSystem());

            int initialUnits = world.Units.Count;
            int initialBuildings = world.Buildings.Count;
            int peakProjectiles = 0;
            const int maximumTicks = 3600;
            for (int tick = 1; tick <= maximumTicks && !world.BattleEnded; tick++)
            {
                simulation.Step(new SimulationStep((ulong)tick, 1f / 30f, tick / 30d));
                if (world.Projectiles.Count > peakProjectiles) peakProjectiles = world.Projectiles.Count;
            }

            int captured = 0;
            for (int i = 0; i < world.TerritoryCells.Count; i++) if (world.TerritoryCells[i].OwnerId != 0) captured++;
            bool passed = world.Tick > 0 && world.Units.Count > initialUnits && world.Buildings.Count > initialBuildings
                && captured > 12 && peakProjectiles > 0;
            Console.WriteLine($"ticks={world.Tick} units={world.Units.Count} buildings={world.Buildings.Count} captured={captured} peakProjectiles={peakProjectiles} ended={world.BattleEnded}");
            simulation.Dispose();
            return passed ? 0 : 2;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }
}
