using System.Collections.Generic;
using UnityEngine;
using ValleyRail.Core;

namespace ValleyRail
{
    /// <summary>
    /// Cosmetic life at every open campsite: the campfire burns with smoke rising, campers stand round it while a child
    /// runs laps, others walk between their pitch, the shower block and the reception, cars stand on the pitches, and
    /// now and then a visitor drives in from the highway to the free pitch (pitching a tent at a tent campsite), stays a
    /// while and drives off again. People show only when zoomed in; the fire and cars always. Never reads or changes
    /// simulation randomness.
    /// </summary>
    public sealed class CampLife : MonoBehaviour
    {
        enum Visit { Away, Arriving, Staying, Leaving }
        sealed class Walker
        {
            public Transform view;
            public List<Vector3> route;
            public float along, wait, pace;
            public int leg, step = 1;
        }
        sealed class Site
        {
            public int a, b, at, side, kind;
            public CampLayout layout;
            public IntercityRoadState road;
            public Transform root, people, fire, visitor, tent;
            public readonly List<Transform> flames = new List<Transform>(), smoke = new List<Transform>(), circle = new List<Transform>();
            public readonly List<Walker> walkers = new List<Walker>();
            public Transform runner;
            public Visit visit;
            public readonly List<Vector3> route = new List<Vector3>();
            public int leg;
            public float along, wait, stay, clock;
        }
        const float CarSpeed = .45f, FigureScale = 1.5f, RunnerRing = .44f, RunnerSpeed = 1.1f, WalkPace = .2f, TentRise = 1.5f;
        // Cars on the pitches; a caravan park's visitors come in an SUV or a pickup.
        static readonly int[] Cars = { 0, 1, 2, 0, 1, 2, 5 };
        static readonly int[] CaravanVisitors = { 2, 5 };
        static readonly Color[] Shirts = { new Color(.9f, .3f, .3f), new Color(.95f, .95f, .92f), new Color(.3f, .6f, .9f), new Color(.95f, .75f, .2f), new Color(.35f, .7f, .45f), new Color(.6f, .4f, .75f) };
        static readonly Color FlameOrange = new Color(1f, .55f, .12f), FlameYellow = new Color(1f, .86f, .3f), FlameRed = new Color(.9f, .25f, .1f), Smoke = new Color(.72f, .72f, .72f),
            TentColour = new Color(.95f, .5f, .15f);
        readonly List<Site> sites = new List<Site>();
        readonly System.Random random = new System.Random(2718);
        WorldView world;
        CityTraffic traffic;
        GameSession session;
        int cityRevision = -1;
        bool showingPeople = true;

        public int Sites => sites.Count;
        /// <summary>Visitors that have driven in from the highway, and ones that have driven back onto it, since the game loaded.</summary>
        public int Arrivals { get; private set; }
        public int Departures { get; private set; }
        public int Kind(int site) => sites[site].kind;
        /// <summary>Campers on the site: round the fire, walking and the running child.</summary>
        public int Campers(int site) => sites[site].circle.Count + sites[site].walkers.Count + (sites[site].runner ? 1 : 0);
        /// <summary>The fire's tallest flame, for tests that it flickers.</summary>
        public float FlameHeight(int site) => sites[site].flames[0].localScale.y;
        public bool VisitorParked(int site) => sites[site].visit == Visit.Staying;
        /// <summary>How far the visitor's tent is pitched: 0 struck, 1 up.</summary>
        public float TentUp(int site) => sites[site].tent ? sites[site].tent.localScale.y : 0;

        /// <summary>Opens life at campsites that have just opened; ones that stay open keep their campers.</summary>
        public void Refresh(WorldView view, GameSession game, CityTraffic vehicles)
        {
            world = view;
            traffic = vehicles;
            if (session == game && cityRevision == game.World.cityRevision)
                return;
            if (session != game)
                for (int i = sites.Count - 1; i >= 0; i--)
                    Close(i);
            session = game;
            cityRevision = game.World.cityRevision;
            for (int i = sites.Count - 1; i >= 0; i--)
            {
                sites[i].road = Road(game, sites[i]);
                if (sites[i].road == null)
                    Close(i);
            }
            foreach (var road in game.World.intercityRoads)
                if (Campsites.Open(road) && Campsites.Shaped(road) && sites.Find(s => Same(s, road)) == null)
                    sites.Add(Open(road));
        }
        static bool Same(Site site, IntercityRoadState road) =>
            site.a == road.a && site.b == road.b && site.at == road.campAt && site.side == road.campSide && site.kind == road.campKind;
        static IntercityRoadState Road(GameSession game, Site site)
        {
            foreach (var road in game.World.intercityRoads)
                if (Campsites.Open(road) && Campsites.Shaped(road) && Same(site, road))
                    return road;
            return null;
        }
        void Close(int index)
        {
            sites[index].root.gameObject.SetActive(false);
            Destroy(sites[index].root.gameObject);
            sites.RemoveAt(index);
        }
        Site Open(IntercityRoadState road)
        {
            var f = new CampLayout(road);
            var site = new Site { a = road.a, b = road.b, at = road.campAt, side = road.campSide, kind = road.campKind, layout = f, road = road };
            site.root = new GameObject("Campsite " + road.path[road.campAt]).transform;
            site.root.SetParent(transform, false);
            site.people = new GameObject("Campsite people").transform;
            site.people.SetParent(site.root, false);
            site.people.gameObject.SetActive(showingPeople);
            OpenFire(site, f);
            // Cars on the taken pitches, noses in.
            for (int p = 0; p < Campsites.Pitches; p++)
                if (p != CampLayout.Visitor)
                {
                    var car = traffic.Vehicle(Cars[random.Next(Cars.Length)], random.Next(8), site.root);
                    car.localPosition = f.CarSpot(p);
                    car.localRotation = f.Facing(0, -CampLayout.ToLane(p));
                }
            // Three campers round the fire, facing it.
            var fire = f.At(CampLayout.FireU, CampLayout.FireV, .025f);
            for (int i = 0; i < 3; i++)
            {
                float angle = Mathf.PI * (.1f + i * .62f);
                var person = CityLife.Figure(world, site.people, "Camper", Shirts[random.Next(Shirts.Length)], FigureScale);
                person.localPosition = fire + f.Offset(Mathf.Cos(angle), Mathf.Sin(angle)) * .24f;
                person.localRotation = Quaternion.LookRotation(fire - person.localPosition);
                site.circle.Add(person);
            }
            site.runner = CityLife.Figure(world, site.people, "Child", Shirts[random.Next(Shirts.Length)], FigureScale * .75f);
            // One camper walks between a back-row pitch and the shower block, another between a front pitch and the reception.
            float lane = CampLayout.LaneV, gate = CampLayout.GateU;
            AddWalker(site, new List<Vector3> {
                f.At(CampLayout.PitchU[3] - .12f, CampLayout.RowB - .3f, .025f), f.At(CampLayout.PitchU[3] - .12f, lane + .1f, .025f),
                f.At(CampLayout.ShowerU + .11f, lane + .1f, .025f), f.At(CampLayout.ShowerU + .11f, CampLayout.ShowerV - .26f, .025f) });
            AddWalker(site, new List<Vector3> {
                f.At(CampLayout.PitchU[1] - .12f, CampLayout.RowA + .3f, .025f), f.At(CampLayout.PitchU[1] - .12f, lane - .1f, .025f),
                f.At(gate + .12f, lane - .1f, .025f), f.At(gate + .12f, CampLayout.OfficeV - .06f, .025f), f.At(CampLayout.OfficeU - .25f, CampLayout.OfficeV - .06f, .025f) });
            // The visitor: waiting away up the road, then a car (and at a tent campsite, the tent it pitches).
            int model = f.kind == 1 ? CaravanVisitors[random.Next(CaravanVisitors.Length)] : Cars[random.Next(Cars.Length)];
            site.visitor = traffic.Vehicle(model, random.Next(8), site.root);
            site.visitor.gameObject.SetActive(false);
            if (f.kind == 0)
            {
                site.tent = world.VisitorTent(site.root, TentColour);
                site.tent.localPosition = f.Unit(CampLayout.Visitor, .024f);
                site.tent.localRotation = f.Facing(0, 1);
                site.tent.localScale = new Vector3(1, 0, 1);
                site.tent.gameObject.SetActive(false);
            }
            site.visit = Visit.Away;
            site.wait = 3 + (float)random.NextDouble() * 6;
            return site;
        }
        void OpenFire(Site site, CampLayout f)
        {
            site.fire = new GameObject("Campfire").transform;
            site.fire.SetParent(site.root, false);
            site.fire.localPosition = f.At(CampLayout.FireU, CampLayout.FireV, .03f);
            var colours = new[] { FlameOrange, FlameYellow, FlameRed };
            for (int i = 0; i < 3; i++)
            {
                var flame = new GameObject("Flame").transform;
                flame.SetParent(site.fire, false);
                flame.localPosition = new Vector3((i - 1) * .025f, 0, (i % 2) * .02f - .01f);
                flame.localRotation = Quaternion.AngleAxis(45 + i * 30, Vector3.up);
                world.Box("Flame", new Vector3(0, .5f, 0), new Vector3(.05f, 1, .05f), colours[i], flame);
                flame.localScale = new Vector3(1, .12f, 1);
                site.flames.Add(flame);
            }
            for (int i = 0; i < 3; i++)
            {
                var puff = world.Box("Smoke", Vector3.zero, Vector3.one * .04f, Smoke, site.fire).transform;
                site.smoke.Add(puff);
            }
        }
        void AddWalker(Site site, List<Vector3> route)
        {
            var view = CityLife.Figure(world, site.people, "Camper", Shirts[random.Next(Shirts.Length)], FigureScale);
            view.localPosition = route[0];
            site.walkers.Add(new Walker { view = view, route = route, pace = WalkPace * (.85f + .3f * (float)random.NextDouble()), wait = (float)random.NextDouble() * 4 });
        }
        public void Animate(float dt, float speed, bool near)
        {
            if (near != showingPeople)
            {
                showingPeople = near;
                foreach (var site in sites)
                    site.people.gameObject.SetActive(near);
            }
            if (speed <= 0)
                return;
            float life = dt * Mathf.Min(speed, 2), drive = dt * speed;
            foreach (var site in sites)
            {
                site.clock += life;
                AnimateFire(site);
                if (near)
                    AnimatePeople(site, life);
                AnimateVisitor(site, drive);
            }
        }
        static void AnimateFire(Site site)
        {
            float t = site.clock;
            for (int i = 0; i < site.flames.Count; i++)
            {
                float flicker = .75f + .25f * Mathf.Sin(t * (9 + i * 3.1f) + i) * Mathf.Sin(t * (4.3f + i) + 2 * i);
                site.flames[i].localScale = new Vector3(.8f + .2f * flicker, (.13f - .025f * i) * flicker, .8f + .2f * flicker);
            }
            for (int i = 0; i < site.smoke.Count; i++)
            {
                float phase = Mathf.Repeat(t * .35f + i / (float)site.smoke.Count, 1);
                var puff = site.smoke[i];
                puff.localPosition = new Vector3(Mathf.Sin(t * .7f + i) * .04f * phase, .14f + phase * .55f, phase * .08f);
                puff.localScale = Vector3.one * (.03f + phase * .07f);
            }
        }
        void AnimatePeople(Site site, float step)
        {
            var f = site.layout;
            var fire = f.At(CampLayout.FireU, CampLayout.FireV, .025f);
            for (int i = 0; i < site.circle.Count; i++)
            {
                var person = site.circle[i];
                var toFire = fire - person.localPosition;
                toFire.y = 0;
                if (toFire.sqrMagnitude > 1e-6f)
                    person.localRotation = Quaternion.AngleAxis(Mathf.Sin(site.clock * .8f + i * 2) * 12, Vector3.up) * Quaternion.LookRotation(toFire);
            }
            // The child runs laps round the fire.
            float angle = site.clock * RunnerSpeed / RunnerRing;
            var ring = f.Offset(Mathf.Cos(angle), Mathf.Sin(angle)) * RunnerRing;
            site.runner.localPosition = fire + ring + Vector3.up * Mathf.Abs(Mathf.Sin(site.clock * 9)) * .012f;
            var ahead = Vector3.Cross(Vector3.up, ring);
            if (ahead.sqrMagnitude > 1e-6f)
                site.runner.localRotation = Quaternion.LookRotation(-ahead);
            foreach (var walker in site.walkers)
                Walk(site, walker, step);
        }
        static void Walk(Site site, Walker walker, float step)
        {
            if (walker.wait > 0)
            {
                walker.wait -= step;
                return;
            }
            int from = walker.leg, to = walker.leg + walker.step;
            var a = walker.route[from];
            var b = walker.route[to];
            float length = Vector3.Distance(a, b);
            walker.along += step * walker.pace;
            if (walker.along >= length)
            {
                walker.along = 0;
                walker.leg = to;
                // At either end the camper spends a while (the showers, the reception, their pitch) and turns back.
                if (to == 0 || to == walker.route.Count - 1)
                {
                    walker.step = -walker.step;
                    walker.wait = 4 + 6 * Mathf.PerlinNoise(site.clock, walker.pace * 10);
                }
                walker.view.localPosition = walker.route[to];
                return;
            }
            walker.view.localPosition = Vector3.Lerp(a, b, length > 0 ? walker.along / length : 1);
            if ((b - a).sqrMagnitude > 1e-6f)
                walker.view.localRotation = Quaternion.LookRotation(b - a);
        }
        void AnimateVisitor(Site site, float step)
        {
            var f = site.layout;
            switch (site.visit)
            {
                case Visit.Away:
                    site.wait -= step;
                    if (site.wait > 0 || site.road == null)
                        return;
                    site.route.Clear();
                    site.route.AddRange(f.WayIn(site.road, CampLayout.Visitor));
                    site.leg = 0;
                    site.along = 0;
                    site.visit = Visit.Arriving;
                    site.visitor.gameObject.SetActive(true);
                    Arrivals++;
                    Drive(site, 0);
                    return;
                case Visit.Arriving:
                    if (Drive(site, step))
                    {
                        site.visit = Visit.Staying;
                        site.stay = site.wait = 25 + (float)random.NextDouble() * 20;
                        if (site.tent)
                            site.tent.gameObject.SetActive(true);
                    }
                    return;
                case Visit.Staying:
                    site.wait -= step;
                    // The tent goes up in the first seconds and comes down in the last ones.
                    if (site.tent)
                        site.tent.localScale = new Vector3(1, Mathf.Clamp01(Mathf.Min(site.stay - site.wait, site.wait) / TentRise), 1);
                    if (site.wait > 0)
                        return;
                    if (site.tent)
                    {
                        site.tent.localScale = new Vector3(1, 0, 1);
                        site.tent.gameObject.SetActive(false);
                    }
                    site.route.Clear();
                    site.route.AddRange(f.WayOut(site.road, CampLayout.Visitor));
                    site.leg = 0;
                    site.along = 0;
                    site.visit = Visit.Leaving;
                    return;
                case Visit.Leaving:
                    if (Drive(site, step))
                    {
                        site.visit = Visit.Away;
                        site.visitor.gameObject.SetActive(false);
                        site.wait = 12 + (float)random.NextDouble() * 18;
                        Departures++;
                    }
                    return;
            }
        }
        /// <summary>Moves the visitor along its route; true at the end. It reverses off the pitch on the way out.</summary>
        bool Drive(Site site, float step)
        {
            site.along += step * CarSpeed;
            while (site.leg < site.route.Count - 1)
            {
                var a = site.route[site.leg];
                var b = site.route[site.leg + 1];
                float length = Vector3.Distance(a, b);
                if (site.along <= length)
                {
                    site.visitor.localPosition = Vector3.Lerp(a, b, length > 0 ? site.along / length : 1);
                    var heading = b - a;
                    heading.y = 0;
                    if (heading.sqrMagnitude > 1e-6f)
                        site.visitor.localRotation = Quaternion.LookRotation(site.visit == Visit.Leaving && site.leg == 0 ? -heading : heading);
                    return false;
                }
                site.along -= length;
                site.leg++;
            }
            site.visitor.localPosition = site.route[site.route.Count - 1];
            return true;
        }
    }
}
