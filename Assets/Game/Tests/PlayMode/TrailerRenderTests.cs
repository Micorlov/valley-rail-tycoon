using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ValleyRail.Core;
namespace ValleyRail.Tests
{
    // The cinematic trailer's footage (Tools/trailer/): the player's grown downtown (Tests/Fixtures/phone-downtown.json) filmed
    // by perspective cameras near the ground, at a steady 24 frames per simulated second. The test owns the simulation clock
    // (GameBootstrap is disabled), so every run replays the same motion and nothing is autosaved.
    //   SurveysTheWorld        Logs/trailer/survey.png (top-down map) + survey.json (towns, stations, bridges, train paths)
    //   RendersTheTrailerShots Logs/trailer/<shot>/0000.png ... for every shot in Tools/trailer/shots.json
    public class TrailerRenderTests
    {
        public const float Fps = 24;
        const string Fixture = "Tests/Fixtures/phone-downtown.json";
        const string Out = "Logs/trailer";
        static readonly FieldInfo TrainViews = typeof(WorldView).GetField("trainViews", BindingFlags.NonPublic | BindingFlags.Instance);
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        static GameBootstrap Boot()
        {
            var app = Object.FindAnyObjectByType<GameBootstrap>();
            return app ? app : new GameObject("Test Bootstrap").AddComponent<GameBootstrap>();
        }

        // Loads the fixture and hands the frame loop to the caller: GameBootstrap.Update would autosave and runs on real time.
        static IEnumerator LoadDowntown(GameBootstrap app)
        {
            yield return null;
            app.NewGame(true);
            app.SetSpeed(0);
            yield return null;
            var codec = new JsonSnapshotCodec();
            var envelope = codec.Decode<SaveEnvelope>(File.ReadAllText(Fixture));
            var state = codec.Decode<WorldState>(envelope.payload);
            SaveMigration.Upgrade(state, app.Game.Balance);
            state.speed = 1;
            app.BuildSession(state);
            for (int frame = 0; frame < 5; frame++)
                yield return null;
            app.enabled = false;
            app.Camera.enabled = false;
            foreach (var text in Object.FindObjectsByType<TMPro.TextMeshPro>(FindObjectsSortMode.None))
                text.GetComponent<Renderer>().enabled = false;
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                canvas.enabled = false;
        }

        // One rendered frame of simulated time: the ticks due at this speed, then every presenter's animation.
        sealed class Driver
        {
            readonly GameBootstrap app; readonly SimulationClock clock = new SimulationClock(); int cityRevision;
            public Driver(GameBootstrap app) { this.app = app; cityRevision = app.Game.World.cityRevision; }
            public void Advance(int speed)
            {
                app.Game.World.speed = speed; // WorldView.Animate scales every presenter by it
                clock.Advance(1 / Fps, speed, app.Game.Step);
                if (app.Game.World.cityRevision != cityRevision)
                {
                    cityRevision = app.Game.World.cityRevision;
                    app.World.Refresh();
                }
                app.World.Animate(1 / Fps, speed == 0);
            }
        }

        static Dictionary<int, Transform[]> Trains(GameBootstrap app) => (Dictionary<int, Transform[]>)TrainViews.GetValue(app.World);

        [UnityTest, Explicit("Writes the trailer survey to Logs/trailer; run it by name.")]
        public IEnumerator SurveysTheWorld()
        {
            var app = Boot();
            yield return LoadDowntown(app);
            var world = app.Game.World;
            Directory.CreateDirectory(Out);

            var json = new StringBuilder("{\n");
            json.Append("\"cities\":[\n").Append(string.Join(",\n", world.cities.Select(c =>
            {
                var tall = c.buildings.OrderByDescending(b => BuildingCatalog.Defaults[b.def].height).Take(5).Select(b => $"[{b.cell.x},{b.cell.z},{BuildingCatalog.Defaults[b.def].height}]");
                return $"{{\"name\":\"{c.name}\",\"center\":[{c.center.x},{c.center.z}],\"level\":\"{c.level}\",\"buildings\":{c.buildings.Count},\"tallest\":[{string.Join(",", tall)}]}}";
            }))).Append("\n],\n");
            json.Append("\"stations\":[\n").Append(string.Join(",\n", world.stations.Select(s =>
                $"{{\"id\":{s.id},\"name\":\"{s.name}\",\"cell\":[{s.cell.x},{s.cell.z}],\"axis\":{s.axis},\"length\":{s.length},\"platforms\":{s.platforms}}}"))).Append("\n],\n");
            json.Append("\"producers\":[\n").Append(string.Join(",\n", world.producers.Select(p =>
                $"{{\"id\":{p.id},\"name\":\"{p.name}\",\"kind\":\"{p.kind}\",\"cell\":[{p.cell.x},{p.cell.z}]}}"))).Append("\n],\n");
            json.Append("\"bridges\":[").Append(string.Join(",", world.tracks.Where(t => t.bridge != 0).Select(t => $"[{t.cell.x},{t.cell.z},{t.mask}]"))).Append("],\n");
            json.Append("\"tramLines\":").Append(world.tramLines.Count).Append(",\n");

            // Where every train's engine is, once per simulated second for 90 s: the render replays exactly this.
            var driver = new Driver(app);
            var paths = world.trains.ToDictionary(t => t.id, t => new List<string>());
            for (int frame = 0; frame <= 90 * Fps; frame++)
            {
                if (frame % (int)Fps == 0)
                    foreach (var pair in Trains(app))
                        if (paths.TryGetValue(pair.Key, out var path))
                        {
                            var engine = pair.Value[0];
                            path.Add(string.Format(Inv, "[{0:0.00},{1:0.00},{2:0}]", engine.position.x, engine.position.z, engine.eulerAngles.y));
                        }
                driver.Advance(1);
                if (frame % 240 == 0)
                    yield return null;
            }
            json.Append("\"trains\":[\n").Append(string.Join(",\n", world.trains.Select(t =>
                $"{{\"id\":{t.id},\"model\":{t.model},\"cargo\":\"{t.cargo}\",\"wagons\":{TrainCatalog.Wagons(t)},\"path\":[{string.Join(",", paths[t.id])}]}}"))).Append("\n]\n}\n");
            File.WriteAllText(Out + "/survey.json", json.ToString());

            // The whole map from straight above, 32 px per cell.
            var go = new GameObject("Survey camera");
            var camera = go.AddComponent<Camera>();
            camera.enabled = false;
            camera.orthographic = true;
            camera.orthographicSize = MapDefinition.Size / 2f;
            camera.nearClipPlane = 1;
            camera.farClipPlane = 400;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            go.transform.position = new Vector3(MapDefinition.Size / 2f - .5f, 200, MapDefinition.Size / 2f - .5f);
            go.transform.rotation = Quaternion.Euler(90, 0, 0);
            Save(camera, 4096, 4096, Out + "/survey.png");
            Object.Destroy(go);
            Assert.That(File.Exists(Out + "/survey.json"));
            LogAssert.NoUnexpectedReceived();
        }

        // One shot of Tools/trailer/shots.json. Eye and look move from their 0 to their 1 values with an ease in and out;
        // when they follow a target they are offsets from it (in its own frame when local), otherwise world points.
        [System.Serializable]
        public class Shot
        {
            public string name;
            public float start;              // simulated seconds after the fixture loads (at speed 1)
            public int frames = 48, speed = 1;
            public Vector3 eye0, eye1, look0, look1;
            public float fov0 = 40, fov1 = 40;
            public string follow = "";       // "train:<id>", "fire", "stage" or empty
            public bool eyeFollows, lookFollows, local;
            public string light = "day";     // day | dusk | sunset
            public float sunYaw = -35, sunPitch = 30, fogStart = 30, fogEnd = 150;
            public string stage = "";        // "fleet": trains abreast on their own track at stageAt, heading stageYaw
            public Vector3 stageAt; public float stageYaw, stageSpeed, stageGap = 1.4f; public int[] stageModels; public int stageWagons = 3;
            public string fire = "";         // town with a building set alight fireLead seconds before the first frame
            public float fireLead = 4;
        }
        [System.Serializable]
        public class ShotList { public string only = ""; public int every = 1; public float scale = 1; public Shot[] shots; }

        const int Width = 1920, Height = 804; // 2.39:1, letterboxed to 1920x1080 by Tools/trailer/assemble.py
        static readonly MethodInfo BuildTrainCars = typeof(WorldView).GetMethod("BuildTrainCars", BindingFlags.NonPublic | BindingFlags.Instance);

        [UnityTest, Explicit("Writes trailer frames to Logs/trailer; run it by name.")]
        public IEnumerator RendersTheTrailerShots()
        {
            var list = JsonUtility.FromJson<ShotList>(File.ReadAllText("Tools/trailer/shots.json"));
            var wanted = list.only.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
            var app = Boot();
            yield return LoadDowntown(app);
            int rendered = 0;
            foreach (var shot in list.shots.Where(s => wanted.Count == 0 || wanted.Contains(s.name)))
            {
                yield return Reload(app);
                var driver = new Driver(app);
                var fires = app.World.GetComponentInChildren<CityFires>();
                int lead = Mathf.RoundToInt((shot.start - (shot.fire.Length > 0 ? shot.fireLead : 0)) * Fps);
                for (int f = 0; f < Mathf.RoundToInt(shot.start * Fps); f++)
                {
                    if (f == Mathf.Max(0, lead) && shot.fire.Length > 0)
                        Assert.That(SetFire(app, fires, shot.fire), Is.True, shot.name + ": a fire starts in " + shot.fire);
                    driver.Advance(1);
                    if (f % 480 == 0)
                        yield return null;
                }
                if (shot.fire.Length > 0 && lead < 0)
                    Assert.That(SetFire(app, fires, shot.fire), Is.True);
                var stage = shot.stage == "fleet" ? Fleet(app, shot) : null;
                var camera = Cinematic(app, shot);
                var dir = Out + "/" + shot.name;
                Directory.CreateDirectory(dir);
                foreach (var old in Directory.GetFiles(dir, "*.jpg"))
                    File.Delete(old);
                Vector3 refPos = default; float refYaw = 0;
                for (int f = 0; f < shot.frames; f++)
                {
                    var target = Target(app, fires, stage, shot.follow);
                    if (target)
                    {
                        // Smooth the followed pose a little so curves and stops do not jerk the camera.
                        refPos = f == 0 ? target.position : Vector3.Lerp(refPos, target.position, .35f);
                        refYaw = f == 0 ? target.eulerAngles.y : Mathf.LerpAngle(refYaw, target.eulerAngles.y, .12f);
                    }
                    float u = shot.frames > 1 ? f / (shot.frames - 1f) : 0, e = u * u * (3 - 2 * u);
                    var basis = shot.local ? Quaternion.Euler(0, refYaw, 0) : Quaternion.identity;
                    Vector3 eye = Vector3.Lerp(shot.eye0, shot.eye1, e), look = Vector3.Lerp(shot.look0, shot.look1, e);
                    if (target && shot.eyeFollows)
                        eye = refPos + basis * eye;
                    if (target && shot.lookFollows)
                        look = refPos + basis * look;
                    camera.fieldOfView = Mathf.Lerp(shot.fov0, shot.fov1, e);
                    camera.transform.position = eye;
                    camera.transform.LookAt(look);
                    app.Camera.focus = new Vector3(look.x, 0, look.z);
                    if (f % Mathf.Max(1, list.every) == 0)
                    {
                        Save(camera, Mathf.RoundToInt(Width * list.scale), Mathf.RoundToInt(Height * list.scale), $"{dir}/{f:0000}.jpg");
                        rendered++;
                    }
                    if (stage)
                        foreach (Transform lane in stage)
                            lane.Find("Consist").localPosition += Vector3.forward * shot.stageSpeed / Fps;
                    driver.Advance(shot.speed);
                    if (f % 24 == 0)
                        yield return null;
                }
                if (stage)
                    Object.Destroy(stage.gameObject);
            }
            Assert.That(rendered, Is.GreaterThan(0), "shots.json selects at least one shot");
            LogAssert.NoUnexpectedReceived();
        }

        // Rebuilds the downtown from the fixture so every shot starts from the same moment.
        static IEnumerator Reload(GameBootstrap app)
        {
            var codec = new JsonSnapshotCodec();
            var state = codec.Decode<WorldState>(codec.Decode<SaveEnvelope>(File.ReadAllText(Fixture)).payload);
            SaveMigration.Upgrade(state, app.Game.Balance);
            state.speed = 1;
            app.BuildSession(state);
            yield return null;
            yield return null;
            app.Camera.enabled = false;
            foreach (var text in Object.FindObjectsByType<TMPro.TextMeshPro>(FindObjectsSortMode.None))
                text.GetComponent<Renderer>().enabled = false;
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                canvas.enabled = false;
        }

        static Transform Target(GameBootstrap app, CityFires fires, Transform stage, string follow)
        {
            if (follow.StartsWith("train:"))
                return Trains(app).TryGetValue(int.Parse(follow.Substring(6)), out var cars) ? cars[0] : null;
            if (follow == "fire")
                return fires && fires.Engine != null ? fires.Engine.Body : null;
            if (follow == "stage" && stage)
                return stage.GetChild(0).Find("Consist").GetChild(0);
            return null;
        }

        // The game's own camera, turned into a film camera: billboards and "zoomed in" town detail read it.
        static Camera Cinematic(GameBootstrap app, Shot shot)
        {
            var camera = app.Camera.view;
            camera.orthographic = false;
            camera.orthographicSize = 2; // CityView.ZoomedIn reads this even in perspective: keep people, yards and rooftops
            camera.nearClipPlane = .03f;
            camera.farClipPlane = 400;
            camera.enabled = false;
            app.Camera.zoom = 2;
            var sun = System.Array.Find(Object.FindObjectsByType<Light>(FindObjectsSortMode.None), l => l.type == LightType.Directional && l.name == "Sun");
            sun.transform.rotation = Quaternion.Euler(shot.sunPitch, shot.sunYaw, 0);
            (sun.color, sun.intensity, RenderSettings.ambientLight, RenderSettings.fogColor) = shot.light switch
            {
                "sunset" => (new Color(1, .74f, .5f), 1.35f, new Color(.42f, .36f, .52f), new Color(.86f, .6f, .46f)),
                "dusk" => (new Color(1, .82f, .62f), 1.2f, new Color(.5f, .46f, .52f), new Color(.8f, .68f, .56f)),
                _ => (new Color(1, .93f, .8f), 1.15f, new Color(.56f, .58f, .62f), new Color(.8f, .76f, .68f)),
            };
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = shot.fogStart;
            RenderSettings.fogEndDistance = shot.fogEnd;
            var sky = Shader.Find("Skybox/Procedural");
            if (sky)
            {
                var material = new Material(sky);
                material.SetFloat("_SunSize", .06f);
                material.SetFloat("_AtmosphereThickness", shot.light == "sunset" ? 1.9f : shot.light == "dusk" ? 1.35f : 1.05f);
                material.SetColor("_SkyTint", new Color(.62f, .6f, .56f));
                material.SetColor("_GroundColor", RenderSettings.fogColor);
                material.SetFloat("_Exposure", 1.25f);
                RenderSettings.skybox = material;
                RenderSettings.sun = sun;
                camera.clearFlags = CameraClearFlags.Skybox;
            }
            else
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = RenderSettings.fogColor;
            }
            return camera;
        }

        // Trains abreast, each on its own straight track, rolling along stageYaw at stageSpeed cells a second.
        static Transform Fleet(GameBootstrap app, Shot shot)
        {
            var root = new GameObject("Trailer fleet").transform;
            root.position = shot.stageAt;
            root.rotation = Quaternion.Euler(0, shot.stageYaw, 0);
            var models = shot.stageModels ?? new[] { 5 };
            for (int i = 0; i < models.Length; i++)
            {
                var lane = new GameObject("Lane " + i).transform;
                lane.SetParent(root, false);
                lane.localPosition = new Vector3((i - (models.Length - 1) / 2f) * shot.stageGap, 0, 0);
                for (int segment = -2; segment <= 2; segment++)
                {
                    var track = new GameObject("Track").transform;
                    track.SetParent(lane, false);
                    track.localPosition = new Vector3(0, 0, segment * 18);
                    AppIconRenderTests.LayTrack(app.World, track);
                }
                var consist = new GameObject("Consist").transform;
                consist.SetParent(lane, false);
                consist.localPosition = new Vector3(0, 0, -Mathf.Abs(i - (models.Length - 1) / 2f) * .7f); // a loose arrowhead
                var cargo = models[i] == 3 ? Cargo.Oil : models[i] == 0 ? Cargo.Coal : Cargo.Passengers;
                var cars = (Transform[])BuildTrainCars.Invoke(app.World, new object[] { new TrainState { model = models[i], cargo = cargo, wagons = shot.stageWagons }, consist, true });
                for (int c = 0; c < cars.Length; c++)
                    cars[c].localPosition = new Vector3(0, .14f, -c * .56f);
            }
            return root;
        }

        static bool SetFire(GameBootstrap app, CityFires fires, string town)
        {
            var city = app.Game.World.cities.First(c => c.name == town);
            foreach (var building in city.buildings.OrderBy(b => b.cell.Distance(city.center)))
                if (BuildingCatalog.Defaults[building.def].height < 200 && fires.Ignite(city, building))
                    return true;
            return false;
        }

        static void Save(Camera camera, int width, int height, string file)
        {
            var texture = new RenderTexture(width, height, 24) { antiAliasing = 4 };
            camera.targetTexture = texture;
            camera.Render();
            RenderTexture.active = texture;
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            RenderTexture.active = null;
            camera.targetTexture = null;
            File.WriteAllBytes(file, file.EndsWith(".jpg") ? image.EncodeToJPG(94) : image.EncodeToPNG());
            Object.Destroy(image);
            Object.Destroy(texture);
        }
    }
}
