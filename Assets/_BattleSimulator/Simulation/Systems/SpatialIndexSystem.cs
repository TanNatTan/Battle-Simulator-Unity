using BattleSimulator.Core;

namespace BattleSimulator.Simulation.Systems
{
    public sealed class SpatialIndexSystem : IBattleSystem
    {
        public int Order => 0;

        public void Tick(BattleWorld world, SimulationStep step)
        {
            world.RebuildSpatialIndex();
        }
    }
}
