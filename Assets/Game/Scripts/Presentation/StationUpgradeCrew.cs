using System.Collections.Generic;
using UnityEngine;
using ValleyRail.Core;
namespace ValleyRail
{
    /// <summary>
    /// The upgrade show's supporting cast. An orange dump truck reverses up to the site while the bulldozer works, takes
    /// the rubble and drives off; a cement mixer backs in when building starts, its drum turning until the station is up.
    /// A crew of five in hard hats and hi-vis vests: two wave the bulldozer through, then everyone walks onto the site,
    /// works up and down the platforms and walks back to the lorry at the end. Both trucks use the lane of the building
    /// strip just past either end of the site.
    /// </summary>
    public sealed partial class WorldView
    {
        const int CrewSize = 5, Flaggers = 2;
        const float WorkerScale = 2.5f, TruckSpeed = 2.2f, CrewArrive = .1f, CrewLeave = .86f;
        sealed class Worker
        {
            public Transform view; public Vector3 home, a, b; public float phase;
        }
        sealed class Crew
        {
            public Transform dumpTruck, dumpLoad, mixer, drum; public Vector3 middle;
            public readonly List<Waypoint> dumpRoute = new List<Waypoint>(), mixerRoute = new List<Waypoint>();
            public readonly List<Worker> workers = new List<Worker>();
        }
        void HireCrew(UpgradeShow show, StationState s)
        {
            int length = StationLayout.Length(s), platforms = StationLayout.Platforms(s);
            var along = s.axis == 1 ? Vector3.right : Vector3.forward;
            var across = new Vector3(Directions.Dx[s.side], 0, Directions.Dz[s.side]);
            var crew = new Crew { middle = new Vector3(s.cell.x, 0, s.cell.z) + along * StationLayout.Middle(length) };
            show.crew = crew;
            var front = crew.middle + across - along * (length * .5f + 1.1f);
            var back = crew.middle + across + along * (length * .5f + 1.1f);
            crew.dumpTruck = BuildDumpTruck(show.root, out crew.dumpLoad);
            crew.mixer = BuildMixer(show.root, out crew.drum);
            // Tippers and mixers reverse up to the work, then drive away forwards.
            if (show.wrecks.Count > 0)
                Visit(crew.dumpRoute, front - along * 6, front, 0, show.buildStart);
            Visit(crew.mixerRoute, back + along * 6, back, show.buildStart - 1.2f, show.end - 2.8f);
            Color[] vests = { new Color(1f, .5f, .08f), new Color(.88f, .95f, .2f) }, hats = { new Color(.96f, .96f, .94f), new Color(1f, .8f, .1f) };
            for (int i = 0; i < CrewSize; i++)
            {
                var view = CityLife.Figure(this, show.root, "Site worker", vests[i % 2], WorkerScale);
                Box("Hard hat", new Vector3(0, .152f, 0) * WorkerScale, new Vector3(.042f, .016f, .042f) * WorkerScale, hats[(i / 2) % 2], view);
                var home = i < Flaggers ? front + across * (.55f + .3f * i) + along * .3f : back + across * (.5f + .25f * (i - Flaggers)) - along * .25f;
                // Work spots on bare ground: outside the building strip, or beyond the last track.
                float spot = (i + .5f) / CrewSize * length - length * .5f;
                var a = crew.middle + along * spot + (i % 2 == 0 ? across * 1.66f : -across * (platforms - 1 + .64f));
                crew.workers.Add(new Worker { view = view, home = home, a = a, b = a + along * .55f, phase = i * 1.7f });
            }
        }
        /// <summary>A truck's visit: reverse in from <paramref name="from"/> to <paramref name="park"/>, wait there until <paramref name="leave"/>, then drive back out forwards.</summary>
        static void Visit(List<Waypoint> route, Vector3 from, Vector3 park, float arrive, float leave)
        {
            float drive = Vector3.Distance(from, park) / TruckSpeed;
            route.Add(new Waypoint { at = from, time = arrive, hit = -1 });
            route.Add(new Waypoint { at = park, time = arrive + drive, hit = -1, reverse = true });
            route.Add(new Waypoint { at = park, time = Mathf.Max(leave, arrive + drive + .1f), hit = -1 });
            route.Add(new Waypoint { at = from + (from - park).normalized, time = Mathf.Max(leave, arrive + drive + .1f) + drive, hit = -1 });
        }
        void AnimateCrew(UpgradeShow show, float time)
        {
            var crew = show.crew;
            Follow(crew.dumpRoute, time, crew.dumpTruck);
            crew.dumpLoad.gameObject.SetActive(time >= show.buildStart - .4f);
            Follow(crew.mixerRoute, time, crew.mixer);
            crew.drum.localRotation = Quaternion.Euler(0, 0, time * 140f);
            float build = (time - show.buildStart) / BuildSeconds;
            for (int i = 0; i < crew.workers.Count; i++)
            {
                var w = crew.workers[i];
                Vector3 at, look;
                bool visible = true;
                if (build < 0)
                {
                    // Flaggers watch the bulldozer; the rest are still on their way in the mixer.
                    visible = i < Flaggers;
                    at = w.home + Vector3.up * Mathf.Abs(Mathf.Sin(time * 5f + w.phase)) * .015f;
                    look = show.dozer.root.localPosition - w.home;
                }
                else if (build < CrewArrive)
                {
                    at = Vector3.Lerp(w.home, w.a, build / CrewArrive);
                    look = w.a - w.home;
                }
                else if (build < CrewLeave)
                {
                    // Pace a short stretch, stopping at each end to hammer.
                    float u = Mathf.PingPong(time * .45f + w.phase, 1f), pace = Mathf.SmoothStep(0, 1, u);
                    at = Vector3.Lerp(w.a, w.b, pace) + Vector3.up * Mathf.Abs(Mathf.Sin(time * 14f + w.phase)) * .02f * (1 - Mathf.Sin(u * Mathf.PI));
                    look = (w.b - w.a) * (Mathf.Repeat(time * .45f + w.phase, 2f) < 1f ? 1 : -1);
                }
                else
                {
                    float f = (build - CrewLeave) / (.97f - CrewLeave);
                    visible = f < 1;
                    at = Vector3.Lerp(w.a, w.home, f);
                    look = w.home - w.a;
                }
                w.view.gameObject.SetActive(visible);
                w.view.localPosition = at;
                look.y = 0;
                if (look.sqrMagnitude > .0001f)
                    w.view.localRotation = Quaternion.LookRotation(look);
            }
        }
        /// <summary>
        /// Moves a vehicle along a timed route, facing where it drives (or away from it on a leg marked reverse). It is hidden
        /// before the route starts and after it ends; true while it is out.
        /// </summary>
        static bool Follow(List<Waypoint> route, float time, Transform body)
        {
            bool moving = route.Count > 1 && time >= route[0].time && time < route[route.Count - 1].time;
            body.gameObject.SetActive(moving);
            if (!moving)
                return false;
            int k = 0;
            while (k + 2 < route.Count && route[k + 1].time <= time)
                k++;
            var from = route[k];
            var to = route[k + 1];
            body.localPosition = Vector3.Lerp(from.at, to.at, Mathf.InverseLerp(from.time, to.time, time)) + Vector3.up * .02f;
            var heading = to.at - from.at;
            heading.y = 0;
            if (heading.sqrMagnitude > .0001f)
                body.localRotation = Quaternion.LookRotation(to.reverse ? -heading : heading);
            return true;
        }
    }
}
