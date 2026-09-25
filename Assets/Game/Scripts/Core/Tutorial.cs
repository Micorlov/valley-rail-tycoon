using System.Collections.Generic;
namespace ValleyRail.Core
{
    /// <summary>
    /// Guided first-delivery steps. The step index lives in WorldState so it survives saves; 0 means finished or disabled.
    /// Every condition is checked against world state, so the guide fast-forwards through anything the player already did.
    /// </summary>
    public static class Tutorial
    {
        public const int GuidedSteps = 5;
        static readonly string[] Texts =
        {
            null,
            "1/5  BUILD → TRACK, then drag from (8,15) to (50,15) across the south bridge site.",
            "2/5  BUILD → STATION: tap (10,15) and (48,15), then choose the industry.",
            "3/5  Tap × DONE, then tap the mine station and buy a Small freight · Coal.",
            "4/5  Choose the train's destination: Eastbank Power Station.",
            "5/5  Tap the speed button for 2x or 4x. Freight pays when it unloads at the power plant.",
            "Delivered! Next: goods from Valley Works to a town, passengers between towns."
        };
        public static string Text(WorldState w) => w.tutorialStep > 0 && w.tutorialStep < Texts.Length ? Texts[w.tutorialStep] : null;
        public static void Advance(WorldState w)
        {
            while (w.tutorialStep > 0 && Complete(w, w.tutorialStep))
                w.tutorialStep = w.tutorialStep + 1 < Texts.Length ? w.tutorialStep + 1 : 0;
        }
        static bool Complete(WorldState w, int step)
        {
            switch (step)
            {
                case 1: return w.tracks.Count > 0;
                case 2: return w.stations.Count >= 2;
                case 3: return w.trains.Count >= 1;
                case 4: return HasRoute(w.trains);
                case 5: return w.delivered > 0;
                case 6: return w.delivered >= 100;
                default: return true;
            }
        }
        static bool HasRoute(List<TrainState> trains)
        {
            foreach (var t in trains)
                if (t.a != 0)
                    return true;
            return false;
        }
    }
}
