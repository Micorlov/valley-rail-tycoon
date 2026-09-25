using System;
using System.Collections.Generic;
using UnityEngine;
using ValleyRail.Core;
namespace ValleyRail
{
    /// <summary>
    /// The station upgrade show. The simulation has already rebuilt the station and demolished what stood in its way; this
    /// replays it: ghosts of the lost buildings and pines stand until a bulldozer drives in and knocks each one down in a
    /// cloud of dust, then the new station rises on its foundation — platforms, columns, a scaffolded hall under a turning
    /// crane and finally the canopy. The finished station is hidden until the show ends. It plays at game speed (at least 1x,
    /// also while paused) and is purely cosmetic: loading a game builds a fresh world without it.
    /// </summary>
    public sealed partial class WorldView
    {
        const int DustPuffs = 9;
        const float DozerSpeed = 2.6f, PushSeconds = .55f, CollapseSeconds = .5f, DustSeconds = .9f, BuildSeconds = 6.5f, DozerExit = 4f;
        sealed class Wreck
        {
            public Transform ghost; public Vector3 at; public bool tree; public float hitTime = -1;
            public readonly List<Transform> dust = new List<Transform>(), debris = new List<Transform>();
        }
        /// <summary>A timed point on a vehicle's drive. <c>hit</c>: the wreck the blade reaches here, or -1; <c>reverse</c>: the leg ending here is driven backwards.</summary>
        struct Waypoint
        {
            public Vector3 at; public float time; public int hit; public bool reverse;
        }
        /// <summary>A construction piece that grows from nothing between two points of build progress, upwards or along the platforms.</summary>
        sealed class Rise
        {
            public Transform piece; public Vector3 at, full, along; public float from, to; public bool lengthwise;
        }
        sealed class UpgradeShow
        {
            public int stationId; public Transform root, site, scaffold, jib; public Dozer dozer; public float time, buildStart, end;
            public readonly List<Wreck> wrecks = new List<Wreck>();
            public readonly List<Waypoint> route = new List<Waypoint>();
            public readonly List<Rise> rises = new List<Rise>();
            public Action<Vector3> onHit; public Action onDone;
            public Crew crew;
        }
        Transform upgradeRoot;
        readonly HashSet<int> upgradingStations = new HashSet<int>();
        readonly List<UpgradeShow> upgradeShows = new List<UpgradeShow>(), finishedShows = new List<UpgradeShow>();
        public int StationUpgradeShows => upgradeShows.Count;
        public bool StationUpgrading(int stationId) => upgradingStations.Contains(stationId);
        /// <summary>Call before applying an upgrade, so the redraw it causes does not show the finished station first.</summary>
        public void BeginStationUpgrade(int stationId) => upgradingStations.Add(stationId);
        /// <summary>The upgrade was refused: draw the station as it is.</summary>
        public void CancelStationUpgrade(int stationId)
        {
            if (!upgradingStations.Remove(stationId))
                return;
            revision = -1;
            Refresh();
        }
        public void PlayStationUpgrade(UpgradeRecord record, Action<Vector3> onHit = null, Action onDone = null)
        {
            var s = game.Trains.Station(record.stationId);
            if (s == null)
            {
                CancelStationUpgrade(record.stationId);
                return;
            }
            upgradingStations.Add(s.id);
            if (!upgradeRoot)
                upgradeRoot = Root("Station upgrades");
            var show = new UpgradeShow { stationId = s.id, onHit = onHit, onDone = onDone };
            show.root = new GameObject("Upgrade of " + s.name).transform;
            show.root.SetParent(upgradeRoot, false);
            foreach (var bs in record.buildings)
                show.wrecks.Add(GhostBuilding(bs, show.root));
            foreach (var c in record.trees)
                show.wrecks.Add(GhostTree(c, show.root));
            PlanDozerRoute(show, s);
            show.dozer = BuildBulldozer(show.root);
            BuildSite(show, s);
            show.end = show.buildStart + BuildSeconds;
            HireCrew(show, s);
            AnimateShow(show, 0);
            upgradeShows.Add(show);
        }
        Transform doomedRoot;
        /// <summary>Where a town building stands, at the height of its roof: the spot for a demolition pin.</summary>
        public Vector3 BuildingSpot(BuildingState bs) => LotFor(bs).at + Vector3.up * Mathf.Min(BuildingCatalog.Get(bs.def).height / 100f, 3.5f);
        /// <summary>Red pins over everything an upgrade would clear, each above its roof or treetop; null or empty removes them.</summary>
        public void MarkDoomed(List<Vector3> spots)
        {
            if (doomedRoot)
                Destroy(doomedRoot.gameObject);
            doomedRoot = null;
            if (spots == null || spots.Count == 0)
                return;
            doomedRoot = Root("Demolition pins");
            foreach (var top in spots)
            {
                Shape("Demolition pin", top + Vector3.up * 1.05f, new Vector3(.3f, .55f, .3f), Red, doomedRoot, Quaternion.Euler(180, 0, 0));
                Box("Pin head", top + Vector3.up * 1.1f, new Vector3(.34f, .08f, .34f), Red, doomedRoot);
            }
        }
        /// <summary>A lost building redrawn with the town's own art, as live boxes that can shake and collapse.</summary>
        Wreck GhostBuilding(BuildingState bs, Transform parent)
        {
            var def = BuildingCatalog.Get(bs.def);
            var lot = LotFor(bs);
            var ghost = new GameObject("Doomed " + def.name).transform;
            ghost.SetParent(parent, false);
            // DrawBuilding draws through cityRoot; pointing it here for one call keeps the ghost out of the batched town mesh.
            var town = cityRoot;
            int emitted = emitters.Count;
            cityRoot = ghost;
            try
            {
                DrawBuilding(def, lot);
            }
            finally
            {
                cityRoot = town;
                emitters.RemoveRange(emitted, emitters.Count - emitted);
            }
            return new Wreck { ghost = ghost, at = lot.at };
        }
        Wreck GhostTree(Cell c, Transform parent)
        {
            int index = game.Scenery.TreeIndex(c);
            float height = index >= 0 ? game.Scenery.Trees[index].height : 1.2f;
            int shade = index >= 0 ? game.Scenery.Trees[index].shade : 1;
            var ghost = new GameObject("Doomed pine").transform;
            ghost.SetParent(parent, false);
            ghost.localPosition = new Vector3(c.x, 0, c.z);
            Box("Trunk", new Vector3(0, TreeSpot.Crown, 0), new Vector3(.13f, .7f, .13f), new Color(.31f, .23f, .14f), ghost);
            Shape("Foliage", new Vector3(0, TreeSpot.Crown, 0), new Vector3(TreeSpot.Radius, height, TreeSpot.Radius), new Color(.12f, .29f + shade * .04f, .18f), ghost);
            return new Wreck { ghost = ghost, at = ghost.localPosition, tree = true };
        }
        /// <summary>The bulldozer's drive: in along the platforms, up to each wreck in turn and a short push, then away. On bare ground it grades the strip.</summary>
        void PlanDozerRoute(UpgradeShow show, StationState s)
        {
            var along = s.axis == 1 ? Vector3.right : Vector3.forward;
            var across = new Vector3(Directions.Dx[s.side], 0, Directions.Dz[s.side]);
            int length = StationLayout.Length(s);
            var middle = new Vector3(s.cell.x, 0, s.cell.z) + along * StationLayout.Middle(length);
            show.wrecks.Sort((a, b) => Vector3.Dot(a.at - middle, along).CompareTo(Vector3.Dot(b.at - middle, along)));
            var pos = show.wrecks.Count > 0 ? show.wrecks[0].at - along * 3f : middle + across - along * (length * .5f + 2.5f);
            float time = 0, lastHit = 0;
            show.route.Add(new Waypoint { at = pos, time = 0, hit = -1 });
            void DriveTo(Vector3 to, int hit, float minimum)
            {
                time += Mathf.Max(minimum, Vector3.Distance(pos, to) / DozerSpeed);
                show.route.Add(new Waypoint { at = to, time = time, hit = hit });
                pos = to;
            }
            for (int i = 0; i < show.wrecks.Count; i++)
            {
                var target = show.wrecks[i].at;
                var heading = target - pos;
                heading.y = 0;
                heading = heading.sqrMagnitude > .01f ? heading.normalized : along;
                DriveTo(target - heading * .55f, i, .3f);
                lastHit = time;
                DriveTo(target - heading * .1f, -1, PushSeconds);
            }
            if (show.wrecks.Count == 0)
            {
                DriveTo(middle + across + along * (length * .5f + .5f), -1, 1.5f);
                lastHit = time - 1f;
            }
            show.buildStart = lastHit + CollapseSeconds + .3f;
            DriveTo(pos + along * DozerExit, -1, 1f);
        }
        /// <summary>The new station in pieces that grow in order, mirroring DrawStation and StationHall.</summary>
        void BuildSite(UpgradeShow show, StationState s)
        {
            int length = StationLayout.Length(s), platforms = StationLayout.Platforms(s);
            float middle = StationLayout.Middle(length);
            var along = s.axis == 1 ? Vector3.right : Vector3.forward;
            var across = new Vector3(Directions.Dx[s.side], 0, Directions.Dz[s.side]);
            var origin = new Vector3(s.cell.x, .15f, s.cell.z) + along * middle;
            Vector3 Span(float width) => along * length + (Vector3.one - along - Vector3.up) * width;
            show.site = new GameObject("Station site").transform;
            show.site.SetParent(show.root, false);
            var p = origin + across;
            AddRise(show, "Foundation", new Vector3(p.x, .02f, p.z), Span(.94f) + Vector3.up * .04f, Stone, along, 0, .12f, true);
            AddRise(show, "Platform", p, Span(.8f) + Vector3.up * .3f, Cream, along, .1f, .3f, false);
            for (int k = 1; k < platforms; k++)
                AddRise(show, "Island platform", origin - across * (k - .5f), Span(.34f) + Vector3.up * .3f, Cream, along, .15f + .05f * k, .32f + .05f * k, false);
            int column = 0;
            for (int i = StationLayout.First(length); i <= StationLayout.Last(length); i++, column++)
                AddRise(show, "Canopy column", p + along * (i - middle) + Vector3.up * .5f, new Vector3(.08f, 1, .08f), Cream, along, .35f + .03f * column, .48f + .03f * column, false);
            var hall = StationHall(s);
            if (hall.size.y > 0)
            {
                AddRise(show, "Rising hall", hall.center, hall.size, hall.color, along, .45f, .82f, false);
                show.scaffold = new GameObject("Scaffolding").transform;
                show.scaffold.SetParent(show.site, false);
                var half = new Vector3(hall.size.x, 0, hall.size.z) * .5f + new Vector3(.04f, 0, .04f);
                float top = hall.center.y + hall.size.y * .5f + .12f, bottom = hall.center.y - hall.size.y * .5f;
                for (int i = 0; i < 4; i++)
                    Box("Scaffold pole", new Vector3(hall.center.x + (i % 2 == 0 ? -half.x : half.x), (top + bottom) / 2, hall.center.z + (i < 2 ? -half.z : half.z)), new Vector3(.03f, top - bottom, .03f), Timber, show.scaffold);
                // Walkway boards along both long faces of the hall.
                var sideways = Vector3.one - along - Vector3.up;
                float reach = Vector3.Dot(half, sideways), run = Vector3.Dot(half, along) * 2 + .06f;
                for (int level = 1; level <= 2; level++)
                    for (int face = -1; face <= 1; face += 2)
                        Box("Scaffold board", new Vector3(hall.center.x, bottom + (top - bottom) * level / 3f, hall.center.z) + sideways * face * reach, along * run + sideways * .06f + Vector3.up * .025f, Gold, show.scaffold);
                var mast = hall.center + along * (Vector3.Dot(hall.size, along) * .5f + .3f) + across * .25f;
                float height = hall.size.y + 1.4f;
                Box("Crane mast", new Vector3(mast.x, bottom + height / 2, mast.z), new Vector3(.08f, height, .08f), new Color(.98f, .78f, .15f), show.scaffold);
                show.jib = new GameObject("Crane jib").transform;
                show.jib.SetParent(show.scaffold, false);
                show.jib.localPosition = new Vector3(mast.x, bottom + height, mast.z);
                Box("Jib", new Vector3(0, 0, .6f), new Vector3(.06f, .06f, 1.6f), new Color(.98f, .78f, .15f), show.jib);
                Box("Counterweight", new Vector3(0, -.05f, -.3f), new Vector3(.16f, .12f, .16f), Slate, show.jib);
                Box("Hook line", new Vector3(0, -.3f, 1.2f), new Vector3(.012f, .6f, .012f), Slate, show.jib);
            }
            AddRise(show, "Station canopy", p + Vector3.up * .95f, Span(.9f) + Vector3.up * .14f, Navy, along, .8f, .95f, true);
            for (int k = 1; k < platforms; k++)
                AddRise(show, "Island canopy", origin - across * (k - .5f) + Vector3.up * .95f, Span(.4f) + Vector3.up * .1f, Navy, along, .82f + .03f * k, .96f, true);
            show.site.gameObject.SetActive(false);
        }
        void AddRise(UpgradeShow show, string name, Vector3 at, Vector3 full, Color color, Vector3 along, float from, float to, bool lengthwise)
        {
            var piece = Box(name, at, full, color, show.site).transform;
            show.rises.Add(new Rise { piece = piece, at = at, full = full, along = along, from = from, to = to, lengthwise = lengthwise });
        }
        void AnimateStationUpgrades(float dt, int speed)
        {
            if (upgradeShows.Count == 0)
                return;
            finishedShows.Clear();
            foreach (var show in upgradeShows)
            {
                show.time += dt * Mathf.Max(1, speed);
                if (game.Trains.Station(show.stationId) == null || show.time >= show.end)
                    finishedShows.Add(show);
                else
                    AnimateShow(show, show.time);
            }
            if (finishedShows.Count == 0)
                return;
            foreach (var show in finishedShows)
            {
                upgradeShows.Remove(show);
                upgradingStations.Remove(show.stationId);
                if (show.root)
                    Destroy(show.root.gameObject);
            }
            revision = -1; // draw the finished stations
            Refresh();
            // A station bulldozed mid-show just ends its show; only a finished station announces itself.
            foreach (var show in finishedShows)
                if (game.Trains.Station(show.stationId) != null)
                    show.onDone?.Invoke();
        }
        void AnimateShow(UpgradeShow show, float time)
        {
            PoseDozer(show, time);
            AnimateCrew(show, time);
            for (int i = 0; i < show.wrecks.Count; i++)
                AnimateWreck(show, show.wrecks[i], time, i);
            float build = (time - show.buildStart) / BuildSeconds;
            show.site.gameObject.SetActive(build > 0);
            if (build <= 0)
                return;
            foreach (var rise in show.rises)
                Grow(rise, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(rise.from, rise.to, build)));
            if (show.scaffold)
                show.scaffold.gameObject.SetActive(build > .4f && build < .93f);
            if (show.jib)
                show.jib.localRotation = Quaternion.Euler(0, Mathf.Sin(build * 9f) * 80f, 0);
        }
        static void Grow(Rise rise, float f)
        {
            rise.piece.gameObject.SetActive(f > 0);
            if (f <= 0)
                return;
            if (rise.lengthwise)
            {
                rise.piece.localScale = Vector3.Scale(rise.full, Vector3.one - rise.along + rise.along * f);
                return;
            }
            float bottom = rise.at.y - rise.full.y * .5f, height = rise.full.y * f;
            rise.piece.localScale = new Vector3(rise.full.x, height, rise.full.z);
            rise.piece.localPosition = new Vector3(rise.at.x, bottom + height * .5f, rise.at.z);
        }
        void PoseDozer(UpgradeShow show, float time)
        {
            var route = show.route;
            bool driving = Follow(route, time, show.dozer.root);
            // Contact: each wreck falls once the blade reaches it, even when a long frame skips past that moment.
            for (int i = 0; i < route.Count; i++)
                if (route[i].hit >= 0 && route[i].time <= time && show.wrecks[route[i].hit].hitTime < 0)
                    Hit(show, route[i].hit, route[i].time);
            if (!driving)
                return;
            int k = 0;
            while (k + 2 < route.Count && route[k + 1].time <= time)
                k++;
            bool pushing = k > 0 && route[k].hit >= 0;
            AnimateBulldozer(show.dozer, time, pushing);
        }
        void Hit(UpgradeShow show, int index, float time)
        {
            var wreck = show.wrecks[index];
            wreck.hitTime = time;
            var rng = new System.Random(show.stationId * 131 + index);
            Color[] rubble = { new Color(.55f, .42f, .32f), Stone, Brick, new Color(.4f, .36f, .31f) };
            // Small tumbling puffs in two shades, spread over the lot, rather than one block of dust.
            for (int i = 0; i < DustPuffs; i++)
            {
                var offset = new Vector3((float)rng.NextDouble() - .5f, 0, (float)rng.NextDouble() - .5f) * 1.1f;
                var shade = i % 2 == 0 ? new Color(.82f, .79f, .72f) : new Color(.66f, .62f, .56f);
                var turn = Quaternion.Euler(rng.Next(90), rng.Next(90), rng.Next(90));
                var puff = Box("Dust", wreck.at + offset + Vector3.up * .15f, Vector3.zero, shade, show.root, turn).transform;
                wreck.dust.Add(puff);
            }
            if (!wreck.tree)
                for (int i = 0; i < 4; i++)
                {
                    var offset = new Vector3((float)rng.NextDouble() - .5f, 0, (float)rng.NextDouble() - .5f) * .6f;
                    float size = .14f + (float)rng.NextDouble() * .12f;
                    var chunk = Box("Rubble", wreck.at + offset + Vector3.up * size * .4f, Vector3.one * size, rubble[i], show.root, Quaternion.Euler(0, rng.Next(90), rng.Next(30))).transform;
                    chunk.gameObject.SetActive(false);
                    wreck.debris.Add(chunk);
                }
            show.onHit?.Invoke(wreck.at);
        }
        void AnimateWreck(UpgradeShow show, Wreck wreck, float time, int index)
        {
            if (wreck.hitTime < 0 || !wreck.ghost)
                return;
            float u = time - wreck.hitTime, fall = Mathf.Clamp01(u / CollapseSeconds);
            if (wreck.tree)
            {
                // The pine topples away from the blade, then settles into the ground.
                var dozer = show.dozer.root.localPosition;
                var away = wreck.at - dozer;
                away.y = 0;
                var axis = Vector3.Cross(Vector3.up, away.sqrMagnitude > .001f ? away.normalized : Vector3.forward);
                wreck.ghost.localRotation = Quaternion.AngleAxis(85f * fall * fall, axis);
                wreck.ghost.localScale = Vector3.one * Mathf.Clamp01(1.6f - u);
            }
            else
            {
                float shake = Mathf.Sin(u * 70f) * .04f * (1 - fall);
                wreck.ghost.localPosition = new Vector3(shake, 0, -shake * .5f);
                wreck.ghost.localScale = new Vector3(1, Mathf.Max(.001f, 1 - fall * fall), 1);
            }
            wreck.ghost.gameObject.SetActive(wreck.tree ? u < 1.6f : fall < 1);
            // Rubble lies where the building stood until the blade shoves it away.
            float shove = Mathf.Clamp01((u - CollapseSeconds - .45f) / .4f);
            foreach (var chunk in wreck.debris)
            {
                chunk.gameObject.SetActive(u > CollapseSeconds * .6f && shove < 1);
                chunk.localScale = Vector3.one * (.2f * (1 - shove));
            }
            float puff = Mathf.Clamp01(u / DustSeconds);
            for (int i = 0; i < wreck.dust.Count; i++)
            {
                var dust = wreck.dust[i];
                dust.gameObject.SetActive(puff < 1);
                float size = Mathf.Sin(puff * Mathf.PI) * (.12f + .03f * (i % 4));
                dust.localScale = Vector3.one * size;
                var at = dust.localPosition;
                dust.localPosition = new Vector3(at.x, .15f + puff * (.35f + .08f * (i % 5)), at.z);
                dust.localRotation *= Quaternion.Euler(0, 90f * Time.deltaTime, 0);
            }
        }
    }
}
