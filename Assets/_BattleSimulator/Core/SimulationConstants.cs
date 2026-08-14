namespace BattleSimulator.Core
{
    /// <summary>
    /// Shared defaults for the deterministic battle simulation layer.
    /// Rendering is intentionally allowed to run at a different frame rate.
    /// </summary>
    public static class SimulationConstants
    {
        public const int DefaultTickRate = 30;
        public const int DefaultMaxCatchUpTicksPerFrame = 5;
        public const int DefaultTargetFrameRate = 60;
    }
}
