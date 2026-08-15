using BattleSimulator.Core;

namespace BattleSimulator.Simulation.Systems
{
    public sealed class SpatialIndexSystem : IBattleSystem, ICadencedBattleSystem
    {
        public int Order => 0;
        public float UpdatesPerSecond => 15f;

        public void Tick(BattleWorld world, SimulationStep step)
        {
            world.RebuildSpatialIndex();
        }
    }
}
