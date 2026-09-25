using System.Collections.Generic;
namespace ValleyRail.Core
{
    public enum GameEventKind : byte
    {
        None, StationBuilt, TrackBuilt, Bulldozed, TrainPurchased, TrainSold, RouteCreated, TrainDeparted, TrainArrived, CargoLoaded, CargoDelivered, StationUpgraded
    }
    /// <summary>
    /// A gameplay fact pushed by the service that knows it. <c>cell</c> locates it on the map; <c>aux</c> and <c>value</c>
    /// depend on the kind (CargoDelivered: units and revenue; TrackBuilt: the first path cell's key and the cost;
    /// Bulldozed: 0 track, 1 station, 2 town building, 3 tree; TrainPurchased: the model).
    /// </summary>
    public struct GameEvent
    {
        public GameEventKind kind; public int id, aux, value; public Cell cell;
    }
    /// <summary>
    /// Bounded queue the presentation drains each frame, following the CitySimulation.Notifications pattern. It is
    /// pre-sized and drops the oldest entry when full, so an undrained queue (tests, headless soaks) never allocates.
    /// Events are not state: they live on GameSession and are never saved.
    /// </summary>
    public sealed class GameEvents
    {
        public const int Capacity = 64;
        readonly Queue<GameEvent> queue = new Queue<GameEvent>(Capacity);
        public int Count => queue.Count;
        public void Push(GameEventKind kind, int id, Cell cell, int aux = 0, int value = 0)
        {
            if (queue.Count >= Capacity)
                queue.Dequeue();
            queue.Enqueue(new GameEvent { kind = kind, id = id, cell = cell, aux = aux, value = value });
        }
        public bool TryDequeue(out GameEvent e)
        {
            if (queue.Count == 0)
            {
                e = default;
                return false;
            }
            e = queue.Dequeue();
            return true;
        }
        public void Clear() => queue.Clear();
    }
}
