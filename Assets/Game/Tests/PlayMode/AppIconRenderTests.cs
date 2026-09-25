using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ValleyRail.Core;
namespace ValleyRail.Tests
{
    // Renders the launcher icon's picture: the red steam engine on a track in front of Sunvale's skyline, from the player's
    // grown downtown (Tests/Fixtures/phone-downtown.json), in a low warm sunset light. Two passes from one camera, each
    // over black and over white so Tools/make_icon.py can matte them (the difference is what the sky shows through):
    //   Logs/icon-city-scene-*.png   the city, ground and track; the engine casts only its shadow
    //   Logs/icon-city-engine-*.png  the engine and its first wagon alone
    public class AppIconRenderTests
    {
        const int Size = 2048; // Tools/make_icon.py downsamples, which smooths the edges beyond the 4x MSAA URP allows
        const string Town = "Sunvale";
        const float ViewYaw = 180; // looking north at the towers
        const float EngineOutside = 6; // cells between the town's nearest building and the engine
        const float FieldOfView = 46; // wider than the icon, so make_icon.py can choose the square
        const int EngineLayer = 31;
        static readonly Vector3 EyeFromEngine = new Vector3(0, .5f, -2.4f), LookFromEngine = new Vector3(0, 1.9f, 6); // along the view
        static readonly MethodInfo BuildTrainCars = typeof(WorldView).GetMethod("BuildTrainCars", BindingFlags.NonPublic | BindingFlags.Instance);

        static GameBootstrap Boot()
        {
            var app = Object.FindAnyObjectByType<GameBootstrap>();
            return app ? app : new GameObject("Test Bootstrap").AddComponent<GameBootstrap>();
        }

        [UnityTest, Explicit("Writes icon renders to Logs; run it by name.")]
        public IEnumerator RendersTheCityAndEngineForTheAppIcon()
        {
            var app = Boot();
            yield return null;
            app.NewGame(true);
            app.SetSpeed(0);
            yield return null;
            var codec = new JsonSnapshotCodec();
            var envelope = codec.Decode<SaveEnvelope>(File.ReadAllText("Tests/Fixtures/phone-downtown.json"));
            var state = codec.Decode<WorldState>(envelope.payload);
            SaveMigration.Upgrade(state, app.Game.Balance);
            state.speed = 0;
            app.BuildSession(state);
            app.SetSpeed(0);
            for (int frame = 0; frame < 5; frame++)
                yield return null;
            // Station counters and the big town-name letters float in the air; a poster shows only the town.
            foreach (var text in Object.FindObjectsByType<TMPro.TextMeshPro>(FindObjectsSortMode.None))
                text.GetComponent<Renderer>().enabled = false;

            var city = app.Game.World.cities.First(c => c.name == Town);
            var towers = city.buildings.Where(b => BuildingCatalog.Defaults[b.def].height >= 250).ToList();
            Assert.That(towers.Count, Is.GreaterThan(10), Town + " has a skyline");
            var centre = new Vector3((float)towers.Average(b => b.cell.x), 0, (float)towers.Average(b => b.cell.z));
            app.Camera.focus = centre;
            app.Camera.zoom = 12;
            for (int frame = 0; frame < 5; frame++)
                yield return null;
            var view = Quaternion.Euler(0, ViewYaw, 0);
            var toward = view * Vector3.forward;
            float reach = city.buildings.Max(b => Vector3.Dot(centre - new Vector3(b.cell.x, 0, b.cell.z), toward));
            var engineAt = centre - toward * (reach + EngineOutside);

            // The engine faces back toward the camera, turned 28 degrees so its side shows.
            var root = new GameObject("Icon engine").transform;
            root.position = engineAt;
            root.rotation = Quaternion.LookRotation(Quaternion.Euler(0, 28, 0) * -toward);
            var track = new GameObject("Track").transform;
            track.SetParent(root, false);
            LayTrack(app.World, track);
            var cars = (Transform[])BuildTrainCars.Invoke(app.World, new object[] { new TrainState { model = 0, cargo = Cargo.Coal }, root, true });
            for (int i = 2; i < cars.Length; i++)
                Object.Destroy(cars[i].gameObject);
            for (int i = 0; i < 2; i++)
            {
                cars[i].localPosition = new Vector3(0, .14f, -i * .56f);
                cars[i].gameObject.layer = EngineLayer;
            }
            var engine = cars.Take(2).Select(c => c.GetComponent<Renderer>()).ToArray();
            var lights = Dramatic(ViewYaw);
            yield return null;

            Vector3 eye = engineAt + view * EyeFromEngine, at = engineAt + view * LookFromEngine;
            foreach (var r in engine)
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
            var black = Shoot(eye, at, ~(1 << EngineLayer), Color.black, "Logs/icon-city-scene-black.png");
            var white = Shoot(eye, at, ~(1 << EngineLayer), Color.white, "Logs/icon-city-scene-white.png");
            Assert.That(Coverage(black, white), Is.InRange(.4f, .9f), "ground and towers fill the lower frame, sky the rest");
            foreach (var r in engine)
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            black = Shoot(eye, at, 1 << EngineLayer, Color.black, "Logs/icon-city-engine-black.png");
            white = Shoot(eye, at, 1 << EngineLayer, Color.white, "Logs/icon-city-engine-white.png");
            Assert.That(Coverage(black, white), Is.InRange(.05f, .5f), "the engine stands out in front");

            lights.Restore();
            Object.Destroy(root.gameObject);
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        // Low warm key light from behind the camera and a pink rim from behind the subject, for a view looking along yaw.
        sealed class DramaticLights
        {
            public Light sun, rim; public Quaternion rotation; public Color color, ambient; public float intensity;
            public void Restore()
            {
                (sun.transform.rotation, sun.color, sun.intensity, RenderSettings.ambientLight) = (rotation, color, intensity, ambient);
                Object.Destroy(rim.gameObject);
            }
        }

        static DramaticLights Dramatic(float viewYaw)
        {
            var sun = System.Array.Find(Object.FindObjectsByType<Light>(FindObjectsSortMode.None), l => l.type == LightType.Directional && l.name == "Sun");
            var lights = new DramaticLights { sun = sun, rotation = sun.transform.rotation, color = sun.color, intensity = sun.intensity, ambient = RenderSettings.ambientLight };
            sun.transform.rotation = Quaternion.Euler(20, viewYaw - 4, 0);
            sun.color = new Color(1, .78f, .56f);
            sun.intensity = 1.35f;
            RenderSettings.ambientLight = new Color(.42f, .36f, .56f);
            lights.rim = new GameObject("Icon rim light").AddComponent<Light>();
            lights.rim.type = LightType.Directional;
            lights.rim.color = new Color(1, .45f, .7f);
            lights.rim.intensity = .9f;
            lights.rim.transform.rotation = Quaternion.Euler(17, viewYaw + 169, 0);
            return lights;
        }

        static void LayTrack(WorldView world, Transform parent)
        {
            const float start = -9, end = 9;
            world.Box("Ballast", new Vector3(0, .035f, 0), new Vector3(.82f, .07f, end - start), new Color(.48f, .46f, .39f), parent);
            for (float z = start + .1f; z < end; z += .2f)
                world.Box("Sleeper", new Vector3(0, .09f, z), new Vector3(.62f, .04f, .09f), new Color(.34f, .26f, .2f), parent);
            foreach (float side in new[] { -.2f, .2f })
                world.Box("Rail", new Vector3(side, .14f, 0), new Vector3(.04f, .05f, end - start), new Color(.64f, .66f, .61f), parent);
        }

        static Color32[] Shoot(Vector3 from, Vector3 at, int cullingMask, Color background, string file)
        {
            var go = new GameObject("Icon camera");
            var camera = go.AddComponent<Camera>();
            camera.enabled = false;
            camera.fieldOfView = FieldOfView;
            camera.nearClipPlane = .05f;
            camera.farClipPlane = 300;
            camera.cullingMask = cullingMask;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = background;
            go.transform.position = from;
            go.transform.LookAt(at);
            var texture = new RenderTexture(Size, Size, 24) { antiAliasing = 4 };
            camera.targetTexture = texture;
            camera.Render();
            RenderTexture.active = texture;
            var image = new Texture2D(Size, Size, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
            image.Apply();
            RenderTexture.active = null;
            Directory.CreateDirectory("Logs");
            File.WriteAllBytes(file, image.EncodeToPNG());
            var pixels = image.GetPixels32();
            camera.targetTexture = null;
            Object.Destroy(image);
            Object.Destroy(texture);
            Object.Destroy(go);
            return pixels;
        }

        // Share of pixels something covers: where the black and white renders agree, nothing lets the background through.
        static float Coverage(Color32[] black, Color32[] white)
        {
            int covered = 0;
            for (int i = 0; i < black.Length; i++)
                if (white[i].r - black[i].r < 128)
                    covered++;
            return covered / (float)black.Length;
        }
    }
}
