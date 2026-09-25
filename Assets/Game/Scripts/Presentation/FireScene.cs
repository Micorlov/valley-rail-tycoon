using System.Collections.Generic;
using UnityEngine;

namespace ValleyRail
{
    /// <summary>
    /// What a town fire looks like (<see cref="CityFires"/>): flames licking out of the windows and roof, embers, and a
    /// tall smoke plume that turns to white steam once the water hits. The crew climbs down from the engine, runs hoses
    /// to both ends of it and sprays arcs of water, one of them from the top of the ladder at a tall building.
    /// Neighbours watch from across the street, and the windows stay sooty until the engine leaves. Flames, embers and
    /// water glow (unlit), so they read even in the building's shadow.
    /// </summary>
    public sealed class FireScene
    {
        /// <summary>The burning building's lot frame and walls, and where the engine stands in the street in front of it.</summary>
        public struct Site
        {
            /// <summary>The lot's middle on the ground; <see cref="turn"/> points +z at the street.</summary>
            public Vector3 at;
            public Quaternion turn;
            /// <summary>The walls in cells: across the front, front to back, and up to the eaves.</summary>
            public float width, depth, height;
            /// <summary>The middle of the street cell in front; the engine's spot and heading there.</summary>
            public Vector3 street, park, heading;
            /// <summary>A building tall enough for the ladder.</summary>
            public bool tall;
        }
        /// <summary>The raised ladder: its foot on the engine's roof, its tip, and how far up it a firefighter has climbed (0 to 1).</summary>
        public struct Ladder
        {
            public Vector3 foot, tip;
            public float climb;
        }
        sealed class Flame
        {
            public Transform outer, side, inner;
            public Vector3 at;
            public float size, from, phase;
        }
        sealed class Crew
        {
            public Transform view;
            public Vector3 post;
            public int target;
        }
        const int Puffs = 18, Embers = 10, DropsPerJet = 12;
        // Firefighters' kit; smoke from black to steam; soot left on the walls.
        static readonly Color Coat = new Color(.14f, .16f, .2f), Band = new Color(.96f, .88f, .28f), Helmet = new Color(.97f, .78f, .1f),
            Hose = new Color(.55f, .12f, .08f), Soot = new Color(.09f, .08f, .08f);
        static readonly Color[] Smoke = { new Color(.16f, .16f, .17f), new Color(.3f, .3f, .31f), new Color(.5f, .5f, .52f), new Color(.7f, .71f, .72f), new Color(.88f, .89f, .9f) };
        static readonly Color FlameOuter = new Color(1, .45f, .08f, .9f), FlameSide = new Color(.93f, .22f, .05f, .9f), FlameInner = new Color(1, .86f, .3f, .95f), Ember = new Color(1, .55f, .12f, 1), Water = new Color(.78f, .92f, 1, .9f);
        // The wind carries the smoke east and a little north, as it does chimney smoke.
        static readonly Vector3 Wind = new Vector3(.55f, 0, .2f);
        readonly WorldView world;
        readonly Transform root;
        readonly Site site;
        readonly Mesh glowCube, tongue;
        readonly System.Func<Color, bool, Material> glow;
        readonly List<Flame> flames = new List<Flame>();
        readonly List<Crew> crew = new List<Crew>();
        readonly List<Transform> puffs = new List<Transform>(), embers = new List<Transform>(), watchers = new List<Transform>(), soot = new List<Transform>(), hoses = new List<Transform>();
        readonly List<Transform[]> jets = new List<Transform[]>();
        readonly int[] shades = new int[Puffs];
        readonly Vector3 across, along, doorway;
        float scorched;
        public Transform Root => root;
        /// <summary>Flames showing now.</summary>
        public int Burning { get; private set; }
        /// <summary>Water drops in the air now.</summary>
        public int Droplets { get; private set; }

        /// <summary>
        /// Builds the scene; <paramref name="unlitCube"/> and <paramref name="unlitTongue"/> are meshes with white vertex
        /// colours for <paramref name="unlit"/> materials (colour, drawn over other glow).
        /// </summary>
        public FireScene(WorldView view, Transform parent, Site where, Mesh unlitCube, Mesh unlitTongue, System.Func<Color, bool, Material> unlit, System.Random random)
        {
            world = view;
            site = where;
            glowCube = unlitCube;
            tongue = unlitTongue;
            glow = unlit;
            root = new GameObject("Fire").transform;
            root.SetParent(parent, false);
            across = site.turn * Vector3.forward;
            along = site.heading;
            doorway = site.park - across * .02f;
            float w = site.width, d = site.depth, h = site.height;
            // The fire starts behind a front window, spreads to the other and then breaks through the roof.
            AddFlame(new Vector3(-w * .26f, h * .45f, d / 2 + .02f), .65f, 0, random);
            AddFlame(new Vector3(w * .26f, h * .45f, d / 2 + .02f), .65f, .15f, random);
            if (site.tall)
            {
                AddFlame(new Vector3(0, h * .78f, d / 2 + .02f), .75f, .25f, random);
                AddFlame(new Vector3(w / 2 + .02f, h * .6f, 0), .65f, .35f, random);
            }
            AddFlame(new Vector3(-w * .2f, h + .08f, -d * .1f), 1, .3f, random);
            AddFlame(new Vector3(w * .2f, h + .12f, d * .06f), 1, .42f, random);
            AddFlame(new Vector3(0, h + .2f, -d * .02f), 1.2f, .55f, random);
            foreach (var flame in flames)
                if (flame.at.y < h)
                    soot.Add(world.Box("Soot", Lot(flame.at + new Vector3(0, .02f, 0)), flame.at.z > d / 2 ? new Vector3(.17f, .14f, .012f) : new Vector3(.012f, .14f, .17f), Soot, root, site.turn).transform);
            for (int i = 0; i < Puffs; i++)
                puffs.Add(world.Box("Smoke", site.at, Vector3.one * .1f, Smoke[0], root).transform);
            for (int i = 0; i < Embers; i++)
                embers.Add(Glow("Ember", Ember, Vector3.one * .025f));
            var street = site.street;
            // Two on hoses at either end of the engine, one at the pump and one up the ladder or beside the pump.
            AddCrew(street - across * .44f + along * .5f, 0);
            AddCrew(street - across * .44f - along * .5f, 1);
            AddCrew(street - across * .56f + along * .06f, -1);
            AddCrew(street - across * .57f - along * .16f, site.tall ? 2 : -1);
            for (int i = 0; i < 2; i++)
                hoses.Add(world.Box("Hose", Vector3.zero, new Vector3(.016f, .012f, 1), Hose, root).transform);
            for (int j = 0; j < 3; j++)
            {
                var drops = new Transform[DropsPerJet];
                for (int i = 0; i < DropsPerJet; i++)
                    drops[i] = Glow("Water", Water, Vector3.one * .028f);
                jets.Add(drops);
            }
            for (int i = 0; i < 3; i++)
            {
                var person = CityLife.Figure(world, root, "Onlooker", CityLife.Shirts[random.Next(CityLife.Shirts.Length)], 1);
                person.localPosition = street + across * .43f + along * (-.3f + i * .27f + (float)random.NextDouble() * .06f);
                person.localRotation = Quaternion.LookRotation(-across);
                watchers.Add(person);
            }
            Animate(0, 0, 0, 0, 0, false, null);
        }
        void AddFlame(Vector3 at, float size, float from, System.Random random) => flames.Add(new Flame
        {
            at = at, size = size, from = from, phase = (float)random.NextDouble() * 6.3f,
            outer = Glow("Flame", FlameOuter, Vector3.one, tongue), side = Glow("Flame", FlameSide, Vector3.one, tongue),
            inner = Glow("Flame core", FlameInner, Vector3.one, tongue, true),
        });
        void AddCrew(Vector3 post, int target)
        {
            var view = CityLife.Figure(world, root, "Firefighter", Coat, 1);
            world.Box("Reflective band", new Vector3(0, .075f, 0), new Vector3(.054f, .012f, .034f), Band, view);
            world.Box("Helmet", new Vector3(0, .15f, 0), new Vector3(.038f, .016f, .038f), Helmet, view);
            crew.Add(new Crew { view = view, post = post, target = target });
        }
        Transform Glow(string name, Color color, Vector3 size, Mesh mesh = null, bool onTop = false)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(root, false);
            go.transform.localScale = size;
            go.GetComponent<MeshFilter>().sharedMesh = mesh ? mesh : glowCube;
            go.GetComponent<MeshRenderer>().sharedMaterial = glow(color, onTop);
            return go.transform;
        }
        Vector3 Lot(Vector3 local) => site.at + site.turn * local;

        /// <summary>
        /// Poses everything at <paramref name="time"/> seconds: <paramref name="heat"/> (0 out, 1 ablaze) sizes the
        /// flames, <paramref name="steam"/> whitens the smoke, <paramref name="deploy"/> walks the crew from the engine to
        /// their posts, <paramref name="spray"/> turns the water on and <paramref name="ladder"/>, once raised, takes one
        /// of them up it.
        /// </summary>
        public void Animate(float time, float heat, float steam, float deploy, float spray, bool watching, Ladder? ladder)
        {
            scorched = Mathf.Max(scorched, heat);
            AnimateFlames(time, heat);
            AnimateSmoke(time, heat, steam);
            AnimateCrew(time, deploy, spray, ladder);
            foreach (var person in watchers)
                person.gameObject.SetActive(watching);
            foreach (var patch in soot)
                patch.gameObject.SetActive(scorched > .4f);
        }
        void AnimateFlames(float time, float heat)
        {
            Burning = 0;
            foreach (var flame in flames)
            {
                // Each spot catches once the fire is hot enough and goes out first when it cools.
                float grown = Mathf.Clamp01((heat - flame.from) / .35f) * flame.size;
                bool on = grown > .02f;
                flame.outer.gameObject.SetActive(on);
                flame.side.gameObject.SetActive(on);
                flame.inner.gameObject.SetActive(on);
                if (!on)
                    continue;
                Burning++;
                float flicker = .8f + .22f * Mathf.Sin(time * 11 + flame.phase) + .12f * Mathf.Sin(time * 27 + flame.phase * 2);
                float other = .8f + .22f * Mathf.Sin(time * 13 + flame.phase * 3) + .12f * Mathf.Sin(time * 23 + flame.phase);
                float sway = .9f + .1f * Mathf.Sin(time * 7 + flame.phase);
                // The tongues lean with the wind and wave a little each on its own beat.
                var lean = Quaternion.Euler(Wind.z * 25 + Mathf.Sin(time * 5 + flame.phase) * 6, flame.phase * 30, -Wind.x * 25 + Mathf.Sin(time * 6 + flame.phase) * 6);
                var at = Lot(flame.at);
                var beside = site.turn * new Vector3(.07f, 0, 0) * grown;
                Tongue(flame.outer, at - beside * .4f, new Vector3(.2f * sway, .42f * flicker, .2f * sway) * grown, lean);
                Tongue(flame.side, at + beside, new Vector3(.13f, .3f * other, .13f) * grown, lean * Quaternion.Euler(0, 45, 0));
                Tongue(flame.inner, at - beside * .4f, new Vector3(.1f, .26f * (2 - flicker), .1f) * grown, lean);
            }
            for (int i = 0; i < embers.Count; i++)
            {
                var flame = flames[flames.Count - 1 - i % 3];
                float u = Mathf.Repeat(time * .55f + i / (float)Embers, 1);
                bool on = heat > .5f;
                embers[i].gameObject.SetActive(on);
                if (!on)
                    continue;
                embers[i].localPosition = Lot(flame.at) + Wind * u * .5f + new Vector3(Mathf.Sin(i * 2.7f + time * 3) * .1f, u * .95f, Mathf.Cos(i * 1.9f + time * 2) * .1f);
                embers[i].localScale = Vector3.one * .026f * (1 - u) * heat;
            }
        }
        static void Tongue(Transform view, Vector3 at, Vector3 size, Quaternion turn)
        {
            view.localPosition = at;
            view.localScale = size;
            view.localRotation = turn;
        }
        void AnimateSmoke(float time, float heat, float steam)
        {
            float density = Mathf.Max(heat, steam * .8f);
            // Black while it burns freely, greyer as the water bites, white steam once it is out.
            float whiteness = Mathf.Clamp01(steam / (heat + steam + .001f) + (1 - heat) * .2f);
            var top = site.at + Vector3.up * (site.height + .3f);
            for (int i = 0; i < puffs.Count; i++)
            {
                float u = Mathf.Repeat(time * .16f + i / (float)Puffs, 1);
                float grow = Mathf.Min(1, u / .08f) * Mathf.Min(1, (1 - u) / .15f);
                float size = (.14f + u * .62f) * density * grow;
                var puff = puffs[i];
                puff.gameObject.SetActive(size > .01f);
                if (size <= .01f)
                    continue;
                puff.localPosition = top + Wind * u * 1.6f + new Vector3(Mathf.Sin(time * .7f + i) * .08f, u * 3.2f, Mathf.Cos(time * .5f + i * 1.3f) * .08f);
                puff.localScale = Vector3.one * size;
                // Higher puffs thin to a paler grey.
                int shade = Mathf.Clamp(Mathf.RoundToInt(whiteness * (Smoke.Length - 1) + u * 1.2f), 0, Smoke.Length - 1);
                if (shades[i] != shade)
                {
                    shades[i] = shade;
                    puff.GetComponent<Renderer>().sharedMaterial = world.Mat(Smoke[shade]);
                }
            }
        }
        void AnimateCrew(float time, float deploy, float spray, Ladder? ladder)
        {
            Droplets = 0;
            float walk = Mathf.SmoothStep(0, 1, deploy);
            for (int i = 0; i < crew.Count; i++)
            {
                var member = crew[i];
                bool upLadder = member.target == 2 && ladder.HasValue && ladder.Value.climb > 0;
                bool on = deploy > .02f;
                member.view.gameObject.SetActive(on);
                if (!on)
                    continue;
                var at = upLadder ? Climb(member.post, ladder.Value) : Vector3.Lerp(doorway, member.post, walk);
                member.view.localPosition = at;
                var aimAt = member.target >= 0 ? Target(member.target) : Lot(Vector3.zero);
                var look = walk < 1 && !upLadder ? member.post - doorway : aimAt - at;
                look.y = 0;
                if (look.sqrMagnitude > 1e-5f)
                    member.view.localRotation = Quaternion.LookRotation(look);
            }
            // The hoses run from the pump along the ground to the two at either end.
            var pump = doorway - across * .04f;
            for (int i = 0; i < hoses.Count; i++)
            {
                var end = crew[i].view.localPosition;
                var span = new Vector3(end.x - pump.x, 0, end.z - pump.z);
                bool laid = deploy > .02f && span.sqrMagnitude > 1e-4f;
                hoses[i].gameObject.SetActive(laid);
                if (!laid)
                    continue;
                hoses[i].localPosition = new Vector3((pump.x + end.x) / 2, site.street.y + .008f, (pump.z + end.z) / 2);
                hoses[i].localRotation = Quaternion.LookRotation(span);
                hoses[i].localScale = new Vector3(.016f, .012f, span.magnitude);
            }
            for (int j = 0; j < jets.Count; j++)
            {
                var from = j < 2 ? crew[j].view : crew[3].view;
                bool on = spray > .01f && deploy >= 1 && (j < 2 || (site.tall && ladder.HasValue && ladder.Value.climb >= 1));
                var start = from.localPosition + from.localRotation * new Vector3(0, .09f, .05f);
                var end = Target(j) + new Vector3(Mathf.Sin(time * 1.7f + j) * .04f, 0, Mathf.Cos(time * 1.3f + j) * .04f);
                float arc = .12f + .2f * Vector3.Distance(new Vector3(start.x, 0, start.z), new Vector3(end.x, 0, end.z));
                var drops = jets[j];
                for (int i = 0; i < drops.Length; i++)
                {
                    // The water leaves the nozzle a drop at a time, so turning it on or off runs along the jet.
                    float u = Mathf.Repeat(time * 1.3f + i / (float)DropsPerJet, 1);
                    bool wet = on && u < spray * 1.2f;
                    drops[i].gameObject.SetActive(wet);
                    if (!wet)
                        continue;
                    Droplets++;
                    drops[i].localPosition = Vector3.Lerp(start, end, u) + Vector3.up * arc * 4 * u * (1 - u);
                }
            }
        }
        /// <summary>A quarter of the climb from the ground up onto the engine's roof, the rest up the ladder to its tip.</summary>
        static Vector3 Climb(Vector3 post, Ladder ladder) => ladder.climb < .25f
            ? Vector3.Lerp(post, ladder.foot, ladder.climb / .25f)
            : Vector3.Lerp(ladder.foot, ladder.tip, (ladder.climb - .25f) / .75f);
        /// <summary>Where each jet aims: the two front windows, and from the ladder the flames on the roof.</summary>
        Vector3 Target(int jet)
        {
            float w = site.width, d = site.depth, h = site.height;
            return jet == 0 ? Lot(new Vector3(w * .26f, h * .5f, d / 2)) : jet == 1 ? Lot(new Vector3(-w * .26f, h * .5f, d / 2)) : Lot(new Vector3(0, h + .12f, d * .05f));
        }
        public void Release()
        {
            if (!root)
                return;
            root.gameObject.SetActive(false);
            Object.Destroy(root.gameObject);
        }
    }
}
