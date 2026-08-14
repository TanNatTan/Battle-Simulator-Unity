using UnityEngine;
using BattleSimulator.Presentation;
using BattleSimulator.Simulation;

namespace BattleSimulator.Core
{
    /// <summary>
    /// Creates the persistent battle-simulation runtime before the first scene loads.
    /// This keeps simulation services independent from scene-specific presentation.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class BattleBootstrap : MonoBehaviour
    {
        public static BattleBootstrap Instance { get; private set; }

        public SimulationClock SimulationClock { get; private set; }
        public BattleSimulationHost SimulationHost { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureRuntimeExists()
        {
            if (UnityEngine.Object.FindFirstObjectByType<BattleBootstrap>() != null)
            {
                return;
            }

            var runtimeObject = new GameObject("[Battle Simulator Runtime]");
            DontDestroyOnLoad(runtimeObject);
            runtimeObject.AddComponent<BattleBootstrap>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            SimulationClock = GetComponent<SimulationClock>();
            if (SimulationClock == null)
            {
                SimulationClock = gameObject.AddComponent<SimulationClock>();
            }

            SimulationHost = GetComponent<BattleSimulationHost>();
            if (SimulationHost == null) SimulationHost = gameObject.AddComponent<BattleSimulationHost>();
            if (GetComponent<BattleObserverView>() == null) gameObject.AddComponent<BattleObserverView>();

            Application.targetFrameRate = SimulationConstants.DefaultTargetFrameRate;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
