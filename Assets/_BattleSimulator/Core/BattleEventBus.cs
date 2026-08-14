using System;
using System.Collections.Generic;
using UnityEngine;

namespace BattleSimulator.Core
{
    public enum BattleEventType
    {
        UnitCreated,
        UnitWounded,
        UnitKilled,
        BuildingConstructed,
        BuildingDestroyed,
        TerritoryCaptured,
        ResourceDepleted,
        EnemySpotted,
        ConvoyAttacked,
        BaseAttacked,
        BattleEnded
    }

    public readonly struct BattleEvent
    {
        public BattleEvent(BattleEventType type, double time, int entityId = 0, int playerId = 0, Vector2 position = default, string message = null)
        {
            Type = type;
            Time = time;
            EntityId = entityId;
            PlayerId = playerId;
            Position = position;
            Message = message ?? string.Empty;
        }

        public BattleEventType Type { get; }
        public double Time { get; }
        public int EntityId { get; }
        public int PlayerId { get; }
        public Vector2 Position { get; }
        public string Message { get; }
    }

    public sealed class BattleEventBus
    {
        private readonly Queue<BattleEvent> history = new Queue<BattleEvent>();
        private readonly int capacity;

        public BattleEventBus(int capacity = 2048)
        {
            this.capacity = Mathf.Max(32, capacity);
        }

        public event Action<BattleEvent> Published;

        public IEnumerable<BattleEvent> History => history;

        public void Publish(BattleEvent battleEvent)
        {
            history.Enqueue(battleEvent);
            while (history.Count > capacity)
            {
                history.Dequeue();
            }

            Published?.Invoke(battleEvent);
        }

        public void Clear()
        {
            history.Clear();
        }
    }
}
