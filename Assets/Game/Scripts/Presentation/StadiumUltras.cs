using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace ValleyRail
{
    /// <summary>
    /// The ultras of one stadium: yellow "ULTRAS" banners on every stand (and a big one over each end), flags in each
    /// club's colours waving behind both goals, and red flares trailing smoke. Flags and flares are held by fans, so they hang off
    /// their block of seats: they jump when it cheers and vanish when the cutaway lowers its stand.
    /// </summary>
    public sealed class StadiumUltras
    {
        sealed class Flag
        {
            public Transform view;
            public Quaternion rest;
            public float phase;
            public int team;
        }
        sealed class Flare
        {
            public Transform flame, spark;
            public Transform[] smoke;
            public Vector3 top;
            public float phase;
            public int team;
        }
        const int FlagsPerEnd = 10, FlaresPerEnd = 8, Puffs = 4;
        const float FlagSwing = 32, FlareCycle = .19f, FlareOnShare = -.35f;
        public const string Slogan = "ULTRAS";
        static readonly Color Yellow = new Color(1f, .84f, .1f), Ink = new Color(.08f, .08f, .08f), Pole = new Color(.25f, .25f, .28f),
            Flame = new Color(1f, .22f, .04f), Spark = new Color(1f, .97f, .75f), Stick = new Color(.45f, .08f, .06f), Smoke = new Color(.72f, .36f, .33f);
        // A flare's flame and white-hot spark at full size, in fan scale units.
        static readonly Vector3 FlameSize = new Vector3(.04f, .07f, .04f), SparkSize = new Vector3(.024f, .024f, .024f);
        readonly List<Flag> flags = new List<Flag>();
        readonly List<Flare> flares = new List<Flare>();
        // The big banners over the ends hang on the upper tier, so they go when their stand is cut away.
        readonly Transform[] curva = new Transform[4];
        readonly float scale;
        float time;

        public int Flags => flags.Count;
        public int Flares => flares.Count;

        /// <param name="holder">The block of fans a seat belongs to: (side, row, along the row) → its transform.</param>
        public StadiumUltras(WorldView world, Transform root, ArenaLayout layout, System.Random random, Func<int, int, float, Transform> holder)
        {
            scale = layout.Spectator;
            for (int side = 0; side < 4; side++)
            {
                var stand = layout.Stand(side);
                float bottom = .015f, high = stand.RowTop(0) - .04f, front = stand.Front - .008f;
                if (stand.alongX)
                {
                    for (int end = -1; end <= 1; end += 2)
                        Banner(world, root, "Ultras banner", stand, end * stand.length * .22f, stand.length * .28f, bottom, high, front);
                    continue;
                }
                Banner(world, root, "Ultras banner", stand, 0, stand.length * .62f, bottom, high, front);
                int team = side == 3 ? 0 : 1;
                if (layout.size >= 4)
                {
                    curva[side] = new GameObject("Curva").transform;
                    curva[side].SetParent(root, false);
                    Banner(world, curva[side], "Curva banner", stand, 0, stand.length * .72f, stand.height * 1.08f, stand.height * .45f, stand.thick * .05f - .035f);
                }
                for (int i = 0; i < FlagsPerEnd; i++)
                    flags.Add(MakeFlag(world, stand, Seat(stand, side, random, holder, out var parent), parent, team, i % 3 == 2, random));
                for (int i = 0; i < FlaresPerEnd; i++)
                    flares.Add(MakeFlare(world, Seat(stand, side, random, holder, out var parent), parent, team, random));
            }
        }

        /// <summary>A seat in the stand, away from the front row, and the block of fans it belongs to.</summary>
        static Vector3 Seat(Stand stand, int side, System.Random random, Func<int, int, float, Transform> holder, out Transform parent)
        {
            int row = random.Next(1, stand.rows);
            float along = ((float)random.NextDouble() - .5f) * stand.length * .9f;
            parent = holder(side, row, along);
            return stand.RowCentre(row) + stand.Along * along;
        }
        /// <summary>A yellow cloth with black edges and the slogan, facing the pitch; <paramref name="depth"/> is out from the stand centre.</summary>
        static void Banner(WorldView world, Transform parent, string name, Stand stand, float along, float length, float bottom, float high, float depth)
        {
            var banner = new GameObject(name).transform;
            banner.SetParent(parent, false);
            banner.localPosition = stand.centre + stand.normal * depth + stand.Along * along + Vector3.up * (bottom + high / 2);
            // Banner frame: x along the cloth, y up, z into the stand, so its face looks at the pitch.
            banner.localRotation = Quaternion.LookRotation(stand.normal);
            world.Box("Cloth", Vector3.zero, new Vector3(length, high, .012f), Yellow, banner);
            for (int edge = -1; edge <= 1; edge += 2)
                world.Box("Edge", Vector3.up * edge * high * .43f, new Vector3(length, high * .12f, .014f), Ink, banner);
            var slogan = new GameObject("Slogan", typeof(TextMeshPro));
            slogan.transform.SetParent(banner, false);
            slogan.transform.localPosition = Vector3.back * .009f;
            var text = slogan.GetComponent<TextMeshPro>();
            text.text = Slogan;
            text.rectTransform.sizeDelta = new Vector2(length * .92f, high * .66f);
            text.enableAutoSizing = true;
            text.fontSizeMin = .1f;
            text.fontSizeMax = 30;
            text.alignment = TextAlignmentOptions.Center;
            text.fontStyle = FontStyles.Bold;
            text.characterSpacing = 12;
            text.color = Ink;
            text.enableWordWrapping = false;
        }
        /// <summary>A flag on a pole held above a fan's head; it sweeps from side to side facing the pitch.</summary>
        Flag MakeFlag(WorldView world, Stand stand, Vector3 seat, Transform parent, int team, bool black, System.Random random)
        {
            float s = scale;
            var view = new GameObject("Ultras flag").transform;
            view.SetParent(parent, false);
            view.localPosition = seat + Vector3.up * .07f * s;
            var rest = Quaternion.LookRotation(-stand.normal);
            view.localRotation = rest;
            // Each end waves its own club's colours: yellow and black at home, red and black away.
            var club = StadiumMatch.TeamShirt(team);
            Color cloth = black ? Ink : club, stripe = black ? club : Ink;
            world.Box("Pole", Vector3.up * .16f * s, new Vector3(.008f, .32f, .008f) * s, Pole, view);
            world.Box("Flag", new Vector3(.07f, .27f, 0) * s, new Vector3(.14f, .09f, .006f) * s, cloth, view);
            world.Box("Flag stripe", new Vector3(.07f, .27f, 0) * s, new Vector3(.14f, .025f, .008f) * s, stripe, view);
            return new Flag { view = view, rest = rest, phase = (float)random.NextDouble() * Mathf.PI * 2, team = team };
        }
        /// <summary>A flare held up at arm's length: a stick, a flickering red flame with a white-hot spark, and a trail of smoke.</summary>
        Flare MakeFlare(WorldView world, Vector3 seat, Transform parent, int team, System.Random random)
        {
            float s = scale;
            var hand = seat + Vector3.up * .1f * s;
            world.Box("Flare stick", hand + Vector3.up * .01f * s, new Vector3(.01f, .05f, .01f) * s, Stick, parent);
            var flare = new Flare
            {
                flame = world.Box("Flare", hand + Vector3.up * .07f * s, FlameSize * s, Flame, parent).transform,
                spark = world.Box("Flare spark", hand + Vector3.up * .105f * s, SparkSize * s, Spark, parent).transform,
                smoke = new Transform[Puffs],
                top = hand + Vector3.up * .11f * s,
                phase = (float)random.NextDouble(),
                team = team,
            };
            for (int i = 0; i < Puffs; i++)
                flare.smoke[i] = world.Box("Flare smoke", flare.top, Vector3.one * .03f * s, Smoke, parent).transform;
            return flare;
        }

        /// <summary>Hides the big end banner of every stand the cutaway lowers.</summary>
        public void Cutaway(int mask)
        {
            for (int side = 0; side < 4; side++)
                if (curva[side])
                    curva[side].gameObject.SetActive((mask & 1 << side) == 0);
        }
        /// <summary>
        /// Flags wave (twice as fast while their team celebrates). Flares burn on and off in long cycles, and every flare in
        /// an end lights up when its team scores.
        /// </summary>
        public void Animate(float dt, int cheering)
        {
            time += dt;
            foreach (var flag in flags)
            {
                if (!flag.view.gameObject.activeInHierarchy)
                    continue;
                float pace = flag.team == cheering ? 2.2f : 1;
                flag.view.localRotation = flag.rest * Quaternion.Euler(0, 0, Mathf.Sin(time * 2.4f * pace + flag.phase) * FlagSwing);
            }
            float s = scale;
            foreach (var flare in flares)
            {
                if (!flare.flame.gameObject.activeInHierarchy)
                    continue;
                bool lit = flare.team == cheering || Mathf.Sin(time * FlareCycle + flare.phase * Mathf.PI * 2) > FlareOnShare;
                float flicker = lit ? .8f + .3f * Mathf.Abs(Mathf.Sin(time * 23 + flare.phase * 40) * Mathf.Sin(time * 9 + flare.phase * 17)) : 0;
                flare.flame.localScale = FlameSize * s * flicker;
                flare.spark.localScale = SparkSize * s * flicker;
                for (int i = 0; i < Puffs; i++)
                {
                    // Small tumbling puffs swell, then thin away as the wind carries them up and off to one side, so the
                    // smoke reads as a drifting trail rather than a column.
                    float life = Mathf.Repeat(time * .4f + (float)i / Puffs + flare.phase, 1);
                    float sway = Mathf.Sin(life * 5 + flare.phase * 20) * .03f;
                    flare.smoke[i].localPosition = flare.top + new Vector3(life * life * .3f + sway, life * .32f, life * .06f) * s;
                    flare.smoke[i].localRotation = Quaternion.Euler(life * 140 + flare.phase * 90, life * 200, 0);
                    flare.smoke[i].localScale = Vector3.one * (lit ? (.012f + Mathf.Sin(life * Mathf.PI) * .055f) * s : 0);
                }
            }
        }
    }
}
