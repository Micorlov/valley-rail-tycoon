using System.Collections.Generic;
using UnityEngine;
using ValleyRail.Core;
namespace ValleyRail
{
    /// <summary>
    /// The trams themselves, and the people around the stops. A tram is three low-floor sections on the line, red with a cream
    /// window band, a cab at each end and a pantograph up to the wire on the middle one; each section follows the chord of the
    /// track like a train car. Riders wait on the platforms, and passengers who change walk between the train platform and the
    /// tram stop along the covered walkway. Cars in the streets treat the cells a tram covers, or is about to enter, as taken
    /// (CityTraffic.LightRailBlocks). Cosmetic only: nothing here changes the simulation.
    /// </summary>
    public sealed partial class WorldView
    {
        const int LightRailSections = 3, LightRailStopFigures = 8, LightRailMaxWalkers = 24;
        const float LightRailSectionLength = .38f, LightRailSectionPitch = 400, LightRailHalfWidth = .15f, LightRailWalkSeconds = 2.4f;
        static readonly Color LightRailRed = new Color(.8f, .15f, .13f), LightRailCream = new Color(.95f, .92f, .82f), LightRailGlass = new Color(.16f, .24f, .32f),
            LightRailSkirt = new Color(.2f, .21f, .23f), LightRailDisplay = new Color(1f, .7f, .2f), LightRailLamp = new Color(1f, .96f, .8f);
        sealed class LightRailTramView
        {
            public Transform[] sections;
            public int units, position = -1;
        }
        sealed class LightRailCrowd
        {
            public Transform[] people;
            public int shown = -1;
        }
        sealed class LightRailWalker
        {
            public Transform view;
            public Vector3 from, to;
            public float time;
        }
        Transform lightRailCarRoot;
        readonly Dictionary<int, LightRailTramView> lightRailViews = new Dictionary<int, LightRailTramView>();
        readonly Dictionary<string, LightRailCrowd> lightRailCrowds = new Dictionary<string, LightRailCrowd>();
        readonly Dictionary<int, int> lightRailLastWaiting = new Dictionary<int, int>();
        readonly List<LightRailWalker> lightRailWalkers = new List<LightRailWalker>();
        readonly HashSet<int> lightRailCells = new HashSet<int>();
        readonly List<int> lightRailRemoved = new List<int>();
        int lightRailShirt;

        /// <summary>True while a tram covers the cell or is about to enter it; street cars wait for it.</summary>
        public bool LightRailBlocks(Cell c) => lightRailCells.Contains(c.Key);

        /// <summary>The line of the tram nearest the cell, within a cell, or null.</summary>
        public TramLineState LightRailTramNear(Cell c)
        {
            foreach (var line in game.World.tramLines)
            {
                var route = game.Trams.Route(line);
                foreach (var t in line.trams)
                {
                    if (t.position < 0)
                        continue;
                    var p = LightRailPoint(route, t.position - 500, out _);
                    if (new Cell(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.z)).Distance(c) <= 1)
                        return line;
                }
            }
            return null;
        }

        void RefreshLightRailCars(bool redrawn)
        {
            if (!lightRailCarRoot)
            {
                lightRailCarRoot = Root("Light rail trams");
                traffic.LightRailBlocks = LightRailBlocks;
            }
            lightRailRemoved.Clear();
            foreach (var id in lightRailViews.Keys)
                if (!LightRailTramExists(id))
                    lightRailRemoved.Add(id);
            foreach (int id in lightRailRemoved)
            {
                foreach (var s in lightRailViews[id].sections)
                    Destroy(s.gameObject);
                lightRailViews.Remove(id);
            }
            foreach (var line in game.World.tramLines)
                foreach (var t in line.trams)
                    if (!lightRailViews.ContainsKey(t.id))
                        lightRailViews[t.id] = CreateLightRailTram();
            if (!redrawn)
                return;
            // Stop crowds follow the stops: rebuilt with the track.
            foreach (var crowd in lightRailCrowds.Values)
                foreach (var person in crowd.people)
                    Destroy(person.gameObject);
            lightRailCrowds.Clear();
            foreach (var line in game.World.tramLines)
            {
                var route = game.Trams.Route(line);
                lightRailCrowds[line.id + " out"] = CreateLightRailCrowd(line, route, 0);
                lightRailCrowds[line.id + " back"] = CreateLightRailCrowd(line, route, route.cells.Count - 1);
            }
        }
        bool LightRailTramExists(int id)
        {
            foreach (var line in game.World.tramLines)
                foreach (var t in line.trams)
                    if (t.id == id)
                        return true;
            return false;
        }
        LightRailTramView CreateLightRailTram()
        {
            var view = new LightRailTramView { sections = new Transform[LightRailSections] };
            for (int i = 0; i < LightRailSections; i++)
            {
                var section = new GameObject(i == 1 ? "Tram middle" : "Tram cab", typeof(MeshFilter), typeof(MeshRenderer)).transform;
                section.SetParent(lightRailCarRoot, false);
                var art = LightRailSectionArt(i == 1 ? 1 : 0, i == LightRailSections - 1);
                section.GetComponent<MeshFilter>().sharedMesh = art.mesh;
                section.GetComponent<MeshRenderer>().sharedMaterials = art.materials;
                section.gameObject.SetActive(false);
                view.sections[i] = section;
            }
            return view;
        }
        /// <summary>A tram section's mesh: kind 0 a cab (reversed for the rear one), kind 1 the middle with the pantograph.</summary>
        (Mesh mesh, Material[] materials) LightRailSectionArt(int kind, bool reversed)
        {
            string key = kind == 1 ? "tram middle" : reversed ? "tram rear" : "tram front";
            if (carArt.TryGetValue(key, out var art))
                return art;
            var b = new TrainMeshBuilder(PaletteTexel);
            float half = LightRailSectionLength / 2, w = LightRailHalfWidth * 2;
            b.Box(new Vector3(0, .04f, 0), new Vector3(w - .02f, .05f, LightRailSectionLength - .02f), new Paint(LightRailSkirt, Finish.Matte));
            b.Box(new Vector3(0, .1f, 0), new Vector3(w, .08f, LightRailSectionLength), LightRailRed);
            b.Box(new Vector3(0, .144f, 0), new Vector3(w + .004f, .012f, LightRailSectionLength), LightRailCream);
            b.Box(new Vector3(0, .185f, 0), new Vector3(w + .002f, .07f, LightRailSectionLength - .04f), new Paint(LightRailGlass, Finish.Glass));
            b.Box(new Vector3(0, .185f, 0), new Vector3(w, .07f, LightRailSectionLength), LightRailCream);
            b.Box(new Vector3(0, .232f, 0), new Vector3(w - .01f, .026f, LightRailSectionLength), LightRailCream);
            if (kind == 1)
            {
                TrainParts.Pantograph(b, 0, .245f, true);
                // Bellows to the cabs either side.
                foreach (float z in new[] { -half - .012f, half + .012f })
                    b.Box(new Vector3(0, .15f, z), new Vector3(w - .06f, .19f, .03f), new Paint(LightRailSkirt, Finish.Matte));
            }
            else
            {
                b.Box(new Vector3(0, .25f, -.05f), new Vector3(.14f, .03f, .16f), new Paint(LightRailSkirt, Finish.Matte));
                // The cab front: a raked windscreen, the destination display, headlights and a bumper.
                b.Box(new Vector3(0, .18f, half + .012f), new Vector3(w - .04f, .09f, .024f), new Paint(LightRailGlass, Finish.Glass));
                b.Box(new Vector3(0, .222f, half + .014f), new Vector3(.14f, .02f, .01f), new Paint(LightRailDisplay, Finish.Glass));
                b.Box(new Vector3(0, .09f, half + .01f), new Vector3(w - .02f, .1f, .02f), LightRailRed);
                foreach (float x in new[] { -.085f, .085f })
                    b.Box(new Vector3(x, .08f, half + .022f), new Vector3(.03f, .022f, .006f), new Paint(LightRailLamp, Finish.Glass));
                b.Box(new Vector3(0, .04f, half + .02f), new Vector3(w - .03f, .025f, .02f), new Paint(LightRailSkirt, Finish.Metal));
            }
            var mesh = b.Build("Tram " + key, reversed, out var finishes);
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
        LightRailCrowd CreateLightRailCrowd(TramLineState line, TramRoute route, int i)
        {
            int n = route.cells.Count;
            var stop = route.cells[i];
            int toLine = i == 0 ? route.exit[0] : Directions.Opp(route.entry[n - 1]);
            int side = LightRailPlatformSide(line, route, i);
            var along = new Vector3(Directions.Dx[toLine], 0, Directions.Dz[toLine]);
            var across = new Vector3(Directions.Dx[side], 0, Directions.Dz[side]);
            var platform = new Vector3(stop.x, LightRailDeck(stop) + .09f, stop.z) - along * .15f + along * .55f + across * LightRailPlatformOffset;
            var crowd = new LightRailCrowd { people = new Transform[LightRailStopFigures] };
            for (int k = 0; k < LightRailStopFigures; k++)
            {
                var person = CityLife.Figure(this, lightRailCarRoot, "Tram rider", CityLife.Shirts[(line.id + k * 5 + i) % CityLife.Shirts.Length], StationCrowds.FigureScale);
                float z = -.4f + .8f * (k / 2) / (LightRailStopFigures / 2 - 1);
                person.localPosition = platform + along * z + across * (k % 2 == 0 ? -.03f : .05f);
                person.localRotation = Quaternion.LookRotation(-across);
                person.gameObject.SetActive(false);
                crowd.people[k] = person;
            }
            return crowd;
        }
        Vector3 LightRailStopSpot(TramLineState line, int i)
        {
            var route = game.Trams.Route(line);
            int n = route.cells.Count;
            var stop = route.cells[i];
            int toLine = i == 0 ? route.exit[0] : Directions.Opp(route.entry[n - 1]);
            int side = LightRailPlatformSide(line, route, i);
            return new Vector3(stop.x, LightRailDeck(stop) + .09f, stop.z) + new Vector3(Directions.Dx[toLine], 0, Directions.Dz[toLine]) * .4f +
                new Vector3(Directions.Dx[side], 0, Directions.Dz[side]) * LightRailPlatformOffset;
        }

        void AnimateLightRail(float dt, bool paused)
        {
            if (!lightRailCarRoot)
                return;
            lightRailCells.Clear();
            bool near = ZoomedIn;
            foreach (var line in game.World.tramLines)
            {
                var route = game.Trams.Route(line);
                foreach (var t in line.trams)
                {
                    if (!lightRailViews.TryGetValue(t.id, out var view))
                        continue;
                    bool shown = t.position >= 0;
                    for (int k = 0; k < LightRailSections; k++)
                    {
                        var section = view.sections[k];
                        if (section.gameObject.activeSelf != shown)
                            section.gameObject.SetActive(shown);
                        if (!shown)
                            continue;
                        int middle = t.position - (int)(LightRailSectionLength * 500) - k * (int)LightRailSectionPitch;
                        var front = LightRailPoint(route, middle + 150, out _);
                        var back = LightRailPoint(route, middle - 150, out _);
                        var pose = (front + back) * .5f;
                        var forward = front - back;
                        bool jump = view.position < 0 || (section.position - pose).sqrMagnitude > 4;
                        section.position = paused || jump ? pose : Vector3.Lerp(section.position, pose, 1 - Mathf.Exp(-dt * 30));
                        if (forward.sqrMagnitude > 1e-6f)
                            section.rotation = Quaternion.LookRotation(forward);
                    }
                    if (shown)
                    {
                        foreach (int ahead in new[] { 700, 0, -550, -1100 })
                            lightRailCells.Add(route.cells[route.Locate(t.position + ahead, out _, out _)].Key);
                        // Riders let off at the station walk to the trains.
                        if (t.position == 0 && view.units > t.units && view.position == 0)
                            SendLightRailWalkers(line, (view.units - t.units + 2) / 3, false);
                    }
                    view.units = t.units;
                    view.position = t.position;
                }
                if (lightRailLastWaiting.TryGetValue(line.id, out int before) && line.waitingOut - before >= 2)
                    SendLightRailWalkers(line, (line.waitingOut - before + 1) / 2, true);
                lightRailLastWaiting[line.id] = line.waitingOut;
                if (lightRailSigns.TryGetValue(line.id, out var signs))
                {
                    ShowLightRailQueue(signs.outbound, line.waitingOut);
                    ShowLightRailQueue(signs.back, line.waitingBack);
                }
                ShowLightRailCrowd(line.id + " out", line.waitingOut, near);
                ShowLightRailCrowd(line.id + " back", line.waitingBack, near);
            }
            AnimateLightRailWalkers(paused ? 0 : dt * Mathf.Max(1, game.World.speed), near);
        }
        void ShowLightRailCrowd(string key, int waiting, bool near)
        {
            if (!lightRailCrowds.TryGetValue(key, out var crowd))
                return;
            int want = !near || waiting <= 0 ? 0 : Mathf.Min(LightRailStopFigures, 1 + waiting / 6);
            if (want == crowd.shown)
                return;
            crowd.shown = want;
            for (int k = 0; k < crowd.people.Length; k++)
                crowd.people[k].gameObject.SetActive(k < want);
        }
        /// <summary>Passengers changing between train and tram walk the covered way: to the tram stop, or back to the trains.</summary>
        void SendLightRailWalkers(TramLineState line, int count, bool toTram)
        {
            var s = game.Trains.Station(line.stationId);
            if (s == null || !ZoomedIn)
                return;
            var trains = new Vector3(s.cell.x, .3f, s.cell.z) + new Vector3(Directions.Dx[s.side], 0, Directions.Dz[s.side]) * .35f;
            var tram = LightRailStopSpot(line, 0);
            for (int k = 0; k < Mathf.Min(count, 6) && lightRailWalkers.Count < LightRailMaxWalkers; k++)
            {
                var view = CityLife.Figure(this, lightRailCarRoot, "Changing passenger", CityLife.Shirts[lightRailShirt++ % CityLife.Shirts.Length], StationCrowds.FigureScale);
                var jitter = new Vector3((k % 3 - 1) * .08f, 0, (k / 3 - .5f) * .08f);
                lightRailWalkers.Add(new LightRailWalker { view = view, from = (toTram ? trains : tram) + jitter, to = (toTram ? tram : trains) + jitter, time = -k * .35f });
                view.gameObject.SetActive(false);
            }
        }
        void AnimateLightRailWalkers(float dt, bool near)
        {
            for (int i = lightRailWalkers.Count - 1; i >= 0; i--)
            {
                var walker = lightRailWalkers[i];
                walker.time += dt;
                if (walker.time >= LightRailWalkSeconds || !near)
                {
                    Destroy(walker.view.gameObject);
                    lightRailWalkers.RemoveAt(i);
                    continue;
                }
                bool walking = walker.time >= 0;
                if (walker.view.gameObject.activeSelf != walking)
                    walker.view.gameObject.SetActive(walking);
                if (!walking)
                    continue;
                float f = walker.time / LightRailWalkSeconds;
                var p = Vector3.Lerp(walker.from, walker.to, f);
                // A little bob as they walk.
                walker.view.localPosition = p + Vector3.up * Mathf.Abs(Mathf.Sin(walker.time * 9)) * .012f;
                var heading = walker.to - walker.from;
                heading.y = 0;
                if (heading.sqrMagnitude > 1e-4f)
                    walker.view.localRotation = Quaternion.LookRotation(heading);
            }
        }
    }
}
