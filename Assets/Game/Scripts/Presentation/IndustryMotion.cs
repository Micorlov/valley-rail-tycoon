using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace ValleyRail
{
    /// <summary>
    /// Makes the map's industries look busy: pumpjacks nod, mine hoists wind, logs feed the sawmills, refinery flares and
    /// furnace mouths flicker, and chimneys smoke. Cosmetic only. It runs on the game clock handed to <see cref="Animate"/>,
    /// so everything freezes while the game is paused.
    /// </summary>
    public sealed class IndustryMotion : MonoBehaviour
    {
        public enum Plume { Steam, Smoke, Soot, Sawdust }

        // Pumpjack linkage in a pump's own frame (the beam runs along z, the horse head at -z over the well).
        /// <summary>Game seconds per pump stroke.</summary>
        public const float PumpStroke = 2.6f;
        public const float CrankRadius = .24f, TailArm = .75f, HeadArm = 1f, BridleDrop = .55f, PitmanSide = .23f, BridleSide = .05f;
        public static readonly Vector3 BeamPivot = new Vector3(0, 1.5f, 0), CrankAxle = new Vector3(0, .55f, .75f);

        // Smoke: every stack keeps a fixed ring of puffs, each a different age, so the plume is continuous.
        /// <summary>Game seconds from leaving the stack to thinning away.</summary>
        public const float PuffLife = 3.2f;
        public const int PuffsPerStack = 8;
        const float PuffRise = 1.5f, PuffSize = .72f;
        static readonly Vector3 Wind = new Vector3(.8f, 0, .35f);

        /// <summary>Game seconds for a mine cage to go up and come back down.</summary>
        public const float HoistCycle = 5f;
        public const float HoistMiddle = 1.05f, HoistTravel = .65f, SheaveRadius = .28f;
        /// <summary>Game seconds for a log to cross the sawmill deck into the hall.</summary>
        public const float LogCycle = 3f;
        public const float LogLength = .5f, DeckEnd = 1.45f, DeckTravel = 1.3f;

        sealed class PumpJack { public Transform beam, crank, carrier; public Transform[] pitmans, bridles; public float phase; }
        sealed class Stack { public Vector3 top; public float size, phase; public Transform[] puffs; }
        sealed class Hoist { public Transform wheel, cage, cable; public Vector3 cableTop; public float phase; }
        sealed class LogFeed { public Transform[] logs; public float phase; }
        sealed class Flame { public Transform flame; public Vector3 size; public float phase; }
        sealed class Glow { public Renderer mouth; public float phase; }

        readonly List<PumpJack> pumps = new List<PumpJack>();
        readonly List<Stack> stacks = new List<Stack>();
        readonly List<Hoist> hoists = new List<Hoist>();
        readonly List<LogFeed> feeds = new List<LogFeed>();
        readonly List<Flame> flames = new List<Flame>();
        readonly List<Glow> glows = new List<Glow>();
        WorldView world;
        Mesh puff;
        Material[] heat;
        // Game seconds since the map was built; double so hours of play never blur the phases.
        double clock;

        public int Pumps => pumps.Count;
        public int Stacks => stacks.Count;
        public int Hoists => hoists.Count;

        public void Initialize(WorldView view)
        {
            world = view;
            puff = PuffMesh();
            heat = new[] { view.Mat(new Color(1, .42f, .12f)), view.Mat(new Color(1, .6f, .2f)), view.Mat(new Color(1, .8f, .38f)) };
        }
        void OnDestroy()
        {
            if (puff)
                Destroy(puff);
        }

        /// <summary>Moves every working part <paramref name="step"/> game seconds on; zero (paused) leaves them where they are.</summary>
        public void Animate(float step)
        {
            if (step <= 0)
                return;
            clock += step;
            foreach (var p in pumps) Pose(p);
            foreach (var s in stacks) Pose(s);
            foreach (var h in hoists) Pose(h);
            foreach (var f in feeds) Pose(f);
            foreach (var f in flames) Pose(f);
            foreach (var g in glows) Pose(g);
        }

        public void AddPumpJack(Transform beam, Transform crank, Transform carrier, Transform[] pitmans, Transform[] bridles, float phase)
        {
            var p = new PumpJack { beam = beam, crank = crank, carrier = carrier, pitmans = pitmans, bridles = bridles, phase = phase };
            pumps.Add(p);
            Pose(p);
        }
        /// <summary>A smoking stack whose mouth is at <paramref name="top"/> in <paramref name="root"/>'s space.</summary>
        public void AddStack(Transform root, Vector3 top, Plume plume, float size = 1)
        {
            var s = new Stack { top = transform.InverseTransformPoint(root.TransformPoint(top)), size = size, phase = Seed(root.TransformPoint(top)), puffs = new Transform[PuffsPerStack] };
            var material = world.Mat(PlumeColor(plume));
            for (int i = 0; i < PuffsPerStack; i++)
            {
                var go = new GameObject("Smoke puff", typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(transform, false);
                go.GetComponent<MeshFilter>().sharedMesh = puff;
                var renderer = go.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                s.puffs[i] = go.transform;
            }
            stacks.Add(s);
            Pose(s);
        }
        public void AddHoist(Transform wheel, Transform cage, Transform cable, Vector3 cableTop, float phase)
        {
            var h = new Hoist { wheel = wheel, cage = cage, cable = cable, cableTop = cableTop, phase = phase };
            hoists.Add(h);
            Pose(h);
        }
        public void AddLogFeed(Transform[] logs, float phase)
        {
            var f = new LogFeed { logs = logs, phase = phase };
            feeds.Add(f);
            Pose(f);
        }
        public void AddFlame(Transform flame, float phase)
        {
            var f = new Flame { flame = flame, size = flame.localScale, phase = phase };
            flames.Add(f);
            Pose(f);
        }
        public void AddGlow(Renderer mouth, float phase)
        {
            var g = new Glow { mouth = mouth, phase = phase };
            glows.Add(g);
            Pose(g);
        }

        /// <summary>
        /// Beam tilt in degrees for a crank angle in degrees; positive lifts the horse head. The pitman arms hold the beam's
        /// tail a fixed height above the crank pin, so the head is at the bottom of its stroke while the pin is at the top.
        /// </summary>
        public static float BeamTilt(float crank) => Mathf.Asin(-CrankRadius * Mathf.Sin(crank * Mathf.Deg2Rad) / TailArm) * Mathf.Rad2Deg;
        /// <summary>How far the polished rod stands above mid-stroke: the horse head is an arc about the pivot, so the bridle unrolls by arc length.</summary>
        public static float RodLift(float tilt) => HeadArm * tilt * Mathf.Deg2Rad;
        public static Vector3 CrankPin(float crank)
        {
            float a = crank * Mathf.Deg2Rad;
            return CrankAxle + new Vector3(0, CrankRadius * Mathf.Sin(a), -CrankRadius * Mathf.Cos(a));
        }
        public static Vector3 BeamTail(float tilt)
        {
            float a = tilt * Mathf.Deg2Rad;
            return BeamPivot + new Vector3(0, -TailArm * Mathf.Sin(a), TailArm * Mathf.Cos(a));
        }
        /// <summary>Where a puff is, relative to its stack's mouth, <paramref name="age"/> game seconds out: it rises, slows and drifts downwind.</summary>
        public static Vector3 PuffOffset(float age)
        {
            float u = Mathf.Clamp01(age / PuffLife);
            return Vector3.up * (PuffRise * (1 - (1 - u) * (1 - u))) + Wind * Mathf.Pow(u, 1.3f);
        }
        /// <summary>A puff's width at <paramref name="age"/>: small at the mouth, swelling, then thinning away to nothing.</summary>
        public static float PuffWidth(float age)
        {
            float u = Mathf.Clamp01(age / PuffLife);
            return PuffSize * (.35f + 1.1f * u) * (1 - u * u * u);
        }
        /// <summary>Stretches a unit cube <paramref name="part"/> into a bar from <paramref name="a"/> to <paramref name="b"/> (parent space).</summary>
        public static void Strut(Transform part, Vector3 a, Vector3 b, float thickness)
        {
            var d = b - a;
            part.localPosition = (a + b) / 2;
            part.localRotation = Quaternion.FromToRotation(Vector3.up, d);
            part.localScale = new Vector3(thickness, d.magnitude, thickness);
        }

        float Cycle(float period, float phase) => (float)((clock / period + phase) % 1.0);

        void Pose(PumpJack p)
        {
            if (!p.beam)
                return;
            float crank = Cycle(PumpStroke, p.phase) * 360;
            float tilt = BeamTilt(crank);
            p.crank.localRotation = Quaternion.Euler(crank, 0, 0);
            p.beam.localRotation = Quaternion.Euler(tilt, 0, 0);
            Vector3 pin = CrankPin(crank), tail = BeamTail(tilt);
            for (int i = 0; i < p.pitmans.Length; i++)
            {
                var side = new Vector3(i == 0 ? -PitmanSide : PitmanSide, 0, 0);
                Strut(p.pitmans[i], pin + side, tail + side, .04f);
            }
            float carrier = BeamPivot.y - BridleDrop + RodLift(tilt);
            p.carrier.localPosition = new Vector3(0, carrier, -HeadArm);
            for (int i = 0; i < p.bridles.Length; i++)
            {
                float x = i == 0 ? -BridleSide : BridleSide;
                Strut(p.bridles[i], new Vector3(x, carrier, -HeadArm), new Vector3(x, BeamPivot.y, -HeadArm), .02f);
            }
        }
        void Pose(Stack s)
        {
            for (int i = 0; i < s.puffs.Length; i++)
            {
                float age = Cycle(PuffLife, s.phase + (float)i / s.puffs.Length) * PuffLife;
                var sway = new Vector3(Mathf.Sin(i * 12.9f + s.phase * 40), 0, Mathf.Cos(i * 7.3f + s.phase * 30)) * (.12f * age / PuffLife);
                var puffTransform = s.puffs[i];
                puffTransform.localPosition = s.top + (PuffOffset(age) + sway) * s.size;
                puffTransform.localRotation = Quaternion.Euler(age * 40 + i * 50, age * 25 + i * 90, 0);
                puffTransform.localScale = Vector3.one * (PuffWidth(age) * s.size);
            }
        }
        void Pose(Hoist h)
        {
            if (!h.wheel)
                return;
            float lift = HoistTravel * Mathf.Sin(Cycle(HoistCycle, h.phase) * 2 * Mathf.PI);
            // The rope runs over the sheave without slipping, so the wheel turns by the cage's travel over its radius.
            h.wheel.localRotation = Quaternion.Euler(lift / SheaveRadius * Mathf.Rad2Deg, 0, 0);
            var cage = h.cage.localPosition;
            h.cage.localPosition = new Vector3(cage.x, HoistMiddle + lift, cage.z);
            Strut(h.cable, new Vector3(h.cableTop.x, HoistMiddle + lift + h.cage.localScale.y / 2, h.cableTop.z), h.cableTop, .025f);
        }
        void Pose(LogFeed f)
        {
            if (f.logs.Length == 0 || !f.logs[0])
                return;
            for (int i = 0; i < f.logs.Length; i++)
            {
                // A log's front end slides from the deck's far end into the hall; the part still off the deck is not drawn.
                float front = DeckEnd - Cycle(LogCycle, f.phase + (float)i / f.logs.Length) * DeckTravel;
                float back = Mathf.Min(front + LogLength, DeckEnd);
                var log = f.logs[i];
                log.localPosition = new Vector3(log.localPosition.x, log.localPosition.y, (front + back) / 2);
                log.localScale = new Vector3(log.localScale.x, log.localScale.y, Mathf.Max(.01f, back - front));
            }
        }
        void Pose(Flame f)
        {
            if (!f.flame)
                return;
            float t = (float)clock + f.phase * 100;
            float flicker = Mathf.PerlinNoise(t * 3.1f, f.phase * 10);
            f.flame.localScale = new Vector3(f.size.x * (.85f + .3f * flicker), f.size.y * (.7f + .6f * flicker), f.size.z * (.85f + .3f * flicker));
            f.flame.localRotation = Quaternion.Euler(Mathf.Sin(t * 2.3f) * 8, 0, Mathf.Sin(t * 1.7f) * 10);
        }
        void Pose(Glow g)
        {
            if (!g.mouth)
                return;
            float t = (float)clock + g.phase * 100;
            int level = Mathf.Min(heat.Length - 1, (int)(Mathf.PerlinNoise(t * 2.2f, g.phase * 10) * heat.Length));
            g.mouth.sharedMaterial = heat[level];
        }

        static Color PlumeColor(Plume plume)
        {
            switch (plume)
            {
                case Plume.Steam: return new Color(.92f, .92f, .9f);
                case Plume.Soot: return new Color(.33f, .31f, .3f);
                case Plume.Sawdust: return new Color(.82f, .7f, .52f);
                default: return new Color(.7f, .7f, .68f);
            }
        }
        /// <summary>A phase from a place on the map, so neighbouring stacks and machines never move in step.</summary>
        public static float Seed(Vector3 at) => Mathf.Repeat(at.x * .137f + at.z * .291f + at.y * .05f, 1);
        /// <summary>A faceted puff: a unit-wide icosahedron with flat faces, matching the map's low-poly look.</summary>
        public static Mesh PuffMesh()
        {
            float g = (1 + Mathf.Sqrt(5)) / 2;
            var corners = new[]
            {
                new Vector3(-1, g, 0), new Vector3(1, g, 0), new Vector3(-1, -g, 0), new Vector3(1, -g, 0),
                new Vector3(0, -1, g), new Vector3(0, 1, g), new Vector3(0, -1, -g), new Vector3(0, 1, -g),
                new Vector3(g, 0, -1), new Vector3(g, 0, 1), new Vector3(-g, 0, -1), new Vector3(-g, 0, 1),
            };
            int[] faces =
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
            };
            var vertices = new List<Vector3>(faces.Length);
            var triangles = new List<int>(faces.Length);
            for (int f = 0; f < faces.Length; f += 3)
            {
                Vector3 a = corners[faces[f]].normalized * .5f, b = corners[faces[f + 1]].normalized * .5f, c = corners[faces[f + 2]].normalized * .5f;
                // Unity draws triangles that wind clockwise seen from outside; flip any that do not.
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), a + b + c) < 0)
                    (b, c) = (c, b);
                int start = vertices.Count;
                vertices.Add(a);
                vertices.Add(b);
                vertices.Add(c);
                triangles.Add(start);
                triangles.Add(start + 1);
                triangles.Add(start + 2);
            }
            var mesh = new Mesh { name = "Smoke puff" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
