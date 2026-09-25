using System.Collections.Generic;
using UnityEngine;
using TMPro;
using ValleyRail.Core;
namespace ValleyRail
{
    public sealed partial class WorldView : MonoBehaviour
    {
        public static readonly Color Navy = new Color(.055f, .12f, .16f), Cream = new Color(.94f, .9f, .77f), Green = new Color(.28f, .62f, .37f), Gold = new Color(.95f, .65f, .24f);
        readonly Dictionary<Color, Material> materials = new Dictionary<Color, Material>();
        readonly Dictionary<int, GameObject> trees = new Dictionary<int, GameObject>();
        readonly Dictionary<int, Transform[]> trainViews = new Dictionary<int, Transform[]>();
        readonly Dictionary<int, TextMeshPro> stationLabels = new Dictionary<int, TextMeshPro>();
        // Map labels face the camera; this follows its current direction (45° is the starting view).
        float labelYaw = 45;
        readonly Dictionary<int, RenderTexture> trainPictures = new Dictionary<int, RenderTexture>();
        readonly HashSet<Mesh> ownedMeshes = new HashSet<Mesh>();
        readonly List<(int trackId, int entry, Renderer red, Renderer green)> crossingLights = new List<(int, int, Renderer, Renderer)>();
        Transform signalRoot;
        CityTraffic traffic;
        public CityTraffic Traffic => traffic;
        StationCrowds crowds;
        StationBoarding boarding;
        LevelCrossings levelCrossings;
        GameSession game; Transform rails, stationRoot, previewRoot, grid, cityRoot; int revision = -1, cityRevision = -1, treeRevision = -1, treeCityRevision = -1, treesFelled = -1; Mesh cone, cube, previewMesh;
        // While a root is being (re)built its boxes and cones are collected here and combined into one static mesh per material.
        Transform batchTarget; Dictionary<Material, List<CombineInstance>> batch;
        // Town details (yards, rooftops, street furniture) batch into their own meshes, shown only when zoomed in.
        Transform cityDetailRoot; Dictionary<Material, List<CombineInstance>> detailBatch;
        static readonly Color[] Walls = { Cream, new Color(.81f, .68f, .47f), new Color(.78f, .78f, .74f), new Color(.62f, .38f, .32f), new Color(.9f, .78f, .55f), new Color(.52f, .66f, .74f) };
        readonly List<Vector3> previewVertices = new List<Vector3>(64000);
        readonly List<int> previewIndices = new List<int>(96000);
        public Material Mat(Color color)
        {
            if (!materials.TryGetValue(color, out var m))
            {
                var template = Resources.Load<Material>("WorldMaterial");
                m = template ? new Material(template) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
                m.SetColor("_BaseColor", color);
                m.color = color;
                m.enableInstancing = true;
                m.SetFloat("_Smoothness", .12f);
                materials.Add(color, m);
            }
            return m;
        }
        public void Initialize(GameSession g)
        {
            game = g;
            cone = Cone();
            prism = Prism();
            pyramid = Pyramid();
            var primitive = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube = primitive.GetComponent<MeshFilter>().sharedMesh;
            primitive.SetActive(false);
            Destroy(primitive);
            BuildLand();
            BuildGrid();
            CreateIndustryMotion();
            BuildScenery();
            BuildSkiResorts();
            rails = Root("Railway");
            signalRoot = Root("Crossing signals");
            stationRoot = Root("Stations");
            cityRoot = Root("Cities");
            cityDetailRoot = Root("City details");
            constructionRoot = Root("Construction sites");
            traffic = Root("City traffic").gameObject.AddComponent<CityTraffic>();
            levelCrossings = Root("Level crossings").gameObject.AddComponent<LevelCrossings>();
            traffic.barriers = levelCrossings;
            life = Root("City life").gameObject.AddComponent<CityLife>();
            crowds = Root("Station crowds").gameObject.AddComponent<StationCrowds>();
            boarding = Root("Station boarding").gameObject.AddComponent<StationBoarding>();
            boarding.Initialize(this, crowds);
            matches = Root("Stadium matches").gameObject.AddComponent<StadiumMatch>();
            cinemas = Root("Drive-in cinemas").gameObject.AddComponent<DriveInCinema>();
            pools = Root("Swimming pools").gameObject.AddComponent<PoolLife>();
            beach = Root("Beach life").gameObject.AddComponent<BeachLife>();
            skiLife = Root("Ski life").gameObject.AddComponent<SkiLife>();
            roadside = Root("Roadside life").gameObject.AddComponent<RoadsideLife>();
            fires = Root("Town fires").gameObject.AddComponent<CityFires>();
            campLife = Root("Camp life").gameObject.AddComponent<CampLife>();
            previewRoot = Root("Construction preview");
            previewMesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            previewMesh.MarkDynamic();
            ownedMeshes.Add(previewMesh);
            previewRoot.gameObject.AddComponent<MeshFilter>().sharedMesh = previewMesh;
            previewRoot.gameObject.AddComponent<MeshRenderer>();
            Refresh();
        }
        Transform Root(string name)
        {
            var root = new GameObject(name).transform;
            root.SetParent(transform, false);
            return root;
        }
        public GameObject Box(string name, Vector3 pos, Vector3 scale, Color color, Transform parent = null, Quaternion? rotation = null)
        {
            var target = BatchFor(parent);
            if (target != null)
            {
                Batch(target, cube, pos, scale, color, rotation);
                return null;
            }
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent ? parent : transform, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.transform.localRotation = rotation ?? Quaternion.identity;
            go.GetComponent<Renderer>().sharedMaterial = Mat(color);
            Destroy(go.GetComponent<Collider>());
            return go;
        }
        Dictionary<Material, List<CombineInstance>> BatchFor(Transform parent) =>
            parent == null ? null : parent == batchTarget ? batch : parent == cityDetailRoot ? detailBatch : null;
        static void Batch(Dictionary<Material, List<CombineInstance>> target, Mesh mesh, Vector3 pos, Vector3 scale, Material material, Quaternion? rotation)
        {
            if (!target.TryGetValue(material, out var list))
            {
                list = new List<CombineInstance>();
                target[material] = list;
            }
            list.Add(new CombineInstance { mesh = mesh, transform = Matrix4x4.TRS(pos, rotation ?? Quaternion.identity, scale) });
        }
        void Batch(Dictionary<Material, List<CombineInstance>> target, Mesh mesh, Vector3 pos, Vector3 scale, Color color, Quaternion? rotation) =>
            Batch(target, mesh, pos, scale, Mat(color), rotation);
        GameObject Shape(string name, Vector3 pos, Vector3 scale, Color color, Transform parent, Quaternion? rotation = null) =>
            Shape(name, cone, pos, scale, color, parent, rotation);
        GameObject Shape(string name, Mesh mesh, Vector3 pos, Vector3 scale, Color color, Transform parent, Quaternion? rotation = null)
        {
            var target = BatchFor(parent);
            if (target != null)
            {
                Batch(target, mesh, pos, scale, color, rotation);
                return null;
            }
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.transform.localRotation = rotation ?? Quaternion.identity;
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = Mat(color);
            return go;
        }
        static readonly Color[] TerrainColors = {
            new Color(.39f, .55f, .28f), new Color(.27f, .43f, .25f),
            new Color(.73f, .62f, .39f), new Color(.66f, .72f, .65f),
            new Color(.47f, .49f, .48f), new Color(.87f, .91f, .9f),
            new Color(.74f, .71f, .51f), new Color(.19f, .48f, .61f),
            new Color(.93f, .85f, .63f) // beach sand (see Coast)
        };
        void BuildLand()
        {
            var ground = Root("Ground");
            // Split by terrain and shade; at most 32 draw calls for all 16,384 tiles.
            for (int surface = 0; surface < TerrainColors.Length; surface++)
                for (int band = 0; band < 4; band++)
                {
                    var vertices = new List<Vector3>();
                    var triangles = new List<int>();
                    for (int z = 0; z < MapDefinition.Size; z++)
                        for (int x = 0; x < MapDefinition.Size; x++)
                        {
                            var c = new Cell(x, z);
                            if (((x * 17 + z * 31) % 4) != band || MapDefinition.Surface(c) != surface) continue;
                            int n = vertices.Count;
                            float y0 = surface == 7 ? -.04f : MapDefinition.Height(x - .5f, z - .5f);
                            float y1 = surface == 7 ? -.04f : MapDefinition.Height(x - .5f, z + .5f);
                            float y2 = surface == 7 ? -.04f : MapDefinition.Height(x + .5f, z + .5f);
                            float y3 = surface == 7 ? -.04f : MapDefinition.Height(x + .5f, z - .5f);
                            vertices.Add(new Vector3(x - .5f, y0, z - .5f));
                            vertices.Add(new Vector3(x - .5f, y1, z + .5f));
                            vertices.Add(new Vector3(x + .5f, y2, z + .5f));
                            vertices.Add(new Vector3(x + .5f, y3, z - .5f));
                            triangles.AddRange(new[] { n, n + 1, n + 2, n, n + 2, n + 3 });
                        }
                    if (vertices.Count == 0) continue;
                    var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                    ownedMeshes.Add(mesh);
                    mesh.SetVertices(vertices);
                    mesh.SetTriangles(triangles, 0);
                    mesh.RecalculateNormals();
                    var go = new GameObject("Terrain " + surface + " shade " + band, typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
                    go.transform.SetParent(ground, false);
                    go.GetComponent<MeshFilter>().sharedMesh = mesh;
                    go.GetComponent<MeshCollider>().sharedMesh = mesh;
                    var color = TerrainColors[surface] * (1f + band * .025f);
                    color.a = 1;
                    go.GetComponent<Renderer>().sharedMaterial = Mat(color);
                }
            foreach (int z in MapDefinition.BridgeRows)
            {
                Box("Bridge blueprint", new Vector3(31, .01f, z), new Vector3(3, .04f, .75f), new Color(.32f, .62f, .62f), ground);
                siteLabels[z] = Label("BRIDGE SITE", new Vector3(31, .3f, z + 1.2f), 1.8f, ground).gameObject;
                bridgeSitesRevision = -1;
            }
            Label("NORTHWOOD LAKES", new Vector3(47, .4f, 69), 5, ground, .6f);
            Label("SUNVALE PLAINS", new Vector3(94, .4f, 40), 5, ground, .6f);
            Label("GRANITE RIDGE", new Vector3(80, 10.5f, 82), 5, ground, .6f);
            Label("FROSTPEAKS", new Vector3(56, 12.5f, 112), 5, ground, .6f);
            BuildSea(ground);
        }
        void BuildGrid()
        {
            grid = Root("Construction grid");
            var vertices = new List<Vector3>();
            var indices = new List<int>();
            // Short segments follow hills instead of disappearing under them.
            for (int z = 0; z <= MapDefinition.Size; z++)
                for (int x = 0; x <= MapDefinition.Size; x++)
                {
                    float px = x - .5f, pz = z - .5f;
                    if (x < MapDefinition.Size)
                    {
                        vertices.Add(new Vector3(px, MapDefinition.Height(px, pz) + .022f, pz));
                        vertices.Add(new Vector3(px + 1, MapDefinition.Height(px + 1, pz) + .022f, pz));
                    }
                    if (z < MapDefinition.Size)
                    {
                        vertices.Add(new Vector3(px, MapDefinition.Height(px, pz) + .022f, pz));
                        vertices.Add(new Vector3(px, MapDefinition.Height(px, pz + 1) + .022f, pz + 1));
                    }
                }
            for (int i = 0; i < vertices.Count; i++) indices.Add(i);
            var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            ownedMeshes.Add(mesh);
            mesh.SetVertices(vertices);
            mesh.SetIndices(indices, MeshTopology.Lines, 0);
            grid.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            grid.gameObject.AddComponent<MeshRenderer>().sharedMaterial = Mat(new Color(.58f, .7f, .41f));
            grid.gameObject.SetActive(false);
        }
        public void SetBuildGrid(bool enabled) => grid.gameObject.SetActive(enabled);
        /// <summary>Shows exactly the pines Core says are standing, whenever track, towns or the felled list changed. Trees a town sign hides stay hidden.</summary>
        void SyncTrees()
        {
            var w = game.World;
            if (treeRevision == w.revision && treeCityRevision == w.cityRevision && treesFelled == w.felledTrees.Count)
                return;
            treeRevision = w.revision;
            treeCityRevision = w.cityRevision;
            treesFelled = w.felledTrees.Count;
            var spots = game.Scenery.Trees;
            for (int i = 0; i < spots.Count; i++)
            {
                int key = spots[i].cell.Key;
                if (trees.TryGetValue(key, out var tree))
                    tree.SetActive(game.Scenery.Standing(i) && !signClearedTrees.Contains(key));
            }
        }
        void BuildScenery()
        {
            var scenery = Root("Scenery");
            // Core owns where pines grow and which are gone (Scenery); SyncTrees shows only the standing ones.
            foreach (var spot in game.Scenery.Trees)
            {
                var root = new GameObject("Pine").transform;
                root.SetParent(scenery);
                root.localPosition = new Vector3(spot.cell.x, 0, spot.cell.z);
                Box("Trunk", new Vector3(0, TreeSpot.Crown, 0), new Vector3(.13f, .7f, .13f), new Color(.31f, .23f, .14f), root);
                Shape("Foliage", new Vector3(0, TreeSpot.Crown, 0), new Vector3(TreeSpot.Radius, spot.height, TreeSpot.Radius), new Color(.12f, .29f + spot.shade * .04f, .18f), root);
                trees[spot.cell.Key] = root.gameObject;
            }
            foreach (var p in game.World.producers)
            {
                // A ski resort is drawn with its gondola and pistes (SkiArt).
                if (p.kind == ProducerKind.SkiResort)
                    continue;
                var root = new GameObject(p.name).transform;
                root.SetParent(scenery);
                root.localPosition = new Vector3(p.cell.x, 0, p.cell.z);
                // Town buildings come from the city mesh (see DrawCity); only industries are drawn here.
                if (p.kind == ProducerKind.Plant)
                    DrawPowerStation(root);
                else if (p.kind >= ProducerKind.Forest)
                    DrawIndustry(root, p.kind);
                else if (p.kind != ProducerKind.Town)
                {
                    Color color = p.kind == ProducerKind.Mine ? new Color(.38f, .39f, .35f) : new Color(.63f, .53f, .39f);
                    Box("Main hall", new Vector3(0, .58f, 0), new Vector3(2, 1.15f, 1.5f), color, root);
                    Box("Roof", new Vector3(0, 1.2f, 0), new Vector3(2.15f, .18f, 1.65f), Navy, root);
                    for (int i = 0; i < 3; i++)
                        Box("Window", new Vector3(-.65f + i * .65f, .7f, -.76f), new Vector3(.36f, .4f, .025f), Gold, root);
                    Box("Chimney", new Vector3(.7f, 1.4f, .4f), new Vector3(.28f, 2.8f, .28f), new Color(.4f, .35f, .3f), root);
                    industry.AddStack(root, new Vector3(.7f, 2.8f, .4f), p.kind == ProducerKind.Mine ? IndustryMotion.Plume.Soot : IndustryMotion.Plume.Smoke);
                    if (p.kind == ProducerKind.Mine)
                        for (int i = 0; i < 3; i++)
                            Shape("Coal pile", new Vector3(-.7f + i * .65f, 0, -1.1f), new Vector3(.55f, .5f, .5f), Navy, root);
                }
                // No floating captions: towns are named by a big sign at their entrance (TownSigns), and a tap on an
                // industry opens a window with what it takes in and ships out (GameplayPresenter.IndustryText).
            }
            for (int i = 0; i < 8; i++)
                Shape("Hills", new Vector3(1 + i % 3 * 1.6f, 0, 53 + i * 1.1f), new Vector3(3, 2 + i % 3, 3), new Color(.48f, .51f, .4f), scenery);
        }
        void DrawIndustry(Transform root, ProducerKind kind)
        {
            var steel = new Color(.34f, .46f, .51f);
            var timber = new Color(.55f, .32f, .15f);
            var rust = new Color(.69f, .32f, .2f);
            Box("Industry yard", new Vector3(0, .04f, 0), new Vector3(2.9f, .08f, 2.9f), new Color(.52f, .53f, .43f), root);
            if (kind == ProducerKind.Forest)
            {
                for (int i = 0; i < 5; i++)
                {
                    float x = -.9f + i % 3 * .85f, z = -.65f + i / 3 * 1.25f;
                    Box("Timber trunk", new Vector3(x, .5f, z), new Vector3(.14f, 1f, .14f), timber, root);
                    Shape("Managed forest", new Vector3(x, .55f, z), new Vector3(.8f, 1.5f + i % 2 * .4f, .8f), new Color(.16f, .39f, .22f), root);
                }
            }
            else if (kind == ProducerKind.OilWells)
                DrawOilWells(root);
            else if (kind == ProducerKind.IronMine)
            {
                Box("Ore shed", new Vector3(-.45f, .5f, .5f), new Vector3(1.5f, 1f, 1.1f), rust, root);
                for (int i = 0; i < 3; i++)
                    Shape("Iron ore pile", new Vector3(-.8f + i * .8f, .05f, -.85f), new Vector3(.7f, .6f, .65f), rust, root);
                for (int i = 0; i < 2; i++)
                    Box("Headframe", new Vector3(.5f + i * .6f, 1f, .5f), new Vector3(.12f, 2f, .15f), Navy, root);
                DrawHoist(root);
            }
            else
            {
                Color color = kind == ProducerKind.Sawmill ? timber : kind == ProducerKind.SteelMill ? rust : steel;
                Box("Processing hall", new Vector3(-.4f, .55f, -.3f), new Vector3(1.65f, 1.1f, 1.9f), color, root);
                Box("Factory roof", new Vector3(-.4f, 1.16f, -.3f), new Vector3(1.8f, .16f, 2.05f), Navy, root);
                for (int i = 0; i < 3; i++)
                    Box("Hall window", new Vector3(-.95f + i * .55f, .65f, -1.26f), new Vector3(.3f, .35f, .03f), Gold, root);
                if (kind == ProducerKind.Sawmill)
                {
                    for (int i = 0; i < 4; i++)
                        Box("Stacked lumber", new Vector3(.95f, .16f + i * .18f, 0), new Vector3(.55f, .13f, 2.25f), new Color(.8f, .61f, .35f), root);
                    Box("Saw vent", new Vector3(-.4f, 1.48f, .3f), new Vector3(.35f, .55f, .4f), steel, root);
                    DrawLogDeck(root);
                }
                else
                {
                    for (int i = 0; i < 2; i++)
                    {
                        float z = -.65f + i * 1.5f;
                        Box(kind == ProducerKind.Refinery ? "Refining column" : "Blast furnace", new Vector3(.9f, 1f, z), new Vector3(.65f, 2f + i * .4f, .65f), steel, root);
                        Box("Column band", new Vector3(.9f, 1.7f, z), new Vector3(.7f, .17f, .7f), kind == ProducerKind.Refinery ? Cream : Gold, root);
                        Box("Feed pipe", new Vector3(.35f, .35f, z), new Vector3(1.2f, .15f, .15f), Gold, root);
                    }
                    if (kind == ProducerKind.Refinery)
                        DrawFlare(root);
                    else
                        DrawFurnaceMouths(root);
                }
            }
        }
        void DrawPowerStation(Transform root)
        {
            var concrete = new Color(.64f, .69f, .7f);
            var steel = new Color(.28f, .4f, .46f);
            Box("Plant foundation", new Vector3(0, .06f, 0), new Vector3(2.9f, .12f, 2.9f), concrete, root);
            Box("Turbine hall", new Vector3(-.35f, .55f, -.45f), new Vector3(1.9f, 1f, 1.25f), steel, root);
            Box("Turbine roof", new Vector3(-.35f, 1.1f, -.45f), new Vector3(2f, .14f, 1.35f), Navy, root);
            for (int i = 0; i < 4; i++)
                Box("Lit turbine window", new Vector3(-1.05f + i * .45f, .64f, -1.09f), new Vector3(.26f, .34f, .03f), Gold, root);
            for (int i = 0; i < 2; i++)
            {
                float x = -.65f + i * 1.2f;
                Box("Boiler house", new Vector3(x, .6f, .75f), new Vector3(.85f, 1.15f, .85f), concrete, root);
                Box("Power stack", new Vector3(x, 1.5f, .75f), new Vector3(.3f, 2.8f, .3f), steel, root);
                Box("Stack band", new Vector3(x, 2.55f, .75f), new Vector3(.32f, .3f, .32f), Cream, root);
                Box("Stack cap", new Vector3(x, 2.91f, .75f), new Vector3(.36f, .1f, .36f), Navy, root);
                industry.AddStack(root, new Vector3(x, 2.96f, .75f), IndustryMotion.Plume.Smoke, 1.3f);
            }
            Box("Transformer", new Vector3(1.02f, .35f, -.5f), new Vector3(.45f, .6f, .85f), Gold, root);
            for (int i = 0; i < 3; i++)
                Box("Transformer insulator", new Vector3(1.02f, .78f, -.8f + i * .3f), new Vector3(.12f, .25f, .12f), Navy, root);
        }
        /// <summary>Turns every map label to face a camera looking along <paramref name="yaw"/>; new labels follow it too.</summary>
        public void FaceCamera(float yaw)
        {
            if (Mathf.Approximately(yaw, labelYaw))
                return;
            labelYaw = yaw;
            var facing = LabelRotation;
            foreach (var label in GetComponentsInChildren<TextMeshPro>(true))
                if ((!signRoot || !label.transform.IsChildOf(signRoot)) && (!stationRoot || !label.transform.IsChildOf(stationRoot)))
                    label.transform.rotation = facing;
            // Station queue signs stand upright on their roofs instead, square to the camera.
            OrientStationLabels();
            // Town signs stand upright rather than tilting with the view, and move once a turn settles past halfway.
            if (game != null)
                UpdateTownSigns();
            TurnTownSigns();
        }
        Quaternion LabelRotation => Quaternion.Euler(35, labelYaw, 0);
        public TextMeshPro Label(string text, Vector3 position, float width, Transform parent, float size = .7f)
        {
            var go = new GameObject(text, typeof(TextMeshPro));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.rotation = LabelRotation;
            var label = go.GetComponent<TextMeshPro>();
            label.text = text;
            label.fontSize = size * 10;
            label.color = Cream;
            label.alignment = TextAlignmentOptions.Center;
            label.rectTransform.sizeDelta = new Vector2(width * 2, .8f);
            label.enableWordWrapping = false;
            return label;
        }
        void Clear(Transform root)
        {
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                var go = root.GetChild(i).gameObject;
                var filter = go.GetComponent<MeshFilter>();
                if (filter && ownedMeshes.Remove(filter.sharedMesh))
                    Destroy(filter.sharedMesh);
                go.SetActive(false);
                Destroy(go);
            }
        }
        public void Refresh()
        {
            if (revision != game.World.revision)
            {
                revision = game.World.revision;
                Clear(rails);
                Clear(signalRoot);
                crossingLights.Clear();
                Clear(stationRoot);
                stationLabels.Clear();
                batchTarget = rails;
                batch = new Dictionary<Material, List<CombineInstance>>();
                foreach (var t in game.World.tracks)
                {
                    if (trees.TryGetValue(t.cell.Key, out var tree))
                        tree.SetActive(false);
                    DrawTrack(t, rails, null);
                    if (t.mask == 15) DrawCrossingSignals(t);
                }
                foreach (var s in game.World.stations)
                {
                    DrawStation(s);
                    // A station being rebuilt gets its roof sign back when the finished station is drawn.
                    if (!upgradingStations.Contains(s.id))
                        stationLabels[s.id] = RoofLabel(s);
                }
                crowds.Rebuild(this, game);
                Combine(rails); // Rebuild only on topology changes; train updates do not recreate rails.
            }
            RefreshBridgeSites();
            if (cityRevision != game.World.cityRevision)
            {
                // Towns change a building at a time; every change recombines that town's boxes into one static mesh per material.
                cityRevision = game.World.cityRevision;
                Clear(cityRoot);
                Clear(cityDetailRoot);
                batchTarget = cityRoot;
                batch = new Dictionary<Material, List<CombineInstance>>();
                detailBatch = new Dictionary<Material, List<CombineInstance>>();
                emitters.Clear();
                venues.Clear();
                driveIns.Clear();
                lidos.Clear();
                CollectTownStreets();
                BeginConstructionPass();
                foreach (var city in game.World.cities)
                    DrawCity(city);
                EndConstructionPass();
                DrawIntercityRoads();
                DrawBeachParks();
                DrawServiceAreas();
                DrawCampsites();
                Combine(cityDetailRoot, detailBatch);
                detailBatch = null;
                Combine(cityRoot);
                life.Refresh(this, game, emitters);
                matches.Refresh(this, venues);
                cinemas.Refresh(this, driveIns, traffic);
            }
            SyncTrees();
            RefreshLightRail();
            UpdateTownSigns();
            levelCrossings.Refresh(this, game);
            traffic.Refresh(this, game);
            pools.Refresh(this, traffic, game, lidos);
            beach.Refresh(this, game, traffic);
            skiLife.Refresh(this, game, traffic);
            roadside.Refresh(this, game, traffic);
            fires.Refresh(this, game, traffic);
            campLife.Refresh(this, game, traffic);
            UpdateStationLabels();
            foreach (var t in game.World.trains)
                if (!trainViews.ContainsKey(t.id))
                    CreateTrain(t);
            var remove = new List<int>();
            foreach (var kv in trainViews)
                if (game.Trains.Train(kv.Key) == null)
                    remove.Add(kv.Key);
            foreach (int id in remove)
            {
                foreach (var v in trainViews[id])
                    Destroy(v.gameObject);
                trainViews.Remove(id);
                trainDress.Remove(id);
                consists.Forget(id);
            }
        }
        /// <summary>
        /// The main platform and canopy on the building strip, as long as the station, and a narrow island platform with its own
        /// canopy between each pair of neighbouring tracks. Islands leave clearance for train bodies (half-width .22).
        /// </summary>
        void DrawStation(StationState s)
        {
            // While its upgrade show plays, the rising construction site stands in for the finished station.
            if (upgradingStations.Contains(s.id))
                return;
            int length = StationLayout.Length(s), platforms = StationLayout.Platforms(s);
            float middle = StationLayout.Middle(length);
            var along = s.axis == 1 ? Vector3.right : Vector3.forward;
            var across = new Vector3(Directions.Dx[s.side], 0, Directions.Dz[s.side]);
            var origin = new Vector3(s.cell.x, .15f, s.cell.z) + along * middle;
            Vector3 Span(float width) => along * length + (Vector3.one - along - Vector3.up) * width;
            foreach (var c in StationLayout.Strip(s))
                if (trees.TryGetValue(c.Key, out var tree))
                    tree.SetActive(false);
            var p = origin + across;
            Box("Platform", p, Span(.8f) + Vector3.up * .3f, Cream, stationRoot);
            Box("Station canopy", p + Vector3.up * .95f, Span(.9f) + Vector3.up * .14f, Navy, stationRoot);
            for (int i = StationLayout.First(length); i <= StationLayout.Last(length); i++)
                Box("Canopy column", p + along * (i - middle) + Vector3.up * .5f, new Vector3(.08f, 1, .08f), Cream, stationRoot);
            for (int k = 1; k < platforms; k++)
            {
                var island = origin - across * (k - .5f);
                Box("Island platform", island, Span(.34f) + Vector3.up * .3f, Cream, stationRoot);
                Box("Island canopy", island + Vector3.up * .95f, Span(.4f) + Vector3.up * .1f, Navy, stationRoot);
                for (int i = StationLayout.First(length); i <= StationLayout.Last(length); i += 2)
                    Box("Island column", island + along * (i - middle) + Vector3.up * .5f, new Vector3(.06f, 1, .06f), Cream, stationRoot);
            }
            DrawStationBuilding(s);
        }
        public void UpdateStationLabels()
        {
            foreach (var station in game.World.stations)
            {
                if (!stationLabels.TryGetValue(station.id, out var label)) continue;
                ShowStationQueue(station, label, StationLoad.Of(game.World, game.Balance, station));
            }
            crowds.Sync(game);
        }
        void DrawCrossingSignals(TrackPieceState track)
        {
            for (int d = 0; d < 4; d++)
            {
                var direction = new Vector3(Directions.Dx[d], 0, Directions.Dz[d]);
                var side = Vector3.Cross(Vector3.up, direction);
                var position = new Vector3(track.cell.x, 0, track.cell.z) + direction * .43f + side * .36f;
                Box("Signal post", position + Vector3.up * .45f, new Vector3(.07f, .9f, .07f), Navy, signalRoot);
                var rotation = Quaternion.LookRotation(direction);
                Box("Signal housing", position + Vector3.up * .97f, new Vector3(.23f, .46f, .14f), Navy, signalRoot, rotation);
                var red = Box("Red light", position + Vector3.up * 1.08f + direction * .08f, new Vector3(.14f, .14f, .035f), Cream, signalRoot, rotation).GetComponent<Renderer>();
                var green = Box("Green light", position + Vector3.up * .87f + direction * .08f, new Vector3(.14f, .14f, .035f), Cream, signalRoot, rotation).GetComponent<Renderer>();
                crossingLights.Add((track.id, d, red, green));
            }
        }
        void UpdateCrossingSignals()
        {
            foreach (var light in crossingLights)
            {
                bool green = CrossingSignals.IsGreen(game.World, light.trackId, light.entry);
                light.red.sharedMaterial = Mat(green ? new Color(.23f, .08f, .06f) : new Color(1f, .12f, .06f));
                light.green.sharedMaterial = Mat(green ? new Color(.15f, 1f, .3f) : new Color(.04f, .2f, .08f));
            }
        }
        public void DrawTrack(TrackPieceState track, Transform parent, Color? tint)
        {
            var c = track.cell;
            Color ballast = tint ?? new Color(.48f, .46f, .39f);
            Box("Ballast", new Vector3(c.x, .035f, c.z), new Vector3(.82f, .07f, .82f), ballast, parent);
            // The middle piece draws the whole span, so a bridge stands in one style from bank to bank (BridgeArt).
            if (track.bridge != 0 && c.x == 31)
                DrawBridge(track.bridge, BridgeCatalog.StyleAt(game.World, track.bridge), RailDeck, parent, tint);
            for (int a = 0; a < 4; a++)
                for (int b = a + 1; b < 4; b++)
                    if (Directions.Allows(track.mask, a, b))
                    {
                        for (int i = 0; i < 8; i++)
                        {
                            Vector3 p = RailGeometry.Sample(c, a, b, i / 8f), q = RailGeometry.Sample(c, a, b, (i + 1) / 8f);
                            Vector3 tangent = (q - p).normalized, side = Vector3.Cross(Vector3.up, tangent) * .20f;
                            Box("Sleeper", (p + q) * .5f - Vector3.up * .045f, new Vector3(.65f, .06f, .075f), tint ?? new Color(.29f, .23f, .17f), parent, Quaternion.LookRotation(tangent));
                            foreach (float sign in new[] { -1f, 1f })
                            {
                                Box("Rail", (p + q) * .5f + side * sign, new Vector3(.04f, .05f, Vector3.Distance(p, q) + .015f), tint ?? new Color(.64f, .66f, .61f), parent, Quaternion.LookRotation(tangent));
                            }
                        }
                    }
        }
        public void Preview(BuildPlan plan)
        {
            PreviewBridge(plan);
            previewRoot.gameObject.SetActive(plan != null);
            if (plan == null)
                return;
            previewVertices.Clear();
            previewIndices.Clear();
            foreach (var c in plan.path)
            {
                var piece = plan.changes.Find(t => t.cell.Equals(c)) ?? game.Network.At(c);
                if (piece == null)
                {
                    AddPreviewQuad(new Vector3(c.x - .4f, .22f, c.z - .4f), new Vector3(c.x - .4f, .22f, c.z + .4f), new Vector3(c.x + .4f, .22f, c.z + .4f), new Vector3(c.x + .4f, .22f, c.z - .4f));
                    continue;
                }
                for (int a = 0; a < 4; a++)
                    for (int b = a + 1; b < 4; b++)
                        if (Directions.Allows(piece.mask, a, b))
                            for (int i = 0; i < 8; i++)
                            {
                                var p = RailGeometry.Sample(c, a, b, i / 8f) + Vector3.up * .08f;
                                var q = RailGeometry.Sample(c, a, b, (i + 1) / 8f) + Vector3.up * .08f;
                                var side = Vector3.Cross(Vector3.up, (q - p).normalized) * .24f;
                                AddPreviewQuad(p - side, q - side, q + side, p + side);
                            }
            }
            previewMesh.Clear();
            previewMesh.SetVertices(previewVertices);
            previewMesh.SetTriangles(previewIndices, 0);
            previewMesh.RecalculateNormals();
            previewMesh.RecalculateBounds();
            previewRoot.GetComponent<MeshRenderer>().sharedMaterial = Mat(plan.valid ? new Color(.35f, .95f, .54f) : new Color(.96f, .25f, .21f));
        }
        void AddPreviewQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int n = previewVertices.Count;
            previewVertices.Add(a);
            previewVertices.Add(b);
            previewVertices.Add(c);
            previewVertices.Add(d);
            previewIndices.Add(n);
            previewIndices.Add(n + 1);
            previewIndices.Add(n + 2);
            previewIndices.Add(n);
            previewIndices.Add(n + 2);
            previewIndices.Add(n + 3);
        }
        // Different liveries, silhouettes and consist lengths make each model identifiable on the map.
        public static int TrainCarCount(int model) => 1 + TrainCatalog.DefaultWagons(model);
        public static int TrainCarCount(TrainState train) => 1 + TrainCatalog.Wagons(train);
        public static Color TrainLivery(int model)
        {
            switch (model)
            {
                case 0: return new Color(.78f, .28f, .15f);
                case 1: return new Color(.16f, .32f, .62f);
                case 3: return new Color(.85f, .57f, .13f);
                case 4: return new Color(.12f, .65f, .68f);
                case 5: return new Color(.65f, .22f, .4f);
                default: return new Color(.18f, .5f, .34f);
            }
        }
        // Render the exact same geometry as the world once per model/cargo choice.
        // Cache the pictures for this session and keep the temporary studio outside the map.
        /// <summary>A picture of the model with <paramref name="wagons"/> wagons; 0 shows its standard consist.</summary>
        public RenderTexture TrainPicture(int model, Cargo cargo, int wagons = 0)
        {
            if (wagons <= 0)
                wagons = TrainCatalog.DefaultWagons(model);
            int key = (model * 8 + (int)cargo) * 16 + wagons;
            if (trainPictures.TryGetValue(key, out var cached)) return cached;
            var studio = new GameObject("Train picture studio");
            studio.transform.position = new Vector3(10000, 10000, 10000);
            var cars = BuildTrainCars(new TrainState { model = model, cargo = cargo, wagons = wagons }, studio.transform, true);
            for (int i = 0; i < cars.Length; i++)
                cars[i].localPosition = new Vector3(0, 0, -i * .56f);
            foreach (var child in studio.GetComponentsInChildren<Transform>()) child.gameObject.layer = 31;
            var cameraObject = new GameObject("Train picture camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.orthographic = true;
            camera.aspect = 3f;
            camera.cullingMask = 1 << 31;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.055f, .09f, .12f, 1);
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 30;
            camera.transform.rotation = Quaternion.Euler(20, 65, 0);
            var bounds = new Bounds(cars[0].position, Vector3.zero);
            foreach (var renderer in studio.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(renderer.bounds);
            camera.transform.position = bounds.center - camera.transform.forward * 10;
            float halfHeight = 0;
            for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                    for (int z = -1; z <= 1; z += 2)
                    {
                        var corner = bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z));
                        var local = camera.transform.InverseTransformPoint(corner);
                        halfHeight = Mathf.Max(halfHeight, Mathf.Abs(local.y), Mathf.Abs(local.x) / camera.aspect);
                    }
            camera.orthographicSize = halfHeight * 1.15f;
            var picture = new RenderTexture(384, 128, 24) { name = TrainCatalog.Name(model) + " picture" };
            picture.Create();
            camera.targetTexture = picture;
            try
            {
                camera.Render();
                trainPictures.Add(key, picture);
                return picture;
            }
            catch
            {
                picture.Release();
                Destroy(picture);
                throw;
            }
            finally
            {
                camera.targetTexture = null;
                studio.SetActive(false);
                Destroy(studio);
                Destroy(cameraObject);
            }
        }
        public void Animate(float dt, bool paused)
        {
            UpdateCrossingSignals();
            bool near = ZoomedIn;
            if (cityDetailRoot.gameObject.activeSelf != near)
                cityDetailRoot.gameObject.SetActive(near);
            life.Animate(dt, paused ? 0 : game.World.speed, near);
            crowds.Animate(dt, paused ? 0 : game.World.speed, near);
            boarding.Animate(game, dt, paused ? 0 : game.World.speed, near);
            matches.Animate(dt, paused ? 0 : game.World.speed, near, cameraRig && cameraRig.view ? cameraRig.view.transform.forward : Vector3.forward);
            cinemas.Animate(dt, paused, near, cameraRig && cameraRig.view ? cameraRig.view.transform.forward : Vector3.forward);
            pools.Animate(dt, paused ? 0 : game.World.speed, near);
            beach.Animate(dt, paused ? 0 : game.World.speed, near);
            skiLife.Animate(dt, paused ? 0 : game.World.speed, near);
            roadside.Animate(dt, paused ? 0 : game.World.speed, near);
            fires.Animate(dt, paused ? 0 : game.World.speed);
            campLife.Animate(dt, paused ? 0 : game.World.speed, near);
            AnimateConstruction(dt, paused ? 0 : game.World.speed);
            industry.Animate(paused ? 0 : dt * Mathf.Min(game.World.speed, 2));
            AnimateStationUpgrades(dt, game.World.speed);
            levelCrossings.Animate(paused ? 0 : dt * game.World.speed, dt);
            if (!paused) traffic.Animate(dt * game.World.speed);
            AnimateTrains(dt, paused);
            AnimateLightRail(dt, paused);
        }
        void Combine(Transform root)
        {
            Combine(root, batch);
            batch = null;
            batchTarget = null;
        }
        void Combine(Transform root, Dictionary<Material, List<CombineInstance>> source)
        {
            foreach (var pair in source)
            {
                var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                ownedMeshes.Add(mesh);
                mesh.CombineMeshes(pair.Value.ToArray());
                var go = new GameObject("Batched " + root.name, typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(root, false);
                go.GetComponent<MeshFilter>().sharedMesh = mesh;
                go.GetComponent<MeshRenderer>().sharedMaterial = pair.Key;
            }
        }
        static Mesh Cone()
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3, b = (i + 1) * Mathf.PI / 3;
                int n = verts.Count;
                verts.Add(new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)));
                verts.Add(Vector3.up);
                verts.Add(new Vector3(Mathf.Cos(b), 0, Mathf.Sin(b)));
                tris.Add(n);
                tris.Add(n + 1);
                tris.Add(n + 2);
            }
            var mesh = new Mesh();
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            return mesh;
        }
        void OnDestroy()
        {
            DestroyTrainArt();
            foreach (var picture in trainPictures.Values)
            {
                picture.Release();
                Destroy(picture);
            }
            foreach (var mesh in ownedMeshes)
                if (mesh)
                    Destroy(mesh);
            foreach (var material in materials.Values)
                Destroy(material);
            foreach (var shape in new[] { cone, prism, pyramid })
                if (shape)
                    Destroy(shape);
        }
    }
}
