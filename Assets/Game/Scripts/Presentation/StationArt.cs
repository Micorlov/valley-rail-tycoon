using UnityEngine;
using ValleyRail.Core;
namespace ValleyRail
{
    /// <summary>
    /// Station buildings by rung (StationCatalog). Every station keeps DrawStation's platforms and canopies; upgraded ones add
    /// a hall on the building strip: a brick Town station with a clock, a stone Central station under a glass train shed, a
    /// Grand terminal with a clock tower, flags and a ribbed glass hall over every track, a timber Freight depot with a
    /// loading crane, or a Freight yard with silos, containers and a gantry crane spanning the tracks.
    /// </summary>
    public sealed partial class WorldView
    {
        /// <summary>The station's main hall: where it stands on the strip, its size and its walls. Size 0 for halts.</summary>
        (Vector3 center, Vector3 size, Color color) StationHall(StationState s)
        {
            if (s.level <= 0)
                return (Vector3.zero, Vector3.zero, Cream);
            bool town = StationCatalog.Town(game.Cargo.Producer(s.producerId).kind);
            int length = StationLayout.Length(s);
            var along = s.axis == 1 ? Vector3.right : Vector3.forward;
            var across = new Vector3(Directions.Dx[s.side], 0, Directions.Dz[s.side]);
            var sideways = Vector3.one - along - Vector3.up;
            float run = Mathf.Min(length - .8f, .9f + .45f * s.level), height = town ? .45f + .3f * s.level : .5f + .2f * s.level;
            var center = new Vector3(s.cell.x, .3f + height / 2, s.cell.z) + along * StationLayout.Middle(length) + across * 1.12f;
            var color = town ? (s.level == 1 ? Brick : s.level == 2 ? Stone : White) : (s.level == 1 ? Timber : Slate);
            return (center, along * run + sideways * .56f + Vector3.up * height, color);
        }
        // Painted ironwork of the train sheds: light, so the frame reads as a canopy rather than bars over the trains.
        static readonly Color ShedSteel = new Color(.7f, .74f, .74f);
        void DrawStationBuilding(StationState s)
        {
            if (s.level <= 0)
                return;
            var hall = StationHall(s);
            bool town = StationCatalog.Town(game.Cargo.Producer(s.producerId).kind);
            int length = StationLayout.Length(s), platforms = StationLayout.Platforms(s);
            var along = s.axis == 1 ? Vector3.right : Vector3.forward;
            var across = new Vector3(Directions.Dx[s.side], 0, Directions.Dz[s.side]);
            var sideways = Vector3.one - along - Vector3.up;
            var turn = Quaternion.LookRotation(across);
            var ridge = s.axis == 1 ? Quaternion.identity : Quaternion.Euler(0, 90, 0);
            float run = Vector3.Dot(hall.size, along), top = hall.center.y + hall.size.y / 2;
            // Middle of the tracks, and the width across all of them, for train sheds and gantries.
            var tracks = new Vector3(s.cell.x, 0, s.cell.z) + along * StationLayout.Middle(length) - across * ((platforms - 1) * .5f);
            float span = platforms + .9f;
            Box("Station hall", hall.center, hall.size, hall.color, stationRoot);
            // Windows and a door on both long faces (the town side and the platform side).
            for (int face = -1; face <= 1; face += 2)
            {
                var wall = hall.center + across * face * (hall.size.magnitude > 0 ? Vector3.Dot(hall.size, sideways) / 2 + .005f : 0);
                Box("Station door", new Vector3(wall.x, .3f + .17f, wall.z), along * .16f + sideways * .02f + Vector3.up * .34f, Timber, stationRoot);
                for (int i = -1; i <= 1; i += 2)
                    Box("Station window", new Vector3(wall.x, top - .22f, wall.z) + along * i * run * .3f, along * .14f + sideways * .02f + Vector3.up * .16f, Navy, stationRoot);
            }
            if (town)
                DrawPassengerStation(s.level, hall, along, across, ridge, turn, top, run, length, tracks, span);
            else
                DrawFreightStation(s.level, hall, along, across, ridge, top, run, length, tracks, span);
        }
        void DrawPassengerStation(int level, (Vector3 center, Vector3 size, Color color) hall, Vector3 along, Vector3 across, Quaternion ridge, Quaternion turn, float top, float run, int length, Vector3 tracks, float span)
        {
            var roofTop = new Vector3(hall.center.x, top, hall.center.z);
            if (level == 1)
            {
                Shape("Station roof", prism, roofTop, new Vector3(run + .1f, .3f, .7f), Terracotta, stationRoot, ridge);
                Clock(roofTop + across * .36f - Vector3.up * .12f, turn);
                return;
            }
            var sideways = Vector3.one - along - Vector3.up;
            Box("Cornice", roofTop + Vector3.up * .03f, along * (run + .08f) + sideways * (Vector3.Dot(hall.size, sideways) + .08f) + Vector3.up * .06f, Cream, stationRoot);
            Clock(roofTop + across * .3f - Vector3.up * .15f, turn);
            // An open train shed over every platform: arched ribs, eaves and a glazed ridge, so trains stay visible below.
            float eaves = level == 2 ? 1.35f : 1.5f, rise = level == 2 ? .4f : .65f, half = span / 2;
            float slope = Mathf.Sqrt(half * half + rise * rise), angle = Mathf.Atan2(rise, half) * Mathf.Rad2Deg;
            for (int i = 0; i <= length; i += level == 2 ? 2 : 1)
            {
                var foot = new Vector3(tracks.x, eaves, tracks.z) + along * (i - length / 2f);
                for (int face = -1; face <= 1; face += 2)
                {
                    var mid = foot + sideways * face * half / 2 + Vector3.up * rise / 2;
                    // Each rib climbs from its eaves (outer end) to the ridge (inner end).
                    var tilt = Quaternion.AngleAxis(-face * angle * RaiseSign(along, sideways), along);
                    Box("Shed rib", mid, sideways * slope + along * .035f + Vector3.up * .035f, ShedSteel, stationRoot, tilt);
                    Box("Shed pillar", foot + sideways * face * half - Vector3.up * (eaves - .3f) / 2, new Vector3(.04f, eaves - .3f, .04f), ShedSteel, stationRoot);
                }
            }
            for (int face = -1; face <= 1; face += 2)
                Box("Shed eaves", new Vector3(tracks.x, eaves, tracks.z) + sideways * face * half, along * length + sideways * .04f + Vector3.up * .04f, ShedSteel, stationRoot);
            Box("Glazed ridge", new Vector3(tracks.x, eaves + rise, tracks.z), along * length + sideways * .34f + Vector3.up * .04f, Glass, stationRoot);
            if (level < 3)
                return;
            // Grand terminal: a clock tower rising from the hall, and flags on its corners.
            var tower = roofTop + along * (run / 2 - .2f);
            Box("Clock tower", tower + Vector3.up * .5f, new Vector3(.36f, 1f, .36f), hall.color, stationRoot);
            Shape("Tower roof", pyramid, tower + Vector3.up * 1f, new Vector3(.46f, .45f, .46f), Slate, stationRoot);
            Clock(tower + Vector3.up * .72f + across * .19f, turn);
            Clock(tower + Vector3.up * .72f - across * .19f, turn * Quaternion.Euler(0, 180, 0));
            for (int i = -1; i <= 1; i += 2)
            {
                var pole = roofTop - along * i * (run / 2 - .12f);
                Box("Flag pole", pole + Vector3.up * .35f, new Vector3(.025f, .7f, .025f), Cream, stationRoot);
                Box("Flag", pole + Vector3.up * .6f + along * .1f, along * .2f + (Vector3.one - along - Vector3.up) * .01f + Vector3.up * .13f, Red, stationRoot);
            }
        }
        /// <summary>+1 when a positive turn about <paramref name="along"/> lifts the <paramref name="sideways"/> end of a beam, else -1.</summary>
        static float RaiseSign(Vector3 along, Vector3 sideways) => Vector3.Dot(Vector3.Cross(along, sideways), Vector3.up) >= 0 ? 1f : -1f;
        void DrawFreightStation(int level, (Vector3 center, Vector3 size, Color color) hall, Vector3 along, Vector3 across, Quaternion ridge, float top, float run, int length, Vector3 tracks, float span)
        {
            Color crane = new Color(.98f, .78f, .15f), steel = new Color(.42f, .42f, .44f);
            var roofTop = new Vector3(hall.center.x, top, hall.center.z);
            Shape("Shed roof", prism, roofTop, new Vector3(run + .1f, .22f, .66f), level == 1 ? Slate : steel, stationRoot, ridge);
            Box("Loading dock", hall.center - across * .36f - Vector3.up * (hall.size.y / 2 - .06f), along * run + (Vector3.one - along - Vector3.up) * .16f + Vector3.up * .12f, Stone, stationRoot);
            var end = new Vector3(hall.center.x, .3f, hall.center.z) + along * (run / 2 + .35f);
            if (level == 1)
            {
                // A small jib crane beside the shed.
                Box("Crane post", end + Vector3.up * .5f, new Vector3(.07f, 1f, .07f), crane, stationRoot);
                Box("Crane jib", end + Vector3.up * .98f - across * .35f, (Vector3.one - along - Vector3.up) * .75f + along * .06f + Vector3.up * .06f, crane, stationRoot);
                Box("Hook line", end + Vector3.up * .75f - across * .65f, new Vector3(.012f, .45f, .012f), Slate, stationRoot);
                return;
            }
            // Freight yard: silos at one end, stacked containers at the other and a gantry crane across every track.
            var silos = new Vector3(hall.center.x, .3f, hall.center.z) - along * (run / 2 + .45f);
            for (int i = 0; i < 2; i++)
            {
                var at = silos - along * i * .42f;
                Box("Silo", at + Vector3.up * .55f, new Vector3(.36f, 1.1f, .36f), new Color(.8f, .8f, .78f), stationRoot);
                Shape("Silo top", at + Vector3.up * 1.1f, new Vector3(.28f, .22f, .28f), steel, stationRoot);
            }
            Color[] boxes = { new Color(.75f, .2f, .15f), new Color(.15f, .4f, .65f), new Color(.2f, .55f, .3f), Gold };
            for (int i = 0; i < 4; i++)
                Box("Container", end + Vector3.up * (.1f + (i / 2) * .2f) + along * ((i % 2) * .05f), along * .5f + (Vector3.one - along - Vector3.up) * .22f + Vector3.up * .19f, boxes[i], stationRoot);
            var gantry = tracks + along * (length / 2f - 1.2f);
            float half = span / 2;
            for (int i = -1; i <= 1; i += 2)
                Box("Gantry leg", gantry + Vector3.up * .8f + (Vector3.one - along - Vector3.up) * i * half, new Vector3(.08f, 1.6f, .08f), crane, stationRoot);
            Box("Gantry beam", gantry + Vector3.up * 1.6f, (Vector3.one - along - Vector3.up) * (span + .1f) + along * .12f + Vector3.up * .12f, crane, stationRoot);
            Box("Gantry trolley", gantry + Vector3.up * 1.5f, new Vector3(.22f, .14f, .22f), Slate, stationRoot);
        }
        void Clock(Vector3 at, Quaternion facing)
        {
            Box("Clock face", at, new Vector3(.2f, .2f, .02f), Cream, stationRoot, facing);
            Box("Clock hands", at + facing * Vector3.forward * .012f, new Vector3(.02f, .12f, .01f), Navy, stationRoot, facing * Quaternion.Euler(0, 0, 35));
        }
    }
}
