using System.Collections.Generic;
namespace ValleyRail.Core
{
    /// <summary>
    /// Guided first-delivery steps, then goals that pay a cash reward and teach the next system: passengers, a supply chain,
    /// its goods, light rail, then growing the company to $1,000,000. The step index lives in WorldState so it survives saves; 0 means finished or disabled.
    /// Every condition is checked against world state, so the guide fast-forwards through anything the player already did.
    /// </summary>
    public static class Tutorial
    {
        public const int GuidedSteps = 5, FirstGoal = 6, LastGoal = 14, DeliveryGoal = 300, FleetGoal = 4, FreightGoal = 2000, BankGoal = 1000000;
        static readonly string[] Texts =
        {
            null,
            "1/5  Tap AUTO ROUTE: pick Pinecrest Mine, then a power station. (By hand: BUILD → TRACK.)",
            "2/5  BUILD → STATION: tap straight track beside an industry at each end of your line. (Or use AUTO ROUTE.)",
            "3/5  Tap × DONE, then tap the station at the mine and buy a train.",
            "4/5  Choose the train's destination, or tap AUTO DESTINATION.",
            "5/5  Tap the speed button for 2x or 4x. Freight pays when it unloads at its destination.",
            "GOAL 1/9 · ${0:N0}  Delivered! Keep it running: deliver {1} units in total ({2} so far).",
            "GOAL 2/9 · ${0:N0}  Carry passengers: AUTO ROUTE → Willowbrook → Oakridge. Pays on the first arrival; towns grow when trains serve them.",
            "GOAL 3/9 · ${0:N0}  Start a supply chain: AUTO ROUTE → Pinewood Forest → Westwood Sawmill. Each load of wood becomes goods.",
            "GOAL 4/9 · ${0:N0}  Finish the chain: AUTO ROUTE → Westwood Sawmill → a town, and deliver its goods. Goods pay well over long lines.",
            "GOAL 5/9 · ${0:N0}  LIGHT RAIL is open: BUILD → LIGHT RAIL and pick a stadium, beach or ski resort near a town station.",
            "GOAL 6/9 · ${0:N0}  Grow the company: run 4 trains at once. Each needs its own line; AUTO ROUTE builds one.",
            "GOAL 7/9 · ${0:N0}  Grow a town into a Small Town. Passenger and goods trains make towns grow.",
            "GOAL 8/9 · ${0:N0}  Deliver 2,000 units in total ({2:N0} so far). Longer trains carry more: tap a train, then WAGONS.",
            "GOAL 9/9 · ${0:N0}  Bank $1,000,000: enough to buy a town's services and earn from every resident.",
        };
        /// <summary>Cash paid when each goal is met, by step.</summary>
        static readonly int[] Rewards = { 0, 0, 0, 0, 0, 0, 8000, 8000, 6000, 8000, 10000, 20000, 30000, 50000, 100000 };
        public static int Reward(int step) => step >= 0 && step < Rewards.Length ? Rewards[step] : 0;
        public static string Text(WorldState w)
        {
            int s = w.tutorialStep;
            if (s <= 0 || s >= Texts.Length)
                return null;
            return s < FirstGoal ? Texts[s] : string.Format(Texts[s], Reward(s), DeliveryGoal, w.delivered);
        }
        /// <summary>
        /// The line the current step asks for, as two place names, so AUTO ROUTE can offer it in one tap. False when the step
        /// names no line (or the goal is not about building one).
        /// </summary>
        public static bool Suggested(WorldState w, out string from, out string to)
        {
            from = to = null;
            switch (w.tutorialStep)
            {
                case 1: case 2: case 3: case 4: from = "Pinecrest Mine"; to = "Westvale Power"; break;
                // Once a train runs for the goal, the line is bought: nothing more to suggest while it makes its first delivery.
                case 7: if (!Running(w.trains, Cargo.Passengers)) { from = "Willowbrook"; to = "Oakridge"; } break;
                case 8: if (!Running(w.trains, Cargo.Wood) && !Running(w.trains, Cargo.Oil) && !Running(w.trains, Cargo.IronOre)) { from = "Pinewood Forest"; to = "Westwood Sawmill"; } break;
                case 9: if (!Running(w.trains, Cargo.Goods) && !Running(w.trains, Cargo.Steel)) { from = "Westwood Sawmill"; to = "Willowbrook"; } break;
            }
            return from != null;
        }
        /// <summary>Moves past every step already done, paying each goal's reward; returns the cash paid.</summary>
        public static int Advance(WorldState w)
        {
            int paid = 0;
            while (w.tutorialStep > 0 && Complete(w, w.tutorialStep))
            {
                int reward = Reward(w.tutorialStep);
                if (reward > 0)
                    EconomyService.Credit(w, reward, false);
                paid += reward;
                w.tutorialStep = w.tutorialStep + 1 < Texts.Length ? w.tutorialStep + 1 : 0;
            }
            return paid;
        }
        static bool Complete(WorldState w, int step)
        {
            switch (step)
            {
                case 1: return w.tracks.Count > 0;
                case 2: return w.stations.Count >= 2;
                case 3: return w.trains.Count >= 1;
                case 4: return Running(w.trains, null);
                case 5: return w.delivered > 0;
                case 6: return w.delivered >= DeliveryGoal;
                // Line goals pay on that cargo's first delivery, not on the purchase.
                case 7: return Earning(w, Cargo.Passengers);
                case 8: return Earning(w, Cargo.Wood) || Earning(w, Cargo.Oil) || Earning(w, Cargo.IronOre);
                case 9: return Earning(w, Cargo.Goods) || Earning(w, Cargo.Steel);
                case 10: return w.tramLines.Count > 0;
                case 11: return w.trains.FindAll(t => t.a != 0).Count >= FleetGoal;
                case 12: return w.cities.Exists(c => c.level >= CityLevel.SmallTown);
                case 13: return w.delivered >= FreightGoal;
                case 14: return w.money >= BankGoal;
                default: return true;
            }
        }
        /// <summary>True when a train carrying <paramref name="cargo"/> has been paid for a delivery in the last game year.</summary>
        static bool Earning(WorldState w, Cargo cargo)
        {
            foreach (var t in w.trains)
                if (t.cargo == cargo && t.accounts.LastYear(w.tick).income > 0)
                    return true;
            return false;
        }
        /// <summary>True when a train has a route, carrying <paramref name="cargo"/> (any cargo when null).</summary>
        static bool Running(List<TrainState> trains, Cargo? cargo)
        {
            foreach (var t in trains)
                if (t.a != 0 && (cargo == null || t.cargo == cargo))
                    return true;
            return false;
        }
    }
}
