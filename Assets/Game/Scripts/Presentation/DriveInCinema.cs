using System.Collections.Generic;
using UnityEngine;

namespace ValleyRail
{
    /// <summary>
    /// Life at every drive-in: the programme playing on the screens (<see cref="DriveInFilm"/>), the projector's beam, and
    /// bodies for the cars and people of each lot's <see cref="DriveInTraffic"/>. Each lot turns as a whole so its screen
    /// always faces the camera. The show runs in real time and freezes while the game is paused. Cosmetic only: never
    /// reads or changes simulation randomness.
    /// </summary>
    public sealed class DriveInCinema : MonoBehaviour
    {
        /// <summary>
        /// A drive-in the town pass drew: its lot centre and size, the quarter turn (0 = +z) of the side its street is on,
        /// and its two lot meshes, which this turns.
        /// </summary>
        public readonly struct Site
        {
            public readonly Vector3 at;
            public readonly int size, hash, street;
            public readonly GameObject art, details;
            public Site(Vector3 at, int size, int hash, int street, GameObject art, GameObject details)
            {
                this.at = at;
                this.size = size;
                this.hash = hash;
                this.street = street;
                this.art = art;
                this.details = details;
            }
            public bool Same(Site other) => (at - other.at).sqrMagnitude < 1e-4f && size == other.size;
        }
        sealed class Cinema
        {
            public Site site;
            public DriveInLayout layout;
            public DriveInTraffic traffic;
            public Transform root, screen;
            public Mesh beamMesh;
            public bool filled;
            public int turn = -1;
            public readonly Dictionary<int, Transform> cars = new Dictionary<int, Transform>();
            public readonly Dictionary<int, Transform> people = new Dictionary<int, Transform>();
        }

        const float FrameRate = 12, BeamAlpha = .07f;
        const int Liveries = 8;
        /// <summary>The programme starts a few seconds into the western, so a new game shows a film at once.</summary>
        public static readonly float Opening = DriveInFilm.Start(DriveInFilm.Reel.Western) + 8;
        // Cars only: hatchbacks and saloons most, then SUVs, pickups and the odd taxi.
        static readonly int[] CarModels = { 0, 0, 1, 1, 2, 5, 0, 1, 3 };
        static readonly Color[] Shirts = { new Color(.85f, .2f, .18f), new Color(.2f, .4f, .75f), new Color(.95f, .8f, .2f), new Color(.3f, .6f, .35f), new Color(.92f, .92f, .9f) };
        static readonly Color Trousers = new Color(.18f, .2f, .3f), Skin = new Color(.9f, .74f, .6f);
        readonly List<Cinema> cinemas = new List<Cinema>();
        readonly List<(Mesh mesh, Material[] materials)> looks = new List<(Mesh, Material[])>();
        readonly HashSet<int> seen = new HashSet<int>();
        readonly List<int> gone = new List<int>();
        DriveInFilm film;
        Texture2D picture;
        Material screenMaterial, beamMaterial;
        Mesh screenMesh;
        WorldView world;
        CityTraffic traffic;
        float clock = Opening;
        int frame = -1;
        // The camera's forward, as last seen; the lots turn to it.
        Vector3 facing = new Vector3(1, -1, 1);

        public int Sites => cinemas.Count;
        /// <summary>Seconds into the programme; tests may move it.</summary>
        public float Clock { get => clock; set => clock = value; }
        public Texture2D Picture => picture;
        public DriveInLayout LayoutAt(int site) => cinemas[site].layout;
        public DriveInTraffic TrafficAt(int site) => cinemas[site].traffic;
        public Transform RootAt(int site) => cinemas[site].root;
        public Transform ScreenAt(int site) => cinemas[site].screen;
        /// <summary>Quarter turns of a lot: its screen faces the camera, 45° round from straight on.</summary>
        public int TurnAt(int site) => cinemas[site].turn;

        /// <summary>
        /// Matches the lots to the drive-ins after the towns were redrawn. A drive-in that did not move keeps its cars and
        /// people; only its fresh lot meshes are turned to match.
        /// </summary>
        public void Refresh(WorldView view, List<Site> sites, CityTraffic cars)
        {
            world = view;
            traffic = cars;
            var kept = new List<Cinema>();
            foreach (var site in sites)
            {
                var cinema = cinemas.Find(c => c.site.Same(site) && !kept.Contains(c));
                if (cinema != null)
                    cinema.site = site;
                else
                    cinema = Build(site);
                kept.Add(cinema);
                // The fresh lot meshes need turning too.
                cinema.turn = -1;
                Face(cinema);
            }
            foreach (var c in cinemas)
                if (!kept.Contains(c))
                    Discard(c);
            cinemas.Clear();
            cinemas.AddRange(kept);
        }

        /// <summary>Runs the show, the cars and the people; <paramref name="camera"/> is the camera's forward.</summary>
        public void Animate(float dt, bool paused, bool near, Vector3 camera)
        {
            if (cinemas.Count == 0)
                return;
            facing = camera;
            foreach (var c in cinemas)
                Face(c);
            float step = paused ? 0 : Mathf.Min(dt, .25f);
            var before = DriveInFilm.ReelAt(clock, out _);
            clock += step;
            if (clock > DriveInFilm.Length * 1000)
                clock = Mathf.Repeat(clock, DriveInFilm.Length);
            var now = DriveInFilm.ReelAt(clock, out _);
            foreach (var c in cinemas)
            {
                if (c.site.details && c.site.details.activeSelf != near)
                    c.site.details.SetActive(near);
                // Car bodies come from the town traffic's shared meshes, which need it to have seen the world first.
                if (!c.filled && traffic != null)
                {
                    c.filled = true;
                    c.traffic.Fill();
                }
                if (step > 0)
                {
                    if (now != before)
                        c.traffic.Cue(now);
                    c.traffic.Step(step);
                }
                if (c.filled)
                    Show(c, near);
            }
            Project();
        }

        // ---- Lots -------------------------------------------------------------------------------------------------

        Cinema Build(Site site)
        {
            EnsureShared();
            var layout = new DriveInLayout(site.size);
            var root = new GameObject("Drive-in cinema").transform;
            root.SetParent(transform, false);
            root.localPosition = site.at;
            var c = new Cinema
            {
                site = site,
                layout = layout,
                root = root,
                traffic = new DriveInTraffic(layout, site.hash * 17 + 3, CarModels, Liveries, Shirts.Length),
            };
            var screen = new GameObject("Screen", typeof(MeshFilter), typeof(MeshRenderer));
            screen.GetComponent<MeshFilter>().sharedMesh = screenMesh;
            screen.GetComponent<MeshRenderer>().sharedMaterial = screenMaterial;
            c.screen = screen.transform;
            c.screen.SetParent(root, false);
            c.screen.localPosition = new Vector3(0, layout.screenBottom + layout.screenHeight / 2, layout.screenZ + .006f);
            c.screen.localScale = new Vector3(layout.screenWidth, layout.screenHeight, 1);
            c.beamMesh = BeamMesh(layout);
            var beam = new GameObject("Projector beam", typeof(MeshFilter), typeof(MeshRenderer));
            beam.GetComponent<MeshFilter>().sharedMesh = c.beamMesh;
            beam.GetComponent<MeshRenderer>().sharedMaterial = beamMaterial;
            beam.transform.SetParent(root, false);
            return c;
        }
        void Discard(Cinema c)
        {
            c.root.gameObject.SetActive(false);
            Destroy(c.root.gameObject);
            Destroy(c.beamMesh);
        }
        void OnDestroy()
        {
            foreach (var c in cinemas)
                Destroy(c.beamMesh);
            foreach (var look in looks)
                Destroy(look.mesh);
            if (picture) Destroy(picture);
            if (screenMaterial) Destroy(screenMaterial);
            if (beamMaterial) Destroy(beamMaterial);
            if (screenMesh) Destroy(screenMesh);
        }
        /// <summary>
        /// The quarter turn that puts the screen on the far side of the lot, facing the camera 45° round from straight on.
        /// Two turns do that; the one with the entrance (the side the screen faces) on the street side wins when there is
        /// one. The view rests at 45° plus whole quarter turns, so this only changes halfway through a turn of the camera.
        /// </summary>
        static int QuarterFacing(Vector3 view, int street)
        {
            float yaw = Mathf.Atan2(-view.x, -view.z) * Mathf.Rad2Deg;
            int right = (int)Mathf.Repeat(Mathf.Round((yaw - 45) / 90), 4), left = (right + 1) % 4;
            return street == left ? left : right;
        }
        void Face(Cinema c)
        {
            int quarter = QuarterFacing(facing, c.site.street);
            if (quarter == c.turn)
                return;
            c.turn = quarter;
            var rotation = Quaternion.Euler(0, 90 * quarter, 0);
            c.root.localRotation = rotation;
            if (c.site.art)
                c.site.art.transform.localRotation = rotation;
            if (c.site.details)
                c.site.details.transform.localRotation = rotation;
        }

        // ---- Bodies -----------------------------------------------------------------------------------------------

        /// <summary>Gives every car and person of the lot a body where the traffic says, and clears away those that left.</summary>
        void Show(Cinema c, bool near)
        {
            seen.Clear();
            foreach (var car in c.traffic.Cars)
            {
                seen.Add(car.id);
                if (!c.cars.TryGetValue(car.id, out var body))
                    c.cars[car.id] = body = traffic.Vehicle(car.model, car.livery, c.root);
                body.localPosition = car.at;
                body.localRotation = Quaternion.Euler(car.pitch, car.yaw, 0);
            }
            Clear(c.cars);
            seen.Clear();
            foreach (var person in c.traffic.Walkers)
            {
                seen.Add(person.id);
                if (!c.people.TryGetValue(person.id, out var body))
                    c.people[person.id] = body = Person(c, person.look);
                bool shown = near && person.visible;
                if (body.gameObject.activeSelf != shown)
                    body.gameObject.SetActive(shown);
                body.localPosition = person.at;
                body.localRotation = Quaternion.Euler(0, person.yaw, 0);
            }
            Clear(c.people);
        }
        /// <summary>Removes the bodies whose owner was not seen this frame.</summary>
        void Clear(Dictionary<int, Transform> bodies)
        {
            gone.Clear();
            foreach (var pair in bodies)
                if (!seen.Contains(pair.Key))
                    gone.Add(pair.Key);
            foreach (int id in gone)
            {
                bodies[id].gameObject.SetActive(false);
                Destroy(bodies[id].gameObject);
                bodies.Remove(id);
            }
        }
        /// <summary>A little person from shared palette meshes: legs, a shirt in one of a few colours, arms and a head.</summary>
        Transform Person(Cinema c, int look)
        {
            if (looks.Count == 0)
                foreach (var shirt in Shirts)
                {
                    var builder = world.PaletteBuilder();
                    builder.Box(new Vector3(0, .03f, 0), new Vector3(.035f, .06f, .02f), Trousers);
                    builder.Box(new Vector3(0, .085f, 0), new Vector3(.04f, .05f, .025f), shirt);
                    builder.Box(new Vector3(0, .125f, 0), new Vector3(.024f, .026f, .024f), Skin);
                    for (int side = -1; side <= 1; side += 2)
                        builder.Box(new Vector3(side * .026f, .085f, 0), new Vector3(.01f, .045f, .014f), shirt);
                    var mesh = world.BuildPaletteMesh(builder, "Drive-in visitor", out var materials);
                    looks.Add((mesh, materials));
                }
            var (personMesh, personMaterials) = looks[look % looks.Count];
            var go = new GameObject("Visitor", typeof(MeshFilter), typeof(MeshRenderer));
            go.GetComponent<MeshFilter>().sharedMesh = personMesh;
            go.GetComponent<MeshRenderer>().sharedMaterials = personMaterials;
            go.transform.SetParent(c.root, false);
            return go.transform;
        }

        // ---- Picture ----------------------------------------------------------------------------------------------

        void EnsureShared()
        {
            if (film != null)
                return;
            film = new DriveInFilm();
            picture = new Texture2D(DriveInFilm.Width, DriveInFilm.Height, TextureFormat.RGBA32, false)
            {
                name = "Drive-in picture",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            // Unlit, so the picture glows at full brightness whatever the sun does. Sprites/Default is always in the build.
            var shader = Shader.Find("Sprites/Default");
            screenMaterial = shader ? new Material(shader) : new Material(world.Mat(Color.white));
            screenMaterial.name = "Drive-in screen";
            screenMaterial.mainTexture = picture;
            if (screenMaterial.HasProperty("_BaseMap"))
                screenMaterial.SetTexture("_BaseMap", picture);
            screenMaterial.color = Color.white;
            beamMaterial = shader ? new Material(shader) : new Material(world.Mat(Color.white));
            beamMaterial.name = "Projector beam";
            beamMaterial.color = new Color(1, 1, .9f, BeamAlpha);
            screenMesh = ScreenMesh();
            frame = -1;
        }
        /// <summary>Draws the next film frame when one is due and lets the beam flicker with it.</summary>
        void Project()
        {
            int now = Mathf.FloorToInt(clock * FrameRate);
            if (now == frame || film == null)
                return;
            frame = now;
            film.Draw(clock);
            picture.SetPixels32(film.pixels);
            picture.Apply(false);
            beamMaterial.color = new Color(1, 1, .9f, BeamAlpha * (.8f + .2f * Mathf.PerlinNoise(clock * 3, 0)));
        }
        /// <summary>A unit quad facing +z, the picture the right way round for someone looking at it from the cars.</summary>
        static Mesh ScreenMesh()
        {
            var mesh = new Mesh { name = "Drive-in screen" };
            mesh.SetVertices(new[] { new Vector3(.5f, .5f, 0), new Vector3(-.5f, .5f, 0), new Vector3(-.5f, -.5f, 0), new Vector3(.5f, -.5f, 0) });
            mesh.SetUVs(0, new[] { new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0), new Vector2(0, 0) });
            mesh.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
        /// <summary>A faint pyramid of light from the projector port to the four corners of the picture, fading towards the screen.</summary>
        static Mesh BeamMesh(DriveInLayout layout)
        {
            float w = layout.screenWidth / 2, bottom = layout.screenBottom, top = bottom + layout.screenHeight, z = layout.screenZ + .01f;
            var apex = layout.projector;
            var corners = new[] { new Vector3(-w, bottom, z), new Vector3(w, bottom, z), new Vector3(w, top, z), new Vector3(-w, top, z) };
            var vertices = new List<Vector3>();
            var colors = new List<Color>();
            var triangles = new List<int>();
            for (int i = 0; i < 4; i++)
            {
                triangles.Add(vertices.Count);
                triangles.Add(vertices.Count + 1);
                triangles.Add(vertices.Count + 2);
                vertices.Add(apex);
                vertices.Add(corners[i]);
                vertices.Add(corners[(i + 1) % 4]);
                colors.Add(Color.white);
                colors.Add(new Color(1, 1, 1, .35f));
                colors.Add(new Color(1, 1, 1, .35f));
            }
            var mesh = new Mesh { name = "Projector beam" };
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
