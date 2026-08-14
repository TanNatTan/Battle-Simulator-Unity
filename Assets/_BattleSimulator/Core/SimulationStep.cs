namespace BattleSimulator.Core
{
    /// <summary>
    /// Immutable timing data delivered to simulation systems once per battle tick.
    /// </summary>
    public readonly struct SimulationStep
    {
        public SimulationStep(ulong tick, float deltaTime, double elapsedTime)
        {
            Tick = tick;
            DeltaTime = deltaTime;
            ElapsedTime = elapsedTime;
        }

        public ulong Tick { get; }
        public float DeltaTime { get; }
        public double ElapsedTime { get; }
    }
}
