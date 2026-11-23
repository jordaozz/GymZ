using System;
using System.Collections.Generic;
using System.Linq;

namespace GymZ
{
    internal sealed partial class Training
    {
        private sealed class SplitDay
        {
            public readonly string Name;
            public readonly string[] Groups;

            public SplitDay(string name, params string[] groups)
            {
                Name = name;
                Groups = groups;
            }
        }

        private static readonly Dictionary<string, SplitDay> SplitDays = new Dictionary<string, SplitDay> {
            { "push", new SplitDay("Peito e tríceps", "chest", "chest", "triceps", "triceps", "chest", "triceps") },
            { "pull", new SplitDay("Costas e bíceps", "back", "biceps", "back", "biceps", "back", "biceps") },
            { "legs", new SplitDay("Pernas completas", "quads", "hamstrings", "glutes", "calves", "adductors", "quads") },
            { "shoulders", new SplitDay("Ombros e abdômen", "shoulders", "core", "shoulders", "core", "shoulders", "core") },
            { "posterior", new SplitDay("Glúteos e posteriores", "glutes", "hamstrings", "glutes", "calves", "adductors", "core") },
            { "push-shoulders", new SplitDay("Peito, ombros e tríceps", "chest", "shoulders", "triceps", "chest", "shoulders", "triceps") },
            { "chest-biceps", new SplitDay("Peito e bíceps", "chest", "biceps", "chest", "biceps", "chest", "biceps") },
            { "back-triceps", new SplitDay("Costas e tríceps", "back", "triceps", "back", "triceps", "back", "triceps") },
            { "shoulders-arms", new SplitDay("Ombros e braços", "shoulders", "biceps", "triceps", "shoulders", "forearms", "biceps") },
            { "upper", new SplitDay("Superiores completos", "chest", "back", "shoulders", "back", "biceps", "triceps") },
            { "core", new SplitDay("Abdômen e estabilidade", "core", "core") }
        };

        internal static readonly Dictionary<string, string> SplitModes = new Dictionary<string, string> {
            { "auto", "Distribuição automática" }, { "groups", "Por grupos musculares" }, { "full", "Corpo inteiro" }
        };

        internal static IEnumerable<KeyValuePair<string, string>> DayThemes
        {
            get { return SplitDays.Select(pair => new KeyValuePair<string, string>(pair.Key, pair.Value.Name)); }
        }

        internal static bool IsDayTheme(string key) { return key != null && (key == "" || SplitDays.ContainsKey(key)); }

        private static readonly string[][] DedicatedSchedules = {
            new[] { "" },
            new[] { "upper", "legs" },
            new[] { "push-shoulders", "legs", "pull" },
            new[] { "push", "legs", "pull", "shoulders" },
            new[] { "push", "legs", "pull", "posterior", "shoulders" },
            new[] { "push-shoulders", "legs", "pull", "push-shoulders", "posterior", "pull" },
            new[] { "push-shoulders", "legs", "pull", "core", "push-shoulders", "posterior", "pull" }
        };

        private static string DayThemeKey(Profile profile, int slot)
        {
            if (slot < 0 || profile.Split == "full") return "";
            int count = profile.Days.Length;
            if (profile.Split == "groups" && profile.DayThemes != null)
            {
                string custom = profile.DayThemes[profile.Days[slot]];
                if (!string.IsNullOrEmpty(custom)) return custom;
            }
            bool consecutive = profile.Days.Any(day => profile.Days.Contains((day + 1) % 7));
            if (count <= 3 && profile.Split != "groups" && !consecutive) return "";
            if (count == 4 && FocusGroups(profile.Focus).Any(group => Lower.Contains(group) && group != "core"))
                return new[] { "push-shoulders", "legs", "pull", "posterior" }[slot];
            return DedicatedSchedules[count - 1][slot];
        }

        internal static string DayTheme(Profile profile, int slot)
        {
            string key = DayThemeKey(profile, slot);
            return key == "" ? "Corpo inteiro" : SplitDays[key].Name;
        }

        private DayPlan PlanFor(DateTime date, Profile profile, int slot)
        {
            if (profile.Split == null && date.Date < Data.SplitStart) return LegacyPlanGroups(profile, slot);
            string key = DayThemeKey(profile, slot);
            string[] groups = key != "" ? SplitDays[key].Groups : slot % 2 == 0
                ? new[] { "quads", "chest", "back", "hamstrings", "shoulders", "core", "calves", "biceps" }
                : new[] { "glutes", "back", "chest", "quads", "core", "shoulders", "triceps", "adductors" };
            var family = (key == "" ? Catalog.Groups : groups).Distinct()
                .Where(group => Catalog.All.Any(exercise => exercise.Group == group && Eligible(exercise, profile))).ToArray();
            var planned = groups.Where(family.Contains).Take(ExerciseLimit(profile, slot)).ToList();
            var focus = FocusGroups(profile.Focus).Where(family.Contains).ToArray();
            if (planned.Count > 1 && focus.Length > 0)
            {
                string preferred = focus[0];
                if (planned.Remove(preferred)) planned.Insert(0, preferred);
                else
                {
                    planned.RemoveAt(planned.Count - 1);
                    planned.Insert(0, preferred);
                }
                if (planned.Count >= 4 && (profile.FocusMode == "priority" ||
                    focus.Length == 1 && (profile.Goal == "muscle" || profile.Goal == "recomp")) &&
                    planned.Count(group => group == preferred) == 1)
                {
                    int secondary = planned.FindLastIndex(group => !focus.Contains(group) && planned.Count(other => other == group) > 1);
                    if (secondary < 0 && key == "" && planned.Count >= 5)
                        secondary = planned.FindLastIndex(group => !focus.Contains(group));
                    if (secondary >= 0) planned[secondary] = preferred;
                }
            }
            return new DayPlan {
                Title = DayTheme(profile, slot), Family = family, Groups = planned,
                LegDay = key == "legs" || key == "posterior"
            };
        }
    }
}
