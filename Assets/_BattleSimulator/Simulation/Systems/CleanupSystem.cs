using BattleSimulator.Core;

namespace BattleSimulator.Simulation.Systems
{
    public sealed class CleanupSystem : IBattleSystem, ICadencedBattleSystem
    {
        public int Order => 1000;
        public float UpdatesPerSecond => 2f;
        public void Tick(BattleWorld world, SimulationStep step)
        {
            for (int i = 0; i < world.Units.Count; i++)
            {
                UnitState unit = world.Units[i];
                if (unit.IsDead && world.Time - unit.DeathTime >= 12d) unit.Active = false;
            }
            world.RemoveInactive();
        }
    }
}
