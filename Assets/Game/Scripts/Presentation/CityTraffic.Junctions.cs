using System.Collections.Generic;
using UnityEngine;
using ValleyRail.Core;

namespace ValleyRail
{
    /// <summary>
    /// Traffic lights and roundabouts (<see cref="JunctionRules"/> picks the junctions). A car stops at a red or amber
    /// light, and a car waiting at the stop line calls the green for its road (the lights rest on green while nobody
    /// waits). On a roundabout it drives anticlockwise round the island, keeping right, and turns off at its exit (a
    /// right turn cuts the corner as at any junction). One car at a time still drives through a junction, so traffic
    /// never crosses paths. The islands and lights stand under "Road junctions", beside the traffic, whose direct
    /// children are all vehicles; the lights show with the other street details when zoomed in.
    /// </summary>
    public sealed partial class CityTraffic
    {
        sealed class Junction
        {
            public Cell cell;
            public JunctionKind kind;
            public SignalPhase phase;
            /// <summary>When a car last stood at the stop line wanting in, by road (0 north-south, 1 east-west).</summary>
            public readonly float[] asked = { -99, -99 };
            /// <summary>The lit lamps by road (0 north-south, 1 east-west) and light (red, amber, green); null where no head shows it.</summary>
            public readonly Renderer[,] lamps = new Renderer[2, 3];
        }
        /// <summary>A way round a roundabout's island from one entry to one exit, sampled finely enough to walk by distance.</summary>
        sealed class RingPath
        {
            public Vector3[] points;
            public float[] along;
            public float length;
        }
        sealed partial class Car
        {
            /// <summary>How far right of the centre line the vehicle drives, in cells.</summary>
            public float lane = Lane;
        }
        // The driving lane right of the centre line, the hard shoulder a tow truck passes a jam on, and the circle cars
        // drive round a roundabout's island. The island with its kerb stays inside the circle less half a car's width.
        const float Lane = .21f, Shoulder = .45f, RingRadius = .27f, IslandRadius = .15f;
        // Samples of each bend between a lane and the circle, and degrees of circle per sample.
        const int LeadSteps = 10;
        const float RingStepDegrees = 5, StreetTop = .024f;
        // Traffic seconds a car's call for green lasts: cars at the stop line call again every frame.
        const float CallMemory = 1f;
        static readonly Color Grass = new Color(.36f, .6f, .3f), IslandKerb = new Color(.8f, .79f, .74f), Marking = new Color(.93f, .93f, .9f),
            Bark = new Color(.42f, .3f, .2f), Crown = new Color(.2f, .46f, .26f), Petal = new Color(.95f, .45f, .55f), Pollen = new Color(.98f, .85f, .3f),
            SignalPole = new Color(.3f, .32f, .35f), SignalHead = new Color(.12f, .13f, .15f);
        static readonly Color[] LensDark = { new Color(.3f, .06f, .05f), new Color(.34f, .22f, .04f), new Color(.05f, .22f, .09f) },
            LensLit = { new Color(1f, .16f, .1f), new Color(1f, .68f, .08f), new Color(.3f, 1f, .45f) };
        static readonly RingPath[] ringPaths = new RingPath[16];
        // Junction cell key → its traffic lights or roundabout.
        readonly Dictionary<int, Junction> controlled = new Dictionary<int, Junction>();
        /// <summary>Junction cells a roundabout may not take, such as where a tram line runs straight through: they get lights instead.</summary>
        readonly HashSet<int> straightThrough = new HashSet<int>();
        readonly List<Mesh> junctionMeshes = new List<Mesh>();
        Transform junctionRoot, signalRoot;
        Mesh cylinder, cone;

        /// <summary>
        /// Puts traffic lights or a roundabout on the junctions the rules pick: a lone junction cell of three or four roads,
        /// off the rails, off the water and not a town plaza, with room on every road in for a queue at its stop line.
        /// </summary>
        void ControlJunctions()
        {
            controlled.Clear();
            var members = new Dictionary<int, int>();
            foreach (int group in junctions.Values)
                members[group] = members.TryGetValue(group, out int count) ? count + 1 : 1;
            foreach (var pair in junctions)
            {
                var cell = Cell.FromKey(pair.Key);
                if (members[pair.Value] != 1 || OnRails(cell) || MapDefinition.Water(cell) || !Roomy(cell))
                    continue;
                var kind = JunctionRules.Kind(cell, lanes.Exits(cell), lanes.IsTownStreet(cell), Plaza(cell));
                if (kind == JunctionKind.Roundabout && straightThrough.Contains(pair.Key))
                    kind = JunctionKind.Lights;
                if (kind != JunctionKind.GiveWay)
                    controlled[pair.Key] = new Junction { cell = cell, kind = kind, phase = JunctionRules.Start(cell) };
            }
            DrawJunctions();
        }
        /// <summary>Every road out of the junction runs two cells before the next junction or dead end.</summary>
        bool Roomy(Cell c)
        {
            int exits = lanes.Exits(c);
            for (int d = 0; d < 4; d++)
            {
                var next = c.Move(d);
                if ((exits & 1 << d) != 0 && (GroupOf(next) >= 0 || GroupOf(next.Move(d)) >= 0))
                    return false;
            }
            return true;
        }
        bool Plaza(Cell c)
        {
            foreach (var city in game.World.cities)
                if (city.center.Equals(c))
                    return true;
            return false;
        }
        /// <summary>
        /// The light ahead is not green for a car at the stop line of the junction at <paramref name="next"/>; standing
        /// there, the car calls the green for its road.
        /// </summary>
        bool RedLight(Car car, Cell next)
        {
            if (!controlled.TryGetValue(next.Key, out var j) || j.kind != JunctionKind.Lights)
                return false;
            j.asked[car.exit & 1] = clock;
            return !JunctionRules.Go(j.phase, car.exit);
        }
        /// <summary>The kind of junction at a cell: lights, a roundabout, or give way (every other cell).</summary>
        JunctionKind KindAt(Cell c) => controlled.TryGetValue(c.Key, out var j) ? j.kind : JunctionKind.GiveWay;
        static bool RightTurn(Car car) => car.exit == (car.entry + 3) % 4;
        bool Circles(Car car) => !car.isolated && !RightTurn(car) && KindAt(car.cell) == JunctionKind.Roundabout;
        /// <summary>How fast progress runs through the car's cell: a way round a roundabout is longer than a cell.</summary>
        float Pace(Car car) => Circles(car) ? 1 / RingFor(car.entry, car.exit).length : 1;
        /// <summary>The car's place and heading on its way round a roundabout; false when it is not on one.</summary>
        bool Ring(Car car, out Vector3 p, out Vector3 forward)
        {
            p = forward = Vector3.zero;
            if (!Circles(car))
                return false;
            var path = RingFor(car.entry, car.exit);
            float at = Mathf.Clamp01(car.progress) * path.length, look = .02f;
            p = PointOn(path, at);
            forward = PointOn(path, Mathf.Min(path.length, at + look)) - PointOn(path, Mathf.Max(0, at - look));
            return true;
        }
        static RingPath RingFor(int entry, int exit) => ringPaths[entry * 4 + exit] ?? (ringPaths[entry * 4 + exit] = BuildRing(entry, exit));
        /// <summary>
        /// From the lane at the entry edge into the circle, anticlockwise round it, and out into the lane at the exit
        /// edge: the lanes meet the circle where it runs Lane off the centre line.
        /// </summary>
        static RingPath BuildRing(int entry, int exit)
        {
            Vector3 outIn = Direction(entry), outOut = Direction(exit), travel = -outIn;
            var start = outIn * .5f + Vector3.Cross(Vector3.up, travel) * Lane;
            var end = outOut * .5f + Vector3.Cross(Vector3.up, outOut) * Lane;
            float join = Mathf.Asin(Lane / RingRadius);
            float from = Mathf.Atan2(outIn.z, outIn.x) + join, to = Mathf.Atan2(outOut.z, outOut.x) - join;
            float sweep = Mathf.Repeat(to - from, Mathf.PI * 2);
            var points = new List<Vector3>(96);
            Bend(points, start, travel, RingPoint(from), RingTangent(from), true);
            int steps = Mathf.Max(1, Mathf.CeilToInt(sweep * Mathf.Rad2Deg / RingStepDegrees));
            for (int k = 1; k <= steps; k++)
                points.Add(RingPoint(from + sweep * k / steps));
            Bend(points, RingPoint(to), RingTangent(to), end, outOut, false);
            var along = new float[points.Count];
            for (int k = 1; k < points.Count; k++)
                along[k] = along[k - 1] + Vector3.Distance(points[k - 1], points[k]);
            return new RingPath { points = points.ToArray(), along = along, length = along[along.Length - 1] };
        }
        static Vector3 RingPoint(float angle) => new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * RingRadius;
        static Vector3 RingTangent(float angle) => new Vector3(-Mathf.Sin(angle), 0, Mathf.Cos(angle));
        /// <summary>A smooth bend from a to b leaving a along <paramref name="ta"/> and reaching b along <paramref name="tb"/>.</summary>
        static void Bend(List<Vector3> points, Vector3 a, Vector3 ta, Vector3 b, Vector3 tb, bool first)
        {
            float reach = Vector3.Distance(a, b);
            for (int k = first ? 0 : 1; k <= LeadSteps; k++)
            {
                float s = k / (float)LeadSteps, s2 = s * s, s3 = s2 * s;
                points.Add((2 * s3 - 3 * s2 + 1) * a + (s3 - 2 * s2 + s) * reach * ta + (3 * s2 - 2 * s3) * b + (s3 - s2) * reach * tb);
            }
        }
        static Vector3 PointOn(RingPath path, float distance)
        {
            int lo = 0, hi = path.along.Length - 1;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) / 2;
                if (path.along[mid] < distance)
                    lo = mid;
                else
                    hi = mid;
            }
            float span = path.along[hi] - path.along[lo];
            return Vector3.Lerp(path.points[lo], path.points[hi], span > 1e-5f ? (distance - path.along[lo]) / span : 0);
        }

        /// <summary>Changes the lights for the cars that called at their stop lines, then shows them.</summary>
        void RunSignals(float seconds)
        {
            foreach (var j in controlled.Values)
                if (j.kind == JunctionKind.Lights)
                    j.phase = JunctionRules.Advance(j.phase, seconds, clock - j.asked[j.phase.road] < CallMemory, clock - j.asked[1 - j.phase.road] < CallMemory);
            ShowSignals();
        }
        /// <summary>Lights each signal head's lamp for its road's phase: a lit lamp over the dark lens.</summary>
        void ShowSignals()
        {
            foreach (var j in controlled.Values)
            {
                if (j.kind != JunctionKind.Lights)
                    continue;
                for (int axis = 0; axis < 2; axis++)
                {
                    int lit = (int)JunctionRules.Shows(j.phase, axis);
                    for (int light = 0; light < 3; light++)
                    {
                        var lamp = j.lamps[axis, light];
                        if (lamp && lamp.enabled != (light == lit))
                            lamp.enabled = light == lit;
                    }
                }
            }
        }
        /// <summary>The traffic lights show with the other street furniture, once the camera is close.</summary>
        void ShowSignalsWhenNear()
        {
            if (!signalRoot || world == null)
                return;
            bool near = world.ZoomedIn;
            if (signalRoot.gameObject.activeSelf != near)
                signalRoot.gameObject.SetActive(near);
        }

        void DrawJunctions()
        {
            ReleaseJunctionArt();
            if (controlled.Count == 0)
                return;
            junctionRoot = new GameObject("Road junctions").transform;
            junctionRoot.SetParent(transform.parent, false);
            signalRoot = new GameObject("Traffic lights").transform;
            signalRoot.SetParent(junctionRoot, false);
            if (!cylinder)
                cylinder = Round(16, false);
            if (!cone)
                cone = Round(7, true);
            foreach (var j in controlled.Values)
            {
                var at = new Vector3(j.cell.x, StreetTop, j.cell.z);
                if (j.kind == JunctionKind.Roundabout)
                    Placed("Roundabout", Roundabout(lanes.Exits(j.cell)), junctionRoot, at);
                else
                    Signals(j, at);
            }
            ShowSignals();
        }
        /// <summary>A grass island in a kerb ring with a tree and flowers, and a give-way line across every lane in.</summary>
        Transform Roundabout(int exits)
        {
            var template = new GameObject("Roundabout template").transform;
            Solid(template, cylinder, new Vector3(0, .018f, 0), new Vector3(IslandRadius * 2 + .03f, .036f, IslandRadius * 2 + .03f), IslandKerb);
            Solid(template, cylinder, new Vector3(0, .026f, 0), new Vector3(IslandRadius * 2, .044f, IslandRadius * 2), Grass);
            world.Box("Trunk", new Vector3(0, .09f, 0), new Vector3(.022f, .09f, .022f), Bark, template);
            Solid(template, cone, new Vector3(0, .2f, 0), new Vector3(.13f, .2f, .13f), Crown);
            for (int k = 0; k < 6; k++)
            {
                float angle = k * Mathf.PI / 3 + .4f;
                world.Box("Flower", new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * .095f + Vector3.up * .052f, new Vector3(.026f, .014f, .026f), k % 2 == 0 ? Petal : Pollen, template);
            }
            for (int d = 0; d < 4; d++)
            {
                if ((exits & 1 << d) == 0)
                    continue;
                var outward = Direction(d);
                var kerbSide = Vector3.Cross(Vector3.up, -outward);
                for (int s = 0; s < 3; s++)
                    world.Box("Give way", outward * .46f + kerbSide * (.07f + s * .14f) + Vector3.up * .004f, Oriented(d, .09f, .006f, .026f), Marking, template);
            }
            return template;
        }
        /// <summary>
        /// A signal on the kerb at the near right corner of every road in, facing its traffic: red over amber over green.
        /// The dark lenses belong to the head; one lit lamp mesh per road and light sits over them and is switched on its phase.
        /// </summary>
        void Signals(Junction j, Vector3 at)
        {
            var template = new GameObject("Traffic light template").transform;
            var lamps = new Transform[2, 3];
            int exits = lanes.Exits(j.cell);
            for (int d = 0; d < 4; d++)
            {
                if ((exits & 1 << d) == 0)
                    continue;
                var outward = Direction(d);
                var corner = outward * .44f + Vector3.Cross(Vector3.up, -outward) * .44f;
                world.Box("Signal pole", corner + Vector3.up * .27f, new Vector3(.026f, .54f, .026f), SignalPole, template);
                var head = corner + Vector3.up * .46f;
                world.Box("Signal head", head, Oriented(d, .07f, .17f, .05f), SignalHead, template);
                world.Box("Signal visor", head + Vector3.up * .09f + outward * .015f, Oriented(d, .08f, .012f, .07f), SignalHead, template);
                for (int light = 0; light < 3; light++)
                {
                    var lens = head + outward * .026f + Vector3.up * (.052f - light * .052f);
                    world.Box("Lens", lens, Oriented(d, .036f, .036f, .008f), LensDark[light], template);
                    if (!lamps[d & 1, light])
                        lamps[d & 1, light] = new GameObject("Signal lamp template").transform;
                    world.Box("Lamp", lens + outward * .004f, Oriented(d, .042f, .042f, .01f), LensLit[light], lamps[d & 1, light]);
                }
            }
            Placed("Traffic lights", template, signalRoot, at);
            for (int axis = 0; axis < 2; axis++)
                for (int light = 0; light < 3; light++)
                    if (lamps[axis, light])
                        j.lamps[axis, light] = Placed("Signal lamp", lamps[axis, light], signalRoot, at).GetComponent<Renderer>();
        }
        /// <summary>A box size given across, up and along a road leaving in direction d.</summary>
        static Vector3 Oriented(int d, float across, float up, float along) =>
            d % 2 == 0 ? new Vector3(across, up, along) : new Vector3(along, up, across);
        void Solid(Transform parent, Mesh mesh, Vector3 at, Vector3 size, Color color)
        {
            var part = new GameObject("Part", typeof(MeshFilter), typeof(MeshRenderer));
            part.transform.SetParent(parent, false);
            part.transform.localPosition = at;
            part.transform.localScale = size;
            part.GetComponent<MeshFilter>().sharedMesh = mesh;
            part.GetComponent<MeshRenderer>().sharedMaterial = world.Mat(color);
        }
        /// <summary>Merges a template into one renderer placed at <paramref name="at"/> under <paramref name="parent"/>.</summary>
        Transform Placed(string name, Transform template, Transform parent, Vector3 at)
        {
            var (mesh, finish) = Merge(template, name);
            junctionMeshes.Add(mesh);
            var placed = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            placed.transform.SetParent(parent, false);
            placed.transform.localPosition = at;
            placed.GetComponent<MeshFilter>().sharedMesh = mesh;
            placed.GetComponent<MeshRenderer>().sharedMaterials = finish;
            return placed.transform;
        }
        /// <summary>
        /// A cylinder (or a cone) one unit across and one high, centred on the origin, with <paramref name="sides"/> faces
        /// round it; a cylinder has a top (its bottom stands on the road), a cone a base. Faces wind clockwise seen from outside.
        /// </summary>
        static Mesh Round(int sides, bool pointed)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();
            for (int k = 0; k < sides; k++)
            {
                float a0 = k * Mathf.PI * 2 / sides, a1 = (k + 1) * Mathf.PI * 2 / sides;
                Vector3 r0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)) * .5f, r1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1)) * .5f;
                Vector3 low = Vector3.down * .5f, high = Vector3.up * .5f;
                int first = vertices.Count;
                if (pointed)
                {
                    var middle = (r0 + r1).normalized;
                    var normal = (middle + Vector3.up * .5f).normalized;
                    vertices.AddRange(new[] { r0 + low, high, r1 + low, low, r0 + low, r1 + low });
                    normals.AddRange(new[] { normal, normal, normal, Vector3.down, Vector3.down, Vector3.down });
                    triangles.AddRange(new[] { first, first + 1, first + 2, first + 3, first + 4, first + 5 });
                    continue;
                }
                vertices.AddRange(new[] { r0 + low, r0 + high, r1 + high, r1 + low, high, r1 + high, r0 + high });
                normals.AddRange(new[] { r0 * 2, r0 * 2, r1 * 2, r1 * 2, Vector3.up, Vector3.up, Vector3.up });
                triangles.AddRange(new[] { first, first + 1, first + 2, first, first + 2, first + 3, first + 4, first + 5, first + 6 });
            }
            var mesh = new Mesh { name = pointed ? "Cone" : "Cylinder" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
        void ReleaseJunctionArt()
        {
            if (junctionRoot)
            {
                junctionRoot.gameObject.SetActive(false);
                Destroy(junctionRoot.gameObject);
            }
            junctionRoot = signalRoot = null;
            foreach (var mesh in junctionMeshes)
                if (mesh)
                    Destroy(mesh);
            junctionMeshes.Clear();
        }
        void ReleaseJunctions()
        {
            ReleaseJunctionArt();
            if (cylinder)
                Destroy(cylinder);
            if (cone)
                Destroy(cone);
        }
    }
}
