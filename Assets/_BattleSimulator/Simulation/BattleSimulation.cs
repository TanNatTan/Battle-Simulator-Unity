using System;
using System.Collections.Generic;
using BattleSimulator.Core;

namespace BattleSimulator.Simulation
{
    public interface IBattleSystem
    {
        int Order { get; }
        void Tick(BattleWorld world, SimulationStep step);
    }

    /// <summary>Allows expensive decision systems to run below the fixed physics rate.</summary>
    public interface ICadencedBattleSystem
    {
        float UpdatesPerSecond { get; }
    }

    public sealed class BattleSimulation : IDisposable
    {
        private readonly List<IBattleSystem> systems = new List<IBattleSystem>();
        private readonly Dictionary<IBattleSystem, float> cadence = new Dictionary<IBattleSystem, float>();

        public BattleSimulation(BattleWorld world)
        {
            World = world;
        }

        public BattleWorld World { get; }

        public void AddSystem(IBattleSystem system)
        {
            systems.Add(system);
            systems.Sort((left, right) => left.Order.CompareTo(right.Order));
        }

        public void Step(SimulationStep step)
        {
            if (World.BattleEnded) return;
            World.Tick = step.Tick;
            World.Time = step.ElapsedTime;
            for (int i = 0; i < systems.Count; i++)
            {
                IBattleSystem system = systems[i];
                if (system is ICadencedBattleSystem scheduled && scheduled.UpdatesPerSecond > 0f)
                {
                    cadence.TryGetValue(system, out float accumulated);
                    accumulated += step.DeltaTime;
                    float interval = 1f / scheduled.UpdatesPerSecond;
                    if (accumulated + 0.00001f < interval)
                    {
                        cadence[system] = accumulated;
                        continue;
                    }
                    cadence[system] = accumulated % interval;
                    system.Tick(World, new SimulationStep(step.Tick, accumulated, step.ElapsedTime));
                }
                else system.Tick(World, step);
            }
            World.RemoveInactive();
        }

        public void Dispose()
        {
            systems.Clear();
            cadence.Clear();
        }
    }
}
