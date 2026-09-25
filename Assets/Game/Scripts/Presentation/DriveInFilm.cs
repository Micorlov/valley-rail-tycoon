using UnityEngine;
namespace ValleyRail
{
    /// <summary>
    /// The drive-in's programme, painted a frame at a time into a small picture for the screens: a welcome slide, a
    /// countdown leader, the western "Valley Express" (a steam train across the desert, a cowboy racing it, a canyon
    /// bridge at dusk), an intermission with dancing snacks, a second leader, the saucer picture "It Came From The Valley"
    /// (a cow beamed up, a giant robot in the city, a flight through the stars) and a goodnight slide, then round again.
    /// Pure drawing: no Unity objects, so it runs headless. Coordinates are pixels from the top left.
    /// </summary>
    public sealed class DriveInFilm
    {
        public const int Width = 160, Height = 90;
        public enum Reel { Welcome, Leader, Western, Intermission, SecondLeader, SciFi, Goodnight }
        static readonly float[] Lengths = { 24, 5, 44, 18, 5, 44, 12 };
        /// <summary>Seconds in one full programme.</summary>
        public static readonly float Length = Total();
        /// <summary>The finished frame, bottom row first as a texture wants it.</summary>
        public readonly Color32[] pixels = new Color32[Width * Height];
        readonly byte[] vignette = new byte[Width * Height];

        static readonly Color32 Black = new Color32(8, 8, 10, 255), White = new Color32(245, 243, 235, 255), Yellow = new Color32(255, 214, 70, 255),
            NeonRed = new Color32(255, 70, 60, 255), Night = new Color32(12, 16, 42, 255), NightLow = new Color32(40, 34, 84, 255),
            Moon = new Color32(244, 240, 210, 255), SlimeGreen = new Color32(110, 255, 90, 255), LeaderGrey = new Color32(170, 168, 160, 255),
            SunsetTop = new Color32(232, 104, 84, 255), SunsetLow = new Color32(255, 196, 120, 255), Sun = new Color32(255, 236, 160, 255),
            FarHills = new Color32(132, 72, 112, 255), Mesa = new Color32(168, 82, 56, 255), Desert = new Color32(214, 150, 88, 255),
            DesertDark = new Color32(186, 122, 70, 255), Rail = new Color32(70, 52, 44, 255), Cactus = new Color32(60, 120, 64, 255),
            Engine = new Color32(30, 30, 36, 255), EngineRed = new Color32(200, 40, 34, 255), Brass = new Color32(236, 190, 80, 255),
            Carriage = new Color32(120, 30, 36, 255), CarriageTrim = new Color32(236, 200, 120, 255), Window = new Color32(255, 226, 130, 255),
            Smoke = new Color32(236, 232, 226, 255), Horse = new Color32(120, 72, 40, 255), HorseDark = new Color32(70, 40, 24, 255),
            Shirt = new Color32(60, 100, 170, 255), Hat = new Color32(90, 60, 30, 255), Skin = new Color32(236, 190, 150, 255),
            Dusk = new Color32(64, 40, 96, 255), DuskLow = new Color32(214, 116, 120, 255), Cliff = new Color32(96, 50, 40, 255),
            River = new Color32(60, 110, 170, 255), Trestle = new Color32(92, 64, 40, 255),
            Stripe = new Color32(200, 36, 40, 255), StripeLight = new Color32(250, 238, 214, 255), Band = new Color32(30, 20, 30, 255),
            Bun = new Color32(226, 170, 96, 255), Sausage = new Color32(178, 64, 42, 255), Mustard = new Color32(250, 210, 40, 255),
            Kernel = new Color32(255, 246, 204, 255), CupBlue = new Color32(40, 110, 210, 255), Straw = new Color32(230, 60, 60, 255),
            Farm = new Color32(20, 50, 34, 255), Barn = new Color32(150, 34, 30, 255), BarnDark = new Color32(110, 24, 22, 255),
            CowWhite = new Color32(240, 240, 236, 255), CowNose = new Color32(240, 170, 170, 255),
            SaucerGrey = new Color32(170, 176, 186, 255), SaucerDark = new Color32(110, 116, 128, 255), Dome = new Color32(150, 226, 236, 255),
            Beam = new Color32(190, 255, 170, 255), CityNight = new Color32(16, 10, 34, 255), CityLow = new Color32(70, 36, 80, 255),
            Tower = new Color32(26, 26, 44, 255), FrontTower = new Color32(12, 10, 24, 255), Lit = new Color32(250, 214, 110, 255),
            RobotGrey = new Color32(150, 156, 168, 255), RobotDark = new Color32(96, 100, 114, 255), Laser = new Color32(255, 60, 40, 255),
            Blast = new Color32(255, 170, 50, 255), Searchlight = new Color32(255, 250, 200, 255), Bulb = new Color32(255, 214, 70, 255),
            BulbOff = new Color32(90, 60, 30, 255), LeaderDark = new Color32(120, 118, 112, 255), SkyBlue = new Color32(90, 170, 255, 255);
        static readonly Color32[] Lights = { NeonRed, Yellow, SlimeGreen, SkyBlue };

        public DriveInFilm()
        {
            // A soft darkening towards the corners, like an old projector's light falling off.
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                {
                    float dx = (x - Width / 2f) / (Width / 2f), dy = (y - Height / 2f) / (Height / 2f);
                    vignette[y * Width + x] = (byte)(255 * Mathf.Clamp01(1.08f - .28f * (dx * dx + dy * dy)));
                }
        }

        static float Total()
        {
            float sum = 0;
            foreach (float length in Lengths)
                sum += length;
            return sum;
        }
        /// <summary>The reel playing at <paramref name="clock"/> seconds into the programme, and how far into it.</summary>
        public static Reel ReelAt(float clock, out float t)
        {
            t = Mathf.Repeat(clock, Length);
            for (int i = 0; i < Lengths.Length; i++)
            {
                if (t < Lengths[i])
                    return (Reel)i;
                t -= Lengths[i];
            }
            t = 0;
            return Reel.Welcome;
        }
        /// <summary>Seconds from the programme's start to the start of <paramref name="reel"/>.</summary>
        public static float Start(Reel reel)
        {
            float start = 0;
            for (int i = 0; i < (int)reel; i++)
                start += Lengths[i];
            return start;
        }
        public static float LengthOf(Reel reel) => Lengths[(int)reel];

        /// <summary>Paints the frame at <paramref name="clock"/> seconds into the programme.</summary>
        public void Draw(float clock)
        {
            switch (ReelAt(clock, out float t))
            {
                case Reel.Welcome: Welcome(t); break;
                case Reel.Leader:
                case Reel.SecondLeader: Leader(t); break;
                case Reel.Western: Western(t); break;
                case Reel.Intermission: Intermission(t); break;
                case Reel.SciFi: SciFi(t); break;
                default: Goodnight(t); break;
            }
            Projector(clock);
        }

        // ---- Slides -------------------------------------------------------------------------------------------------

        void Welcome(float t)
        {
            Gradient(0, Height, Night, NightLow);
            Stars(70, t, 3);
            Disc(136, 16, 7, Moon);
            Disc(139, 14, 6, Night);
            Bulbs(t);
            Text("WELCOME TO THE", 80, 12, 1, White);
            bool flicker = Hash((int)(t * 8)) % 23 == 0;
            Text("VALLEY", 80, 26, 2, flicker ? Band : Yellow);
            Text("DRIVE-IN", 80, 46, 2, NeonRed);
            if ((int)(t * 2) % 2 == 0)
                Text(t < 14 ? "DOUBLE FEATURE TONIGHT" : "SHOW STARTS SOON", 80, 70, 1, White);
        }
        /// <summary>Marquee bulbs chasing round the edge of the frame.</summary>
        void Bulbs(float t)
        {
            int step = (int)(t * 6), n = 0;
            for (int x = 3; x < Width - 2; x += 5, n++)
            {
                var c = (n + step) % 3 == 0 ? Bulb : BulbOff;
                Rect(x, 2, 2, 2, c);
                Rect(Width - 1 - x, Height - 4, 2, 2, c);
            }
            for (int y = 7; y < Height - 5; y += 5, n++)
            {
                var c = (n + step) % 3 == 0 ? Bulb : BulbOff;
                Rect(2, Height - 1 - y, 2, 2, c);
                Rect(Width - 4, y, 2, 2, c);
            }
        }
        /// <summary>The countdown before each film: a sweeping hand over rings and a big number.</summary>
        void Leader(float t)
        {
            Fill(LeaderGrey);
            float cx = Width / 2f, cy = Height / 2f, sweep = t % 1 * 360;
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                {
                    float angle = Mathf.Atan2(x - cx, cy - y) * Mathf.Rad2Deg;
                    if (angle < 0)
                        angle += 360;
                    if (angle < sweep)
                        pixels[y * Width + x] = LeaderDark;
                }
            Ring(cx, cy, 38, 1.4f, White);
            Ring(cx, cy, 30, 1.4f, White);
            Rect(0, cy, Width, 1, Black);
            Rect(cx, 0, 1, Height, Black);
            Text(Mathf.Clamp(5 - (int)t, 1, 5).ToString(), cx, cy - 17, 5, Black);
        }
        void Goodnight(float t)
        {
            Gradient(0, Height, Night, NightLow);
            Stars(70, t, 11);
            Disc(24, 18, 8, Moon);
            Disc(28, 16, 7, Night);
            Text("THANKS FOR COMING", 80, 14, 1, White);
            Text("DRIVE", 80, 30, 2, Yellow);
            Text("SAFELY!", 80, 48, 2, Yellow);
            Text("GOOD NIGHT", 80, 72, 1, White);
            if (t > 10)
                Darken((t - 10) / 2);
        }

        // ---- Valley Express -------------------------------------------------------------------------------------------

        void Western(float t)
        {
            if (t < 5)
            {
                Gradient(0, Height, SunsetTop, SunsetLow);
                Disc(80, 62, 22, Sun);
                Ridge(0, 1, 62, 5, FarHills);
                Rect(0, 72, Width, Height - 72, Desert);
                Text("VALLEY", 81, 13, 3, Band);
                Text("EXPRESS", 81, 39, 3, Band);
                Text("VALLEY", 80, 12, 3, Yellow);
                Text("EXPRESS", 80, 38, 3, Yellow);
                if (t < 1)
                    Darken(1 - t);
            }
            else if (t < 20)
                DesertRun(t - 5);
            else if (t < 32)
                Chase(t - 20);
            else if (t < 41)
                Canyon(t - 32);
            else
                TheEnd(t - 41, "THE END", White);
        }
        /// <summary>The desert streaming past a steam train that the camera follows.</summary>
        void DesertRun(float t)
        {
            float scroll = t * 34;
            Desertscape(scroll, true);
            Train(122, 72, t, scroll);
        }
        void Desertscape(float scroll, bool sun)
        {
            Gradient(0, 64, SunsetTop, SunsetLow);
            if (sun)
                Disc(112, 44, 14, Sun);
            Ridge(scroll * .15f, 1, 50, 7, FarHills);
            Mesas(scroll * .4f);
            Rect(0, 64, Width, Height - 64, Desert);
            for (int x = 0; x < Width; x++)
            {
                int ground = (int)(x + scroll * .9f);
                if (Hash(ground) % 7 == 0)
                    Put(x, 66 + (int)(Hash(ground * 3) % 22), DesertDark);
            }
            // Sleepers race past under the rails.
            float offset = scroll % 6;
            for (float x = -offset; x < Width; x += 6)
                Rect(x, 73, 3, 2, Rail);
            Rect(0, 72, Width, 1, Rail);
            // Saguaros in the foreground go by fastest.
            for (int i = 0; i < 4; i++)
            {
                float x = Mathf.Repeat(i * 53 + 20 - scroll * 1.4f, Width + 60) - 30;
                Saguaro(x, 90, 12 + i % 3 * 4);
            }
        }
        void Mesas(float scroll)
        {
            for (int x = 0; x < Width; x++)
            {
                float u = (x + scroll) / 70f;
                float h = Mathf.Clamp(Mathf.Sin(u * 2.1f) * 16 + Mathf.Sin(u * 5.3f) * 4, -4, 11);
                if (h > 0)
                    Rect(x, 64 - h, 1, h + 1, Mesa);
            }
        }
        void Saguaro(float x, float ground, float height)
        {
            Rect(x - 2, ground - height, 4, height, Cactus);
            Rect(x - 7, ground - height * .75f, 3, height * .35f, Cactus);
            Rect(x - 7, ground - height * .45f, 7, 2, Cactus);
            Rect(x + 4, ground - height * .85f, 3, height * .35f, Cactus);
            Rect(x + 2, ground - height * .55f, 5, 2, Cactus);
        }
        /// <summary>A steam engine and two carriages, the engine's nose at <paramref name="front"/>, wheels on <paramref name="rail"/>.</summary>
        void Train(float front, float rail, float t, float travelled)
        {
            float bob = Mathf.Sin(t * 18) > .6f ? 1 : 0, y = rail - bob;
            // Smoke puffs leave the chimney every quarter second and drift back, rise, grow and thin out.
            float chimneyX = front - 12, chimneyY = y - 24;
            for (int k = 0; k < 12; k++)
            {
                float born = Mathf.Floor(t * 4) / 4 - k * .25f, age = t - born;
                float px = chimneyX - age * 26, py = chimneyY - age * 9 - Mathf.Sin(born * 7) * 2;
                DiscBlend(px, py, 2.5f + age * 2.2f, Smoke, Mathf.Clamp01(.85f - age * .3f));
            }
            // Engine: boiler, cab, chimney, dome, cowcatcher and brass trim.
            Rect(front - 30, y - 16, 22, 10, Engine);
            Rect(front - 42, y - 22, 13, 16, Engine);
            Rect(front - 41, y - 20, 5, 5, Window);
            Rect(front - 44, y - 24, 17, 3, EngineRed);
            Rect(front - 14, y - 24, 5, 8, Engine);
            Rect(front - 15, y - 25, 7, 2, Engine);
            Rect(front - 23, y - 19, 5, 3, Brass);
            Rect(front - 30, y - 9, 30, 3, EngineRed);
            Tri(new Vector2(front - 8, y - 9), new Vector2(front + 1, y - 1), new Vector2(front - 8, y - 1), EngineRed);
            Rect(front - 9, y - 15, 2, 2, Window);
            float spin = travelled / 5f;
            Wheel(front - 34, y - 5, 5, spin);
            Wheel(front - 22, y - 5, 5, spin);
            Wheel(front - 11, y - 3, 3, spin * 1.6f);
            // Carriages with lit windows.
            for (int c = 0; c < 2; c++)
            {
                float right = front - 46 - c * 38, left = right - 35;
                Rect(left, y - 19, 35, 14, Carriage);
                Rect(left - 1, y - 21, 37, 2, Band);
                Rect(left, y - 8, 35, 1, CarriageTrim);
                for (int w = 0; w < 5; w++)
                    Rect(left + 3 + w * 7, y - 16, 4, 5, Window);
                Wheel(left + 6, y - 3, 3, spin * 1.6f);
                Wheel(right - 6, y - 3, 3, spin * 1.6f);
                Rect(right, y - 9, 3, 2, Band);
            }
        }
        void Wheel(float cx, float cy, float r, float spin)
        {
            Disc(cx, cy, r, Band);
            Disc(cx, cy, r - 1.2f, EngineRed);
            Line(cx, cy, cx + Mathf.Cos(spin) * r, cy + Mathf.Sin(spin) * r, Band);
            Line(cx, cy, cx - Mathf.Cos(spin) * r, cy - Mathf.Sin(spin) * r, Band);
        }
        /// <summary>A cowboy gallops up beside the train and waves his hat.</summary>
        void Chase(float t)
        {
            float scroll = 510 + t * 34;
            Desertscape(scroll, false);
            Train(206 - t * 2, 72, 15 + t, scroll);
            float hx = 18 + Mathf.Min(t, 9) * 6.5f;
            for (int k = 0; k < 6; k++)
            {
                float age = (t * 5 + k) % 6 / 5;
                DiscBlend(hx - 14 - age * 22, 76 - age * 5, 1.5f + age * 3, DesertDark, .7f * (1 - age));
            }
            Rider(hx, 80, t, t > 8);
        }
        void Rider(float x, float ground, float t, bool waving)
        {
            float gallop = t * 13, lift = Mathf.Abs(Mathf.Sin(gallop * .5f)) * 3, y = ground - lift;
            // Legs swing in pairs under the body.
            for (int leg = 0; leg < 4; leg++)
            {
                float root = x - 8 + leg * 5.5f, swing = Mathf.Sin(gallop + (leg < 2 ? 0 : Mathf.PI)) * 5;
                Line(root, y - 10, root + swing, y - 1, HorseDark);
                Line(root + 1, y - 10, root + swing + 1, y - 1, HorseDark);
            }
            Rect(x - 11, y - 17, 22, 8, Horse);
            Disc(x - 10, y - 13, 4, Horse);
            Disc(x + 10, y - 13, 4, Horse);
            // Neck and head reaching forward, a dark mane and a streaming tail.
            Tri(new Vector2(x + 8, y - 17), new Vector2(x + 17, y - 26), new Vector2(x + 13, y - 12), Horse);
            Rect(x + 15, y - 28, 8, 5, Horse);
            Line(x + 9, y - 18, x + 16, y - 27, HorseDark);
            Line(x - 12, y - 15, x - 19, y - 10 + Mathf.Sin(gallop) * 2, HorseDark);
            Line(x - 12, y - 14, x - 19, y - 9 + Mathf.Sin(gallop) * 2, HorseDark);
            // The cowboy.
            Rect(x - 3, y - 27, 6, 10, Shirt);
            Rect(x - 2, y - 19, 3, 6, Band);
            Disc(x, y - 30, 2.6f, Skin);
            if (waving)
            {
                float wave = Mathf.Sin(t * 10) * 3;
                Line(x + 2, y - 26, x + 6, y - 33 + wave, Shirt);
                Rect(x + 3, y - 38 + wave, 7, 2, Hat);
                Rect(x + 4, y - 41 + wave, 5, 3, Hat);
            }
            else
            {
                Line(x + 2, y - 25, x + 9, y - 22, Shirt);
                Rect(x - 4, y - 33, 9, 1, Hat);
                Rect(x - 2, y - 36, 5, 3, Hat);
            }
        }
        /// <summary>At dusk the train crosses a timber trestle over a canyon while the moon comes up.</summary>
        void Canyon(float t)
        {
            Gradient(0, Height, Dusk, DuskLow);
            Stars(30, t, 5);
            Disc(34, 18 - t * .6f, 6, Moon);
            Rect(0, 82, Width, 8, River);
            for (int x = 0; x < Width; x++)
                if (Hash(x * 7 + (int)(t * 6)) % 9 == 0)
                    Put(x, 83 + (int)(Hash(x) % 5), White);
            for (int y = 40; y < Height; y++)
            {
                float left = 26 - (y - 40) * .35f + Mathf.Sin(y * .4f) * 2, right = 134 + (y - 40) * .3f + Mathf.Sin(y * .3f + 1) * 2;
                Rect(0, y, left, 1, Cliff);
                Rect(right, y, Width - right, 1, Cliff);
            }
            // The trestle: bents every 16 px with cross bracing, a deck on top.
            for (int x = 28; x <= 132; x += 16)
            {
                Rect(x, 50, 2, 34, Trestle);
                if (x + 16 > 132)
                    continue;
                for (int level = 0; level < 2; level++)
                {
                    float top = 52 + level * 16;
                    Line(x, top, x + 16, top + 15, Trestle);
                    Line(x + 16, top, x, top + 15, Trestle);
                }
            }
            Rect(0, 48, Width, 3, Trestle);
            Train(-10 + t * 30, 48, 30 + t, t * 30);
        }
        void TheEnd(float t, string words, Color32 ink)
        {
            Fill(Black);
            Text(words, 80, 34, 3, ink);
            if (t < 1)
                Darken(1 - t);
        }

        // ---- Intermission -------------------------------------------------------------------------------------------

        void Intermission(float t)
        {
            int slide = (int)(t * 6) % 20;
            for (int x = 0; x < Width; x++)
                Rect(x, 0, 1, Height, (x + slide) % 20 < 10 ? Stripe : StripeLight);
            Rect(0, 0, Width, 22, Band);
            Text("INTERMISSION", 80, 4, 2, Yellow);
            Rect(0, 76, Width, 14, Band);
            if (t < 12)
                Text("VISIT THE SNACK BAR!", 80, 80, 1, White);
            else if ((int)(t * 3) % 3 != 0)
                Text("SHOW STARTS IN " + Mathf.CeilToInt(18 - t), 80, 80, 1, White);
            float beat = t * Mathf.PI * 2.2f;
            HotDog(34, 74 - Mathf.Abs(Mathf.Sin(beat)) * 5, beat);
            Popcorn(80, 74 - Mathf.Abs(Mathf.Sin(beat + 1)) * 5, beat + 1);
            Soda(126, 74 - Mathf.Abs(Mathf.Sin(beat + 2)) * 5, beat + 2);
        }
        /// <summary>Two kicking legs and waving arms for a dancing snack standing on <paramref name="feet"/>.</summary>
        void Limbs(float x, float feet, float hip, float shoulder, float halfWidth, float beat)
        {
            float kick = Mathf.Sin(beat) * 4;
            Line(x - 3, hip, x - 4 - kick, feet, Band);
            Line(x + 3, hip, x + 4 - kick, feet, Band);
            Rect(x - 7 - kick, feet - 1, 4, 2, Band);
            Rect(x + 3 - kick, feet - 1, 4, 2, Band);
            float wave = Mathf.Cos(beat) * 5;
            Line(x - halfWidth, shoulder, x - halfWidth - 6, shoulder - 6 + wave, Band);
            Line(x + halfWidth, shoulder, x + halfWidth + 6, shoulder - 6 - wave, Band);
        }
        void Face(float x, float y)
        {
            Disc(x - 3, y, 2.2f, White);
            Disc(x + 3, y, 2.2f, White);
            Put((int)(x - 3), (int)y, Band);
            Put((int)(x + 3), (int)y, Band);
            Line(x - 3, y + 4, x - 1, y + 5, Band);
            Line(x - 1, y + 5, x + 1, y + 5, Band);
            Line(x + 1, y + 5, x + 3, y + 4, Band);
        }
        void HotDog(float x, float feet, float beat)
        {
            Limbs(x, feet, feet - 9, feet - 16, 13, beat);
            Rect(x - 14, feet - 20, 28, 11, Bun);
            Disc(x - 14, feet - 14.5f, 5, Bun);
            Disc(x + 14, feet - 14.5f, 5, Bun);
            Rect(x - 16, feet - 23, 32, 6, Sausage);
            Disc(x - 16, feet - 20, 3, Sausage);
            Disc(x + 16, feet - 20, 3, Sausage);
            for (int i = 0; i < 6; i++)
                Line(x - 13 + i * 5, feet - 21, x - 10 + i * 5, feet - 19, Mustard);
            Face(x, feet - 15);
        }
        void Popcorn(float x, float feet, float beat)
        {
            Limbs(x, feet, feet - 9, feet - 18, 9, beat);
            for (int row = 0; row < 22; row++)
            {
                float half = 7 + row * .12f;
                for (int col = (int)-half; col <= half; col++)
                    Put((int)(x + col), (int)(feet - 9 - row), (col + 20) / 4 % 2 == 0 ? Stripe : White);
            }
            for (int i = 0; i < 7; i++)
                Disc(x - 9 + i * 3, feet - 32 - i % 2 * 2 - Mathf.Abs(Mathf.Sin(beat * 2 + i)) * 1.5f, 2.6f, Kernel);
            Rect(x - 6, feet - 25, 12, 8, White);
            Face(x, feet - 23);
        }
        void Soda(float x, float feet, float beat)
        {
            Limbs(x, feet, feet - 9, feet - 18, 9, beat);
            for (int row = 0; row < 24; row++)
            {
                float half = 6 + row * .14f;
                Rect(x - half, feet - 9 - row, half * 2 + 1, 1, CupBlue);
            }
            Rect(x - 10, feet - 35, 21, 3, White);
            Line(x + 2, feet - 35, x + 7, feet - 46, Straw);
            Line(x + 3, feet - 35, x + 8, feet - 46, Straw);
            Face(x, feet - 22);
        }

        // ---- It Came From The Valley --------------------------------------------------------------------------------

        void SciFi(float t)
        {
            if (t < 5)
                SlimeTitle(t);
            else if (t < 20)
                Abduction(t - 5);
            else if (t < 34)
                RobotAttack(t - 20);
            else if (t < 41)
                Warp(t - 34);
            else
                TheEnd(t - 41, "THE END?", SlimeGreen);
        }
        void SlimeTitle(float t)
        {
            Fill(Black);
            Stars(50, t, 17);
            Text("IT CAME FROM", 80, 16, 1, SlimeGreen);
            Text("THE VALLEY", 80, 34, 2, SlimeGreen);
            // Slime drips from the bottom of the big letters, each at its own pace.
            int bottom = 34 + 2 * PixelFont.Height - 1;
            for (int x = 0; x < Width; x++)
            {
                if (!Same(Get(x, bottom), SlimeGreen) || Hash(x * 13) % 3 != 0)
                    continue;
                float length = Mathf.Min(t * (2 + Hash(x) % 5), 6 + Hash(x * 5) % 14);
                Rect(x, bottom + 1, 1, length, SlimeGreen);
                Disc(x + .5f, bottom + 1 + length, 1.2f, SlimeGreen);
            }
        }
        void Abduction(float t)
        {
            Gradient(0, Height, Night, NightLow);
            Stars(60, t, 23);
            Disc(132, 14, 7, Moon);
            Ridge(0, .7f, 64, 4, Farm);
            Rect(0, 70, Width, Height - 70, Farm);
            // The barn and a fence.
            Rect(112, 46, 30, 22, Barn);
            Tri(new Vector2(108, 47), new Vector2(127, 34), new Vector2(146, 47), BarnDark);
            Rect(121, 54, 12, 14, BarnDark);
            Line(121, 54, 132, 67, White);
            Line(132, 54, 121, 67, White);
            for (int x = 4; x < 104; x += 8)
                Rect(x, 64, 1, 8, Trestle);
            Rect(0, 66, 104, 1, Trestle);
            float saucerY = t < 4 ? -12 + t * 8.5f : t < 12 ? 22 + Mathf.Sin(t * 3) : 22 - (t - 12) * 14;
            float saucerX = t < 12 ? 52 : 52 + (t - 12) * (t - 12) * 16;
            // The beam, then the cow floating up it, legs flailing.
            float cowY = t < 6 ? 78 : Mathf.Max(saucerY + 10, 78 - (t - 6) * 9);
            if (t > 5 && t < 11.5f)
                TrapezoidBlend(saucerX, saucerY + 4, 7, 80, 22, Beam, .35f + Mathf.Sin(t * 20) * .05f);
            if (t < 11)
                Cow(48, cowY, t, t > 6);
            Saucer(saucerX, saucerY, 1, t);
        }
        void Cow(float x, float ground, float t, bool floating)
        {
            float graze = floating ? 0 : Mathf.Max(0, Mathf.Sin(t * 1.7f)) * 3;
            for (int leg = 0; leg < 4; leg++)
            {
                float root = x - 8 + leg * 5, kick = floating ? Mathf.Sin(t * 16 + leg) * 2 : 0;
                Rect(root + kick, ground - 7, 2, 7, CowWhite);
                Put((int)(root + kick), (int)ground - 1, Band);
            }
            Rect(x - 10, ground - 15, 21, 9, CowWhite);
            Disc(x - 4, ground - 11, 2.5f, Band);
            Disc(x + 5, ground - 13, 2, Band);
            Disc(x + 1, ground - 8, 1.5f, Band);
            Rect(x + 10, ground - 17 + graze, 7, 6, CowWhite);
            Rect(x + 15, ground - 14 + graze, 3, 3, CowNose);
            Rect(x + 11, ground - 19 + graze, 1, 2, Band);
            Rect(x - 12, ground - 14, 2, 6, CowWhite);
        }
        void Saucer(float x, float y, float scale, float t)
        {
            Ellipse(x, y - 4 * scale, 8 * scale, 6 * scale, Dome);
            Ellipse(x, y, 20 * scale, 5 * scale, SaucerGrey);
            Ellipse(x, y + 2 * scale, 14 * scale, 2.5f * scale, SaucerDark);
            for (int i = 0; i < 5; i++)
                Disc(x + (i - 2) * 7 * scale, y, 1.3f * scale, Lights[(i + (int)(t * 6)) % Lights.Length]);
        }
        void RobotAttack(float t)
        {
            Gradient(0, Height, CityNight, CityLow);
            Stars(40, t, 29);
            // Searchlights sweep behind the skyline.
            for (int i = 0; i < 2; i++)
            {
                float angle = Mathf.Sin(t * .9f + i * 2) * .55f + (i == 0 ? .25f : -.25f), baseX = i == 0 ? 36 : 124;
                var tip = new Vector2(baseX + Mathf.Sin(angle) * 110, 84 - Mathf.Cos(angle) * 110);
                var side = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 9;
                TriBlend(new Vector2(baseX, 84), tip - side, tip + side, Searchlight, .22f);
            }
            float rx = -30 + t * 15, blast = t % 3.2f;
            float targetX = 20 + Hash((int)(t / 3.2f) * 7) % 120;
            Skyline(t, true);
            Robot(rx, 84, t);
            // Every few seconds its eyes fire at a rooftop, which goes up in a fireball.
            if (blast > 1.2f && blast < 1.6f)
            {
                Line(rx + 2, 31, targetX, 60, Laser);
                Line(rx + 3, 31, targetX + 1, 60, Laser);
            }
            if (blast > 1.5f && blast < 2.8f)
            {
                float age = (blast - 1.5f) / 1.3f;
                DiscBlend(targetX, 60, 3 + age * 10, Blast, 1 - age);
                DiscBlend(targetX, 60, 1 + age * 6, Yellow, 1 - age);
            }
            Skyline(t, false);
        }
        /// <summary>Towers with lit windows; the back row is drawn before the robot, the low front row after it.</summary>
        void Skyline(float t, bool back)
        {
            for (int i = 0; i < 12; i++)
            {
                uint h = Hash(i * 31 + (back ? 0 : 400));
                float width = 10 + h % 9, height = back ? 22 + h % 32 : 8 + h % 12, x = i * 14 - 4 + (back ? 0 : 7);
                float top = 84 - height;
                Rect(x, top, width, height, back ? Tower : FrontTower);
                for (float wy = top + 3; wy < 82; wy += 4)
                    for (float wx = x + 2; wx < x + width - 2; wx += 4)
                        if (Hash((int)(wx * 17 + wy * 5) + (i % 3 == 0 ? (int)(t * .5f) : 0)) % 3 != 0)
                            Rect(wx, wy, 2, 2, Lit);
            }
            if (!back)
                Rect(0, 84, Width, Height - 84, FrontTower);
        }
        void Robot(float x, float ground, float t)
        {
            float step = t * 3.4f, lift = Mathf.Max(0, Mathf.Sin(step)) * 4, lift2 = Mathf.Max(0, -Mathf.Sin(step)) * 4;
            Rect(x - 9, ground - 22 - lift, 7, 22, RobotDark);
            Rect(x + 2, ground - 22 - lift2, 7, 22, RobotDark);
            Rect(x - 10, ground - 3 - lift, 9, 3, RobotGrey);
            Rect(x + 1, ground - 3 - lift2, 9, 3, RobotGrey);
            float sway = Mathf.Sin(step);
            Rect(x - 12 + sway, ground - 46, 24, 24, RobotGrey);
            Rect(x - 7 + sway, ground - 40, 14, 8, RobotDark);
            for (int i = 0; i < 3; i++)
                Rect(x - 5 + i * 4 + sway, ground - 38, 2, 2, (i + (int)(t * 4)) % 3 == 0 ? Yellow : NeonRed);
            float arm = Mathf.Sin(step) * 6;
            Rect(x - 17 + sway, ground - 45 + arm * .3f, 5, 20, RobotDark);
            Rect(x + 12 + sway, ground - 45 - arm * .3f, 5, 20, RobotDark);
            Rect(x - 7 + sway, ground - 57, 14, 11, RobotGrey);
            Rect(x - 5 + sway, ground - 53, 10, 3, Laser);
            Rect(x + sway, ground - 62, 1, 5, RobotDark);
            Disc(x + .5f + sway, ground - 63, 1.5f, (int)(t * 3) % 2 == 0 ? NeonRed : Yellow);
        }
        /// <summary>The saucer escapes through a tunnel of streaking stars, growing as it comes at the audience.</summary>
        void Warp(float t)
        {
            Fill(Black);
            float cx = Width / 2f, cy = Height / 2f;
            for (int i = 0; i < 70; i++)
            {
                float angle = Hash(i * 3) % 628 / 100f, phase = (t * (.4f + Hash(i * 7) % 60 / 100f) + Hash(i) % 100 / 100f) % 1;
                float far = phase * phase * 110, near = far * .82f;
                var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                byte level = (byte)(80 + 175 * phase);
                Line(cx + dir.x * near, cy + dir.y * near, cx + dir.x * far, cy + dir.y * far, new Color32(level, level, (byte)Mathf.Min(255, level + 30), 255));
            }
            Saucer(cx + Mathf.Sin(t * 2) * 6, cy + Mathf.Cos(t * 1.5f) * 3, .2f + t * t * .05f, t);
        }

        // ---- Film look ----------------------------------------------------------------------------------------------

        /// <summary>Flicker, dust, the odd scratch and the vignette, while flipping the rows over for the texture.</summary>
        void Projector(float clock)
        {
            int frame = (int)(clock * 12);
            float flicker = .95f + Hash(frame) % 100 / 2000f;
            int scratchX = frame / 30 % 5 == 0 ? (int)(Hash(frame / 30) % Width) : -1;
            for (int i = 0; i < 3; i++)
            {
                uint h = Hash(frame * 5 + i);
                if (h % 4 == 0)
                    Put((int)(h / 7 % Width), (int)(h / 13 % Height), Black);
            }
            for (int y = 0; y < Height / 2; y++)
                for (int x = 0; x < Width; x++)
                {
                    int top = y * Width + x, bottom = (Height - 1 - y) * Width + x;
                    var above = pixels[top];
                    pixels[top] = Finish(pixels[bottom], x == scratchX, vignette[bottom], flicker);
                    pixels[bottom] = Finish(above, x == scratchX, vignette[top], flicker);
                }
        }
        static Color32 Finish(Color32 c, bool scratch, byte shade, float flicker)
        {
            if (scratch)
                c = new Color32((byte)((c.r + 220) / 2), (byte)((c.g + 220) / 2), (byte)((c.b + 210) / 2), 255);
            float k = shade / 255f * flicker;
            return new Color32((byte)Mathf.Min(255, c.r * k), (byte)Mathf.Min(255, c.g * k), (byte)Mathf.Min(255, c.b * k), 255);
        }

        // ---- Drawing ------------------------------------------------------------------------------------------------

        static uint Hash(int n)
        {
            uint x = (uint)n * 747796405u + 2891336453u;
            x = ((x >> (int)((x >> 28) + 4)) ^ x) * 277803737u;
            return (x >> 22) ^ x;
        }
        static bool Same(Color32 a, Color32 b) => a.r == b.r && a.g == b.g && a.b == b.b;
        Color32 Get(int x, int y) => x >= 0 && x < Width && y >= 0 && y < Height ? pixels[y * Width + x] : default;
        void Put(int x, int y, Color32 c)
        {
            if (x >= 0 && x < Width && y >= 0 && y < Height)
                pixels[y * Width + x] = c;
        }
        void Blend(int x, int y, Color32 c, float a)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height || a <= 0)
                return;
            int i = y * Width + x;
            pixels[i] = Color32.Lerp(pixels[i], c, Mathf.Clamp01(a));
        }
        void Fill(Color32 c)
        {
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = c;
        }
        void Darken(float amount)
        {
            float k = 1 - Mathf.Clamp01(amount);
            for (int i = 0; i < pixels.Length; i++)
            {
                var p = pixels[i];
                pixels[i] = new Color32((byte)(p.r * k), (byte)(p.g * k), (byte)(p.b * k), 255);
            }
        }
        void Gradient(int from, int to, Color32 top, Color32 bottom)
        {
            for (int y = Mathf.Max(0, from); y < Mathf.Min(Height, to); y++)
            {
                var c = Color32.Lerp(top, bottom, (y - from) / (float)Mathf.Max(1, to - from - 1));
                for (int x = 0; x < Width; x++)
                    pixels[y * Width + x] = c;
            }
        }
        void Rect(float x, float y, float w, float h, Color32 c)
        {
            int x0 = Mathf.Max(0, Mathf.RoundToInt(x)), y0 = Mathf.Max(0, Mathf.RoundToInt(y));
            int x1 = Mathf.Min(Width, Mathf.RoundToInt(x + w)), y1 = Mathf.Min(Height, Mathf.RoundToInt(y + h));
            for (int yy = y0; yy < y1; yy++)
                for (int xx = x0; xx < x1; xx++)
                    pixels[yy * Width + xx] = c;
        }
        void Disc(float cx, float cy, float r, Color32 c) => Ellipse(cx, cy, r, r, c);
        void Ellipse(float cx, float cy, float rx, float ry, Color32 c)
        {
            if (rx <= 0 || ry <= 0)
                return;
            for (int y = Mathf.FloorToInt(cy - ry); y <= Mathf.CeilToInt(cy + ry); y++)
                for (int x = Mathf.FloorToInt(cx - rx); x <= Mathf.CeilToInt(cx + rx); x++)
                {
                    float dx = (x + .5f - cx) / rx, dy = (y + .5f - cy) / ry;
                    if (dx * dx + dy * dy <= 1)
                        Put(x, y, c);
                }
        }
        void DiscBlend(float cx, float cy, float r, Color32 c, float a)
        {
            for (int y = Mathf.FloorToInt(cy - r); y <= Mathf.CeilToInt(cy + r); y++)
                for (int x = Mathf.FloorToInt(cx - r); x <= Mathf.CeilToInt(cx + r); x++)
                {
                    float dx = x + .5f - cx, dy = y + .5f - cy;
                    if (dx * dx + dy * dy <= r * r)
                        Blend(x, y, c, a);
                }
        }
        void Ring(float cx, float cy, float r, float thickness, Color32 c)
        {
            for (int y = Mathf.FloorToInt(cy - r - 1); y <= Mathf.CeilToInt(cy + r + 1); y++)
                for (int x = Mathf.FloorToInt(cx - r - 1); x <= Mathf.CeilToInt(cx + r + 1); x++)
                {
                    float dx = x + .5f - cx, dy = y + .5f - cy;
                    if (Mathf.Abs(Mathf.Sqrt(dx * dx + dy * dy) - r) <= thickness / 2)
                        Put(x, y, c);
                }
        }
        void Line(float x0, float y0, float x1, float y1, Color32 c)
        {
            int steps = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0))));
            for (int i = 0; i <= steps; i++)
            {
                float k = i / (float)steps;
                Put(Mathf.FloorToInt(Mathf.Lerp(x0, x1, k)), Mathf.FloorToInt(Mathf.Lerp(y0, y1, k)), c);
            }
        }
        void Tri(Vector2 a, Vector2 b, Vector2 c, Color32 col) => Triangle(a, b, c, col, 1, false);
        void TriBlend(Vector2 a, Vector2 b, Vector2 c, Color32 col, float alpha) => Triangle(a, b, c, col, alpha, true);
        void Triangle(Vector2 a, Vector2 b, Vector2 c, Color32 col, float alpha, bool blend)
        {
            int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x)))), x1 = Mathf.Min(Width - 1, Mathf.CeilToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x))));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, Mathf.Min(b.y, c.y)))), y1 = Mathf.Min(Height - 1, Mathf.CeilToInt(Mathf.Max(a.y, Mathf.Max(b.y, c.y))));
            float area = Edge(a, b, c);
            if (Mathf.Abs(area) < 1e-3f)
                return;
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    var p = new Vector2(x + .5f, y + .5f);
                    if (Edge(b, c, p) / area < 0 || Edge(c, a, p) / area < 0 || Edge(a, b, p) / area < 0)
                        continue;
                    if (blend)
                        Blend(x, y, col, alpha);
                    else
                        pixels[y * Width + x] = col;
                }
        }
        static float Edge(Vector2 a, Vector2 b, Vector2 p) => (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);
        /// <summary>A light beam: a trapezoid from a narrow top to a wide bottom, blended over the picture.</summary>
        void TrapezoidBlend(float cx, float top, float topHalf, float bottom, float bottomHalf, Color32 c, float alpha)
        {
            for (int y = Mathf.Max(0, (int)top); y < Mathf.Min(Height, (int)bottom); y++)
            {
                float half = Mathf.Lerp(topHalf, bottomHalf, (y - top) / (bottom - top));
                for (int x = (int)(cx - half); x <= (int)(cx + half); x++)
                    Blend(x, y, c, alpha);
            }
        }
        /// <summary>Rolling hills: each column filled from a height made of two sines down to the bottom of the frame.</summary>
        void Ridge(float scroll, float roughness, float baseY, float amplitude, Color32 c)
        {
            for (int x = 0; x < Width; x++)
            {
                float u = (x + scroll) / 40f;
                float top = baseY - amplitude * (1 + Mathf.Sin(u * 1.3f) * .8f + Mathf.Sin(u * 3.7f + 1) * .4f * roughness);
                Rect(x, top, 1, Height - top, c);
            }
        }
        void Stars(int count, float t, int seed)
        {
            for (int i = 0; i < count; i++)
            {
                uint h = Hash(i * 97 + seed);
                int x = (int)(h % Width), y = (int)(h / Width % 60);
                float twinkle = Mathf.Sin(t * (1 + h % 5) + h % 13);
                byte level = (byte)(150 + 100 * Mathf.Clamp01(twinkle));
                Put(x, y, new Color32(level, level, (byte)Mathf.Min(255, level + 20), 255));
            }
        }
        /// <summary>A line of text centred on <paramref name="cx"/>, its top at <paramref name="top"/>, each font pixel <paramref name="scale"/> pixels.</summary>
        void Text(string text, float cx, float top, int scale, Color32 c)
        {
            int width = PixelFont.Measure(text) * scale;
            int left = Mathf.RoundToInt(cx - width / 2f), y0 = Mathf.RoundToInt(top);
            for (int i = 0; i < text.Length; i++)
            {
                var glyph = PixelFont.Rows(text[i]);
                if (glyph == null)
                    continue;
                int x0 = left + i * PixelFont.Advance * scale;
                for (int row = 0; row < PixelFont.Height; row++)
                    for (int col = 0; col < PixelFont.Width; col++)
                        if (PixelFont.Lit(glyph, col, row))
                            Rect(x0 + col * scale, y0 + row * scale, scale, scale, c);
            }
        }
    }
}
