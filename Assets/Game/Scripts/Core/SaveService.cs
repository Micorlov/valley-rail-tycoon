using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
namespace ValleyRail.Core
{
    public interface ISnapshotCodec
    {
        string Encode<T>(T value); T Decode<T>(string json);
    }
    [Serializable]
    public class SaveEnvelope
    {
        public int schemaVersion = 1; public string payload, checksum;
    }
    public sealed class SaveService
    {
        readonly string folder; readonly ISnapshotCodec codec; readonly Balance balance;
        public SaveService(string folder, ISnapshotCodec codec, Balance balance)
        {
            this.folder = folder;
            this.codec = codec;
            this.balance = balance;
        }
        string PathFor(bool auto) => Path.Combine(folder, auto ? "autosave.json" : "manual.json");
        public bool Exists(bool auto, bool backup = false) => File.Exists(PathFor(auto) + (backup ? ".bak" : ""));
        public string CaptureSnapshot(WorldState w)
        {
            Validate(w, balance);
            string payload = codec.Encode(w);
            return codec.Encode(new SaveEnvelope { payload = payload, checksum = Hash(payload) });
        }
        public WorldState RestoreSnapshot(string json)
        {
            var envelope = codec.Decode<SaveEnvelope>(json);
            if (envelope == null || envelope.schemaVersion != 1)
                throw new InvalidDataException("Unsupported save schema. The file has not been changed.");
            if (envelope.payload == null || envelope.checksum != Hash(envelope.payload))
                throw new InvalidDataException("Save checksum failed. Try the backup.");
            var state = codec.Decode<WorldState>(envelope.payload);
            SaveMigration.Upgrade(state, balance);
            Validate(state, balance);
            return state;
        }
        public void Save(WorldState w, bool auto)
        {
            Directory.CreateDirectory(folder);
            string path = PathFor(auto), temp = path + ".tmp";
            string json = CaptureSnapshot(w);
            using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                byte[] data = Encoding.UTF8.GetBytes(json);
                stream.Write(data, 0, data.Length);
                stream.Flush(true);
            }
            RestoreSnapshot(File.ReadAllText(temp));
            if (File.Exists(path))
            {
                string previous = File.ReadAllText(path);
                bool valid = false;
                try
                {
                    var envelope = codec.Decode<SaveEnvelope>(previous);
                    if (envelope != null && envelope.schemaVersion != 1)
                        throw new NotSupportedException("Refusing to overwrite a newer save schema.");
                    if (envelope?.payload != null)
                    {
                        var state = codec.Decode<WorldState>(envelope.payload);
                        if (state != null && (state.mapId != "green-valley" || state.mapVersion > SaveMigration.CurrentMapVersion))
                            throw new NotSupportedException("Refusing to overwrite an incompatible map save.");
                    }
                    RestoreSnapshot(previous);
                    valid = true;
                }
                catch (NotSupportedException) { File.Delete(temp); throw; }
                catch (Exception) { /* Keep the last known-good backup when the primary is corrupt. */ }
                File.Replace(temp, path, valid ? path + ".bak" : null);
            }
            else
                File.Move(temp, path);
        }
        public WorldState Load(bool auto, bool backup = false) => RestoreSnapshot(File.ReadAllText(PathFor(auto) + (backup ? ".bak" : "")));
        public static string Hash(string text)
        {
            using (var sha = SHA256.Create())
                return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(text)));
        }
        public static void Validate(WorldState w, Balance b)
        {
            if (w == null || w.mapId != "green-valley" || (w.mapVersion < 1 || w.mapVersion > SaveMigration.CurrentMapVersion))
                throw new InvalidDataException("Map version is incompatible.");
            if (w.tracks == null || w.stations == null || w.trains == null || w.producers == null || w.ledger == null || w.cities == null)
                throw new InvalidDataException("Missing save data.");
            if (w.cityLayout < 0 || w.cityLayout > CityLayout.Current)
                throw new InvalidDataException("Town layout version is incompatible.");
            if (w.roadLayout < 0 || w.roadLayout > CitySimulation.RoadLayout)
                throw new InvalidDataException("Highway layout version is incompatible.");
            if (w.money < 0 || w.tick < 0 || w.tracks.Count > 1500 || w.stations.Count > 24 || w.trains.Count > 12 || w.producers.Count < MapProducers(w) || w.producers.Count > MapProducers(w) + (w.mapVersion >= 5 ? CitySimulation.FoundedLimit + SkiResorts.Count : 0))
                throw new InvalidDataException("Invalid world limits.");
            if (w.speed != 0 && w.speed != 1 && w.speed != 2 && w.speed != 4)
                throw new InvalidDataException("Invalid simulation speed.");
            if (float.IsNaN(w.cameraX) || float.IsInfinity(w.cameraX) || float.IsNaN(w.cameraZ) || float.IsInfinity(w.cameraZ) || float.IsNaN(w.zoom) || float.IsInfinity(w.zoom) || w.cameraTurn < 0 || w.cameraTurn > 3)
                throw new InvalidDataException("Invalid camera.");
            if (w.felledTrees != null)
            {
                var felled = new HashSet<int>();
                foreach (var c in w.felledTrees)
                    if (!MapDefinition.InBounds(c) || !felled.Add(c.Key))
                        throw new InvalidDataException("Invalid felled tree.");
            }
            var ids = new HashSet<int>();
            int max = 0;
            Action<int> check = id => { if (id <= 0 || !ids.Add(id)) throw new InvalidDataException("Duplicate entity ID."); max = Math.Max(max, id); };
            var defaults = WorldState.New(b);
            int mapProducers = 0;
            foreach (var p in w.producers)
            {
                check(p.id);
                var original = defaults.producers.Find(a => a.id == p.id);
                // Producers past the map's own are towns settlers founded (CitySimulation.Founding).
                if (original != null)
                    mapProducers++;
                if (original == null ? !(CitySimulation.FoundedSite(w, p) || SkiResorts.Valid(w, p)) : (w.mapVersion < 3 && p.id > 5) || (w.mapVersion < 4 && p.id > 7) || (w.mapVersion < 5 && p.id > 13) || original.kind != p.kind || !original.cell.Equals(p.cell))
                    throw new InvalidDataException("Invalid producer.");
                if (p.inventory < 0 || p.inventory > (p.kind == ProducerKind.Town ? p.storage : b.storage) || p.remainder < 0 || p.remainder >= 1200)
                    throw new InvalidDataException("Invalid producer.");
                if (!CargoTransfer.Valid(p, w.producers, b.storage))
                    throw new InvalidDataException("Invalid transfer stock.");
            }
            if (mapProducers != MapProducers(w))
                throw new InvalidDataException("Invalid producer.");
            var cells = new HashSet<int>();
            foreach (var t in w.tracks)
            {
                check(t.id);
                if (!MapDefinition.InBounds(t.cell) || MapDefinition.Blocked(t.cell, w) || !cells.Add(t.cell.Key) || t.mask < 1 || t.mask > 15 || Directions.Count(t.mask) < 2 || Directions.Count(t.mask) > 4 || t.paid < 0 || t.bridge != MapDefinition.Bridge(t.cell) || (MapDefinition.Water(t.cell) && (t.bridge == 0 || t.mask != 10)))
                    throw new InvalidDataException("Invalid track.");
            }
            if (w.bridgeStyles != null)
            {
                var styled = new HashSet<int>();
                foreach (var s in w.bridgeStyles)
                    if (s == null || !BridgeCatalog.Site(s.row) || !BridgeCatalog.Valid(s.style) || !styled.Add(s.row))
                        throw new InvalidDataException("Invalid bridge style.");
            }
            var net = new RailNetwork(w);
            var cities = new CitySimulation(w, net, b);
            ValidateCities(w, b, net, check);
            foreach (var t in w.tracks)
                if (cities.BlocksTrack(t.cell))
                    throw new InvalidDataException("Track through a town building.");
            var stationCells = new HashSet<int>();
            foreach (var t in w.tracks)
                if (t.bridge != 0)
                    for (int x = 30; x <= 32; x++)
                        if (net.At(new Cell(x, t.bridge)) == null)
                            throw new InvalidDataException("Incomplete bridge.");
            foreach (var s in w.stations)
            {
                check(s.id);
                if (s.axis < 0 || s.axis > 1 || s.side < 0 || s.side > 3 || s.side % 2 == s.axis || s.paid < 0 || !StationLayout.ValidStored(s) || !w.producers.Exists(p => p.id == s.producerId))
                    throw new InvalidDataException("Invalid station.");
                if (!StationCatalog.ValidLevel(w.producers.Find(p => p.id == s.producerId).kind, s.level))
                    throw new InvalidDataException("Invalid station type.");
                foreach (var c in StationLayout.Cells(s))
                {
                    var track = net.At(c);
                    if (track == null || track.mask != StationLayout.Mask(s.axis) || track.bridge != 0 || !stationCells.Add(c.Key))
                        throw new InvalidDataException("Invalid station platform.");
                }
                if (!new StationService(w, net, b, cities).Nearby(s).Exists(p => p.id == s.producerId))
                    throw new InvalidDataException("Station outside catchment.");
            }
            var occupied = new HashSet<int>();
            var finder = new RailPathfinder(net);
            foreach (var t in w.trains)
            {
                check(t.id);
                var station = w.stations.Find(s => s.id == t.stationId);
                if (station == null || !TrainCatalog.SupportsCargo(t.model, t.cargo) || t.wagons < 0 || t.wagons > TrainCatalog.MaxWagons || t.units < 0 || t.units > b.Capacity(t) || t.costRemainder < 0 || t.costRemainder >= 1200 || t.moveRemainder < 0 || t.moveRemainder >= 20 || t.prepaid < 0 || !Enum.IsDefined(typeof(ServiceState), t.state) || t.path == null || t.returnPath == null)
                    throw new InvalidDataException("Invalid train.");
                if (!TrainAccounts.Valid(t.accounts, w.tick))
                    throw new InvalidDataException("Invalid train accounts.");
                if (t.platform < 0 || t.platform >= StationLayout.Platforms(station))
                    throw new InvalidDataException("Invalid train platform.");
                if (!occupied.Add(net.components[net.At(StationLayout.Center(station, t.platform)).id]))
                    throw new InvalidDataException("Multiple trains in a railway network.");
                if (t.units > 0 && (!w.producers.Exists(p => p.id == t.origin && MapDefinition.Produces(p.kind, t.cargo)) || !w.producers.Exists(p => p.id == t.cargoDestination && (MapDefinition.Accepts(p.kind, t.cargo) || CargoTransfer.Holds(p.kind, t.cargo))) || t.origin == t.cargoDestination))
                    throw new InvalidDataException("Invalid cargo manifest.");
                if (t.a == 0 && t.b == 0)
                {
                    if (t.state != ServiceState.Parked || t.path.Count != 0 || t.returnPath.Count != 0)
                        throw new InvalidDataException("Unrouted train is moving.");
                    continue;
                }
                var a = w.stations.Find(s => s.id == t.a);
                var z = w.stations.Find(s => s.id == t.b);
                if (a == null || z == null || a.id == z.id || (t.destination != t.a && t.destination != t.b))
                    throw new InvalidDataException("Invalid route endpoints.");
                var from = t.destination == a.id ? z : a;
                var to = t.destination == a.id ? a : z;
                ValidatePath(t.path, from, to, net);
                ValidatePath(t.returnPath, to, from, net);
                if (t.step < 0 || t.step >= t.path.Count || t.distance < 0 || t.distance > t.path[t.step].length || t.dwell < 0 || t.dwell > b.dwellTicks)
                    throw new InvalidDataException("Invalid train progress.");
            }
            TramLines.Validate(w, b, check);
            if (w.nextId <= max)
                throw new InvalidDataException("Invalid next entity ID.");
        }
        /// <summary>The map's own producers at this map version; founded towns come on top.</summary>
        static int MapProducers(WorldState w) => w.mapVersion >= 5 ? CitySimulation.MapProducers : w.mapVersion >= 4 ? 13 : w.mapVersion >= 3 ? 7 : 5;
        static void ValidateCities(WorldState w, Balance b, RailNetwork net, Action<int> check)
        {
            int towns = 0;
            foreach (var p in w.producers)
                if (p.kind == ProducerKind.Town)
                    towns++;
            if (w.cities.Count != towns)
                throw new InvalidDataException("Every town needs exactly one city.");
            var cells = new HashSet<int>();
            var owners = new HashSet<int>();
            foreach (var city in w.cities)
            {
                check(city.id);
                var p = w.producers.Find(x => x.id == city.producerId);
                if (p == null || p.kind != ProducerKind.Town || !owners.Add(p.id) || !city.center.Equals(p.cell) || city.buildings == null || city.roads == null || city.cleared == null || city.paxIn == null || city.paxOut == null || city.goodsIn == null || city.paxIn.Length != 4 || city.paxOut.Length != 4 || city.goodsIn.Length != 4 || city.bucket < 0 || city.bucket > 3 || city.growthPoints < 0 || city.blockedEvaluations < 0 || city.buildings.Count > b.city.maxBuildings || city.roads.Count > b.city.maxRoads || !city.roads.Exists(r => r.cell.Equals(p.cell)))
                    throw new InvalidDataException("Invalid city.");
                // Only a founded town carries its founding tick.
                if (city.founded < 0 || city.founded > w.tick || (city.founded > 0) != CitySimulation.Founded(p))
                    throw new InvalidDataException("Invalid city.");
                if (city.fund < 0 || city.fund > b.city.maxFund)
                    throw new InvalidDataException("Invalid town fund.");
                if (city.tram < 0 || city.tram > w.tick)
                    throw new InvalidDataException("Invalid town tram.");
                for (int i = 0; i < 4; i++)
                    if (city.paxIn[i] < 0 || city.paxOut[i] < 0 || city.goodsIn[i] < 0)
                        throw new InvalidDataException("Invalid city statistics.");
                foreach (var bs in city.buildings)
                {
                    if (!BuildingCatalog.Valid(bs.def))
                        throw new InvalidDataException("Invalid building.");
                    // Every cell of a building's footprint, so 2×2 landmarks never overlap tracks, water or neighbours.
                    for (int i = 0; i < CityLayout.FootprintCells(bs); i++)
                    {
                        var cell = CityLayout.FootprintCell(bs, i);
                        if (!MapDefinition.InBounds(cell) || MapDefinition.Water(cell) || MapDefinition.Blocked(cell, w) || !cells.Add(cell.Key) || net.At(cell) != null || BuildService.StationFootprint(w, cell))
                            throw new InvalidDataException("Invalid building.");
                    }
                }
                foreach (var r in city.roads)
                    if (!MapDefinition.InBounds(r.cell) || MapDefinition.Water(r.cell) || MapDefinition.Blocked(r.cell, w) || !cells.Add(r.cell.Key) || BuildService.StationFootprint(w, r.cell))
                        throw new InvalidDataException("Invalid street.");
                CitySimulation.Derive(city, b, out int population, out int jobs, out var level, out int production, out int storage);
                if (city.population != population || city.jobs != jobs || city.level != level || p.production != production || p.storage != storage)
                    throw new InvalidDataException("City totals do not match its buildings.");
            }
            var pairs = new HashSet<string>();
            // Every highway cell, so no roadside service area stands on another road.
            var highways = new HashSet<int>();
            foreach (var road in w.intercityRoads)
                if (road?.path != null)
                    foreach (var cell in road.path)
                        highways.Add(cell.Key);
            foreach (var road in w.intercityRoads)
            {
                // A beach road runs from a town to a car park on the coast (Coast.Resort); its park stages follow the finished road.
                bool beach = road != null && road.ToBeach;
                // A ski road runs from a town to its resort's car park entrance (SkiResorts); b is the resort's id.
                bool ski = road != null && road.ToSki && w.producers.Exists(x => x.id == road.b && x.kind == ProducerKind.SkiResort);
                if (road == null || road.a == road.b || !owners.Contains(road.a) || !(beach || ski || owners.Contains(road.b)) ||
                    !pairs.Add(Math.Min(road.a, road.b) + ":" + Math.Max(road.a, road.b)) ||
                    road.path == null || road.path.Count < 2 || road.path.Count > MapDefinition.Size * MapDefinition.Size ||
                    road.built < 1 || road.built > road.path.Count || road.diversionRemainder < 0 || road.diversionRemainder >= 100 ||
                    road.park < 0 || road.park > (beach && road.Complete ? Coast.ParkSteps : 0) ||
                    road.service < 0 || road.service > (!beach && !ski && road.Complete ? Roadside.Steps : 0) ||
                    road.serviceKind < 0 || road.serviceKind >= Roadside.Kinds || (road.serviceOwned && !Roadside.Open(road)) || road.serviceEarned < 0 ||
                    road.camp < 0 || road.camp > (!ski && road.Complete ? Campsites.Steps : 0) ||
                    road.campKind < 0 || road.campKind >= Campsites.Kinds)
                    throw new InvalidDataException("Invalid intercity road.");
                var routeCells = new HashSet<int>();
                for (int i = 0; i < road.path.Count; i++)
                {
                    var cell = road.path[i];
                    if (!MapDefinition.InBounds(cell) || !routeCells.Add(cell.Key) || MapDefinition.Raised(cell) ||
                        (MapDefinition.Water(cell) && MapDefinition.Bridge(cell) == 0) || MapDefinition.Blocked(cell, w) ||
                        (i > 0 && cell.Distance(road.path[i - 1]) != 1) ||
                        w.cities.Exists(c => c.buildings.Exists(bs => CityLayout.Covers(bs, cell))))
                        throw new InvalidDataException("Invalid road corridor.");
                }
                if (beach)
                    ValidateBeachPark(w, net, road, cells);
                if (Roadside.Planned(road))
                    ValidateServiceArea(w, net, road, cells, highways);
                if (Campsites.Planned(road))
                    ValidateCampsite(w, net, road, cells, highways);
                foreach (int endpoint in new[] { road.a, road.b })
                {
                    if (endpoint == Coast.Resort)
                        continue;
                    if (ski && endpoint == road.b)
                    {
                        if (!road.path[road.path.Count - 1].Equals(SkiResorts.Entrance(w.producers.Find(x => x.id == road.b))))
                            throw new InvalidDataException("Ski road does not reach its resort.");
                        continue;
                    }
                    var center = w.producers.Find(p => p.id == endpoint).cell;
                    var cell = road.path[endpoint == road.a ? 0 : road.path.Count - 1];
                    // Highways join a street of their town; older ones end within two cells of its plaza.
                    bool onStreet = w.cities.Find(c => c.producerId == endpoint).roads.Exists(r => r.cell.Equals(cell));
                    if (!onStreet && Math.Max(Math.Abs(cell.x - center.x), Math.Abs(cell.z - center.z)) > 2)
                        throw new InvalidDataException("Road does not reach its town.");
                }
            }
        }
        /// <summary>True when the piece is the stopping cell of one of the station's platforms.</summary>
        static bool Stops(StationState s, int trackId, RailNetwork net) => net.ids.TryGetValue(trackId, out var piece) && StationLayout.IsStop(s, piece.cell);
        /// <summary>A beach road ends on the car park's entrance; the car park is open ground no town, track, station or other car park uses.</summary>
        static void ValidateBeachPark(WorldState w, RailNetwork net, IntercityRoadState road, HashSet<int> cells)
        {
            var entrance = Coast.Entrance(road);
            if (!Coast.IsEntrance(entrance))
                throw new InvalidDataException("Beach road does not reach the coast.");
            for (int i = 0; i < Coast.ParkCells; i++)
            {
                var cell = Coast.ParkCell(entrance, i);
                if (!MapDefinition.InBounds(cell) || MapDefinition.Raised(cell) || MapDefinition.Water(cell) || MapDefinition.Blocked(cell, w) ||
                    net.At(cell) != null || BuildService.StationFootprint(w, cell) || !cells.Add(cell.Key))
                    throw new InvalidDataException("Invalid beach car park.");
            }
        }
        /// <summary>A service area stands beside a straight stretch of its road, on open ground no town, highway, track, station or car park uses.</summary>
        static void ValidateServiceArea(WorldState w, RailNetwork net, IntercityRoadState road, HashSet<int> cells, HashSet<int> highways)
        {
            if (!Roadside.Shaped(road))
                throw new InvalidDataException("Invalid service area.");
            for (int i = 0; i < Roadside.Cells; i++)
            {
                var cell = Roadside.SiteCell(road, i);
                if (!MapDefinition.InBounds(cell) || MapDefinition.Raised(cell) || MapDefinition.Water(cell) || Coast.Beach(cell) || MapDefinition.Blocked(cell, w) ||
                    net.At(cell) != null || BuildService.StationFootprint(w, cell) || highways.Contains(cell.Key) || !cells.Add(cell.Key))
                    throw new InvalidDataException("Invalid service area.");
            }
        }
        /// <summary>A campsite stands beside a straight stretch of its road, on open ground no town, highway, track, station, car park or service area uses.</summary>
        static void ValidateCampsite(WorldState w, RailNetwork net, IntercityRoadState road, HashSet<int> cells, HashSet<int> highways)
        {
            if (!Campsites.Shaped(road))
                throw new InvalidDataException("Invalid campsite.");
            for (int i = 0; i < Campsites.Cells; i++)
            {
                var cell = Campsites.SiteCell(road, i);
                if (!MapDefinition.InBounds(cell) || MapDefinition.Raised(cell) || MapDefinition.Water(cell) || Coast.Beach(cell) || MapDefinition.Blocked(cell, w) ||
                    net.At(cell) != null || BuildService.StationFootprint(w, cell) || highways.Contains(cell.Key) || !cells.Add(cell.Key))
                    throw new InvalidDataException("Invalid campsite.");
            }
        }
        static void ValidatePath(List<RailStep> path, StationState from, StationState to, RailNetwork net)
        {
            if (path.Count < 2 || path.Count > 6000 || !Stops(from, path[0].trackId, net) || !Stops(to, path[path.Count - 1].trackId, net))
                throw new InvalidDataException("Invalid path endpoints.");
            for (int i = 0; i < path.Count; i++)
            {
                var s = path[i];
                if (s == null || !net.ids.TryGetValue(s.trackId, out var track) || s.entry < 0 || s.entry > 3 || s.exit < 0 || s.exit > 3 || !Directions.Allows(track.mask, s.entry, s.exit) || s.fromCenter != (i == 0) || s.toCenter != (i == path.Count - 1) || s.length != ((i == 0 || i == path.Count - 1) ? 500 : RailPathfinder.Length(s.entry, s.exit)))
                    throw new InvalidDataException("Invalid path segment.");
                if (i + 1 < path.Count && (!net.Neighbor(track, s.exit, out var next) || next.id != path[i + 1].trackId || Directions.Opp(s.exit) != path[i + 1].entry))
                    throw new InvalidDataException("Disconnected route.");
            }
        }
    }
}
