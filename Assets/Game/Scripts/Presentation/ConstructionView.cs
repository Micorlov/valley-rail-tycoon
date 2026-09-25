using System.Collections.Generic;
using UnityEngine;
using ValleyRail.Core;
namespace ValleyRail
{
    /// <summary>
    /// Construction sites: a building the town adds (or upgrades) while the game runs first appears as foundations,
    /// rising walls and scaffolding, with a crane on larger jobs (and on every job in a town the player funds), and swaps in once built. Purely cosmetic: the
    /// simulation already counts the building. Loading a game, pausing or big layout changes show buildings at once.
    /// </summary>
    public sealed partial class WorldView
    {
        /// <summary>Tests and art captures switch this off to see finished buildings immediately.</summary>
        public bool ConstructionEnabled = true;
        const int MaxNewSitesPerRefresh = 12;
        // Donations: every site in a town with a development fund (or just given to) gets a working crane, takes at least
        // GiftSiteSeconds, and opens and works even while the game is paused, so each gift shows cranes at once.
        const float GiftSiteSeconds = 6f, GiftMarkSeconds = 20f;
        readonly Dictionary<int, float> giftUntil = new Dictionary<int, float>();
        sealed class Site
        {
            public Transform root, walls, jib, load;
            public float progress, seconds, height;
            public bool gift;
        }
        /// <summary>Called just before a gift is applied: the buildings it starts open as crane sites, paused or not.</summary>
        public void MarkGift(int cityId) => giftUntil[cityId] = Time.unscaledTime + GiftMarkSeconds;
        bool GiftTown(CityState city) => city.fund > 0 || (giftUntil.TryGetValue(city.id, out float until) && until > Time.unscaledTime);
        public int GiftCranes
        {
            get
            {
                int n = 0;
                foreach (var site in sites.Values)
                    if (site.gift && site.jib)
                        n++;
                return n;
            }
        }
        Transform constructionRoot;
        readonly Dictionary<int, Site> sites = new Dictionary<int, Site>();
        readonly HashSet<int> knownBuildings = new HashSet<int>(), currentBuildings = new HashSet<int>();
        readonly List<int> finishedSites = new List<int>();
        bool constructionBaseline = true;
        public int ConstructionSites => sites.Count;
        static int SiteKey(BuildingState bs) => bs.cell.Key * 64 + bs.def;
        bool UnderConstruction(BuildingState bs) => sites.ContainsKey(SiteKey(bs));
        /// <summary>Before a city redraw: start sites for buildings that appeared since the last one.</summary>
        void BeginConstructionPass()
        {
            currentBuildings.Clear();
            int fresh = 0;
            foreach (var city in game.World.cities)
                foreach (var bs in city.buildings)
                    if (currentBuildings.Add(SiteKey(bs)) && !knownBuildings.Contains(SiteKey(bs)))
                        fresh++;
            bool start = ConstructionEnabled && !constructionBaseline && fresh <= MaxNewSitesPerRefresh, running = game.World.speed > 0;
            if (start)
                foreach (var city in game.World.cities)
                {
                    // A paused game shows new buildings at once, except in a town the player is funding.
                    bool gift = GiftTown(city);
                    if (!running && !gift)
                        continue;
                    foreach (var bs in city.buildings)
                    {
                        int key = SiteKey(bs);
                        if (!knownBuildings.Contains(key) && !sites.ContainsKey(key))
                            sites[key] = OpenSite(bs, gift);
                    }
                }
            finishedSites.Clear();
            foreach (var pair in sites)
                if (!currentBuildings.Contains(pair.Key) || !ConstructionEnabled)
                    finishedSites.Add(pair.Key);
            foreach (int key in finishedSites)
                CloseSite(key);
        }
        void EndConstructionPass()
        {
            knownBuildings.Clear();
            knownBuildings.UnionWith(currentBuildings);
            constructionBaseline = false;
        }
        Site OpenSite(BuildingState bs, bool gift)
        {
            var def = BuildingCatalog.Get(bs.def);
            var lot = LotFor(bs);
            float w = def.width / 100f, d = def.depth / 100f, h = Mathf.Min(def.height / 100f, 3.5f);
            bool civic = def.category == BuildingCategory.Civic;
            var site = new Site { seconds = civic ? 3f + 1.2f * def.size : 2f + def.level, height = Mathf.Max(.25f, h), gift = gift };
            if (gift)
                site.seconds = Mathf.Max(site.seconds, GiftSiteSeconds);
            site.root = new GameObject("Construction site").transform;
            site.root.SetParent(constructionRoot, false);
            site.root.localPosition = lot.at;
            site.root.localRotation = lot.turn;
            Box("Foundation", new Vector3(0, .02f, 0), new Vector3(w + .08f, .04f, d + .08f), Stone, site.root);
            site.walls = Box("Rising walls", new Vector3(0, .02f, 0), new Vector3(w * .92f, .02f, d * .92f), Sand, site.root).transform;
            float top = site.height + .15f;
            for (int i = 0; i < 4; i++)
                Box("Scaffold pole", new Vector3((i % 2 == 0 ? -1 : 1) * (w / 2 + .03f), top / 2, (i < 2 ? -1 : 1) * (d / 2 + .03f)), new Vector3(.025f, top, .025f), Timber, site.root);
            for (int level = 1; level <= 2; level++)
                for (int side = -1; side <= 1; side += 2)
                    Box("Scaffold board", new Vector3(0, top * level / 3f, side * (d / 2 + .03f)), new Vector3(w + .06f, .02f, .05f), Gold, site.root);
            Box("Site barrier", new Vector3(0, .06f, d / 2 + .12f), new Vector3(w * .8f, .05f, .02f), Red, site.root);
            if (civic || def.level >= 3 || gift)
            {
                float mast = site.height + 1f;
                var at = new Vector3(w / 2 + .18f, 0, -d / 2 + .1f);
                Box("Crane mast", at + Vector3.up * mast / 2, new Vector3(.07f, mast, .07f), SchoolBus, site.root);
                site.jib = new GameObject("Crane jib").transform;
                site.jib.SetParent(site.root, false);
                site.jib.localPosition = at + Vector3.up * mast;
                float reach = w + .6f;
                Box("Jib", new Vector3(-reach / 2 + .2f, 0, 0), new Vector3(reach, .05f, .05f), SchoolBus, site.jib);
                Box("Counterweight", new Vector3(.3f, -.04f, 0), new Vector3(.14f, .1f, .1f), Slate, site.jib);
                Box("Hook line", new Vector3(-reach * .6f, -.25f, 0), new Vector3(.01f, .5f, .01f), Slate, site.jib);
                // A pallet of bricks on the hook, hoisted up and down while the crane swings.
                site.load = Box("Crane load", new Vector3(-reach * .6f, -.55f, 0), new Vector3(.16f, .1f, .16f), Red, site.jib).transform;
            }
            return site;
        }
        void CloseSite(int key)
        {
            if (sites.TryGetValue(key, out var site) && site.root)
                Destroy(site.root.gameObject);
            sites.Remove(key);
        }
        /// <summary>Raises walls and turns cranes at game speed (gift sites keep working while paused); finished sites trigger one city redraw.</summary>
        void AnimateConstruction(float dt, float speed)
        {
            if (sites.Count == 0)
                return;
            finishedSites.Clear();
            foreach (var pair in sites)
            {
                var site = pair.Value;
                float pace = site.gift ? Mathf.Max(speed, 1f) : speed;
                if (pace <= 0)
                    continue;
                site.progress += dt * pace / site.seconds;
                float wall = Mathf.Max(.02f, site.height * Mathf.Clamp01(site.progress));
                var scale = site.walls.localScale;
                site.walls.localScale = new Vector3(scale.x, wall, scale.z);
                site.walls.localPosition = new Vector3(0, wall / 2, 0);
                if (site.jib)
                    site.jib.localRotation = Quaternion.Euler(0, Mathf.Sin(site.progress * 6f) * 70f, 0);
                if (site.load)
                {
                    var at = site.load.localPosition;
                    site.load.localPosition = new Vector3(at.x, -.35f - .25f * Mathf.Abs(Mathf.Sin(site.progress * 9f)), at.z);
                }
                if (site.progress >= 1)
                    finishedSites.Add(pair.Key);
            }
            if (finishedSites.Count == 0)
                return;
            foreach (int key in finishedSites)
                CloseSite(key);
            cityRevision = -1; // draw the finished buildings into the city meshes
            Refresh();
        }
    }
}
