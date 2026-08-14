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

    public sealed class BattleSimulation : IDisposable
    {
        private readonly List<IBattleSystem> systems = new List<IBattleSystem>();

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
                systems[i].Tick(World, step);
            }
            World.RemoveInactive();
        }

        public void Dispose()
        {
            systems.Clear();
        }
    }
}
