using BattleSimulator.Core;
using BattleSimulator.Simulation.Systems;
using UnityEngine;

namespace BattleSimulator.Simulation
{
    [DefaultExecutionOrder(-800)]
    public sealed class BattleSimulationHost : MonoBehaviour
    {
        private SimulationClock clock;
        public BattleSimulation Simulation { get; private set; }
        public BattleWorld World => Simulation?.World;

        private void Awake()
        {
            clock = GetComponent<SimulationClock>();
            Simulation = new BattleSimulation(BattleScenarioFactory.CreateAutonomousBattle());
            Simulation.AddSystem(new SpatialIndexSystem());
            Simulation.AddSystem(new AutonomousAISystem());
            Simulation.AddSystem(new MovementSystem());
            Simulation.AddSystem(new CombatSystem());
            Simulation.AddSystem(new ProjectileSystem());
            Simulation.AddSystem(new EconomyTerritorySystem());
            Simulation.AddSystem(new ConstructionProductionSystem());
            Simulation.AddSystem(new VictorySystem());
            Simulation.AddSystem(new CleanupSystem());
        }

        private void OnEnable()
        {
            if (clock == null) clock = GetComponent<SimulationClock>();
            if (clock != null) clock.Tick += HandleTick;
        }

        private void OnDisable()
        {
            if (clock != null) clock.Tick -= HandleTick;
        }

        private void OnDestroy()
        {
            Simulation?.Dispose();
        }

        private void HandleTick(SimulationStep step)
        {
            Simulation?.Step(step);
        }
    }
}
