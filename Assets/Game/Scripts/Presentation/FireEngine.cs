using System.Collections.Generic;
using UnityEngine;
using ValleyRail.Core;

namespace ValleyRail
{
    /// <summary>
    /// The fire engine that answers a town fire (<see cref="CityFires"/>). It pulls out of its station's bay and races
    /// along the middle of the road, clear of the cars in both lanes. By the fire it pulls up onto the kerb and swings
    /// its turntable ladder up to a tall building. Afterwards it drives home and back into the bay. It is the traffic's
    /// fire engine model, but its roof ladder sits on a pivot of its own so it can swing up.
    /// </summary>
    public sealed class FireEngine
    {
        /// <summary>The fire engine in <see cref="VehicleCatalog"/>.</summary>
        public const int Model = 12;
        // Cells per game second: racing along the road, easing out of and into the bay, pulling onto the kerb.
        const float Racing = 1.8f, Easing = .6f, Kerbing = .9f;
        // Curve handles: a quarter turn through a cell (a circle of radius .5), the bay door and the kerb.
        const float Turn = .28f, Door = .2f, Kerb = .3f;
        // How far off a plaza's middle the engine passes its fountain (the basin's half width plus the engine's).
        const float PlazaSwerve = .36f;
        // The turntable in the engine's frame. The roof ladder is every part at LadderY; its tip is LadderTip ahead of the turntable.
        static readonly Vector3 Mount = new Vector3(0, .282f, -.22f);
        const float LadderY = .289f, LadderTip = .41f, FlyLift = .014f, FlyInset = .012f;
        struct Leg
        {
            public Vector3 a, c1, c2, b;
            public float length, pace;
        }
        readonly List<Leg> legs = new List<Leg>();
        readonly Transform body, turntable, fly;
        readonly EmergencyLights lights;
        readonly System.Func<Cell, float> deck;
        readonly System.Func<Cell, bool> plaza;
        int leg;
        float along, extension;
        Quaternion aim = Quaternion.identity;
        public Transform Body => body;
        /// <summary>True while the engine still has road ahead of it.</summary>
        public bool Moving => leg < legs.Count;
        /// <summary>Cells left to drive on the current trip.</summary>
        public float Remaining
        {
            get
            {
                float left = 0;
                for (int i = leg; i < legs.Count; i++)
                    left += legs[i].length * (i == leg ? 1 - along : 1);
                return left;
            }
        }

        /// <summary>
        /// Builds the engine under <paramref name="parent"/>. <paramref name="deckAt"/> is the road height of a cell, and
        /// <paramref name="plazaAt"/> is true for a town's central plaza, whose fountain it drives round.
        /// </summary>
        public FireEngine(WorldView world, Transform parent, EmergencyLights beacons, int livery, System.Func<Cell, float> deckAt, System.Func<Cell, bool> plazaAt)
        {
            lights = beacons;
            deck = deckAt;
            plaza = plazaAt;
            var model = VehicleCatalog.Models[Model];
            body = new GameObject(model.name + " on a call").transform;
            body.SetParent(parent, false);
            turntable = new GameObject("Turntable ladder").transform;
            turntable.SetParent(body, false);
            turntable.localPosition = Mount;
            fly = new GameObject("Fly ladder").transform;
            fly.SetParent(turntable, false);
            foreach (var part in model.Parts(livery))
            {
                if (!Ladder(part))
                {
                    world.Box("Part", part.center, part.size, part.color, body);
                    continue;
                }
                world.Box("Ladder", part.center - Mount, part.size, part.color, turntable);
                // The fly section rides on top of the main ladder, a little narrower, and slides out along it.
                var narrower = part.size.x > part.size.z ? new Vector3(-FlyInset * 2, 0, 0) : Vector3.zero;
                var inward = part.center.x == 0 ? Vector3.zero : new Vector3(-Mathf.Sign(part.center.x) * FlyInset, 0, 0);
                world.Box("Fly ladder", part.center - Mount + inward + Vector3.up * FlyLift, part.size + narrower, part.color, fly);
            }
            lights?.Fit(world, body, model, false);
        }
        /// <summary>The roof ladder's rails and rungs (not the beacons at almost the same height).</summary>
        public static bool Ladder(VehiclePart part) => !part.IsBeacon && Mathf.Abs(part.center.y - LadderY) < .001f;

        /// <summary>The siren, heard like the traffic's emergency calls (SoundEffects).</summary>
        public void Siren(bool on) => lights?.Sound(body, on);

        /// <summary>Out of the bay, along <paramref name="call"/>'s route and onto the kerb at <paramref name="park"/>, pointing <paramref name="heading"/>.</summary>
        public void Out(FireCall call, Vector3 park, int heading)
        {
            legs.Clear();
            leg = 0;
            along = 0;
            var route = call.route;
            int fs = call.stationFacing;
            Add(Centre(call.station.cell), Dir(fs), Edge(call.station.cell, fs), Dir(fs), Door, Easing);
            for (int i = 0; i + 1 < route.Count; i++)
                Through(route[i], i == 0 ? Directions.Opp(fs) : Directions.Between(route[i], route[i - 1]), Directions.Between(route[i], route[i + 1]));
            var last = route[route.Count - 1];
            int entry = route.Count == 1 ? Directions.Opp(fs) : Directions.Between(last, route[route.Count - 2]);
            Add(Edge(last, entry), -Dir(entry), park, Dir(heading), Kerb, Kerbing);
            Pose();
        }
        /// <summary>
        /// Off the kerb at <paramref name="park"/> (turning round across the street when home lies behind), along
        /// <paramref name="route"/> (fire street first, station street last) and back into the bay.
        /// </summary>
        public void Home(FireCall call, List<Cell> route, Vector3 park, int heading)
        {
            legs.Clear();
            leg = 0;
            along = 0;
            int fs = call.stationFacing;
            var street = route[0];
            int exit = route.Count > 1 ? Directions.Between(street, route[1]) : Directions.Opp(fs);
            if (exit == Directions.Opp(heading))
            {
                // Swing out across the street, then back the other way.
                var across = Dir(call.facing);
                var middle = Centre(street) + across * .18f;
                Add(park, Dir(heading), middle, across, Kerb, Kerbing);
                Add(middle, across, Edge(street, exit), Dir(exit), Door, Kerbing);
            }
            else
                Add(park, Dir(heading), Edge(street, exit), Dir(exit), Kerb, Kerbing);
            for (int i = 1; i < route.Count; i++)
                Through(route[i], Directions.Between(route[i], route[i - 1]), i + 1 < route.Count ? Directions.Between(route[i], route[i + 1]) : Directions.Opp(fs));
            Add(Edge(call.station.cell, fs), -Dir(fs), Centre(call.station.cell), -Dir(fs), Door, Easing);
        }
        void Through(Cell cell, int entry, int exit)
        {
            Vector3 inward = -Dir(entry), outward = Dir(exit);
            if (plaza == null || !plaza(cell))
            {
                Add(Edge(cell, entry), inward, Edge(cell, exit), outward, Turn, Racing);
                return;
            }
            // Round the fountain in the middle of the plaza, passing it on the left.
            var right = Right(inward) + Right(outward);
            var round = Centre(cell) + right.normalized * PlazaSwerve;
            var heading = (inward + outward).normalized;
            Add(Edge(cell, entry), inward, round, heading, Door, Racing);
            Add(round, heading, Edge(cell, exit), outward, Door, Racing);
        }
        static Vector3 Right(Vector3 v) => new Vector3(v.z, 0, -v.x);
        /// <summary>A cubic curve from a heading <paramref name="from"/> at a to a heading <paramref name="to"/> at b.</summary>
        void Add(Vector3 a, Vector3 from, Vector3 b, Vector3 to, float handle, float pace)
        {
            var l = new Leg { a = a, c1 = a + from * handle, c2 = b - to * handle, b = b, pace = pace };
            var last = a;
            for (int i = 1; i <= 8; i++)
            {
                var p = Point(l, i / 8f);
                l.length += Vector2.Distance(new Vector2(last.x, last.z), new Vector2(p.x, p.z));
                last = p;
            }
            l.length = Mathf.Max(l.length, .01f);
            legs.Add(l);
        }

        /// <summary>Drives on by <paramref name="seconds"/> of game time; stops at the end of the trip.</summary>
        public void Drive(float seconds)
        {
            while (seconds > 0 && leg < legs.Count)
            {
                var l = legs[leg];
                float left = (1 - along) * l.length, reach = seconds * l.pace;
                if (reach < left)
                {
                    along += reach / l.length;
                    break;
                }
                seconds -= left / l.pace;
                along = 0;
                leg++;
            }
            Pose();
        }
        void Pose()
        {
            if (legs.Count == 0)
                return;
            var l = legs[Mathf.Min(leg, legs.Count - 1)];
            float t = leg < legs.Count ? along : 1;
            body.localPosition = Point(l, t);
            var forward = Tangent(l, t);
            if (forward.sqrMagnitude > 1e-6f)
                body.localRotation = Quaternion.LookRotation(forward);
        }
        static Vector3 Point(Leg l, float t)
        {
            float u = 1 - t;
            var p = u * u * u * l.a + 3 * u * u * t * l.c1 + 3 * u * t * t * l.c2 + t * t * t * l.b;
            p.y = Mathf.Lerp(l.a.y, l.b.y, t);
            return p;
        }
        static Vector3 Tangent(Leg l, float t)
        {
            float u = 1 - t;
            var d = 3 * u * u * (l.c1 - l.a) + 6 * u * t * (l.c2 - l.c1) + 3 * t * t * (l.b - l.c2);
            return new Vector3(d.x, 0, d.z);
        }

        /// <summary>Points the raised ladder at <paramref name="target"/> (in the engine parent's frame), sliding the fly section out to reach it.</summary>
        public void Aim(Vector3 target)
        {
            var local = Quaternion.Inverse(body.localRotation) * (target - body.localPosition) - Mount;
            aim = Quaternion.LookRotation(local);
            extension = Mathf.Max(0, local.magnitude - LadderTip);
        }
        /// <summary>Swings the ladder from its roof rest (0) up to its aim (.6), then slides the fly section out (1).</summary>
        public void Raise(float t)
        {
            float swing = Mathf.SmoothStep(0, 1, Mathf.Clamp01(t / .6f)), slide = Mathf.SmoothStep(0, 1, Mathf.Clamp01((t - .6f) / .4f));
            turntable.localRotation = Quaternion.Slerp(Quaternion.identity, aim, swing);
            fly.localPosition = new Vector3(0, 0, extension * slide);
        }
        /// <summary>The foot of the ladder on the turntable, in the engine parent's frame.</summary>
        public Vector3 Foot => body.localPosition + body.localRotation * Mount;
        /// <summary>The top of the fly ladder, where a firefighter stands, in the engine parent's frame.</summary>
        public Vector3 Tip => body.localPosition + body.localRotation * (Mount + turntable.localRotation * (fly.localPosition + new Vector3(0, FlyLift, LadderTip)));

        Vector3 Centre(Cell c) => new Vector3(c.x, deck(c), c.z);
        /// <summary>The middle of a cell's side, as high as the higher of the two decks it joins.</summary>
        Vector3 Edge(Cell c, int side) =>
            new Vector3(c.x + Directions.Dx[side] * .5f, Mathf.Max(deck(c), deck(c.Move(side))), c.z + Directions.Dz[side] * .5f);
        static Vector3 Dir(int d) => new Vector3(Directions.Dx[d], 0, Directions.Dz[d]);
        public void Release()
        {
            Siren(false);
            if (!body)
                return;
            body.gameObject.SetActive(false);
            Object.Destroy(body.gameObject);
        }
    }
}
