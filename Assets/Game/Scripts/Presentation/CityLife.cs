using System.Collections.Generic;
using UnityEngine;
using ValleyRail.Core;

namespace ValleyRail
{
    /// <summary>
    /// Cosmetic town life shown only when zoomed in: pedestrians on the pavements, chimney smoke, waving flags,
    /// flashing vehicle lights and fountain jets. Never reads or changes simulation randomness.
    /// </summary>
    public sealed class CityLife : MonoBehaviour
    {
        sealed class Walker
        {
            public Transform view;
            public Cell cell;
            public int dir, side;
            public float progress, pace;
        }
        sealed class Motion
        {
            public Transform view;
            public WorldView.EmitterKind kind;
            public Vector3 origin;
            public Quaternion turn;
            public float phase, size;
        }
        const int WalkersPerTown = 14, MaxPuffs = 90;
        internal static readonly Color[] Shirts = { new Color(.85f, .25f, .2f), new Color(.2f, .45f, .8f), new Color(.95f, .75f, .2f), new Color(.3f, .6f, .35f), new Color(.9f, .9f, .88f), new Color(.5f, .3f, .6f) };
        internal static readonly Color Skin = new Color(.93f, .76f, .6f), Trousers = new Color(.12f, .16f, .22f);
        static readonly Color Smoke = new Color(.76f, .78f, .8f);
        readonly HashSet<Cell> streets = new HashSet<Cell>();
        readonly List<Walker> walkers = new List<Walker>();
        readonly List<Motion> motions = new List<Motion>();
        readonly List<CampusLife> campuses = new List<CampusLife>();
        readonly List<ParkLife> parks = new List<ParkLife>();
        readonly List<ZooLife> zoos = new List<ZooLife>();
        readonly System.Random random = new System.Random(4217);
        WorldView world;
        float time;
        public int Walkers => walkers.Count;
        public int Motions => motions.Count;
        public int Campuses => campuses.Count;
        public int Parks => parks.Count;
        public ParkLife Park(int i) => parks[i];
        public int Zoos => zoos.Count;
        public ZooLife Zoo(int i) => zoos[i];
        /// <summary>Students on every university quad.</summary>
        public int Students
        {
            get
            {
                int count = 0;
                foreach (var campus in campuses)
                    count += campus.People;
                return count;
            }
        }

        /// <summary>Rebuilds walkers and animated effects after the towns were redrawn.</summary>
        public void Refresh(WorldView view, GameSession game, List<WorldView.Emitter> emitters)
        {
            world = view;
            foreach (Transform child in transform)
                Destroy(child.gameObject);
            walkers.Clear();
            motions.Clear();
            campuses.Clear();
            parks.Clear();
            zoos.Clear();
            streets.Clear();
            foreach (var city in game.World.cities)
                foreach (var road in city.roads)
                    if (game.Network.At(road.cell) == null)
                        streets.Add(road.cell);
            foreach (var city in game.World.cities)
            {
                int stride = Mathf.Max(2, city.roads.Count / WalkersPerTown), count = 0;
                for (int i = 1; i < city.roads.Count && count < WalkersPerTown; i += stride)
                {
                    var cell = city.roads[i].cell;
                    if (!streets.Contains(cell))
                        continue;
                    var walker = new Walker { cell = cell, dir = random.Next(4), side = random.Next(2) * 2 - 1, progress = (float)random.NextDouble(), pace = .35f + (float)random.NextDouble() * .2f };
                    walker.view = Person(Shirts[random.Next(Shirts.Length)]);
                    ChooseDirection(walker);
                    Pose(walker);
                    walkers.Add(walker);
                    count++;
                }
            }
            int puffs = 0;
            foreach (var e in emitters)
            {
                switch (e.kind)
                {
                    case WorldView.EmitterKind.Smoke:
                        for (int i = 0; i < 3 && puffs < MaxPuffs; i++, puffs++)
                            motions.Add(Make(e, world.Box("Smoke puff", e.at, Vector3.one * .06f, Smoke, transform).transform, i / 3f));
                        break;
                    case WorldView.EmitterKind.Flag:
                        {
                            var pivot = new GameObject("Flag").transform;
                            pivot.SetParent(transform, false);
                            pivot.localPosition = e.at;
                            pivot.localRotation = e.turn;
                            world.Box("Cloth", new Vector3(.07f * e.size, 0, 0), new Vector3(.14f * e.size, .09f * e.size, .01f), e.color, pivot);
                            motions.Add(Make(e, pivot, (float)random.NextDouble()));
                            break;
                        }
                    case WorldView.EmitterKind.Beacon:
                        motions.Add(Make(e, world.Box("Beacon", e.at, new Vector3(.06f, .04f, .06f), e.color, transform).transform, (float)random.NextDouble()));
                        break;
                    case WorldView.EmitterKind.Campus:
                        campuses.Add(new CampusLife(world, transform, e));
                        break;
                    case WorldView.EmitterKind.Park:
                        parks.Add(new ParkLife(world, transform, e));
                        break;
                    case WorldView.EmitterKind.Zoo:
                        zoos.Add(new ZooLife(world, transform, e));
                        break;
                    case WorldView.EmitterKind.Fountain:
                        motions.Add(Make(e, world.Box("Fountain jet", e.at, new Vector3(.05f, .2f, .05f) * e.size, e.color, transform).transform, (float)random.NextDouble()));
                        break;
                }
            }
        }
        static Motion Make(WorldView.Emitter e, Transform view, float phase) =>
            new Motion { view = view, kind = e.kind, origin = e.at, turn = e.turn, phase = phase, size = e.size };
        Transform Person(Color shirt) => Figure(world, transform, "Pedestrian", shirt, 1);
        /// <summary>A little person (legs, shirt, head) standing on its root's origin; <paramref name="scale"/> 1 is pavement size.</summary>
        internal static Transform Figure(WorldView world, Transform parent, string name, Color shirt, float scale)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            world.Box("Legs", new Vector3(0, .03f, 0) * scale, new Vector3(.035f, .06f, .025f) * scale, Trousers, root);
            world.Box("Body", new Vector3(0, .085f, 0) * scale, new Vector3(.05f, .06f, .03f) * scale, shirt, root);
            world.Box("Head", new Vector3(0, .13f, 0) * scale, new Vector3(.03f, .03f, .03f) * scale, Skin, root);
            return root;
        }
        /// <summary>Places a walker on the pavement beside its street cell, facing the way it walks.</summary>
        static void Pose(Walker walker)
        {
            var along = new Vector3(Directions.Dx[walker.dir], 0, Directions.Dz[walker.dir]);
            var across = new Vector3(along.z, 0, -along.x);
            walker.view.localPosition = new Vector3(walker.cell.x, .025f, walker.cell.z) + along * (walker.progress - .5f) + across * walker.side * .42f;
            walker.view.localRotation = Quaternion.LookRotation(along);
        }
        void ChooseDirection(Walker walker)
        {
            int chosen = -1, choices = 0;
            for (int d = 0; d < 4; d++)
                if (d != (walker.dir + 2) % 4 && streets.Contains(walker.cell.Move(d)) && random.Next(++choices) == 0)
                    chosen = d;
            walker.dir = chosen >= 0 ? chosen : (walker.dir + 2) % 4;
        }
        /// <summary>Shows life only when zoomed in; walkers and effects pause with the game.</summary>
        public void Animate(float dt, float speed, bool near)
        {
            if (gameObject.activeSelf != near)
                gameObject.SetActive(near);
            if (!near || speed <= 0)
                return;
            time += dt;
            foreach (var walker in walkers)
            {
                walker.progress += dt * walker.pace * Mathf.Min(speed, 2);
                while (walker.progress >= 1)
                {
                    walker.progress -= 1;
                    if (streets.Contains(walker.cell.Move(walker.dir)))
                        walker.cell = walker.cell.Move(walker.dir);
                    ChooseDirection(walker);
                }
                Pose(walker);
            }
            foreach (var campus in campuses)
                campus.Animate(dt * Mathf.Min(speed, 2), time);
            foreach (var park in parks)
                park.Animate(dt * Mathf.Min(speed, 2), time);
            foreach (var zoo in zoos)
                zoo.Animate(dt * Mathf.Min(speed, 2), time);
            foreach (var m in motions)
            {
                float t = time + m.phase * 3f;
                switch (m.kind)
                {
                    case WorldView.EmitterKind.Smoke:
                        {
                            // Puffs swell then shrink away as they drift up, so smoke thins out instead of piling up.
                            float life = Mathf.Repeat(time * .45f + m.phase, 1f);
                            m.view.localPosition = m.origin + new Vector3(life * .1f, life * .45f, life * .04f);
                            m.view.localScale = Vector3.one * (.02f + Mathf.Sin(life * Mathf.PI) * .07f);
                            break;
                        }
                    case WorldView.EmitterKind.Flag:
                        m.view.localRotation = m.turn * Quaternion.Euler(0, Mathf.Sin(t * 2.6f) * 25f, Mathf.Sin(t * 5.1f) * 4f);
                        break;
                    case WorldView.EmitterKind.Beacon:
                        m.view.localScale = Mathf.Repeat(t * 1.6f, 1f) < .5f ? new Vector3(.06f, .04f, .06f) : Vector3.zero;
                        break;
                    case WorldView.EmitterKind.Fountain:
                        {
                            float height = (.14f + .08f * Mathf.Sin(t * 4f)) * m.size;
                            m.view.localScale = new Vector3(.05f * m.size, height, .05f * m.size);
                            m.view.localPosition = m.origin + Vector3.up * height / 2;
                            break;
                        }
                }
            }
        }
    }
}
