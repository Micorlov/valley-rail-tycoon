using System.Collections.Generic;
using UnityEngine;
using ValleyRail.Core;
namespace ValleyRail
{
    /// <summary>
    /// Draws the ski resorts (SkiResorts) once per session, merged into static meshes: the snowy plaza and the car park of
    /// each base, the chalet lodge, the gondola's valley and top stations with the towers and cables between them, three
    /// groomed pistes with coloured marker poles, and snowy pines on the slope. Everything moving is SkiLife's.
    /// </summary>
    public sealed partial class WorldView
    {
        /// <summary>Resort colours. Kept in their own class: statics of other WorldView partials are not ready in a defined order.</summary>
        static class Alpine
        {
            public static readonly Color Snow = new Color(.93f, .95f, .97f), Groomed = new Color(.97f, .98f, 1f), Asphalt = new Color(.3f, .31f, .33f),
                Paint = new Color(.92f, .92f, .88f), Timber = new Color(.52f, .31f, .16f), DarkTimber = new Color(.33f, .2f, .11f),
                Stone = new Color(.56f, .55f, .52f), Concrete = new Color(.7f, .71f, .7f), Steel = new Color(.4f, .43f, .47f),
                Cable = new Color(.13f, .13f, .15f), LiftRed = new Color(.78f, .14f, .12f), Glass = new Color(.2f, .3f, .38f),
                Window = new Color(1f, .78f, .38f), Pine = new Color(.11f, .27f, .17f), ParkBlue = new Color(.16f, .36f, .75f),
                Blue = new Color(.15f, .35f, .85f), Red = new Color(.85f, .15f, .12f), Black = new Color(.08f, .08f, .1f),
                Canvas = new Color(.9f, .3f, .2f), Gold = new Color(.95f, .7f, .2f);
        }
        readonly List<SkiLayout> skiLayouts = new List<SkiLayout>();
        /// <summary>The resorts drawn this session, one layout each, in producer order.</summary>
        public IReadOnlyList<SkiLayout> SkiLayouts => skiLayouts;
        Transform skiRoot;
        SkiLife skiLife;

        /// <summary>Draws every resort of the session. Resorts only appear when a session starts, so this runs once.</summary>
        void BuildSkiResorts()
        {
            skiRoot = Root("Ski resorts");
            skiLayouts.Clear();
            foreach (var p in game.World.producers)
                if (p.kind == ProducerKind.SkiResort)
                    skiLayouts.Add(new SkiLayout(p));
            if (skiLayouts.Count == 0)
                return;
            batchTarget = skiRoot;
            batch = new Dictionary<Material, List<CombineInstance>>();
            foreach (var layout in skiLayouts)
            {
                DrawSkiBase(layout);
                DrawLodge(layout);
                DrawValleyStation(layout);
                DrawTopStation(layout);
                DrawLiftLine(layout);
                DrawPisteMarkers(layout);
                DrawSlopePines(layout);
            }
            Combine(skiRoot);
            foreach (var layout in skiLayouts)
                DrawPisteSnow(layout);
        }
        void SkiBox(SkiLayout l, string name, float a, float f, float y, Vector3 size, Color color) =>
            Box(name, l.At(a, f, y), size, color, skiRoot, l.facing);

        void DrawSkiBase(SkiLayout l)
        {
            float half = SkiResorts.Half + .5f;
            // Snow over the back of the base, asphalt in front with snowbanks round it and painted bays.
            // Snow from just behind the back row of bays to the foot of the slope; asphalt in front of it.
            SkiBox(l, "Resort snow", 0, (-.35f + half) / 2, .012f, new Vector3(2 * half, .024f, half + .35f), Alpine.Snow);
            SkiBox(l, "Car park", 0, (-half - .35f) / 2, .01f, new Vector3(2 * half, .022f, half - .35f), Alpine.Asphalt);
            SkiBox(l, "Snowbank", -half + .05f, -1.4f, .035f, new Vector3(.1f, .07f, 2.1f), Alpine.Snow);
            SkiBox(l, "Snowbank", half - .05f, -1.4f, .035f, new Vector3(.1f, .07f, 2.1f), Alpine.Snow);
            SkiBox(l, "Snowbank", -1.45f, -half + .05f, .035f, new Vector3(2.1f, .07f, .1f), Alpine.Snow);
            SkiBox(l, "Snowbank", 1.45f, -half + .05f, .035f, new Vector3(2.1f, .07f, .1f), Alpine.Snow);
            for (int k = 0; k <= 11; k++)
            {
                float a = -2.31f + k * .42f;
                SkiBox(l, "Bay line", a, -.8f, .024f, new Vector3(.025f, .004f, .62f), Alpine.Paint);
                if (Mathf.Abs(a) > .3f)
                    SkiBox(l, "Bay line", a, -2.02f, .024f, new Vector3(.025f, .004f, .62f), Alpine.Paint);
            }
            // The gate: posts either side of the lane, the barrier raised, and a blue parking sign.
            for (int s = -1; s <= 1; s += 2)
                SkiBox(l, "Gate post", s * .36f, -half + .08f, .12f, new Vector3(.05f, .24f, .05f), Alpine.Paint);
            SkiBox(l, "Raised barrier", -.36f, -half + .08f, .45f, new Vector3(.03f, .5f, .03f), Alpine.Red);
            SkiBox(l, "Parking sign post", .62f, -half + .1f, .2f, new Vector3(.03f, .4f, .03f), Alpine.Steel);
            SkiBox(l, "Parking sign", .62f, -half + .1f, .44f, new Vector3(.2f, .2f, .02f), Alpine.ParkBlue);
            SkiBox(l, "Parking sign letter", .62f, -half + .088f, .44f, new Vector3(.06f, .12f, .01f), Alpine.Paint);
            // Lamps over the aisle.
            for (int s = -1; s <= 1; s += 2)
            {
                SkiBox(l, "Lamp post", s * 1.9f, SkiLayout.AisleF, .3f, new Vector3(.035f, .6f, .035f), Alpine.Steel);
                SkiBox(l, "Lamp", s * 1.9f - s * .08f, SkiLayout.AisleF, .6f, new Vector3(.16f, .03f, .06f), Alpine.Window);
            }
            // Ski racks, a ticket hut and flags on the plaza.
            for (int i = 0; i < 3; i++)
            {
                SkiBox(l, "Ski rack", -.25f + i * .28f, .1f, .08f, new Vector3(.22f, .16f, .04f), Alpine.Steel);
                for (int k = 0; k < 4; k++)
                    SkiBox(l, "Skis in rack", -.33f + i * .28f + k * .05f, .12f, .12f, new Vector3(.018f, .22f, .012f), k % 2 == 0 ? Alpine.Red : Alpine.Blue);
            }
            SkiBox(l, "Ticket hut", .9f, -.05f, .2f, new Vector3(.36f, .4f, .3f), Alpine.Timber);
            SkiBox(l, "Ticket window", .9f, -.21f, .26f, new Vector3(.2f, .12f, .02f), Alpine.Window);
            Shape("Ticket hut roof", prism, l.At(.9f, -.05f, .4f), new Vector3(.36f, .14f, .38f), Alpine.Snow, skiRoot, l.facing);
            for (int i = 0; i < 3; i++)
            {
                SkiBox(l, "Flag pole", -.3f + i * .3f, -.25f, .4f, new Vector3(.02f, .8f, .02f), Alpine.Paint);
                SkiBox(l, "Flag", -.22f + i * .3f, -.25f, .7f, new Vector3(.14f, .1f, .01f), i == 0 ? Alpine.Red : i == 1 ? Alpine.Blue : Alpine.Gold);
            }
        }
        /// <summary>A two-storey chalet: stone plinth, timber walls with lit windows and a balcony, a steep roof under snow.</summary>
        void DrawLodge(SkiLayout l)
        {
            const float a = -1.4f, f = 1.35f;
            SkiBox(l, "Lodge plinth", a, f, .09f, new Vector3(1.9f, .18f, 1.8f), Alpine.Stone);
            SkiBox(l, "Lodge walls", a, f, .56f, new Vector3(1.78f, .76f, 1.68f), Alpine.Timber);
            SkiBox(l, "Lodge corner beams", a, f, .56f, new Vector3(1.82f, .76f, .06f), Alpine.DarkTimber);
            for (int row = 0; row < 2; row++)
                for (int i = 0; i < 4; i++)
                {
                    float x = a - .63f + i * .42f, y = .38f + row * .36f;
                    SkiBox(l, "Lodge window", x, f - .85f, y, new Vector3(.2f, .17f, .02f), Alpine.Window);
                    SkiBox(l, "Lodge window", a - .9f, f - .55f + i * .36f, y, new Vector3(.02f, .17f, .2f), Alpine.Window);
                }
            SkiBox(l, "Lodge door", a + .35f, f - .85f, .3f, new Vector3(.22f, .3f, .02f), Alpine.DarkTimber);
            SkiBox(l, "Lodge balcony", a, f - .95f, .56f, new Vector3(1.86f, .04f, .22f), Alpine.DarkTimber);
            SkiBox(l, "Balcony rail", a, f - 1.05f, .65f, new Vector3(1.86f, .08f, .02f), Alpine.DarkTimber);
            // Ridge up the mountain, so the gable with its balcony faces the valley.
            var ridge = Quaternion.LookRotation(l.right);
            Shape("Lodge roof", prism, l.At(a, f, .94f), new Vector3(2.08f, .72f, 2.14f), Alpine.DarkTimber, skiRoot, ridge);
            Shape("Snow on the lodge roof", prism, l.At(a, f, .99f), new Vector3(1.98f, .7f, 2.02f), Alpine.Snow, skiRoot, ridge);
            SkiBox(l, "Lodge chimney", a + .45f, f + .35f, 1.5f, new Vector3(.14f, .5f, .14f), Alpine.Stone);
            // The sun terrace in front, with parasols.
            SkiBox(l, "Terrace", a, f - 1.35f, .03f, new Vector3(1.7f, .06f, .5f), Alpine.Timber);
            for (int i = 0; i < 3; i++)
            {
                var at = l.At(a - .55f + i * .55f, f - 1.35f, 0);
                Box("Parasol pole", at + Vector3.up * .16f, new Vector3(.015f, .32f, .015f), Alpine.Paint, skiRoot);
                Shape("Parasol", cone, at + Vector3.up * .3f, new Vector3(.2f, .09f, .2f), i == 1 ? Alpine.Gold : Alpine.Canvas, skiRoot);
                Box("Terrace table", at + Vector3.up * .12f, new Vector3(.14f, .03f, .14f), Alpine.Paint, skiRoot);
            }
        }
        /// <summary>The gondola's valley station: concrete base, glass hall, a red canopy and the bullwheel under it.</summary>
        void DrawValleyStation(SkiLayout l)
        {
            const float a = 1.45f, f = 1.45f;
            SkiBox(l, "Valley station base", a, f, .15f, new Vector3(1.75f, .3f, 1.9f), Alpine.Concrete);
            SkiBox(l, "Valley station hall", a, f, .6f, new Vector3(1.5f, .6f, 1.7f), Alpine.Glass);
            SkiBox(l, "Hall frame", a, f - .86f, .6f, new Vector3(1.52f, .6f, .03f), Alpine.Steel);
            for (int i = 0; i < 3; i++)
                SkiBox(l, "Hall door", a - .4f + i * .4f, f - .88f, .45f, new Vector3(.24f, .3f, .02f), Alpine.Window);
            SkiBox(l, "Valley station canopy", a, f + .1f, 1.08f, new Vector3(1.95f, .1f, 2.2f), Alpine.LiftRed);
            SkiBox(l, "Canopy snow", a, f + .1f, 1.14f, new Vector3(1.85f, .03f, 2.1f), Alpine.Snow);
            Shape("Bullwheel", cone, l.valleyWheel - Vector3.up * .02f, new Vector3(.34f, .02f, .34f), Alpine.Steel, skiRoot);
            SkiBox(l, "Bullwheel mast", a, f, .8f, new Vector3(.1f, .3f, .1f), Alpine.Steel);
            // The waiting line's rails in front of the doors.
            for (int i = 0; i < 3; i++)
                SkiBox(l, "Queue rail", a - .3f + i * .3f, f - 1.2f, .08f, new Vector3(.02f, .08f, .5f), Alpine.Steel);
        }
        /// <summary>The top station on a tall plinth set into the slope, with a sun deck facing the valley.</summary>
        void DrawTopStation(SkiLayout l)
        {
            var along = l.LineDirection;
            var turn = Quaternion.LookRotation(along);
            var at = new Vector3(l.topWheel.x, l.topFloor, l.topWheel.z) + along * SkiLayout.TopHall;
            // The plinth reaches from the floor down to the lowest ground under the hall, so the downhill side never floats.
            float plinth = l.topFloor - l.topFoot + .1f;
            Box("Top station plinth", at - Vector3.up * (plinth / 2), new Vector3(1.25f, plinth, 1.35f), Alpine.Concrete, skiRoot, turn);
            Box("Top station hall", at + Vector3.up * .3f, new Vector3(1.15f, .6f, 1.2f), Alpine.Glass, skiRoot, turn);
            Box("Top station roof", at + Vector3.up * .64f, new Vector3(1.35f, .08f, 1.45f), Alpine.LiftRed, skiRoot, turn);
            Box("Top roof snow", at + Vector3.up * .69f, new Vector3(1.25f, .03f, 1.35f), Alpine.Snow, skiRoot, turn);
            Shape("Top bullwheel", cone, l.topWheel - Vector3.up * .02f, new Vector3(.34f, .02f, .34f), Alpine.Steel, skiRoot);
            var deck = at - along * .85f;
            Box("Sun deck", new Vector3(deck.x, l.topFloor - .02f, deck.z), new Vector3(1f, .06f, .45f), Alpine.Timber, skiRoot, turn);
            Box("Deck rail", new Vector3(deck.x, l.topFloor + .06f, deck.z) - along * .22f, new Vector3(1f, .1f, .02f), Alpine.DarkTimber, skiRoot, turn);
        }
        /// <summary>Tapered steel towers with a cross arm and sheaves, and the two cables sagging a little between supports.</summary>
        void DrawLiftLine(SkiLayout l)
        {
            var along = l.LineDirection;
            var turn = Quaternion.LookRotation(along);
            foreach (var saddle in l.towers)
            {
                float ground = SkiLayout.Ground(saddle.x, saddle.z), height = saddle.y - ground;
                var foot = new Vector3(saddle.x, ground, saddle.z);
                Box("Tower footing", foot + Vector3.up * .03f, new Vector3(.26f, .1f, .26f), Alpine.Concrete, skiRoot, turn);
                Box("Tower", foot + Vector3.up * (height / 2), new Vector3(.1f, height, .12f), Alpine.Steel, skiRoot, turn);
                Box("Tower cross arm", saddle + Vector3.up * .04f, new Vector3(.4f, .05f, .08f), Alpine.Steel, skiRoot, turn);
                for (int s = -1; s <= 1; s += 2)
                    Box("Sheave train", saddle + Vector3.Cross(Vector3.up, along) * (s * .13f), new Vector3(.05f, .05f, .24f), Alpine.Cable, skiRoot, turn);
            }
            foreach (var cable in new[] { l.upCable, l.downCable })
                for (int i = 0; i + 1 < cable.Count; i++)
                {
                    var a = cable[i];
                    var b = cable[i + 1];
                    float span = new Vector2(b.x - a.x, b.z - a.z).magnitude;
                    const int pieces = 4;
                    for (int k = 0; k < pieces; k++)
                    {
                        float s0 = k / (float)pieces, s1 = (k + 1) / (float)pieces;
                        var p0 = Vector3.Lerp(a, b, s0) - Vector3.up * SkiLayout.Sag(s0, span);
                        var p1 = Vector3.Lerp(a, b, s1) - Vector3.up * SkiLayout.Sag(s1, span);
                        var d = p1 - p0;
                        Box("Cable", (p0 + p1) / 2, new Vector3(.018f, .018f, d.magnitude + .01f), Alpine.Cable, skiRoot, Quaternion.LookRotation(d));
                    }
                }
        }
        /// <summary>Marker poles down both edges of every piste, coloured by grade.</summary>
        void DrawPisteMarkers(SkiLayout l)
        {
            for (int i = 0; i < l.pistes.Count; i++)
            {
                var color = l.pistes[i].grade == 0 ? Alpine.Blue : l.pistes[i].grade == 1 ? Alpine.Red : Alpine.Black;
                for (float t = .06f; t < .9f; t += .09f)
                    for (int side = -1; side <= 1; side += 2)
                    {
                        var p = l.OnPiste(i, t, side * 1.1f, out _);
                        Box("Piste marker", p + Vector3.up * .08f, new Vector3(.025f, .16f, .025f), color, skiRoot);
                    }
            }
        }
        /// <summary>A scatter of snowy pines on the slope either side of the lift, clear of the pistes and the line.</summary>
        void DrawSlopePines(SkiLayout l)
        {
            var random = new System.Random(l.resort.id * 7919);
            var along = l.LineDirection;
            var across = Vector3.Cross(Vector3.up, along);
            var from = new Vector3(l.valleyWheel.x, 0, l.valleyWheel.z);
            float length = Vector3.Distance(from, new Vector3(l.topWheel.x, 0, l.topWheel.z));
            int planted = 0;
            for (int attempt = 0; attempt < 120 && planted < 16; attempt++)
            {
                float t = .12f + .6f * (float)random.NextDouble(), side = (random.Next(2) * 2 - 1) * (.7f + 2.6f * (float)random.NextDouble());
                var p = from + along * (length * t) + across * side;
                if (!MapDefinition.Raised(new Cell(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.z))) || NearPiste(l, p))
                    continue;
                float h = .45f + .3f * (float)random.NextDouble();
                var foot = SkiLayout.OnGround(p);
                Box("Slope pine trunk", foot + Vector3.up * .08f, new Vector3(.06f, .16f, .06f), Alpine.DarkTimber, skiRoot);
                Shape("Slope pine", cone, foot + Vector3.up * .12f, new Vector3(.26f, h, .26f), Alpine.Pine, skiRoot);
                Shape("Snow on the pine", cone, foot + Vector3.up * (.12f + h * .55f), new Vector3(.13f, h * .46f, .13f), Alpine.Snow, skiRoot);
                planted++;
            }
        }
        static bool NearPiste(SkiLayout l, Vector3 p)
        {
            foreach (var piste in l.pistes)
                foreach (var q in piste.line)
                    if (new Vector2(q.x - p.x, q.z - p.z).sqrMagnitude < Mathf.Pow(piste.width * .5f + .35f, 2))
                        return true;
            return false;
        }
        /// <summary>Each piste is a ribbon of groomed snow draped a little above the slope, four points across.</summary>
        void DrawPisteSnow(SkiLayout l)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (int i = 0; i < l.pistes.Count; i++)
            {
                var line = l.pistes[i].line;
                int first = vertices.Count;
                for (int k = 0; k < line.Count; k++)
                    for (int j = 0; j < 4; j++)
                        vertices.Add(l.OnPiste(i, k / (float)(line.Count - 1), -1 + j * 2 / 3f, out _) + Vector3.up * .035f);
                for (int k = 0; k + 1 < line.Count; k++)
                    for (int j = 0; j < 3; j++)
                    {
                        int a = first + k * 4 + j, b = a + 1, c = a + 4, d = a + 5;
                        triangles.AddRange(new[] { a, c, d, a, d, b });
                    }
            }
            var mesh = new Mesh { name = "Pistes", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            ownedMeshes.Add(mesh);
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            var go = new GameObject("Pistes " + l.resort.name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(skiRoot, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = Mat(Alpine.Groomed);
        }
    }
}
