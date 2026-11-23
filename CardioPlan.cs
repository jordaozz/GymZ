using System.Collections.Generic;
using System.Linq;

namespace GymZ
{
    internal sealed partial class Training
    {
        public static bool WantsAutoCardio(Profile profile)
        {
            return profile.Cardio != "none" && (profile.Goal == "weight" || profile.Goal == "recomp" || profile.Goal == "fitness" || profile.Goal == "endurance");
        }

        private static bool AutoCardio(Profile profile, int slot)
        {
            if (slot < 0 || !WantsAutoCardio(profile)) return false;
            if (profile.Goal == "endurance" || profile.Goal == "weight") return true;
            if (profile.Days.Length <= 2) return true;
            return slot % 2 == 0;
        }

        private static string CardioDuration(Profile profile, bool legDay)
        {
            if (profile.Minutes <= 30) return profile.Goal == "endurance" ? "10–15 min" : "6–10 min";
            if (profile.Goal == "endurance") return profile.Level == 0 || profile.Minutes <= 45 ? "12–18 min" : "18–25 min";
            if (legDay || profile.Goal == "recomp") return profile.Level == 0 ? "8–12 min" : "10–15 min";
            return profile.Level == 0 || profile.Minutes <= 45 ? "10–15 min" : "15–25 min";
        }

        private Exercise PickCardio(Profile profile, int slot, bool legDay, Dictionary<string, ExerciseUse> familiarity)
        {
            var allowed = Catalog.All.Where(e => e.Type == 'A' && Eligible(e, profile)).ToArray();
            if (allowed.Length == 0) return null;
            // The lighter leg-day block uses a seated option when the profile allows it.
            if (legDay && profile.Goal != "endurance")
            {
                var bike = allowed.FirstOrDefault(e => e.Id == "bike" && familiarity[e.Id].ChoiceScore >= 0);
                if (bike != null) return bike;
            }
            int score = allowed.Max(e => familiarity[e.Id].ChoiceScore);
            var matches = allowed.Where(e => familiarity[e.Id].ChoiceScore == score).ToArray();
            int rotation = profile.Goal == "fitness" || profile.Goal == "recomp" ? slot / 2 : slot;
            return matches[rotation % matches.Length];
        }

        public static void OrderCardio(Session session)
        {
            session.Items = session.Items.OrderBy(item => item.CardioPhase == "before" ? -1 : item.CardioPhase == "after" ? 1 : 0).ToList();
        }
    }
}
