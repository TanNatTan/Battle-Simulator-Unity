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
        public const float DefaultWorldWidth = 1920f;
        public const float DefaultWorldHeight = 1080f;
        public const float ContactGraceSeconds = 8f;
        public const float SpatialCellSize = 64f;
        public static readonly float[] SupportedSpeeds = { 1f, 2f, 4f, 8f };
    }
}
