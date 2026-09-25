using System;
namespace ValleyRail.Core
{
    /// <summary>
    /// Ski resorts on the valley's snowy peaks. Every peak with snow on top has one resort at its foot, on the south face
    /// the camera looks at when it can: a <see cref="Size"/> × <see cref="Size"/> base with the gondola's valley station and
    /// the lodge on the mountain side and a car park in front, whose road ends on <see cref="Entrance"/>. The gondola and the
    /// pistes are scenery on the hill itself, which blocks track anyway.
    ///
    /// A resort is a producer (<see cref="ProducerKind.SkiResort"/>) with a fixed id per peak (<see cref="FirstId"/> onwards,
    /// ids WorldState.New never hands out), so it keeps its place in every save. It sends tourists home and takes tourists
    /// in, so a railway station within <see cref="Catchment"/> cells of the base runs passenger trains to and from the towns.
    /// <see cref="Establish"/> adds the resorts a world is missing when a session starts: new games and older saves alike.
    /// Towns build a road to each resort once no pair of towns needs a highway (CitySimulation.Ski); cars park there when
    /// it opens.
    /// </summary>
    public static class SkiResorts
    {
        public struct Peak
        {
            public string name; public int x, z; public float radius, height;
        }
        /// <summary>The snow-capped hills of <see cref="MapDefinition.Height"/>, in the order of their resort ids.</summary>
        public static readonly Peak[] Peaks =
        {
            new Peak { name = "Granite Ridge", x = 80, z = 82, radius = 13, height = 10 },
            new Peak { name = "Eastfell", x = 107, z = 67, radius = 11, height = 8 },
            new Peak { name = "Frostpeaks", x = 56, z = 112, radius = 12, height = 12 },
            new Peak { name = "Northcrest", x = 91, z = 121, radius = 7, height = 6 },
        };
        public const int FirstId = 23, Count = 4;
        /// <summary>The base reaches Half cells either side of the resort's cell.</summary>
        public const int Half = 2, Size = 2 * Half + 1;
        /// <summary>A station serves the resort when a platform cell is this close to the base (like a town's streets).</summary>
        public const int Catchment = 3;
        /// <summary>How far from the car park entrance a town's plaza may be for the town to build the resort's road.</summary>
        public const int Reach = 64;
        /// <summary>How far the base may slide sideways, and move further out, from the foot of its face to find free ground.</summary>
        const int Slide = 6, Further = 2;
        // Faces in order of preference: south and west face the camera's starting view, then east, then north.
        static readonly int[] Faces = { 2, 3, 1, 0 };

        public static bool IsResortId(int id) => id >= FirstId && id < FirstId + Count;
        public static bool IsResort(ProducerState p) => p != null && p.kind == ProducerKind.SkiResort;
        public static string Name(int peak) => Peaks[peak].name + " Ski Resort";
        /// <summary>The peak a resort id belongs to, or -1.</summary>
        public static int PeakOf(int id) => IsResortId(id) ? id - FirstId : -1;
        public static Cell Summit(int peak) => new Cell(Peaks[peak].x, Peaks[peak].z);
        /// <summary>True when the peak's top is snow (surface 5), so it gets a resort.</summary>
        public static bool Snowy(int peak) => MapDefinition.Terrain(Summit(peak)) == 5;
        /// <summary>The direction from the base up to the summit: the dominant axis, so the lodge row faces the mountain.</summary>
        public static int Up(Cell center, int peak)
        {
            int dx = Peaks[peak].x - center.x, dz = Peaks[peak].z - center.z;
            return Math.Abs(dz) >= Math.Abs(dx) ? (dz >= 0 ? 0 : 2) : (dx >= 0 ? 1 : 3);
        }
        public static int Up(ProducerState p) => Up(p.cell, PeakOf(p.id));
        /// <summary>Where the resort's road ends: in front of the car park, on the base's centre line, away from the mountain.</summary>
        public static Cell Entrance(Cell center, int up) =>
            new Cell(center.x - Directions.Dx[up] * (Half + 1), center.z - Directions.Dz[up] * (Half + 1));
        public static Cell Entrance(ProducerState p) => Entrance(p.cell, Up(p));
        public static bool Covers(Cell center, Cell c) => Math.Abs(c.x - center.x) <= Half && Math.Abs(c.z - center.z) <= Half;
        public const int FootprintCells = Size * Size;
        /// <summary>Base cell <paramref name="i"/> (0 to FootprintCells - 1), row by row.</summary>
        public static Cell FootprintCell(Cell center, int i) => new Cell(center.x - Half + i % Size, center.z - Half + i / Size);
        /// <summary>Manhattan distance from a cell to the nearest cell of the base (0 inside it).</summary>
        public static int Distance(ProducerState p, Cell c) =>
            Math.Max(0, Math.Abs(c.x - p.cell.x) - Half) + Math.Max(0, Math.Abs(c.z - p.cell.z) - Half);

        /// <summary>
        /// Adds a resort for every snowy peak that has none yet, on the first free site at the foot of the peak. Only map
        /// version 5 worlds get resorts: older saves may already use the fixed ids. Returns how many were added.
        /// </summary>
        public static int Establish(WorldState w, CitySimulation cities, RailNetwork net)
        {
            if (w.mapVersion < 5)
                return 0;
            int added = 0;
            for (int peak = 0; peak < Count; peak++)
            {
                int id = FirstId + peak;
                if (!Snowy(peak) || IdTaken(w, id) || !FindSite(w, cities, net, peak, out var cell))
                    continue;
                w.producers.Add(new ProducerState { id = id, name = Name(peak), kind = ProducerKind.SkiResort, cell = cell });
                added++;
            }
            if (added > 0)
            {
                w.revision++;
                w.cityRevision++;
            }
            return added;
        }
        static bool IdTaken(WorldState w, int id) =>
            w.producers.Exists(p => p.id == id) || w.tracks.Exists(t => t.id == id) || w.stations.Exists(s => s.id == id) ||
            w.trains.Exists(t => t.id == id) || w.cities.Exists(c => c.id == id);
        /// <summary>
        /// The base nearest the peak on its preferred face: the first cell out from the summit where the whole base sits on
        /// flat land, then sliding sideways and a little further out until the base and its entrance are free.
        /// </summary>
        public static bool FindSite(WorldState w, CitySimulation cities, RailNetwork net, int peak, out Cell center)
        {
            var summit = Summit(peak);
            int far = (int)Math.Ceiling(Peaks[peak].radius) + Size + 2;
            foreach (int face in Faces)
            {
                int side = (face + 1) % 4;
                for (int extra = 0; extra <= Further; extra++)
                    for (int k = 0; k <= 2 * Slide; k++)
                    {
                        // Offsets 0, +1, -1, +2, -2 ... along the face.
                        int slide = (k + 1) / 2 * (k % 2 == 1 ? 1 : -1);
                        for (int t = 1; t <= far; t++)
                        {
                            var c = new Cell(summit.x + Directions.Dx[face] * t + Directions.Dx[side] * slide, summit.z + Directions.Dz[face] * t + Directions.Dz[side] * slide);
                            if (!OnFlat(c))
                                continue;
                            c = new Cell(c.x + Directions.Dx[face] * extra, c.z + Directions.Dz[face] * extra);
                            if (Up(c, peak) == Directions.Opp(face) && Shaped(c, peak) && Free(w, cities, net, c, peak))
                            {
                                center = c;
                                return true;
                            }
                            break;
                        }
                    }
            }
            center = default;
            return false;
        }
        /// <summary>Every base cell is on the map, on flat dry land off the beach.</summary>
        static bool OnFlat(Cell center)
        {
            for (int i = 0; i < FootprintCells; i++)
            {
                var c = FootprintCell(center, i);
                if (!MapDefinition.InBounds(c) || MapDefinition.Raised(c) || MapDefinition.Water(c) || Coast.Beach(c))
                    return false;
            }
            return true;
        }
        /// <summary>A base any resort of this peak could have: flat ground near the foot, its entrance on flat ground too.</summary>
        static bool Shaped(Cell center, int peak)
        {
            if (!OnFlat(center))
                return false;
            var entrance = Entrance(center, Up(center, peak));
            if (!MapDefinition.InBounds(entrance) || MapDefinition.Raised(entrance) || MapDefinition.Water(entrance) || Coast.Beach(entrance))
                return false;
            return center.Distance(Summit(peak)) <= (int)Math.Ceiling(Peaks[peak].radius) + Size + Slide + Further + 2;
        }
        /// <summary>Nothing stands on the base or its entrance: no industry, resort, track, station, town building, street or highway.</summary>
        static bool Free(WorldState w, CitySimulation cities, RailNetwork net, Cell center, int peak)
        {
            for (int i = 0; i < FootprintCells; i++)
            {
                var c = FootprintCell(center, i);
                if (MapDefinition.Blocked(c, w) || cities.Occupied(c) || net.At(c) != null || BuildService.StationFootprint(w, c))
                    return false;
            }
            var entrance = Entrance(center, Up(center, peak));
            return !MapDefinition.Blocked(entrance, w) && !cities.BlocksTrack(entrance) && !cities.HasBuilding(entrance) &&
                net.At(entrance) == null && !BuildService.StationFootprint(w, entrance);
        }
        /// <summary>The snowy peak nearest a cell.</summary>
        public static int PeakOfSite(Cell c)
        {
            int best = 0;
            for (int i = 1; i < Count; i++)
                if (c.Distance(Summit(i)) < c.Distance(Summit(best)))
                    best = i;
            return best;
        }
        /// <summary>
        /// Save validation: a resort of a known peak with its own id and name, whose base lies at the foot of that peak on
        /// flat land and overlaps no other industry or resort. Track, streets and highways on the base fail their own checks,
        /// since MapDefinition.Blocked covers the whole base.
        /// </summary>
        public static bool Valid(WorldState w, ProducerState p)
        {
            int peak = PeakOf(p.id);
            if (p.kind != ProducerKind.SkiResort || peak < 0 || w.mapVersion < 5 || !Snowy(peak) || PeakOfSite(p.cell) != peak ||
                string.IsNullOrWhiteSpace(p.name) || p.name.Length > 40 || p.transfers == null || p.transfers.Count != 0 || !Shaped(p.cell, peak))
                return false;
            foreach (var other in w.producers)
            {
                if (other == p || other.kind == ProducerKind.Town)
                    continue;
                int reach = MapDefinition.Reach(other.kind) + Half;
                if (Math.Abs(other.cell.x - p.cell.x) <= reach && Math.Abs(other.cell.z - p.cell.z) <= reach)
                    return false;
            }
            return true;
        }
    }
}
