using System;
using UnityEngine;

namespace BattleSimulator.Core
{
    /// <summary>
    /// Drives battle logic at a fixed tick rate that is independent from rendering FPS.
    /// Heavy simulation systems will eventually subscribe to this clock or run from
    /// DOTS system groups fed by the same tick cadence.
    /// </summary>
    [DefaultExecutionOrder(-900)]
    public sealed class SimulationClock : MonoBehaviour
    {
        [SerializeField, Min(1)]
        private int tickRate = SimulationConstants.DefaultTickRate;

        [SerializeField, Range(1, 16)]
        private int maxCatchUpTicksPerFrame = SimulationConstants.DefaultMaxCatchUpTicksPerFrame;

        private double accumulator;
        private double elapsedSimulationTime;
        private ulong currentTick;
        private bool isPaused;
        private float simulationSpeed = 1f;

        public event Action<SimulationStep> Tick;

        public int TickRate => tickRate;
        public float TickDeltaTime => 1f / tickRate;
        public ulong CurrentTick => currentTick;
        public double ElapsedSimulationTime => elapsedSimulationTime;
        public bool IsPaused => isPaused;
        public float SimulationSpeed => simulationSpeed;

        private void Update()
        {
            if (isPaused || simulationSpeed <= 0f)
            {
                return;
            }

            double tickDelta = 1d / tickRate;
            accumulator += Time.unscaledDeltaTime * simulationSpeed;

            // Prevent a stalled render frame from creating an unbounded simulation spiral.
            double maximumBacklog = tickDelta * maxCatchUpTicksPerFrame;
            if (accumulator > maximumBacklog)
            {
                accumulator = maximumBacklog;
            }

            int processedTicks = 0;
            while (accumulator >= tickDelta && processedTicks < maxCatchUpTicksPerFrame)
            {
                accumulator -= tickDelta;
                currentTick++;
                elapsedSimulationTime += tickDelta;
                processedTicks++;

                Tick?.Invoke(new SimulationStep(
                    currentTick,
                    (float)tickDelta,
                    elapsedSimulationTime));
            }
        }

        public void SetPaused(bool paused)
        {
            isPaused = paused;
        }

        public void SetSimulationSpeed(float speed)
        {
            simulationSpeed = Mathf.Clamp(speed, 0f, 16f);
        }

        public void ResetClock()
        {
            accumulator = 0d;
            elapsedSimulationTime = 0d;
            currentTick = 0;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            tickRate = Mathf.Max(1, tickRate);
            maxCatchUpTicksPerFrame = Mathf.Clamp(maxCatchUpTicksPerFrame, 1, 16);
        }
#endif
    }
}
