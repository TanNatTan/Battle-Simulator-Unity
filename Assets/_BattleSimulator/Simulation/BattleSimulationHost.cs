using BattleSimulator.Core;
using BattleSimulator.Configuration;
using BattleSimulator.Data;
using BattleSimulator.Simulation.Systems;
using UnityEngine;

namespace BattleSimulator.Simulation
{
    [DefaultExecutionOrder(-800)]
    public sealed class BattleSimulationHost : MonoBehaviour
    {
        private SimulationClock clock;
        public BattleSetup Setup { get; private set; }
        public BattleSimulation Simulation { get; private set; }
        public BattleWorld World => Simulation?.World;

        private void Awake()
        {
            clock = GetComponent<SimulationClock>();
            StartBattle(BattleSetup.CreateDefault(2));
        }

        public void StartBattle(BattleSetup setup)
        {
            if (clock == null) clock = GetComponent<SimulationClock>();
            clock?.ResetClock();
            clock?.SetPaused(false);
            Simulation?.Dispose();
            Setup = setup ?? BattleSetup.CreateDefault(2);
            BattleDataRepository data = BattleDataRepository.Instance;
            Simulation = new BattleSimulation(BattleScenarioFactory.Create(Setup, data));
            Simulation.AddSystem(new SpatialIndexSystem());
            Simulation.AddSystem(new PerceptionIntelSystem());
            Simulation.AddSystem(new StrategicCommandSystem(data));
            Simulation.AddSystem(new SquadFormationSystem());
            Simulation.AddSystem(new AutonomousAISystem());
            Simulation.AddSystem(new VehicleDeploymentSystem());
            Simulation.AddSystem(new MovementSystem());
            Simulation.AddSystem(new CombatSystem());
            Simulation.AddSystem(new ProjectileSystem());
            Simulation.AddSystem(new EconomyTerritorySystem());
            Simulation.AddSystem(new LandmarkEconomySystem());
            Simulation.AddSystem(new SustainmentSystem());
            Simulation.AddSystem(new FactionIdentitySystem());
            Simulation.AddSystem(new ConstructionProductionSystem(data));
            Simulation.AddSystem(new VictorySystem(data));
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
