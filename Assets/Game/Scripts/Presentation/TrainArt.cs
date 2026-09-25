using System;
using System.Collections.Generic;
using UnityEngine;
using ValleyRail.Core;
namespace ValleyRail
{
    // Train cars: one combined mesh per car, shared by every train of the same model and cargo,
    // coloured through a palette texture so a car draws with at most four materials.
    public sealed partial class WorldView
    {
        const int PaletteSize = 64;
        // Smoothness and metallic per Finish: satin paint, worn metal, glossy glass, dull bulk loads.
        static readonly (float smoothness, float metallic)[] FinishLooks = { (.45f, 0), (.5f, .45f), (.88f, .1f), (.05f, 0) };
        Texture2D trainPalette;
        bool paletteDirty;
        Material[] trainFinishes;
        readonly Dictionary<int, Vector2> paletteTexels = new Dictionary<int, Vector2>();
        readonly Dictionary<string, (Mesh mesh, Material[] materials)> carArt = new Dictionary<string, (Mesh, Material[])>();
        // What each train's wagons currently show (cargo and whether it is loaded), so meshes swap only on a change.
        readonly Dictionary<int, int> trainDress = new Dictionary<int, int>();
        // Which end of each train its locomotive is at, so cars stay on the rails through reversals at stops.
        readonly TrainConsist consists = new TrainConsist();

        static int DressOf(TrainState train) => (int)train.cargo * 2 + (train.units > 0 ? 1 : 0);

        void CreateTrain(TrainState train)
        {
            var cars = BuildTrainCars(train, transform, train.units > 0);
            trainViews[train.id] = cars;
            trainDress[train.id] = DressOf(train);
            for (int i = 0; i < cars.Length; i++)
            {
                cars[i].position = CarPose(train, i, out var forward);
                if (forward.sqrMagnitude > .1f)
                    cars[i].rotation = Quaternion.LookRotation(forward);
            }
        }

        Transform[] BuildTrainCars(TrainState train, Transform parent, bool loaded)
        {
            int count = TrainCarCount(train);
            var cars = new Transform[count];
            for (int i = 0; i < count; i++)
            {
                var car = new GameObject(i == 0 ? "Locomotive" : "Wagon", typeof(MeshFilter), typeof(MeshRenderer)).transform;
                car.SetParent(parent, false);
                cars[i] = car;
                DressCar(car, train.model, train.cargo, i, count, loaded);
            }
            return cars;
        }

        void DressCar(Transform car, int model, Cargo cargo, int index, int count, bool loaded)
        {
            var art = CarArt(model, cargo, index, count, loaded);
            car.GetComponent<MeshFilter>().sharedMesh = art.mesh;
            car.GetComponent<MeshRenderer>().sharedMaterials = art.materials;
        }

        (Mesh mesh, Material[] materials) CarArt(int model, Cargo cargo, int index, int count, bool loaded)
        {
            bool lead = index == 0;
            bool tail = !lead && index == count - 1 && TrainCatalog.MultipleUnit(model);
            int variant = lead || tail ? 0 : (index * 3 + model) % WagonArt.Variants;
            bool showLoad = !lead && !tail && loaded && WagonArt.ShowsLoad(cargo, variant);
            string key = lead ? "lead " + model : tail ? "tail " + model : $"{model} {cargo} {variant} {(showLoad ? "loaded" : "empty")}";
            if (carArt.TryGetValue(key, out var art))
                return art;
            var builder = new TrainMeshBuilder(PaletteTexel);
            if (lead || tail)
                LocomotiveArt.Draw(builder, model, TrainLivery(model), tail);
            else
                WagonArt.Draw(builder, model, TrainLivery(model), cargo, variant, showLoad);
            var mesh = builder.Build("Train car " + key, tail, out var finishes);
            ownedMeshes.Add(mesh);
            if (paletteDirty)
            {
                trainPalette.Apply(false);
                paletteDirty = false;
            }
            var all = TrainFinishes();
            var materials = new Material[finishes.Length];
            for (int i = 0; i < finishes.Length; i++)
                materials[i] = all[(int)finishes[i]];
            art = (mesh, materials);
            carArt.Add(key, art);
            return art;
        }

        Vector2 PaletteTexel(Color color)
        {
            Color32 c = color;
            int key = c.r << 16 | c.g << 8 | c.b;
            if (paletteTexels.TryGetValue(key, out var uv))
                return uv;
            if (trainPalette == null)
                trainPalette = new Texture2D(PaletteSize, PaletteSize, TextureFormat.RGBA32, false)
                {
                    name = "Train palette",
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp
                };
            int index = paletteTexels.Count;
            if (index >= PaletteSize * PaletteSize)
                throw new InvalidOperationException("The train palette is full; reuse colours or raise PaletteSize.");
            int x = index % PaletteSize, y = index / PaletteSize;
            trainPalette.SetPixel(x, y, new Color32(c.r, c.g, c.b, 255));
            paletteDirty = true;
            uv = new Vector2((x + .5f) / PaletteSize, (y + .5f) / PaletteSize);
            paletteTexels.Add(key, uv);
            return uv;
        }

        Material[] TrainFinishes()
        {
            if (trainFinishes != null)
                return trainFinishes;
            var template = Resources.Load<Material>("WorldMaterial");
            trainFinishes = new Material[TrainMeshBuilder.FinishCount];
            for (int i = 0; i < trainFinishes.Length; i++)
            {
                var m = template ? new Material(template) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
                m.name = "Train " + (Finish)i;
                m.SetTexture("_BaseMap", trainPalette);
                m.mainTexture = trainPalette;
                m.SetColor("_BaseColor", Color.white);
                m.color = Color.white;
                m.SetFloat("_Smoothness", FinishLooks[i].smoothness);
                m.SetFloat("_Metallic", FinishLooks[i].metallic);
                m.enableInstancing = true;
                trainFinishes[i] = m;
            }
            return trainFinishes;
        }

        // Cars ride on two bogies, so on curves the body follows the chord between them and its ends overhang the rail.
        Vector3 CarPose(TrainState train, int car, out Vector3 forward) => consists.CarPose(game, train, car, TrainCarCount(train), out forward);

        /// <summary>Where a car of <paramref name="train"/> stands on the rails, from the simulation rather than the smoothed view.</summary>
        internal Vector3 CarPosition(TrainState train, int car) => CarPose(train, car, out _);

        void AnimateTrains(float dt, bool paused)
        {
            foreach (var t in game.World.trains)
            {
                if (!trainViews.TryGetValue(t.id, out var cars))
                    continue;
                if (cars.Length != TrainCarCount(t))
                {
                    // Wagons were added or removed at a station: rebuild the consist where it stands.
                    foreach (var car in cars)
                        Destroy(car.gameObject);
                    CreateTrain(t);
                    continue;
                }
                int dress = DressOf(t);
                if (!trainDress.TryGetValue(t.id, out int shown) || shown != dress)
                {
                    // Hoppers fill, container flats get their boxes and log cars their logs as the train loads.
                    trainDress[t.id] = dress;
                    for (int i = 1; i < cars.Length; i++)
                        DressCar(cars[i], t.model, t.cargo, i, cars.Length, t.units > 0);
                }
                for (int i = 0; i < cars.Length; i++)
                {
                    var p = CarPose(t, i, out var f);
                    cars[i].position = paused ? p : Vector3.Lerp(cars[i].position, p, 1 - Mathf.Exp(-dt * 30));
                    if (f.sqrMagnitude > .1f)
                        cars[i].rotation = Quaternion.LookRotation(f);
                }
            }
        }

        void DestroyTrainArt()
        {
            if (trainFinishes != null)
                foreach (var material in trainFinishes)
                    Destroy(material);
            if (trainPalette)
                Destroy(trainPalette);
        }
    }
}
