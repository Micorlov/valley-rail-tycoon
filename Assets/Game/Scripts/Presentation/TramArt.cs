using System.Collections.Generic;
using TMPro;
using UnityEngine;
using ValleyRail.Core;
namespace ValleyRail
{
    /// <summary>
    /// Light rail track and stops. Every line is double track: out on the right of the way to the venue, back on the other
    /// side, merging into one stub at each stop. In a street or on a highway the grooved rails lie flush in the asphalt;
    /// elsewhere they run on a grassy bed with sleepers. Poles every other cell carry the overhead wires the trams' pantographs
    /// touch. Each stop has a platform, a glass shelter and a sign on its roof with the riders waiting; the transfer stop on
    /// the station forecourt is joined to the station hall by a covered walkway. Everything is batched into one mesh per
    /// colour and redrawn only when the lines, the railway or the streets change.
    /// </summary>
    public sealed partial class WorldView
    {
        const float LightRailLane = .21f, LightRailGauge = .07f, LightRailWire = .4f, LightRailPoleSide = .47f, LightRailPoleTop = .5f;
        const float LightRailBedTop = .045f, LightRailPlatformOffset = .36f, LightRailPlatformWidth = .2f, LightRailShelterRoof = .44f;
        static readonly Color LightRailBed = new Color(.43f, .56f, .33f), LightRailSleeper = new Color(.4f, .36f, .3f), LightRailRail = new Color(.66f, .67f, .64f),
            LightRailGroove = new Color(.42f, .42f, .44f), LightRailPole = new Color(.22f, .27f, .29f), LightRailWireColor = new Color(.1f, .1f, .11f),
            LightRailPlatform = new Color(.8f, .78f, .72f), LightRailShelterGlass = new Color(.6f, .8f, .86f), LightRailShelterRoofColor = new Color(.82f, .2f, .17f),
            LightRailWalkway = new Color(.72f, .7f, .66f);
        Transform lightRailRoot, lightRailSignRoot;
        string lightRailSignature;
        readonly Dictionary<int, (TextMeshPro outbound, TextMeshPro back)> lightRailSigns = new Dictionary<int, (TextMeshPro, TextMeshPro)>();

        /// <summary>The height a tram runs at on a cell: a town street, a highway (a bridge deck over the river), a railway crossing or its own bed.</summary>
        float LightRailDeck(Cell c)
        {
            if (MapDefinition.Water(c))
                return .165f;
            if (game.Network.At(c) != null)
                return .12f;
            int bits = game.Cities.Bits(c);
            if ((bits & (CitySimulation.Road | CitySimulation.Plaza)) != 0)
                return .026f;
            return (bits & CitySimulation.Highway) != 0 ? .052f : LightRailBedTop;
        }
        /// <summary>How far a lane sits from the line's middle: full width between the stops, closing to nothing at each stop.</summary>
        static float LightRailTaper(TramRoute route, int i, float u) =>
            i == 0 ? Mathf.Clamp01((u - .5f) * 2) : i == route.cells.Count - 1 ? Mathf.Clamp01((.5f - u) * 2) : 1;
        /// <summary>A point <paramref name="lateral"/> to the right of the way-out line, <paramref name="u"/> of the way through cell <paramref name="i"/>.</summary>
        Vector3 LightRailLanePoint(TramRoute route, int i, float u, float lateral, out Vector3 tangent)
        {
            var c = route.cells[i];
            int from = Directions.Opp(route.entry[i]), to = route.exit[i];
            var a = RailGeometry.Sample(c, from, to, Mathf.Max(0, u - .01f));
            var b = RailGeometry.Sample(c, from, to, Mathf.Min(1, u + .01f));
            tangent = (b - a).normalized;
            var p = RailGeometry.Sample(c, from, to, u) + Vector3.Cross(Vector3.up, tangent) * lateral;
            p.y = LightRailDeck(c);
            return p;
        }
        /// <summary>Where a loop position lies in the world, on its track, and the way a tram there faces.</summary>
        public Vector3 LightRailPoint(TramRoute route, int position, out Vector3 forward)
        {
            int i = route.Locate(position, out int along, out bool back);
            int n = route.cells.Count;
            float u = i == 0 ? .5f + along / 1000f : i == n - 1 ? along / 1000f : along / (float)route.length[i];
            var p = LightRailLanePoint(route, i, u, (back ? -LightRailLane : LightRailLane) * LightRailTaper(route, i, u), out var tangent);
            forward = back ? -tangent : tangent;
            return p;
        }

        void RefreshLightRail()
        {
            if (!lightRailRoot)
            {
                lightRailRoot = Root("Light rail");
                lightRailSignRoot = Root("Light rail signs");
            }
            var lines = game.World.tramLines;
            string signature = game.World.revision + ":" + game.World.cityRevision + ":" + lines.Count;
            bool redrawn = signature != lightRailSignature;
            if (redrawn)
            {
                lightRailSignature = signature;
                Clear(lightRailRoot);
                Clear(lightRailSignRoot);
                lightRailSigns.Clear();
                batchTarget = lightRailRoot;
                batch = new Dictionary<Material, List<CombineInstance>>();
                foreach (var line in lines)
                    DrawLightRailLine(line);
                Combine(lightRailRoot);
            }
            RefreshLightRailCars(redrawn);
        }

        void DrawLightRailLine(TramLineState line)
        {
            var route = game.Trams.Route(line);
            int n = route.cells.Count;
            for (int i = 0; i < n; i++)
            {
                var c = route.cells[i];
                if (trees.TryGetValue(c.Key, out var tree))
                    tree.SetActive(false);
                int bits = game.Cities.Bits(c);
                bool street = (bits & (CitySimulation.Road | CitySimulation.Highway | CitySimulation.Plaza)) != 0 || MapDefinition.Water(c);
                bool crossing = game.Network.At(c) != null;
                if (!street && !crossing)
                    Box("Tram bed", new Vector3(c.x, LightRailBedTop / 2, c.z), new Vector3(i == 0 || i == n - 1 ? .7f : .9f, LightRailBedTop, i == 0 || i == n - 1 ? .7f : .9f), LightRailBed, lightRailRoot);
                float u0 = i == 0 ? .5f : 0, u1 = i == n - 1 ? .5f : 1;
                const int Steps = 6;
                for (int k = 0; k < Steps; k++)
                {
                    float ua = Mathf.Lerp(u0, u1, k / (float)Steps), ub = Mathf.Lerp(u0, u1, (k + 1) / (float)Steps);
                    foreach (float lane in new[] { LightRailLane, -LightRailLane })
                    {
                        var pa = LightRailLanePoint(route, i, ua, lane * LightRailTaper(route, i, ua), out var ta);
                        var pb = LightRailLanePoint(route, i, ub, lane * LightRailTaper(route, i, ub), out _);
                        var dir = pb - pa;
                        if (dir.sqrMagnitude < 1e-6f)
                            continue;
                        var turn = Quaternion.LookRotation(dir);
                        var side = Vector3.Cross(Vector3.up, dir.normalized);
                        float length = dir.magnitude + .012f;
                        var mid = (pa + pb) * .5f;
                        if (!street && !crossing)
                            Box("Tram sleeper", mid + Vector3.up * .006f, new Vector3(.24f, .012f, .05f), LightRailSleeper, lightRailRoot, turn);
                        foreach (float g in new[] { -LightRailGauge, LightRailGauge })
                            Box("Tram rail", mid + side * g + Vector3.up * (street ? .004f : .016f), new Vector3(street ? .026f : .02f, street ? .008f : .022f, length), street ? LightRailGroove : LightRailRail, lightRailRoot, turn);
                        // The contact wire over each track, at the height a raised pantograph reaches; none over a railway crossing.
                        if (!crossing)
                            Box("Tram wire", new Vector3(mid.x, LightRailWire + mid.y, mid.z), new Vector3(.01f, .01f, length), LightRailWireColor, lightRailRoot, turn);
                    }
                }
                if (i % 2 == 1 && i < n - 1 && !crossing && !MapDefinition.Water(c))
                    DrawLightRailPoles(route, i);
            }
            DrawLightRailStop(line, route, 0);
            DrawLightRailStop(line, route, n - 1);
        }
        /// <summary>A pair of poles either side of the line with a cross-span holding both wires.</summary>
        void DrawLightRailPoles(TramRoute route, int i)
        {
            var p = LightRailLanePoint(route, i, .5f, 0, out var tangent);
            var side = Vector3.Cross(Vector3.up, tangent);
            float deck = p.y;
            foreach (float s in new[] { -LightRailPoleSide, LightRailPoleSide })
                Box("Tram pole", new Vector3(p.x, deck + LightRailPoleTop / 2, p.z) + side * s, new Vector3(.035f, LightRailPoleTop, .035f), LightRailPole, lightRailRoot);
            Box("Tram span", new Vector3(p.x, deck + LightRailWire + .03f, p.z), new Vector3(2 * LightRailPoleSide, .018f, .018f), LightRailPole, lightRailRoot, Quaternion.LookRotation(side));
        }
        /// <summary>The platform side of a stop: towards the station hall or the venue when the line runs past it, else the right.</summary>
        int LightRailPlatformSide(TramLineState line, TramRoute route, int i)
        {
            int n = route.cells.Count;
            // Direction from the stop along the line, towards the tram standing at it.
            int toLine = i == 0 ? route.exit[0] : Directions.Opp(route.entry[n - 1]);
            int right = (toLine + 1) % 4, left = (toLine + 3) % 4;
            var stop = route.cells[i];
            if (i == 0)
            {
                var s = game.Trains.Station(line.stationId);
                if (s != null && (Directions.Opp(s.side) == right || Directions.Opp(s.side) == left))
                    return Directions.Opp(s.side);
                return right;
            }
            // The venue side: the neighbour cell the line does not use and that is no street.
            foreach (int d in new[] { right, left })
            {
                var c = stop.Move(d);
                if (!game.Cities.HasTram(c) && (game.Cities.Bits(c) & (CitySimulation.Building | CitySimulation.Resort)) != 0 || MapDefinition.Blocked(c, game.World))
                    return d;
            }
            return right;
        }
        void DrawLightRailStop(TramLineState line, TramRoute route, int i)
        {
            int n = route.cells.Count;
            var stop = route.cells[i];
            int toLine = i == 0 ? route.exit[0] : Directions.Opp(route.entry[n - 1]);
            int side = LightRailPlatformSide(line, route, i);
            var along = new Vector3(Directions.Dx[toLine], 0, Directions.Dz[toLine]);
            var across = new Vector3(Directions.Dx[side], 0, Directions.Dz[side]);
            float deck = LightRailDeck(stop);
            var start = new Vector3(stop.x, deck, stop.z) - along * .15f;
            var platform = start + along * .55f + across * LightRailPlatformOffset;
            var turn = Quaternion.LookRotation(along);
            Box("Tram platform", platform + Vector3.up * .045f, new Vector3(LightRailPlatformWidth, .09f, 1.1f), LightRailPlatform, lightRailRoot, turn);
            Box("Tram platform edge", platform + Vector3.up * .092f - across * (LightRailPlatformWidth / 2 - .015f), new Vector3(.03f, .006f, 1.1f), Gold, lightRailRoot, turn);
            // The glass shelter at the back of the platform, and its red roof.
            var shelter = platform + along * .2f + across * .05f;
            Box("Tram shelter back", shelter + Vector3.up * .26f + across * .06f, new Vector3(.02f, .32f, .5f), LightRailShelterGlass, lightRailRoot, turn);
            foreach (float z in new[] { -.24f, .24f })
                Box("Tram shelter post", shelter + Vector3.up * .26f + along * z, new Vector3(.025f, .34f, .025f), LightRailPole, lightRailRoot);
            Box("Tram shelter roof", shelter + Vector3.up * LightRailShelterRoof, new Vector3(.2f, .03f, .58f), LightRailShelterRoofColor, lightRailRoot, turn);
            var sign = LightRailSign(line.id + (i == 0 ? " out" : " back"), shelter + Vector3.up * (LightRailShelterRoof + .03f));
            var signs = lightRailSigns.TryGetValue(line.id, out var pair) ? pair : (null, null);
            lightRailSigns[line.id] = i == 0 ? (sign, signs.Item2) : (signs.Item1, sign);
            if (i == 0)
                DrawLightRailWalkway(line, stop, along, across, platform);
        }
        /// <summary>A covered, paved walk from the station hall's town-side wall to the transfer stop's platform.</summary>
        void DrawLightRailWalkway(TramLineState line, Cell stop, Vector3 along, Vector3 across, Vector3 platform)
        {
            var s = game.Trains.Station(line.stationId);
            if (s == null)
                return;
            var axis = StationAlong(s);
            var outward = new Vector3(Directions.Dx[s.side], 0, Directions.Dz[s.side]);
            float half = StationLayout.Length(s) / 2f - .2f, middle = StationLayout.Middle(StationLayout.Length(s));
            // The walk leaves the hall straight out from the wall, level with the platform where the hall allows.
            var rel = platform - new Vector3(s.cell.x, 0, s.cell.z);
            float at = Mathf.Clamp(Vector3.Dot(rel, axis), middle - half, middle + half);
            var door = new Vector3(s.cell.x, platform.y, s.cell.z) + axis * at + outward * 1.4f;
            // Out from the wall first, then along the forecourt to the platform.
            var corner = door + outward * Mathf.Max(0, Vector3.Dot(platform - door, outward));
            var legs = new[] { (door, corner), (corner, platform) };
            foreach (var (a, b) in legs)
            {
                var d = b - a;
                if (d.magnitude < .05f)
                    continue;
                var mid = (a + b) * .5f;
                var turn = Quaternion.LookRotation(d);
                Box("Transfer walk", mid + Vector3.up * .05f, new Vector3(.24f, .02f, d.magnitude + .24f), LightRailWalkway, lightRailRoot, turn);
                Box("Transfer canopy", mid + Vector3.up * .42f, new Vector3(.3f, .025f, d.magnitude + .3f), LightRailShelterRoofColor, lightRailRoot, turn);
                int posts = Mathf.Max(1, Mathf.RoundToInt(d.magnitude / .45f));
                for (int k = 0; k <= posts; k++)
                {
                    var p = Vector3.Lerp(a, b, k / (float)posts);
                    var sideways = Vector3.Cross(Vector3.up, d.normalized) * .13f;
                    Box("Transfer canopy post", p + sideways + Vector3.up * .24f, new Vector3(.02f, .36f, .02f), LightRailPole, lightRailRoot);
                    Box("Transfer canopy post", p - sideways + Vector3.up * .24f, new Vector3(.02f, .36f, .02f), LightRailPole, lightRailRoot);
                }
            }
        }
        TextMeshPro LightRailSign(string name, Vector3 at)
        {
            var face = RoofSignLayer("Tram stop " + name, lightRailSignRoot);
            face.transform.localPosition = at;
            face.transform.rotation = Quaternion.Euler(0, labelYaw, 0);
            face.fontSharedMaterial = RoofSignMaterial(face.fontSharedMaterial);
            for (int k = 1; k <= RoofSignDepthLayers; k++)
                RoofSignLayer("Letter depth " + k, face.transform).transform.localPosition = Vector3.forward * (k * RoofSignDepth / RoofSignDepthLayers);
            Box(StationSignBoard, Vector3.forward * (RoofSignDepth + SignBoardThickness), Vector3.zero, Navy, face.transform);
            return face;
        }
        /// <summary>"12 waiting" on the stop's roof, coloured like a station's queue: mint, amber, then red as it fills.</summary>
        void ShowLightRailQueue(TextMeshPro face, int waiting)
        {
            if (!face)
                return;
            string text = $"{waiting:N0}{SmallWords} waiting";
            var color = StationCrowds.LoadColor(waiting * 3 >= TramCatalog.QueueCap * 2 ? LoadLevel.Crowded : waiting * 3 >= TramCatalog.QueueCap ? LoadLevel.Busy : LoadLevel.Calm);
            face.transform.rotation = Quaternion.Euler(0, labelYaw, 0);
            if (face.text == text && face.color == color)
                return;
            face.GetComponentsInChildren(roofSignLayers);
            foreach (var layer in roofSignLayers)
            {
                layer.text = text;
                layer.fontSize = 5;
                layer.color = layer == face ? color : Color.Lerp(color, Navy, .45f);
            }
            face.ForceMeshUpdate();
            var words = face.textBounds;
            var board = face.transform.Find(StationSignBoard);
            board.localPosition = new Vector3(words.center.x, words.center.y, board.localPosition.z);
            board.localScale = new Vector3(words.size.x + 2 * SignBoardMargin, words.size.y + 2 * SignBoardMargin, SignBoardThickness);
        }
    }
}
