using System;
using System.Collections.Generic;

namespace ValleyRail.Core
{
    public enum Cargo
    {
        Coal, Goods, Passengers, Wood, Oil, IronOre, Steel
    }
    public enum ProducerKind
    {
        Mine, Plant, Factory, Town, Forest, Sawmill, OilWells, Refinery, IronMine, SteelMill,
        /// <summary>A ski resort at the foot of a snowy peak (SkiResorts): it sends tourists home and takes tourists in.</summary>
        SkiResort
    }
    public enum ServiceState
    {
        Parked, Loading, Travelling, InsufficientFunds, InvalidRoute
    }
    [Serializable]
    public struct Cell : IEquatable<Cell>
    {
        public int x, z;
        public Cell(int x, int z)
        {
            this.x = x;
            this.z = z;
        }
        public int Key => z * MapDefinition.Size + x;
        public static Cell FromKey(int key) => new Cell(key % MapDefinition.Size, key / MapDefinition.Size);
        public bool Equals(Cell b) => x == b.x && z == b.z;
        public override bool Equals(object o) => o is Cell c && Equals(c);
        public override int GetHashCode() => Key;
        public int Distance(Cell b) => Math.Abs(x - b.x) + Math.Abs(z - b.z);
        public Cell Move(int d) => new Cell(x + Directions.Dx[d], z + Directions.Dz[d]);
        public override string ToString() => $"{x}, {z}";
    }
    public static class Directions
    {
        public static readonly int[] Dx = { 0, 1, 0, -1 }, Dz = { 1, 0, -1, 0 };
        public static int Opp(int d) => (d + 2) % 4;
        public static int Between(Cell a, Cell b)
        {
            for (int d = 0; d < 4; d++)
                if (a.Move(d).Equals(b))
                    return d;
            throw new ArgumentException("Cells must be adjacent");
        }
        public static int Count(int mask)
        {
            int n = 0;
            for (int d = 0; d < 4; d++)
                if ((mask & (1 << d)) != 0)
                    n++;
            return n;
        }
        public static bool Straight(int mask) => mask == 5 || mask == 10;
        public static bool Allows(int mask, int entry, int exit)
        {
            if (entry == exit || (mask & (1 << entry)) == 0 || (mask & (1 << exit)) == 0)
                return false;
            if (mask == 15)
                return true; // Four-way crossing: straight across either line, or turn onto the other one.
            if (Count(mask) == 2)
                return true;
            // The port opposite the missing arm is the common turnout stem.
            int missing = 0;
            while ((mask & (1 << missing)) != 0)
                missing++;
            int stem = Opp(missing);
            return entry == stem || exit == stem;
        }
    }
    // Model IDs are persisted in saves. Append new models; never reorder existing IDs.
    public static class TrainCatalog
    {
        public const int Count = 6;
        public static string Name(int model)
        {
            switch (model)
            {
                case 0: return "Small freight";
                case 1: return "Fast freight";
                case 2: return "Passenger";
                case 3: return "Heavy freight";
                case 4: return "Commuter";
                case 5: return "Express passenger";
                default: throw new ArgumentOutOfRangeException(nameof(model));
            }
        }
        public static bool SupportsCargo(int model, Cargo cargo)
        {
            if (model < 0 || model >= Count || cargo < Cargo.Coal || cargo > Cargo.Steel)
                return false;
            bool passenger = model == 2 || model == 4 || model == 5;
            return passenger == (cargo == Cargo.Passengers);
        }
        /// <summary>Wagons a player may put behind one locomotive when buying or rebuilding a train.</summary>
        public const int MinWagons = 1, MaxWagons = 8;
        static readonly int[] StandardWagons = { 2, 3, 3, 5, 1, 4 };
        /// <summary>Wagons in the model's standard consist, the length the balance prices and capacities describe. 0 for an unknown model.</summary>
        public static int DefaultWagons(int model) => model >= 0 && model < Count ? StandardWagons[model] : 0;
        /// <summary>Commuter and express sets carry passengers in their cab cars too, and end in a second cab.</summary>
        public static bool MultipleUnit(int model) => model == 4 || model == 5;
        /// <summary>The train's wagon count. Trains from saves written before wagons could be chosen carry 0 and run the standard consist.</summary>
        public static int Wagons(TrainState t) => t.wagons > 0 ? t.wagons : DefaultWagons(t.model);
    }
    [Serializable]
    public class Balance
    {
        public int startingMoney = 50000, straightCost = 100, curveCost = 120, junctionCost = 200, bridgeCost = 1500, stationCost = 2000, treeClearCost = 50;
        public int[] trainPrice = { 8000, 14000, 10000, 22000, 6000, 26000 }, capacity = { 30, 45, 40, 90, 24, 64 }, speed = { 2000, 3000, 3000, 1500, 2500, 4500 }, runningCost = { 20, 35, 25, 50, 12, 65 };
        public int[] production = { 20, 15, 20 }, rate = { 2, 3, 2 };
        public int storage = 200, dwellTicks = 60;
        // Existing balance assets contain only the original three cargo entries.
        public int CargoRate(Cargo cargo) => (int)cargo < rate.Length ? rate[(int)cargo] : cargo == Cargo.Steel ? 4 : 3;
        public int CargoProduction(Cargo cargo) => (int)cargo < production.Length ? production[(int)cargo] : 18;
        public CityBalance city = new CityBalance();
        public int PieceCost(int mask) => mask == 15 ? junctionCost * 2 : Directions.Count(mask) == 3 ? junctionCost : Directions.Straight(mask) ? straightCost : curveCost;
        // The arrays above describe each model's standard consist (TrainCatalog.DefaultWagons). Half the price and running
        // cost belong to the locomotive; the other half is shared by the wagons, so every wagon added or removed changes
        // the train by one wagon's share. Speed does not depend on length.
        public int WagonPrice(int model) => (trainPrice[model] / (2 * TrainCatalog.DefaultWagons(model)) + 25) / 50 * 50;
        public int WagonRunningCost(int model) => Math.Max(1, (runningCost[model] + TrainCatalog.DefaultWagons(model)) / (2 * TrainCatalog.DefaultWagons(model)));
        public int WagonCapacity(int model)
        {
            // Multiple units seat passengers in the lead cab as well, so it counts as one of the carrying cars.
            int carrying = TrainCatalog.DefaultWagons(model) + (TrainCatalog.MultipleUnit(model) ? 1 : 0);
            return (capacity[model] + carrying / 2) / carrying;
        }
        public int TrainPrice(int model, int wagons) => Math.Max(WagonPrice(model), trainPrice[model] + (wagons - TrainCatalog.DefaultWagons(model)) * WagonPrice(model));
        public int Capacity(int model, int wagons) => Math.Max(1, capacity[model] + (wagons - TrainCatalog.DefaultWagons(model)) * WagonCapacity(model));
        public int RunningCost(int model, int wagons) => Math.Max(1, runningCost[model] + (wagons - TrainCatalog.DefaultWagons(model)) * WagonRunningCost(model));
        public int TrainPrice(TrainState t) => TrainPrice(t.model, TrainCatalog.Wagons(t));
        public int Capacity(TrainState t) => Capacity(t.model, TrainCatalog.Wagons(t));
        public int RunningCost(TrainState t) => RunningCost(t.model, TrainCatalog.Wagons(t));
    }
    [Serializable]
    public class TrackPieceState
    {
        public int id, mask, paid, bridge; public Cell cell;
    }
    [Serializable]
    public class StationState
    {
        public int id, producerId, paid, side = 1; public string name; public Cell cell; public int axis;
        /// <summary>Cells per platform and parallel platform tracks (StationLayout). Saves without them load as 0: the original 3 × 1 station.</summary>
        public int length, platforms;
        /// <summary>Rung on the station ladder (StationCatalog): 0 Halt / Freight halt in saves from before upgrades existed.</summary>
        public int level;
    }
    [Serializable]
    public class ProducerState
    {
        public int id; public string name; public Cell cell; public ProducerKind kind; public int inventory, remainder;
        // Towns produce and store passengers according to their population; industries use the balance values.
        public int production, storage;
        /// <summary>Freight a town holds for onward trains (CargoTransfer). Always empty for industries; older saves load empty.</summary>
        public List<TransferStock> transfers = new List<TransferStock>();
    }
    [Serializable]
    public class RailStep
    {
        public int trackId, entry, exit, length; public bool fromCenter, toCenter;
    }
    [Serializable]
    public class TrainState
    {
        public int id, model, stationId, a, b, destination, step, distance, units, origin, cargoDestination, dwell, costRemainder, moveRemainder;
        // Player-facing fleet number (1, 2, 3…). The id shares the world counter with tracks and stations.
        public int number;
        /// <summary>Which platform track of its current station the train stands at (StationLayout.Center). Older saves load as 0.</summary>
        public int platform;
        /// <summary>Wagons behind the locomotive (TrainCatalog.Wagons reads it). 0 in older saves means the model's standard consist.</summary>
        public int wagons;
        /// <summary>Transfer credit already paid for the cargo aboard (CargoTransfer); the final delivery pays the rest. Older saves load as 0.</summary>
        public int prepaid;
        /// <summary>This train's own income and running costs by month. Saves without it load with empty accounts.</summary>
        public TrainAccounts accounts = new TrainAccounts();
        public Cargo cargo; public ServiceState state; public bool returnToStation;
        public List<RailStep> path = new List<RailStep>(), returnPath = new List<RailStep>();
    }
    [Serializable]
    public struct LedgerEntry
    {
        public long tick; public int income, expense;
    }
    [Serializable]
    public class WorldState
    {
        public string mapId = "green-valley"; public int mapVersion = SaveMigration.CurrentMapVersion, nextId = 10, nextTrainNumber = 1, revision, speed = 1, money = 50000;
        public long tick, delivered, totalIncome, totalExpenses;
        public int tutorialStep = 1, cityRevision;
        // Town layout generation (CityLayout.Current). Older saves load as 0 and are re-laid out once.
        public int cityLayout;
        // Highway route generation (CitySimulation.RoadLayout). Older saves load as 0 and are straightened once.
        public int roadLayout;
        public float cameraX = 29, cameraZ = 18, zoom = 20;
        /// <summary>View direction in clockwise quarter turns (0-3). Saves without it load facing the original direction.</summary>
        public int cameraTurn;
        public List<TrackPieceState> tracks = new List<TrackPieceState>();
        public List<StationState> stations = new List<StationState>();
        public List<TrainState> trains = new List<TrainState>();
        public List<ProducerState> producers = new List<ProducerState>();
        public List<CityState> cities = new List<CityState>();
        public List<IntercityRoadState> intercityRoads = new List<IntercityRoadState>();
        /// <summary>Cells whose pine is gone (see <see cref="Scenery"/>). Saves without it keep the whole forest.</summary>
        public List<Cell> felledTrees = new List<Cell>();
        /// <summary>Bridge styles chosen per marked site (<see cref="BridgeCatalog"/>). Saves without it show each site's default.</summary>
        public List<BridgeStyleState> bridgeStyles = new List<BridgeStyleState>();
        /// <summary>Light rail lines to the stadiums, beaches and ski resorts (TramService). Saves without it load with none.</summary>
        public List<TramLineState> tramLines = new List<TramLineState>();
        public List<LedgerEntry> ledger = new List<LedgerEntry>(64);
        /// <summary>
        /// A what-if copy for planning: its own track and station lists, everything else shared with this world. Planners add
        /// pieces and stations to the copy and must never change the shared parts.
        /// </summary>
        public WorldState PlanningCopy()
        {
            var copy = (WorldState)MemberwiseClone();
            copy.tracks = new List<TrackPieceState>(tracks);
            copy.stations = new List<StationState>(stations);
            return copy;
        }
        public static WorldState New(Balance b)
        {
            var w = new WorldState { money = b.startingMoney, cameraX = 63, cameraZ = 58, zoom = 58, cityLayout = CityLayout.Current, roadLayout = CitySimulation.RoadLayout };
            w.producers.Add(new ProducerState { id = 1, name = "Pinecrest Mine", kind = ProducerKind.Mine, cell = new Cell(10, 12), inventory = 60 });
            w.producers.Add(new ProducerState { id = 2, name = "Eastbank Power", kind = ProducerKind.Plant, cell = new Cell(48, 12) });
            w.producers.Add(new ProducerState { id = 3, name = "Valley Works", kind = ProducerKind.Factory, cell = new Cell(12, 29), inventory = 45 });
            w.producers.Add(new ProducerState { id = 4, name = "Willowbrook", kind = ProducerKind.Town, cell = new Cell(16, 43), inventory = 40 });
            w.producers.Add(new ProducerState { id = 5, name = "Oakridge", kind = ProducerKind.Town, cell = new Cell(48, 43), inventory = 40 });
            w.producers.Add(new ProducerState { id = 6, name = "Westvale Power", kind = ProducerKind.Plant, cell = new Cell(20, 23) });
            w.producers.Add(new ProducerState { id = 7, name = "Riverside Power", kind = ProducerKind.Plant, cell = new Cell(42, 30) });
            w.producers.Add(new ProducerState { id = 8, name = "Pinewood Forest", kind = ProducerKind.Forest, cell = new Cell(8, 53), inventory = 54 });
            w.producers.Add(new ProducerState { id = 9, name = "Westwood Sawmill", kind = ProducerKind.Sawmill, cell = new Cell(21, 53), inventory = 0 });
            w.producers.Add(new ProducerState { id = 10, name = "Eastbank Oil Wells", kind = ProducerKind.OilWells, cell = new Cell(39, 7), inventory = 54 });
            w.producers.Add(new ProducerState { id = 11, name = "Riverside Refinery", kind = ProducerKind.Refinery, cell = new Cell(50, 23), inventory = 0 });
            w.producers.Add(new ProducerState { id = 12, name = "Highland Iron Mine", kind = ProducerKind.IronMine, cell = new Cell(40, 55), inventory = 54 });
            w.producers.Add(new ProducerState { id = 13, name = "Oakridge Steel Mill", kind = ProducerKind.SteelMill, cell = new Cell(53, 55), inventory = 0 });
            w.producers.Add(new ProducerState { id = 14, name = "Sunvale", kind = ProducerKind.Town, cell = new Cell(78, 28), inventory = 40 });
            w.producers.Add(new ProducerState { id = 15, name = "Frostford", kind = ProducerKind.Town, cell = new Cell(105, 91), inventory = 40 });
            w.producers.Add(new ProducerState { id = 16, name = "Lakewood", kind = ProducerKind.Town, cell = new Cell(19, 96), inventory = 40 });
            w.producers.Add(new ProducerState { id = 17, name = "Northwood Forest", kind = ProducerKind.Forest, cell = new Cell(12, 78), inventory = 54 });
            w.producers.Add(new ProducerState { id = 18, name = "Lakeside Sawmill", kind = ProducerKind.Sawmill, cell = new Cell(45, 88), inventory = 0 });
            w.producers.Add(new ProducerState { id = 19, name = "Sunvale Oil Wells", kind = ProducerKind.OilWells, cell = new Cell(107, 25), inventory = 54 });
            w.producers.Add(new ProducerState { id = 20, name = "Desert Refinery", kind = ProducerKind.Refinery, cell = new Cell(87, 54), inventory = 0 });
            w.producers.Add(new ProducerState { id = 21, name = "Alpine Iron Mine", kind = ProducerKind.IronMine, cell = new Cell(76, 108), inventory = 54 });
            w.producers.Add(new ProducerState { id = 22, name = "Northern Steel Mill", kind = ProducerKind.SteelMill, cell = new Cell(106, 111), inventory = 0 });
            w.nextId = 30;
            foreach (var p in w.producers)
                if (p.kind == ProducerKind.Town)
                    CitySimulation.Found(w, p, b);
            return w;
        }
    }
    public static class MapDefinition
    {
        public const int Size = 128;
        public static readonly string[] SurfaceNames = { "Grassland", "Woodland", "Dry plains", "Alpine meadow", "Rocky hills", "Snowy peaks", "Shore", "Water", "Beach" };
        public static readonly int[] BridgeRows = { 15, 46, 78, 110 };
        public static bool InBounds(Cell c) => c.x >= 0 && c.z >= 0 && c.x < Size && c.z < Size;
        public static bool Water(Cell c) => (c.x >= 30 && c.x <= 32) ||
            ((c.x - 54) * (c.x - 54) / 49f + (c.z - 76) * (c.z - 76) / 36f < 1f);
        public static int Bridge(Cell c) => c.x >= 30 && c.x <= 32 && Array.IndexOf(BridgeRows, c.z) >= 0 ? c.z : 0;
        // Keep the original valley flat. Raised terrain lives entirely in the expansion.
        static float Hill(float x, float z, float cx, float cz, float radius, float height)
        {
            float dx = x - cx, dz = z - cz;
            double d = Math.Sqrt(dx * dx + dz * dz) / radius;
            return d >= 1 ? 0 : (float)((1 - d) * (1 - d) * height);
        }
        public static float Height(float x, float z) =>
            Hill(x, z, 80, 82, 13, 10) + Hill(x, z, 107, 67, 11, 8) +
            Hill(x, z, 56, 112, 12, 12) + Hill(x, z, 91, 121, 7, 6);
        public static bool Raised(Cell c) => Height(c.x - .5f, c.z - .5f) > .025f ||
            Height(c.x + .5f, c.z - .5f) > .025f || Height(c.x - .5f, c.z + .5f) > .025f ||
            Height(c.x + .5f, c.z + .5f) > .025f;
        // 0 meadow, 1 woodland, 2 dry plains, 3 alpine, 4 rock, 5 snow, 6 shore, 7 water, 8 beach (sand by the sea, see Coast).
        public static int Surface(Cell c) => Coast.Beach(c) && !Water(c) && !Raised(c) ? 8 : Terrain(c);
        /// <summary>The surface as the map had it before the coast got its beach. The pine scatter reads this, so no tree moves.</summary>
        public static int Terrain(Cell c)
        {
            if (Water(c)) return 7;
            float height = Height(c.x, c.z);
            if (height > 5 || (c.z > 100 && height > 2)) return 5;
            if (Raised(c)) return 4;
            if (Water(c.Move(0)) || Water(c.Move(1)) || Water(c.Move(2)) || Water(c.Move(3))) return 6;
            if (c.z >= 98) return 3;
            if (c.x >= 67 && c.z < 74) return 2;
            if (c.z >= 65 && c.x < 64) return 1;
            return 0;
        }
        public static bool Blocked(Cell c, WorldState w)
        {
            if (!InBounds(c))
                return true;
            // Industries block their 3×3 footprint and ski resorts their 5×5 base; towns block through their buildings and plaza instead (CitySimulation).
            foreach (var p in w.producers)
                if (p.kind != ProducerKind.Town && Math.Abs(c.x - p.cell.x) <= Reach(p.kind) && Math.Abs(c.z - p.cell.z) <= Reach(p.kind))
                    return true;
            return Raised(c) || (c.x < 6 && c.z > 49 && c.z < 64) || (c.x > 55 && c.x < 64 && c.z < 7);
        }
        /// <summary>How many cells either side of its cell a non-town producer blocks: 1 for an industry, SkiResorts.Half for a resort.</summary>
        public static int Reach(ProducerKind kind) => kind == ProducerKind.SkiResort ? SkiResorts.Half : 1;
        public static bool Produces(ProducerKind p, Cargo c) => IndustryCatalog.Output(p) == c;
        public static bool Accepts(ProducerKind p, Cargo c) =>
            (p == ProducerKind.Plant && c == Cargo.Coal) ||
            (p == ProducerKind.Town && (c == Cargo.Goods || c == Cargo.Passengers)) ||
            (p == ProducerKind.Factory && c == Cargo.Steel) ||
            (p == ProducerKind.Sawmill && c == Cargo.Wood) ||
            (p == ProducerKind.Refinery && c == Cargo.Oil) ||
            (p == ProducerKind.SteelMill && c == Cargo.IronOre) ||
            (p == ProducerKind.SkiResort && c == Cargo.Passengers);
    }
    public static class IndustryCatalog
    {
        public static Cargo? Output(ProducerKind kind)
        {
            switch (kind)
            {
                case ProducerKind.Mine: return Cargo.Coal;
                case ProducerKind.Factory: case ProducerKind.Sawmill: case ProducerKind.Refinery: return Cargo.Goods;
                case ProducerKind.Town: case ProducerKind.SkiResort: return Cargo.Passengers;
                case ProducerKind.Forest: return Cargo.Wood;
                case ProducerKind.OilWells: return Cargo.Oil;
                case ProducerKind.IronMine: return Cargo.IronOre;
                case ProducerKind.SteelMill: return Cargo.Steel;
                default: return null;
            }
        }
        public static bool Processing(ProducerKind kind) => kind == ProducerKind.Sawmill || kind == ProducerKind.Refinery || kind == ProducerKind.SteelMill;
        public static string CargoName(Cargo cargo) => cargo == Cargo.IronOre ? "Iron ore" : cargo.ToString();
        public static string Inputs(ProducerKind kind)
        {
            switch (kind)
            {
                case ProducerKind.Plant: return "Coal";
                case ProducerKind.Town: return "Goods, passengers";
                case ProducerKind.Factory: return "Steel";
                case ProducerKind.Sawmill: return "Wood";
                case ProducerKind.Refinery: return "Oil";
                case ProducerKind.SteelMill: return "Iron ore";
                case ProducerKind.SkiResort: return "Passengers";
                default: return "Nothing";
            }
        }
        public static string Caption(ProducerKind kind)
        {
            var output = Output(kind);
            return Inputs(kind) + " > " + (output.HasValue ? CargoName(output.Value) : "Power");
        }
    }
    public class Result
    {
        public bool ok; public string message; public int id;
        public static Result Good(string m, int id = 0) => new Result { ok = true, message = m, id = id };
        public static Result Fail(string m) => new Result { message = m };
    }
}
