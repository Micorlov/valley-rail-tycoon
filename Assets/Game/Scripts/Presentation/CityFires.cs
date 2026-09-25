using System.Collections.Generic;
using UnityEngine;
using ValleyRail.Core;

namespace ValleyRail
{
    /// <summary>
    /// Every so often a home or business in the valley catches fire (<see cref="TownFires"/> picks one that a fire
    /// station can reach by road). The nearest station's engine races there with its siren on (<see cref="FireEngine"/>)
    /// and the crew puts the fire out (<see cref="FireScene"/>). Then the engine drives home. Toasts tell the player
    /// where it is burning and when the fire is out. Cosmetic like the traffic: it never changes the simulation, its
    /// saves or its random state, and it stands still while the game is paused.
    /// </summary>
    public sealed class CityFires : MonoBehaviour
    {
        public enum Stage { None, Alarm, Racing, Deploying, Fighting, MoppingUp, Packing, Returning }
        /// <summary>Game seconds from one fire's end to the next (one fire at a time); a freshly opened game waits about FirstFire for its first.</summary>
        public const float SoonestFire = 360, LatestFire = 720, FirstFire = 240;
        // Game seconds: the alarm before the engine rolls, the flames taking hold, the crew walking out (and back),
        // raising the ladder, climbing it, dousing a full blaze, spraying on once it is out and the steam clearing.
        const float AlarmTime = 2.5f, Kindle = 9, DeployTime = 2, RaiseTime = 3, ClimbTime = 1.5f, Douse = 14, MopUp = 3, SteamClears = 12;
        // Neighbours come out this many game seconds after it starts.
        const float Gather = 4;
        // A building at least this many cells high gets the ladder; the engine parks this far from the street's middle.
        const float LadderHeight = 1, Kerbside = .4f, StreetDeck = .026f;
        WorldView world;
        GameSession game;
        CityTraffic traffic;
        readonly System.Random random = new System.Random(2211);
        readonly Dictionary<(Color color, bool onTop), Material> glows = new Dictionary<(Color, bool), Material>();
        Mesh glowCube, tongue;
        float untilNext = FirstFire, burning, stageTime, heat, steam, deploy, raise, climb, spray, look;
        int heading;
        Vector3 park;
        bool tall;
        FireCall call;
        FireEngine engine;
        FireScene scene;
        public Stage Now { get; private set; }
        /// <summary>The fire burning now, or null.</summary>
        public FireCall Call => call;
        /// <summary>0 out, 1 ablaze.</summary>
        public float Heat => heat;
        /// <summary>The engine out on this fire, or null while it is still in its bay or back home.</summary>
        public FireEngine Engine => engine;
        public FireScene Scene => scene;
        /// <summary>Game seconds until the next fire breaks out, once this one is over.</summary>
        public float UntilNext => untilNext;

        public void Refresh(WorldView view, GameSession session, CityTraffic cars)
        {
            world = view;
            traffic = cars;
            if (game != session)
            {
                Stop();
                game = session;
                untilNext = FirstFire * (.5f + (float)random.NextDouble());
                return;
            }
            // A burning building that was bulldozed or rebuilt takes its fire with it.
            if (call != null && !Standing())
                Stop();
        }
        bool Standing()
        {
            var city = Town(call.cityId);
            if (city == null)
                return false;
            foreach (var b in city.buildings)
                if (b.cell.Equals(call.building.cell) && b.def == call.building.def)
                    return true;
            return false;
        }

        /// <summary>Plays the fire on by <paramref name="dt"/> real seconds at game <paramref name="speed"/> (0 while paused).</summary>
        public void Animate(float dt, float speed)
        {
            float step = dt * speed;
            if (game == null || step <= 0)
                return;
            if (call == null)
            {
                untilNext -= step;
                if (untilNext <= 0)
                {
                    untilNext = Mathf.Lerp(SoonestFire, LatestFire, (float)random.NextDouble());
                    Ignite();
                }
                return;
            }
            look += dt * Mathf.Min(speed, 2);
            Advance(step);
            if (call == null)
                return;
            var ladder = engine != null && tall && raise >= 1 ? new FireScene.Ladder { foot = engine.Foot, tip = engine.Tip, climb = climb } : (FireScene.Ladder?)null;
            scene.Animate(look, heat, steam, deploy, spray, burning > Gather && Now < Stage.Returning, ladder);
        }

        /// <summary>Sets a random home or business on fire now; false when no fire station can reach one or one is already burning.</summary>
        public bool Ignite()
        {
            if (game == null || call != null)
                return false;
            var lanes = RoadLanes.Build(game.World, game.Network);
            var next = TownFires.Plan(game.World, game.Network, lanes, random, world ? world.Standing : (System.Func<BuildingState, bool>)null);
            return next != null && Begin(next, lanes);
        }
        /// <summary>Sets <paramref name="building"/> on fire now; false when it cannot burn, no engine can reach it or a fire is already burning.</summary>
        public bool Ignite(CityState city, BuildingState building)
        {
            if (game == null || call != null)
                return false;
            var lanes = RoadLanes.Build(game.World, game.Network);
            var next = TownFires.Call(game.World, game.Network, lanes, city, building, world ? world.Standing : (System.Func<BuildingState, bool>)null);
            return next != null && Begin(next, lanes);
        }
        bool Begin(FireCall next, RoadLanes lanes)
        {
            call = next;
            Now = Stage.Alarm;
            burning = stageTime = heat = steam = deploy = raise = climb = spray = look = 0;
            heading = TownFires.ParkHeading(lanes, call);
            var def = BuildingCatalog.Get(call.building.def);
            var toStreet = Dir(call.facing);
            var street = new Vector3(call.street.x, Deck(call.street), call.street.z);
            park = street - toStreet * Kerbside;
            tall = def.height / 100f >= LadderHeight;
            var site = new FireScene.Site
            {
                at = new Vector3(call.building.cell.x, 0, call.building.cell.z), turn = Quaternion.LookRotation(toStreet),
                width = def.width / 100f, depth = def.depth / 100f, height = def.height / 100f,
                street = street, park = park, heading = Dir(heading), tall = tall,
            };
            scene = new FireScene(world, transform, site, GlowCube(), Tongue(), Glow, random);
            var town = Town(call.cityId);
            var from = Town(call.stationCityId);
            Say($"Fire in {town?.name}! Smoke is rising from the {def.name.ToLowerInvariant()}. " +
                (from == null || from == town ? "The fire brigade is on its way." : $"{from.name}'s fire engine is on its way."));
            return true;
        }
        void Advance(float step)
        {
            burning += step;
            stageTime += step;
            if (Now < Stage.Fighting)
                heat = Mathf.Min(1, heat + step / Kindle);
            switch (Now)
            {
                case Stage.Alarm:
                    if (stageTime >= AlarmTime)
                    {
                        engine = new FireEngine(world, transform, traffic ? traffic.Emergency : null, random.Next(2), Deck, Plaza);
                        engine.Out(call, park, heading);
                        engine.Siren(true);
                        Enter(Stage.Racing);
                    }
                    break;
                case Stage.Racing:
                    engine.Drive(step);
                    if (!engine.Moving)
                    {
                        engine.Siren(false);
                        if (tall)
                            engine.Aim(Upstairs());
                        Enter(Stage.Deploying);
                    }
                    break;
                case Stage.Deploying:
                    deploy = Mathf.Min(1, deploy + step / DeployTime);
                    if (tall)
                    {
                        engine.Raise(raise = Mathf.Min(1, raise + step / RaiseTime));
                        if (raise >= 1 && deploy >= 1)
                            climb = Mathf.Min(1, climb + step / ClimbTime);
                    }
                    if (deploy >= 1 && (!tall || climb >= 1))
                        Enter(Stage.Fighting);
                    break;
                case Stage.Fighting:
                    spray = Mathf.Min(1, spray + step);
                    steam = Mathf.Min(1, steam + step / 3);
                    heat = Mathf.Max(0, heat - step / Douse);
                    if (heat <= 0)
                    {
                        var town = Town(call.cityId);
                        Say($"Firefighters put out the fire in {town?.name}. Nobody was hurt.");
                        Enter(Stage.MoppingUp);
                    }
                    break;
                case Stage.MoppingUp:
                    if (stageTime >= MopUp)
                        Enter(Stage.Packing);
                    break;
                case Stage.Packing:
                    spray = Mathf.Max(0, spray - step);
                    steam = Mathf.Max(0, steam - step / SteamClears);
                    if (spray <= 0)
                        climb = Mathf.Max(0, climb - step / ClimbTime);
                    if (spray <= 0 && climb <= 0)
                    {
                        deploy = Mathf.Max(0, deploy - step / DeployTime);
                        if (tall)
                            engine.Raise(raise = Mathf.Max(0, raise - step / RaiseTime));
                    }
                    if (deploy <= 0 && raise <= 0)
                    {
                        engine.Home(call, TownFires.HomeRoute(RoadLanes.Build(game.World, game.Network), call, heading), park, heading);
                        Enter(Stage.Returning);
                    }
                    break;
                case Stage.Returning:
                    steam = Mathf.Max(0, steam - step / SteamClears);
                    if (engine != null)
                    {
                        engine.Drive(step);
                        if (!engine.Moving)
                        {
                            engine.Release();
                            engine = null;
                        }
                    }
                    if (engine == null && steam <= 0)
                        Stop();
                    break;
            }
        }
        void Enter(Stage stage)
        {
            Now = stage;
            stageTime = 0;
        }
        /// <summary>Where the ladder reaches: just off the front wall, near the eaves.</summary>
        Vector3 Upstairs()
        {
            var def = BuildingCatalog.Get(call.building.def);
            var toStreet = Dir(call.facing);
            return new Vector3(call.building.cell.x, def.height / 100f * .95f, call.building.cell.z) + toStreet * (def.depth / 200f + .2f);
        }
        /// <summary>Puts any fire out at once and clears it away: a new game, or the building is gone.</summary>
        public void Stop()
        {
            engine?.Release();
            scene?.Release();
            engine = null;
            scene = null;
            call = null;
            Now = Stage.None;
        }
        CityState Town(int id) => game?.World.cities.Find(c => c.id == id);
        float Deck(Cell c) => traffic ? traffic.Deck(c) : StreetDeck;
        bool Plaza(Cell c) => game.World.cities.Exists(town => town.center.Equals(c));
        static Vector3 Dir(int d) => new Vector3(Directions.Dx[d], 0, Directions.Dz[d]);
        static void Say(string text)
        {
            var app = FindAnyObjectByType<GameBootstrap>();
            if (app)
                app.Notice = text;
        }

        /// <summary>A unit cube with white vertex colours, so the unlit sprite shader draws it in its material's colour.</summary>
        Mesh GlowCube()
        {
            if (glowCube)
                return glowCube;
            var primitive = GameObject.CreatePrimitive(PrimitiveType.Cube);
            glowCube = Instantiate(primitive.GetComponent<MeshFilter>().sharedMesh);
            glowCube.name = "Fire glow cube";
            primitive.SetActive(false);
            Destroy(primitive);
            var white = new Color[glowCube.vertexCount];
            for (int i = 0; i < white.Length; i++)
                white[i] = Color.white;
            glowCube.colors = white;
            return glowCube;
        }
        /// <summary>
        /// A flame tongue: a point at the bottom, widest a third of the way up, tapering to a point at the top. Flat
        /// facets with white vertex colours, one unit tall.
        /// </summary>
        Mesh Tongue()
        {
            if (tongue)
                return tongue;
            var bottom = Vector3.zero;
            var top = Vector3.up;
            var ring = new[] { new Vector3(.5f, .32f, 0), new Vector3(0, .32f, .5f), new Vector3(-.5f, .32f, 0), new Vector3(0, .32f, -.5f) };
            var vertices = new List<Vector3>();
            for (int i = 0; i < 4; i++)
            {
                Vector3 a = ring[i], b = ring[(i + 1) % 4];
                vertices.AddRange(new[] { bottom, b, a, a, b, top });
            }
            var indices = new int[vertices.Count];
            var white = new Color[vertices.Count];
            for (int i = 0; i < indices.Length; i++)
            {
                indices[i] = i;
                white[i] = Color.white;
            }
            tongue = new Mesh { name = "Flame tongue" };
            tongue.SetVertices(vertices);
            tongue.SetTriangles(indices, 0);
            tongue.colors = white;
            tongue.RecalculateNormals();
            tongue.RecalculateBounds();
            return tongue;
        }
        /// <summary>
        /// An unlit material, so flames and water glow whatever the sun does; Sprites/Default is always in the build and
        /// draws both faces. One drawn <paramref name="onTop"/> of the others shows through them, like a flame's hot core.
        /// </summary>
        Material Glow(Color color, bool onTop)
        {
            if (glows.TryGetValue((color, onTop), out var material))
                return material;
            var shader = Shader.Find("Sprites/Default");
            material = shader ? new Material(shader) : new Material(world.Mat(color));
            material.name = "Fire glow";
            material.color = color;
            if (onTop)
                material.renderQueue = material.renderQueue + 1;
            glows.Add((color, onTop), material);
            return material;
        }
        void OnDestroy()
        {
            Stop();
            foreach (var material in glows.Values)
                if (material)
                    Destroy(material);
            glows.Clear();
            if (glowCube)
                Destroy(glowCube);
            if (tongue)
                Destroy(tongue);
        }
    }
}
