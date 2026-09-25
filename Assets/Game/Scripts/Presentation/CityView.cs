using System.Collections.Generic;
using UnityEngine;
using ValleyRail.Core;
namespace ValleyRail
{
    /// <summary>Towns: streets, plazas, highways and the building dispatch. Every part is batched into the city meshes.</summary>
    public sealed partial class WorldView
    {
        static readonly Color Asphalt = new Color(.3f, .3f, .32f), Kerb = new Color(.72f, .71f, .66f), PlazaStone = new Color(.8f, .74f, .6f), Water = new Color(.3f, .6f, .78f);
        // Buildings face the long block sides first, so rows of houses line up along each street.
        static readonly int[] FacingOrder = { 2, 0, 1, 3 };
        Mesh prism, pyramid;
        readonly HashSet<int> townStreets = new HashSet<int>();
        // Per town street cell: the directions in which a highway leaves it, so no kerb blocks the way out of town.
        readonly Dictionary<int, int> highwayJoints = new Dictionary<int, int>();
        /// <summary>Town details and life show below this camera size (the orthographic half-height in cells).</summary>
        public const float DetailZoom = 20f;
        public enum EmitterKind { Smoke, Flag, Beacon, Fountain, Campus, Park, Zoo }
        /// <summary>A spot where town life animates: a chimney, a flag, a flashing light, a fountain, a university quad (size = lot span), a city park or a zoo (size = cells per side).</summary>
        public struct Emitter
        {
            public EmitterKind kind; public Vector3 at; public Quaternion turn; public Color color; public float size;
        }
        readonly List<Emitter> emitters = new List<Emitter>();
        CityLife life;
        CameraController cameraRig;
        /// <summary>True while the camera is close enough for town details and life.</summary>
        public bool ZoomedIn
        {
            get
            {
                if (!cameraRig)
                    cameraRig = FindAnyObjectByType<CameraController>();
                return cameraRig && cameraRig.view && cameraRig.view.orthographicSize < DetailZoom;
            }
        }
        void Emit(EmitterKind kind, Lot lot, Vector3 local, Color color, float size = 1) =>
            emitters.Add(new Emitter { kind = kind, at = lot.at + lot.turn * local, turn = lot.turn, color = color, size = size });
        /// <summary>A building's local frame: origin on the lot, +z towards the street it faces.</summary>
        struct Lot
        {
            public Vector3 at; public Quaternion turn; public int hash;
        }
        void DrawIntercityRoads()
        {
            var drawn = new HashSet<int>();
            var bridges = new HashSet<int>();
            foreach (var road in game.World.intercityRoads)
                for (int i = 0; i < road.built; i++)
                {
                    var cell = road.path[i];
                    if (!drawn.Add(cell.Key)) continue;
                    if (trees.TryGetValue(cell.Key, out var tree)) tree.SetActive(false);
                    // Town streets draw themselves where a highway joins them.
                    if (townStreets.Contains(cell.Key) || game.Network.At(cell) != null || BuildService.StationFootprint(game.World, cell)) continue;
                    // A highway crossing the river gets the whole span of its site's bridge style under the asphalt.
                    int bridge = MapDefinition.Bridge(cell);
                    if (bridge != 0 && cell.x == 31 && bridges.Add(bridge))
                        DrawBridge(bridge, BridgeCatalog.StyleAt(game.World, bridge), RoadDeck, cityRoot);
                    var at = new Vector3(cell.x, MapDefinition.Water(cell) ? .14f : .03f, cell.z);
                    Box("Intercity road", at, new Vector3(1, .04f, 1), new Color(.24f, .25f, .27f), cityRoot);
                    int ways = 0;
                    if (i > 0 && road.path[i - 1].Distance(cell) == 1) ways |= 1 << Directions.Between(cell, road.path[i - 1]);
                    if (i + 1 < road.path.Count && road.path[i + 1].Distance(cell) == 1) ways |= 1 << Directions.Between(cell, road.path[i + 1]);
                    CentreLine(at + Vector3.up * .026f, ways);
                    if (!road.Complete && i == road.built - 1)
                        Box("Road construction", at + Vector3.up * .22f, new Vector3(.6f, .4f, .12f), Gold, cityRoot);
                }
        }
        /// <summary>A highway's centre dash: straight along the road, or bent into an L where the road turns.</summary>
        void CentreLine(Vector3 at, int ways)
        {
            if (Directions.Count(ways) != 2 || Directions.Straight(ways))
            {
                bool eastWest = (ways & 10) != 0;
                Box("Road marking", at, eastWest ? new Vector3(.5f, .01f, .055f) : new Vector3(.055f, .01f, .5f), Cream, cityRoot);
                return;
            }
            // Each arm runs from just past the centre a quarter cell towards its side, so the bend keeps the dash length.
            for (int d = 0; d < 4; d++)
                if ((ways & 1 << d) != 0)
                    Box("Road marking", at + new Vector3(Directions.Dx[d], 0, Directions.Dz[d]) * .125f,
                        d % 2 == 1 ? new Vector3(.305f, .01f, .055f) : new Vector3(.055f, .01f, .305f), Cream, cityRoot);
        }
        void CollectTownStreets()
        {
            townStreets.Clear();
            foreach (var city in game.World.cities)
                foreach (var r in city.roads)
                    townStreets.Add(r.cell.Key);
            highwayJoints.Clear();
            foreach (var road in game.World.intercityRoads)
                for (int i = 0; i + 1 < road.built; i++)
                {
                    Cell a = road.path[i], b = road.path[i + 1];
                    bool fromTown = townStreets.Contains(a.Key);
                    if (a.Distance(b) != 1 || fromTown == townStreets.Contains(b.Key))
                        continue;
                    Cell street = fromTown ? a : b, away = fromTown ? b : a;
                    int d = Directions.Between(street, away);
                    // Open the kerb only where cars can really drive on (not onto rails running along the highway).
                    if (!RoadLanes.Passable(game.World, game.Network, street, d) || !RoadLanes.Passable(game.World, game.Network, away, d))
                        continue;
                    highwayJoints[street.Key] = (highwayJoints.TryGetValue(street.Key, out int joints) ? joints : 0) | 1 << d;
                }
        }
        void DrawCity(CityState city)
        {
            foreach (var r in city.roads)
                DrawStreet(city, r.cell);
            foreach (var bs in city.buildings)
            {
                for (int i = 0; i < CityLayout.FootprintCells(bs); i++)
                    if (trees.TryGetValue(CityLayout.FootprintCell(bs, i).Key, out var tree))
                        tree.SetActive(false);
                if (UnderConstruction(bs))
                    continue; // the construction site stands in for it until it is built
                var def = BuildingCatalog.Get(bs.def);
                var lot = LotFor(bs);
                if (def.style == BuildingStyle.Tower || def.style == BuildingStyle.CommercialTower)
                    DrawSkyscraper(bs, def, lot.at);
                else
                    DrawBuilding(def, lot);
                DrawDetails(def, lot);
            }
        }
        void DrawStreet(CityState city, Cell cell)
        {
            if (trees.TryGetValue(cell.Key, out var tree))
                tree.SetActive(false);
            if (game.Network.At(cell) != null)
                return; // a level crossing: the track's own ballast and sleepers show here
            var at = new Vector3(cell.x, .012f, cell.z);
            if (cell.Equals(city.center))
            {
                Box("Plaza", at, new Vector3(.96f, .024f, .96f), PlazaStone, cityRoot);
                Box("Fountain basin", at + new Vector3(0, .05f, 0), new Vector3(.46f, .08f, .46f), Cream, cityRoot);
                Box("Fountain water", at + new Vector3(0, .09f, 0), new Vector3(.36f, .02f, .36f), Water, cityRoot);
                Box("Fountain column", at + new Vector3(0, .17f, 0), new Vector3(.08f, .18f, .08f), Cream, cityRoot);
                DrawPlazaDetails(cell);
                return;
            }
            Box("Street", at, new Vector3(1f, .024f, 1f), Asphalt, cityRoot);
            int mask = 0;
            for (int d = 0; d < 4; d++)
                if (townStreets.Contains(cell.Move(d).Key))
                    mask |= 1 << d;
            if (highwayJoints.TryGetValue(cell.Key, out int highways))
                mask |= highways;
            for (int d = 0; d < 4; d++)
            {
                if ((mask & 1 << d) != 0)
                    continue;
                // A pavement on every side that faces a block rather than another street.
                var side = new Vector3(Directions.Dx[d], 0, Directions.Dz[d]);
                bool eastWest = d % 2 == 1;
                Box("Pavement", at + side * .44f + Vector3.up * .014f, eastWest ? new Vector3(.12f, .03f, 1f) : new Vector3(1f, .03f, .12f), Kerb, cityRoot);
            }
            if (Directions.Straight(mask))
                Box("Centre line", at + Vector3.up * .014f, mask == 10 ? new Vector3(.6f, .01f, .05f) : new Vector3(.05f, .01f, .6f), new Color(.72f, .7f, .62f), cityRoot);
            DrawStreetDetails(cell, mask);
        }
        /// <summary>The lot frame of a building: centred on its footprint and facing the first side that touches a street.</summary>
        Lot LotFor(BuildingState bs)
        {
            int size = BuildingCatalog.Size(bs.def), facing = FacingOrder[0];
            var cell = bs.cell;
            foreach (int d in FacingOrder)
                if (FrontsStreet(bs, size, d))
                {
                    facing = d;
                    break;
                }
            float half = (size - 1) / 2f;
            return new Lot
            {
                at = new Vector3(cell.x + half, 0, cell.z + half),
                turn = Quaternion.LookRotation(new Vector3(Directions.Dx[facing], 0, Directions.Dz[facing])),
                hash = (cell.x * 73856093 ^ cell.z * 19349663) & 0x7fffffff,
            };
        }
        bool FrontsStreet(BuildingState bs, int size, int d)
        {
            for (int i = 0; i < size * size; i++)
            {
                var n = CityLayout.FootprintCell(bs, i).Move(d);
                if (!CityLayout.Covers(bs, n) && townStreets.Contains(n.Key))
                    return true;
            }
            return false;
        }
        // Parts in the lot frame: x across the frontage, y up, z towards the street.
        void Part(Lot lot, string name, Vector3 local, Vector3 size, Color color) =>
            Box(name, lot.at + lot.turn * local, size, color, cityRoot, lot.turn);
        /// <summary>A gable roof whose ridge runs across the frontage, or towards the street when <paramref name="endOn"/>.</summary>
        void Gable(Lot lot, Vector3 local, float width, float rise, float depth, Color color, bool endOn = false)
        {
            var turn = endOn ? lot.turn * Quaternion.Euler(0, 90, 0) : lot.turn;
            var size = endOn ? new Vector3(depth, rise, width) : new Vector3(width, rise, depth);
            Shape("Gable roof", prism, lot.at + lot.turn * local, size, color, cityRoot, turn);
        }
        void Hip(Lot lot, Vector3 local, float width, float rise, float depth, Color color) =>
            Shape("Hip roof", pyramid, lot.at + lot.turn * local, new Vector3(width, rise, depth), color, cityRoot, lot.turn);
        void Spire(Lot lot, Vector3 local, float radius, float height, Color color) =>
            Shape("Spire", cone, lot.at + lot.turn * local, new Vector3(radius, height, radius), color, cityRoot, lot.turn);
        /// <summary>Window strips on all four walls of a block, one pair of boxes per floor.</summary>
        void Windows(Lot lot, Vector3 centre, float width, float depth, float bottom, float top, int rows, Color color)
        {
            float step = (top - bottom) / rows, band = Mathf.Min(.12f, step * .45f);
            for (int i = 0; i < rows; i++)
            {
                float y = bottom + step * (i + .5f);
                Part(lot, "Windows", centre + new Vector3(0, y, 0), new Vector3(width * .7f, band, depth + .03f), color);
                Part(lot, "Windows", centre + new Vector3(0, y, 0), new Vector3(width + .03f, band, depth * .6f), color);
            }
        }
        void Door(Lot lot, float x, float front, Color color, float width = .12f, float height = .24f) =>
            Part(lot, "Door", new Vector3(x, height / 2, front + .012f), new Vector3(width, height, .03f), color);
        void Chimney(Lot lot, float x, float z, float top, Color color)
        {
            Part(lot, "Chimney", new Vector3(x, top - .1f, z), new Vector3(.08f, .2f, .08f), color);
            Emit(EmitterKind.Smoke, lot, new Vector3(x, top + .02f, z), color);
        }
        static Color Pick(Color[] palette, int hash, int salt = 0) => palette[(hash / 7 + salt) % palette.Length];
        /// <summary>Triangular prism, ridge along x at the top: a gable roof.</summary>
        static Mesh Prism()
        {
            Vector3 a = new Vector3(-.5f, 0, -.5f), b = new Vector3(.5f, 0, -.5f), c = new Vector3(.5f, 0, .5f), d = new Vector3(-.5f, 0, .5f);
            Vector3 r0 = new Vector3(-.5f, 1, 0), r1 = new Vector3(.5f, 1, 0);
            return Faceted(new[] { a, r0, r1, a, r1, b, c, r1, r0, c, r0, d, d, r0, a, b, r1, c });
        }
        /// <summary>Four-sided pyramid: a hip roof.</summary>
        static Mesh Pyramid()
        {
            Vector3 a = new Vector3(-.5f, 0, -.5f), b = new Vector3(.5f, 0, -.5f), c = new Vector3(.5f, 0, .5f), d = new Vector3(-.5f, 0, .5f), top = Vector3.up;
            return Faceted(new[] { a, top, b, b, top, c, c, top, d, d, top, a });
        }
        static Mesh Faceted(Vector3[] triangles)
        {
            var indices = new int[triangles.Length];
            for (int i = 0; i < indices.Length; i++)
                indices[i] = i;
            var mesh = new Mesh();
            mesh.SetVertices(triangles);
            mesh.SetTriangles(indices, 0);
            mesh.RecalculateNormals();
            return mesh;
        }
        float DrawSkyscraper(BuildingState building, BuildingDefinition def, Vector3 at)
        {
            bool office = def.category == BuildingCategory.Commercial;
            int variant = (building.cell.x * 7 + building.cell.z * 13) % 3;
            float h = def.height / 100f * (1f + variant * .1f);
            float w = def.width / 100f, d = def.depth / 100f;
            Color glass = office ? new Color(.18f, .43f, .56f) : new Color(.32f, .51f, .58f);
            Color trim = office ? new Color(.68f, .78f, .8f) : Cream;
            Box("Tower podium", at + Vector3.up * .3f, new Vector3(w, .6f, d), trim, cityRoot);
            Box("Glass tower", at + Vector3.up * (h * .5f), new Vector3(w * .86f, h, d * .86f), glass, cityRoot);
            for (int floor = 1; floor <= def.windows + variant; floor++)
            {
                float y = .45f + (h - .65f) * floor / (def.windows + variant + 1);
                Box("Floor band", at + Vector3.up * y, new Vector3(w * .89f, .045f, d * .89f), trim, cityRoot);
                // Four facades remain legible from every side of the isometric map.
                for (int side = 0; side < 4; side++)
                {
                    bool eastWest = side % 2 == 1;
                    var normal = new Vector3(Directions.Dx[side], 0, Directions.Dz[side]);
                    var offset = normal * (eastWest ? w : d) * .435f;
                    Color window = (floor + side + variant) % 4 == 0 ? Gold : Navy;
                    Box("Tower windows", at + offset + Vector3.up * (y + .13f), eastWest ? new Vector3(.015f, .14f, d * .62f) : new Vector3(w * .62f, .14f, .015f), window, cityRoot);
                }
            }
            Box("Setback crown", at + Vector3.up * (h + .22f), new Vector3(w * .62f, .44f, d * .62f), trim, cityRoot);
            Box("Roof cap", at + Vector3.up * (h + .47f), new Vector3(w * .65f, .08f, d * .65f), Navy, cityRoot);
            if (office)
            {
                Box("Antenna", at + Vector3.up * (h + .84f), new Vector3(.045f, .7f, .045f), trim, cityRoot);
                emitters.Add(new Emitter { kind = EmitterKind.Beacon, at = at + Vector3.up * (h + 1.21f), turn = Quaternion.identity, color = Red, size = 1 });
            }
            return h + (office ? 1.2f : .52f);
        }
    }
}
