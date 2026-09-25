using System.Collections.Generic;
using TMPro;
using UnityEngine;
using ValleyRail.Core;

namespace ValleyRail
{
    /// <summary>
    /// Life inside every stadium when zoomed in: fans filling the stands in their team's colours and a football match on
    /// the pitch with a live scoreboard. Fans jump when their side scores and a Mexican wave goes round now and then. The
    /// two stands facing the camera are cut down to their front rows so the pitch stays in view.
    /// Cosmetic only: never reads or changes simulation randomness.
    /// </summary>
    public sealed class StadiumMatch : MonoBehaviour
    {
        /// <summary>
        /// A stadium to bring to life: its lot frame (origin on the centre spot, x between the goals), measurements, and the
        /// town's objects for each stand (full and cut away) and each floodlight mast, which the cutaway switches.
        /// </summary>
        public readonly struct Venue
        {
            public readonly Vector3 at;
            public readonly Quaternion turn;
            public readonly ArenaLayout layout;
            public readonly int hash;
            public readonly GameObject[] full, cut, masts;
            public Venue(Vector3 at, Quaternion turn, ArenaLayout layout, int hash, GameObject[] full, GameObject[] cut, GameObject[] masts)
            {
                this.at = at;
                this.turn = turn;
                this.layout = layout;
                this.hash = hash;
                this.full = full;
                this.cut = cut;
                this.masts = masts;
            }
            public bool Same(Venue other) => (at - other.at).sqrMagnitude < 1e-4f && layout.size == other.layout.size && Quaternion.Angle(turn, other.turn) < 1;
        }
        sealed class Section
        {
            public Transform view;
            public int fans; // the team this block supports
            public float order; // 0..1 round the ground, for the wave
            public int side, part;
            public bool upper; // above the rows a cut-away stand keeps
        }
        sealed class Ground
        {
            public Venue venue;
            public Transform root, ball, board;
            public FootballMatch match;
            public StadiumUltras ultras;
            public Transform[] bodies;
            public Transform[,] legs;
            public TextMeshPro[] boards;
            public int shown = -1, cutaway = -1;
            public int fans;
            public float wave = -1, nextWave = FirstWave;
            public readonly List<Section> sections = new List<Section>();
            public readonly List<Mesh> meshes = new List<Mesh>();
        }
        const float MaxStep = .05f, MaxFrame = .25f, FirstWave = 20, WaveLap = 5, WaveRise = .8f, EmptySeats = .08f;
        const int AisleEvery = 12;
        static readonly Color Black = new Color(.12f, .12f, .14f), Number = new Color(.95f, .95f, .93f), Board = new Color(1f, .78f, .3f);
        // The two clubs never change: yellow shirts with black sleeves at home, red-and-black stripes away, black shorts
        // for both. Keepers wear blue and green; the fans wear their club's two colours.
        static readonly Color[] Shirts = { new Color(1f, .82f, .1f), new Color(.84f, .12f, .1f) }, Keepers = { new Color(.45f, .72f, .95f), new Color(.35f, .8f, .3f) };
        static readonly Color[] Skins = { new Color(.95f, .8f, .66f), new Color(.86f, .65f, .5f), new Color(.62f, .43f, .3f), new Color(.42f, .28f, .2f) };
        static readonly Color[] Hair = { new Color(.12f, .09f, .07f), new Color(.35f, .22f, .12f), new Color(.8f, .65f, .35f), new Color(.5f, .5f, .5f) };
        readonly List<Ground> grounds = new List<Ground>();
        WorldView world;

        public int Grounds => grounds.Count;
        public int Fans
        {
            get
            {
                int total = 0;
                foreach (var g in grounds)
                    total += g.fans;
                return total;
            }
        }
        public FootballMatch MatchAt(int ground) => grounds[ground].match;
        public Transform BallAt(int ground) => grounds[ground].ball;
        public Transform RootAt(int ground) => grounds[ground].root;
        public StadiumUltras UltrasAt(int ground) => grounds[ground].ultras;
        /// <summary>The shirt colour of team 0 (home, yellow) or 1 (away, red); coaches and flags wear it too.</summary>
        public static Color TeamShirt(int team) => Shirts[team];

        /// <summary>
        /// Matches grounds to the stadiums after the towns were redrawn. A stadium that did not move keeps its crowd and its
        /// match (score and all); only new ones are built.
        /// </summary>
        public void Refresh(WorldView view, List<Venue> venues)
        {
            world = view;
            var kept = new List<Ground>();
            foreach (var venue in venues)
            {
                var ground = grounds.Find(g => g.venue.Same(venue) && !kept.Contains(g));
                if (ground != null)
                {
                    // Same ground, freshly drawn stands: keep the match and re-apply the cutaway to the new objects.
                    ground.venue = venue;
                    ground.cutaway = -1;
                }
                kept.Add(ground ?? Build(venue));
            }
            foreach (var g in grounds)
                if (!kept.Contains(g))
                    Discard(g);
            grounds.Clear();
            grounds.AddRange(kept);
        }
        void OnDestroy()
        {
            foreach (var g in grounds)
                foreach (var mesh in g.meshes)
                    Destroy(mesh);
        }
        void Discard(Ground g)
        {
            g.root.gameObject.SetActive(false);
            Destroy(g.root.gameObject);
            foreach (var mesh in g.meshes)
                Destroy(mesh);
        }

        /// <summary>
        /// Shows fans and match only while zoomed in, cutting away the stands that face <paramref name="view"/> (the camera's
        /// forward). Play pauses with the game; at 4× the match runs at 2× so it stays watchable.
        /// </summary>
        public void Animate(float dt, float speed, bool near, Vector3 view)
        {
            float step = Mathf.Min(dt * Mathf.Min(speed, 2), MaxFrame);
            int substeps = Mathf.Max(1, Mathf.CeilToInt(step / MaxStep));
            foreach (var g in grounds)
            {
                Cutaway(g, near ? FacingSides(g.venue, view) : 0);
                if (g.root.gameObject.activeSelf != near)
                    g.root.gameObject.SetActive(near);
                if (!near || speed <= 0)
                    continue;
                for (int i = 0; i < substeps; i++)
                    g.match.Step(step / substeps);
                Pose(g);
                Cheer(g, step);
                g.ultras.Animate(step, g.match.CheeringTeam);
                ShowScore(g);
            }
        }

        Ground Build(Venue venue)
        {
            var root = new GameObject("Stadium match").transform;
            root.SetParent(transform, false);
            root.localPosition = venue.at;
            root.localRotation = venue.turn;
            var ground = new Ground { venue = venue, root = root };
            // Seeded per stadium, so a ground keeps its kits, faces and seats between redraws.
            var random = new System.Random(venue.hash * 31 + 7);
            BuildCrowd(ground, random);
            BuildTeams(ground, random);
            ground.ultras = new StadiumUltras(world, root, venue.layout, random, (side, row, along) => Holder(ground, side, row, along));
            var ballMesh = BallMesh();
            ground.meshes.Add(ballMesh);
            var ball = new GameObject("Football", typeof(MeshFilter), typeof(MeshRenderer));
            ball.GetComponent<MeshFilter>().sharedMesh = ballMesh;
            ball.GetComponent<MeshRenderer>().sharedMaterial = world.Mat(new Color(.97f, .97f, .95f));
            ground.ball = ball.transform;
            ground.ball.SetParent(root, false);
            ground.ball.localScale = Vector3.one * BallSize(venue.layout);
            BuildBoards(ground);
            Cutaway(ground, 0);
            Pose(ground);
            ShowScore(ground);
            return ground;
        }
        static float BallSize(ArenaLayout layout) => .042f * layout.Player;
        /// <summary>
        /// A low-poly ball one unit across. Built by hand: the sphere primitive adds a SphereCollider, and the player build
        /// strips the physics module, so on the phone it logs an error instead.
        /// </summary>
        static Mesh BallMesh()
        {
            const int Rings = 6, Segments = 10;
            var vertices = new List<Vector3>((Rings + 1) * (Segments + 1));
            var triangles = new List<int>(Rings * Segments * 6);
            for (int ring = 0; ring <= Rings; ring++)
            {
                float polar = Mathf.PI * ring / Rings;
                for (int segment = 0; segment <= Segments; segment++)
                {
                    float around = 2 * Mathf.PI * segment / Segments;
                    vertices.Add(new Vector3(Mathf.Sin(polar) * Mathf.Cos(around), Mathf.Cos(polar), Mathf.Sin(polar) * Mathf.Sin(around)) * .5f);
                }
            }
            for (int ring = 0; ring < Rings; ring++)
                for (int segment = 0; segment < Segments; segment++)
                {
                    int top = ring * (Segments + 1) + segment, bottom = top + Segments + 1;
                    triangles.AddRange(new[] { top, top + 1, bottom, bottom, top + 1, bottom + 1 });
                }
            var mesh = new Mesh { name = "Football" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Seats a fan on nearly every seat. The clubs' fans never share a stand: yellow-and-black fill the end behind the -x
        /// goal and the +z touchline, red-and-black the other end and the -z touchline. Each block of seats is one mesh, so a
        /// whole block can stand up and cheer.
        /// </summary>
        void BuildCrowd(Ground ground, System.Random random)
        {
            var layout = ground.venue.layout;
            float s = layout.Spectator, spacing = .068f * s;
            for (int side = 0; side < 4; side++)
            {
                var stand = layout.Stand(side);
                int parts = Parts(stand), seats = Mathf.FloorToInt(stand.length * .96f / spacing);
                // One block per part of the stand and per band (the front rows a cut-away stand keeps, and the rest).
                var builders = new TrainMeshBuilder[parts, 2];
                for (int row = 0; row < stand.rows; row++)
                {
                    var middle = stand.RowCentre(row);
                    for (int seat = 0; seat < seats; seat++)
                    {
                        if ((seat + 1) % AisleEvery == 0 || random.NextDouble() < EmptySeats)
                            continue;
                        float along = (seat + .5f - seats / 2f) * spacing;
                        int part = PartOf(stand, along);
                        int fans = Supports(side);
                        var at = middle + stand.Along * (along + ((float)random.NextDouble() - .5f) * spacing * .2f);
                        int band = row < stand.CutRows ? 0 : 1;
                        var builder = builders[part, band] ??= world.PaletteBuilder();
                        builder.Box(at + Vector3.up * .03f * s, stand.Span(.034f * s, .06f * s, .05f * s), new Paint(FanShirt(fans, random), Finish.Matte));
                        builder.Box(at + Vector3.up * .075f * s, Vector3.one * .03f * s, new Paint(Skins[random.Next(Skins.Length)], Finish.Matte));
                        ground.fans++;
                    }
                }
                for (int part = 0; part < parts; part++)
                    for (int band = 0; band < 2; band++)
                    {
                        if (builders[part, band] == null)
                            continue;
                        var view = MeshObject("Fans", world.BuildPaletteMesh(builders[part, band], "Stadium fans", out var materials), materials, ground);
                        var centre = stand.centre + stand.Along * ((part + .5f) / parts - .5f) * stand.length;
                        float order = Mathf.Repeat(Mathf.Atan2(centre.z, centre.x) / (2 * Mathf.PI), 1);
                        ground.sections.Add(new Section { view = view, fans = Supports(side), order = order, side = side, part = part, upper = band == 1 });
                    }
            }
        }
        static int Parts(Stand stand) => stand.alongX ? 4 : 2;
        static int PartOf(Stand stand, float along) => Mathf.Clamp((int)((along / stand.length + .5f) * Parts(stand)), 0, Parts(stand) - 1);
        /// <summary>The block of fans sitting at a seat, so whatever they hold jumps and hides with them.</summary>
        static Transform Holder(Ground ground, int side, int row, float along)
        {
            var stand = ground.venue.layout.Stand(side);
            int part = PartOf(stand, along);
            bool upper = row >= stand.CutRows;
            foreach (var section in ground.sections)
                if (section.side == side && section.part == part && section.upper == upper)
                    return section.view;
            return ground.root;
        }
        static int Supports(int side) => side == 0 || side == 3 ? 0 : 1;
        static Color FanShirt(int team, System.Random random) => random.NextDouble() < .62 ? Shirts[team] : Black;

        void BuildTeams(Ground ground, System.Random random)
        {
            var layout = ground.venue.layout;
            var match = new FootballMatch(layout.PitchLength, layout.PitchWidth, layout.GoalWidth, layout.GoalDepth, layout.GoalHeight, layout.Player, layout.size >= 4, ground.venue.hash);
            ground.match = match;
            float p = layout.Player;
            int count = match.players.Length;
            ground.bodies = new Transform[count];
            ground.legs = new Transform[count, 2];
            var legMeshes = new Dictionary<Color, (Mesh mesh, Material[] materials)>();
            for (int i = 0; i < count; i++)
            {
                var player = match.players[i];
                var shirt = player.role == FootballMatch.Role.Referee ? Black : player.role == FootballMatch.Role.Keeper ? Keepers[player.team] : Shirts[player.team];
                var shorts = Black;
                // Outfield trim: black sleeves on the yellow shirts, black stripes on the red ones.
                var trim = player.role == FootballMatch.Role.Outfield ? player.team : -1;
                var root = new GameObject(player.role == FootballMatch.Role.Referee ? "Referee" : "Player").transform;
                root.SetParent(ground.root, false);
                ground.bodies[i] = root;
                var body = PlayerBody(shirt, shorts, trim, p, random);
                MeshObject("Body", world.BuildPaletteMesh(body, "Footballer", out var bodyMaterials), bodyMaterials, ground).SetParent(root, false);
                if (!legMeshes.TryGetValue(shirt, out var leg))
                {
                    var builder = world.PaletteBuilder();
                    builder.Box(new Vector3(0, -.024f, 0) * p, new Vector3(.019f, .048f, .02f) * p, new Paint(shirt, Finish.Matte));
                    builder.Box(new Vector3(0, -.05f, .004f) * p, new Vector3(.021f, .01f, .028f) * p, new Paint(Black, Finish.Matte));
                    leg.mesh = world.BuildPaletteMesh(builder, "Footballer leg", out leg.materials);
                    ground.meshes.Add(leg.mesh);
                    legMeshes.Add(shirt, leg);
                }
                for (int side = 0; side < 2; side++)
                {
                    var limb = new GameObject("Leg", typeof(MeshFilter), typeof(MeshRenderer)).transform;
                    limb.SetParent(root, false);
                    limb.localPosition = new Vector3((side == 0 ? -1 : 1) * .013f * p, .055f * p, 0);
                    limb.GetComponent<MeshFilter>().sharedMesh = leg.mesh;
                    limb.GetComponent<MeshRenderer>().sharedMaterials = leg.materials;
                    ground.legs[i, side] = limb;
                }
            }
        }
        /// <summary>
        /// Shorts, shirt with sleeves and a number on the back, hands, head and usually hair; the hips sit at .055.
        /// <paramref name="trim"/> 0 gives black sleeves, 1 black vertical stripes, anything else a plain shirt.
        /// </summary>
        TrainMeshBuilder PlayerBody(Color shirt, Color shorts, int trim, float p, System.Random random)
        {
            var builder = world.PaletteBuilder();
            var skin = Skins[random.Next(Skins.Length)];
            static Paint Matte(Color color) => new Paint(color, Finish.Matte);
            builder.Box(new Vector3(0, .066f, 0) * p, new Vector3(.052f, .03f, .032f) * p, Matte(shorts));
            builder.Box(new Vector3(0, .1f, 0) * p, new Vector3(.056f, .046f, .034f) * p, Matte(shirt));
            for (int side = -1; side <= 1; side += 2)
            {
                builder.Box(new Vector3(side * .036f, .1f, 0) * p, new Vector3(.016f, .036f, .02f) * p, Matte(trim == 0 ? Black : shirt));
                builder.Box(new Vector3(side * .036f, .076f, 0) * p, new Vector3(.013f, .012f, .015f) * p, Matte(skin));
                if (trim == 1)
                    builder.Box(new Vector3(side * .014f, .1f, 0) * p, new Vector3(.01f, .046f, .036f) * p, Matte(Black));
            }
            builder.Box(new Vector3(0, .104f, -.0175f) * p, new Vector3(.02f, .022f, .002f) * p, Matte(shirt.grayscale > .6f ? Black : Number));
            builder.Box(new Vector3(0, .14f, 0) * p, Vector3.one * .03f * p, Matte(skin));
            if (random.NextDouble() < .85)
                builder.Box(new Vector3(0, .157f, -.002f) * p, new Vector3(.032f, .009f, .033f) * p, Matte(Hair[random.Next(Hair.Length)]));
            return builder;
        }
        Transform MeshObject(string name, Mesh mesh, Material[] materials, Ground ground)
        {
            ground.meshes.Add(mesh);
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(ground.root, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterials = materials;
            return go.transform;
        }

        void BuildBoards(Ground ground)
        {
            var size = ground.venue.layout.BoardSize;
            ground.board = new GameObject("Scoreboard").transform;
            ground.board.SetParent(ground.root, false);
            world.Box("Scoreboard", Vector3.zero, size, WorldView.Navy, ground.board);
            ground.boards = new TextMeshPro[2];
            for (int face = 0; face < 2; face++)
            {
                // One face looks over the pitch, the other out over the town; text reads from the front of each.
                var go = new GameObject("Score", typeof(TextMeshPro));
                go.transform.SetParent(ground.board, false);
                go.transform.localPosition = Vector3.forward * (face == 0 ? 1 : -1) * (size.z / 2 + .004f);
                go.transform.localRotation = face == 0 ? Quaternion.Euler(0, 180, 0) : Quaternion.identity;
                var text = go.GetComponent<TextMeshPro>();
                text.rectTransform.sizeDelta = new Vector2(size.x * .9f, size.y * .86f);
                text.enableAutoSizing = true;
                text.fontSizeMin = .1f;
                text.fontSizeMax = 40;
                text.alignment = TextAlignmentOptions.Center;
                text.fontStyle = FontStyles.Bold;
                text.color = Board;
                text.enableWordWrapping = false;
                ground.boards[face] = text;
            }
        }
        static void ShowScore(Ground ground)
        {
            var m = ground.match;
            // Rebuild the text only when the score, minute or phase changes, so a running match makes no garbage per frame.
            int key = ((m.score[0] * 100 + m.score[1]) * 100 + m.Minute) * 4 + (int)m.Stage;
            if (key == ground.shown)
                return;
            ground.shown = key;
            string status = m.Stage == FootballMatch.Phase.Goal ? "GOAL!" : m.Stage == FootballMatch.Phase.FullTime ? "FULL TIME" : m.Minute + "'";
            string text = m.score[0] + " - " + m.score[1] + "\n<size=55%>" + status + "</size>";
            foreach (var board in ground.boards)
                board.text = text;
        }

        /// <summary>Stands whose outside faces the camera (bit per side), so they would hide the pitch.</summary>
        static int FacingSides(Venue venue, Vector3 view)
        {
            var flat = new Vector3(view.x, 0, view.z).normalized;
            int mask = 0;
            for (int side = 0; side < 4; side++)
                if (Vector3.Dot(venue.turn * new Vector3(Directions.Dx[side], 0, Directions.Dz[side]), flat) < -.3f)
                    mask |= 1 << side;
            return mask;
        }
        /// <summary>
        /// Lowers the stands in <paramref name="mask"/> to their front rows (fans above them hidden), drops the floodlight
        /// between two lowered stands and hangs the scoreboard over the far touchline.
        /// </summary>
        static void Cutaway(Ground ground, int mask)
        {
            if (mask == ground.cutaway)
                return;
            ground.cutaway = mask;
            var venue = ground.venue;
            for (int side = 0; side < 4; side++)
            {
                bool lowered = (mask & 1 << side) != 0;
                if (venue.full?[side])
                    venue.full[side].SetActive(!lowered);
                if (venue.cut?[side])
                    venue.cut[side].SetActive(lowered);
            }
            for (int corner = 0; corner < 4; corner++)
            {
                int across = corner % 2 == 0 ? 3 : 1, along = corner < 2 ? 2 : 0;
                if (venue.masts?[corner])
                    venue.masts[corner].SetActive((mask & 1 << across) == 0 || (mask & 1 << along) == 0);
            }
            foreach (var section in ground.sections)
                section.view.gameObject.SetActive(!section.upper || (mask & 1 << section.side) == 0);
            ground.ultras.Cutaway(mask);
            bool farSide = (mask & 1 << 2) != 0;
            var centre = venue.layout.BoardCentre;
            ground.board.localPosition = farSide ? new Vector3(centre.x, centre.y, -centre.z) : centre;
            ground.board.localRotation = farSide ? Quaternion.Euler(0, 180, 0) : Quaternion.identity;
        }

        static void Pose(Ground ground)
        {
            var m = ground.match;
            for (int i = 0; i < m.players.Length; i++)
            {
                var p = m.players[i];
                var body = ground.bodies[i];
                body.localPosition = new Vector3(p.position.x, ArenaLayout.PitchTop + p.hop, p.position.y);
                // A tackled player goes down flat for a moment, then is back on his feet.
                float fallen = Mathf.Clamp01(p.stunned / .25f);
                body.localRotation = Quaternion.Euler(fallen * 80, p.facing, 0);
                float swing = Mathf.Sin(p.stride * Mathf.PI) * 40 * Mathf.Clamp01(p.velocity.magnitude / m.JogSpeed);
                ground.legs[i, 0].localRotation = Quaternion.Euler(swing, 0, 0);
                ground.legs[i, 1].localRotation = Quaternion.Euler(-swing, 0, 0);
            }
            float radius = BallSize(ground.venue.layout) / 2;
            ground.ball.localPosition = new Vector3(m.Ball.x, ArenaLayout.PitchTop + radius + m.Ball.y, m.Ball.z);
        }
        /// <summary>Supporters of a scoring side jump up and down; in open play a Mexican wave goes round every half minute or so.</summary>
        static void Cheer(Ground ground, float dt)
        {
            var m = ground.match;
            float lift = .045f * ground.venue.layout.Spectator;
            if (m.Stage == FootballMatch.Phase.Play && ground.wave < 0 && (ground.nextWave -= dt) <= 0)
            {
                ground.wave = 0;
                ground.nextWave = 30 + (ground.venue.hash + m.Goals * 7) % 20;
            }
            if (ground.wave >= 0 && (ground.wave += dt) > WaveLap + WaveRise)
                ground.wave = -1;
            int cheering = m.CheeringTeam;
            foreach (var section in ground.sections)
            {
                float y = 0;
                if (cheering >= 0)
                    y = section.fans == cheering ? lift * Mathf.Abs(Mathf.Sin(m.PhaseTime * 8 + section.order * 17)) : 0;
                else if (ground.wave >= 0)
                {
                    float u = (ground.wave - section.order * WaveLap) / WaveRise;
                    if (u > 0 && u < 1)
                        y = lift * 1.3f * Mathf.Sin(u * Mathf.PI);
                }
                section.view.localPosition = new Vector3(0, y, 0);
            }
        }
    }
}
