using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ValleyRail.Core;
namespace ValleyRail.Tests
{
    public class TrainArtTests
    {
        static readonly MethodInfo BuildTrainCars = typeof(WorldView).GetMethod("BuildTrainCars", BindingFlags.NonPublic | BindingFlags.Instance);

        static Transform[] Cars(WorldView world, int model, Cargo cargo, bool loaded, Transform parent) =>
            (Transform[])BuildTrainCars.Invoke(world, new object[] { new TrainState { model = model, cargo = cargo }, parent, loaded });

        static Mesh MeshOf(Transform car) => car.GetComponent<MeshFilter>().sharedMesh;

        static GameBootstrap Boot()
        {
            var app = Object.FindAnyObjectByType<GameBootstrap>();
            return app ? app : new GameObject("Test Bootstrap").AddComponent<GameBootstrap>();
        }

        [UnityTest]
        public IEnumerator EveryModelAndCargoBuildsCompactSharedCars()
        {
            var app = Boot();
            yield return null;
            app.NewGame(true);
            app.SetSpeed(0);
            yield return null;
            var root = new GameObject("Train art check").transform;
            for (int model = 0; model < TrainCatalog.Count; model++)
                for (var cargo = Cargo.Coal; cargo <= Cargo.Steel; cargo++)
                {
                    if (!TrainCatalog.SupportsCargo(model, cargo))
                        continue;
                    foreach (bool loaded in new[] { false, true })
                    {
                        var cars = Cars(app.World, model, cargo, loaded, root);
                        Assert.That(cars.Length, Is.EqualTo(WorldView.TrainCarCount(model)));
                        foreach (var car in cars)
                        {
                            string what = $"model {model} {cargo} {car.name}";
                            var mesh = MeshOf(car);
                            Assert.That(mesh.vertexCount, Is.GreaterThan(100), what);
                            Assert.That(car.GetComponent<MeshRenderer>().sharedMaterials.Length, Is.EqualTo(mesh.subMeshCount).And.InRange(1, 4), what);
                            var bounds = mesh.bounds;
                            Assert.That(bounds.max.z, Is.LessThan(.32f), what + " stays inside its .56 slot");
                            Assert.That(bounds.min.z, Is.GreaterThan(-.32f), what + " stays inside its .56 slot");
                            Assert.That(bounds.extents.x, Is.LessThan(.27f), what + " fits the track");
                            Assert.That(bounds.min.y, Is.GreaterThanOrEqualTo(-.01f), what + " rests on the rails");
                            Assert.That(bounds.max.y, Is.LessThan(.75f), what);
                        }
                    }
                }
            // Trains of one model and cargo share meshes, and bulk wagons show whether they carry a load.
            var first = Cars(app.World, 3, Cargo.Coal, true, root);
            var second = Cars(app.World, 3, Cargo.Coal, true, root);
            var empty = Cars(app.World, 3, Cargo.Coal, false, root);
            Assert.That(MeshOf(second[1]), Is.SameAs(MeshOf(first[1])));
            Assert.That(MeshOf(empty[1]), Is.Not.SameAs(MeshOf(first[1])));
            Assert.That(MeshOf(empty[1]).vertexCount, Is.LessThan(MeshOf(first[1]).vertexCount));
            Object.Destroy(root.gameObject);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator LoadingATrainFillsItsWagons()
        {
            var app = Boot();
            yield return null;
            app.NewGame(true);
            app.SetSpeed(0);
            yield return null;
            var train = app.Game.World.trains.Find(t => t.cargo == Cargo.Coal);
            train.units = 0;
            app.World.Refresh();
            app.World.Animate(.1f, true);
            var wagon = FindCars(app.World, train)[1];
            var emptyMesh = MeshOf(wagon);
            train.units = 10;
            app.World.Animate(.1f, true);
            Assert.That(MeshOf(wagon), Is.Not.SameAs(emptyMesh), "A loaded hopper shows its coal");
            train.units = 0;
            app.World.Animate(.1f, true);
            Assert.That(MeshOf(wagon), Is.SameAs(emptyMesh));
            LogAssert.NoUnexpectedReceived();
        }

        static Transform[] FindCars(WorldView world, TrainState train)
        {
            var views = (System.Collections.Generic.Dictionary<int, Transform[]>)typeof(WorldView)
                .GetField("trainViews", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(world);
            return views[train.id];
        }

        // Renders close-ups for review: every model in its usual service, every freight wagon, and the live map.
        [UnityTest, Explicit("Writes review renders to Logs; run it by name.")]
        public IEnumerator GalleryShowsEveryTrainUpClose()
        {
            var app = Boot();
            yield return null;
            app.NewGame(true);
            app.SetSpeed(0);
            yield return null;
            var root = new GameObject("Train gallery").transform;
            root.position = new Vector3(2000, 0, 2000);
            app.World.Box("Gallery ground", new Vector3(0, -.02f, -1.4f), new Vector3(14, .04f, 8), new Color(.39f, .55f, .28f), root);
            Cargo[] service = { Cargo.Coal, Cargo.Goods, Cargo.Passengers, Cargo.Wood, Cargo.Passengers, Cargo.Passengers };
            for (int model = 0; model < TrainCatalog.Count; model++)
                Lay(app.World, root, (model - 2.5f) * 1.15f, model, service[model], true);
            yield return null;
            Shoot(root.position + new Vector3(7.5f, 5.5f, 4.5f), root.position + new Vector3(0, .2f, -1.3f), "Logs/train-gallery.png", 30);
            Shoot(root.position + new Vector3(2.6f, 1.3f, 2.3f), root.position + new Vector3(0, .3f, 0), "Logs/train-gallery-fronts.png", 34);
            Object.Destroy(root.gameObject);
            yield return null;

            root = new GameObject("Freight gallery").transform;
            root.position = new Vector3(2000, 0, 2000);
            app.World.Box("Gallery ground", new Vector3(0, -.02f, -1.4f), new Vector3(14, .04f, 8), new Color(.39f, .55f, .28f), root);
            Cargo[] freight = { Cargo.Coal, Cargo.IronOre, Cargo.Goods, Cargo.Wood, Cargo.Oil, Cargo.Steel };
            for (int i = 0; i < freight.Length; i++)
                Lay(app.World, root, (i - 2.5f) * 1.15f, 3, freight[i], i % 2 == 0 || freight[i] != Cargo.Coal);
            yield return null;
            Shoot(root.position + new Vector3(7.5f, 5.5f, 3.5f), root.position + new Vector3(0, .2f, -1.8f), "Logs/train-freight.png", 30);
            Object.Destroy(root.gameObject);
            yield return null;

            var train = app.Game.World.trains[0];
            app.SetSpeed(1);
            for (int frame = 0; frame < 90; frame++)
                yield return null;
            app.SetSpeed(0);
            var cars = FindCars(app.World, train);
            app.Camera.focus = cars[0].position;
            app.Camera.zoom = 7;
            var camera = app.Camera.view;
            for (int frame = 0; frame < 10; frame++)
                yield return null;
            var texture = new RenderTexture(1600, 900, 24);
            camera.targetTexture = texture;
            camera.Render();
            Save(texture, "Logs/train-world.png");
            camera.targetTexture = null;
            Object.Destroy(texture);
            LogAssert.NoUnexpectedReceived();
        }

        static void Lay(WorldView world, Transform root, float x, int model, Cargo cargo, bool loaded)
        {
            var ballast = new Color(.48f, .46f, .39f);
            var rail = new Color(.64f, .66f, .61f);
            world.Box("Ballast", new Vector3(x, .035f, -1.4f), new Vector3(.82f, .07f, 4.2f), ballast, root);
            foreach (float side in new[] { -.2f, .2f })
                world.Box("Rail", new Vector3(x + side, .14f, -1.4f), new Vector3(.04f, .05f, 4.2f), rail, root);
            var cars = Cars(world, model, cargo, loaded, root);
            for (int i = 0; i < cars.Length; i++)
                cars[i].localPosition = new Vector3(x, .14f, -i * .56f);
        }

        static void Shoot(Vector3 from, Vector3 at, string file, float fieldOfView)
        {
            var go = new GameObject("Gallery camera");
            var camera = go.AddComponent<Camera>();
            camera.enabled = false;
            camera.fieldOfView = fieldOfView;
            camera.nearClipPlane = .05f;
            camera.farClipPlane = 100;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.62f, .74f, .8f);
            go.transform.position = from;
            go.transform.LookAt(at);
            var texture = new RenderTexture(2400, 1350, 24) { antiAliasing = 4 };
            camera.targetTexture = texture;
            camera.Render();
            Save(texture, file);
            camera.targetTexture = null;
            Object.Destroy(texture);
            Object.Destroy(go);
        }

        static void Save(RenderTexture texture, string file)
        {
            RenderTexture.active = texture;
            var image = new Texture2D(texture.width, texture.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
            image.Apply();
            RenderTexture.active = null;
            Directory.CreateDirectory("Logs");
            File.WriteAllBytes(file, image.EncodeToPNG());
            Object.Destroy(image);
        }
    }
}
