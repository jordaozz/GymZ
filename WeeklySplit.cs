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
            public bool Manual;

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
            { "auto", "Sugestão do GymZ para toda a semana" }, { "groups", "Escolher por dia" }, { "full", "Corpo inteiro" }
        };
        internal static bool IsDayTheme(string key) { return key != null && (key == "" || SplitDays.ContainsKey(key)); }

        private static SplitDay PresetDay(string key) { return string.IsNullOrEmpty(key) ? null : SplitDays[key]; }

        internal static DayRoutine[] EditDayRoutines(Profile profile)
        {
            return Enumerable.Range(0, 7).Select(day => {
                if (profile.DayRoutines != null)
                {
                    var stored = profile.DayRoutines[day];
                    return new DayRoutine { Manual = stored.Manual, Groups = stored.Groups.ToArray() };
                }
                var preset = profile.DayThemes == null ? null : PresetDay(profile.DayThemes[day]);
                return preset == null ? new DayRoutine() : new DayRoutine { Manual = true, Groups = preset.Groups.Distinct().ToArray() };
            }).ToArray();
        }

        internal static int? EmptyManualDay(Profile profile)
        {
            if (profile.Split == "groups" && profile.DayRoutines != null)
                foreach (int day in profile.Days)
                    if (profile.DayRoutines[day].Manual && profile.DayRoutines[day].Groups.Length == 0) return day;
            return null;
        }

        private static SplitDay ChosenDay(Profile profile, int day)
        {
            if (profile.DayRoutines == null)
                return profile.DayThemes == null ? null : PresetDay(profile.DayThemes[day]);
            var routine = profile.DayRoutines[day];
            return routine.Manual ? CreateManualDay(routine.Groups) : null;
        }

        private static SplitDay CreateManualDay(string[] groups) { return new SplitDay(ManualDayTitle(groups), groups) { Manual = true }; }

        private static string ManualDayTitle(string[] groups)
        {
            var names = groups.Select(group => Catalog.Names[group]).ToArray();
            if (names.Length <= 1) return names.FirstOrDefault() ?? "Treino manual";
            if (names.Length > 4) return "Treino de " + names.Length + " regiões";
            return names[0] + (names.Length > 2 ? ", " + string.Join(", ", names.Skip(1).Take(names.Length - 2)).ToLower(Pt) : "") +
                " e " + names[names.Length - 1].ToLower(Pt);
        }

        private static bool SameDayChoices(Profile first, Profile second)
        {
            if (first.Split != "groups" || second.Split != "groups") return true;
            return first.Days.All(day => {
                var oldDay = ChosenDay(first, day);
                var newDay = ChosenDay(second, day);
                return oldDay == null ? newDay == null : newDay != null &&
                    oldDay.Manual == newDay.Manual && oldDay.Groups.SequenceEqual(newDay.Groups);
            });
        }

        private static readonly string[][] DedicatedSchedules = {
            new[] { "" },
            new[] { "upper", "legs" },
            new[] { "push-shoulders", "legs", "pull" },
            new[] { "push", "legs", "pull", "shoulders" },
            new[] { "push", "legs", "pull", "posterior", "shoulders" },
            new[] { "push-shoulders", "legs", "pull", "push-shoulders", "posterior", "pull" },
            new[] { "push-shoulders", "legs", "pull", "core", "push-shoulders", "posterior", "pull" }
        };
        private static readonly string[] SuggestedThemes = { "push", "pull", "legs", "shoulders", "posterior", "core" };

        private static SplitDay SplitFor(Profile profile, int slot)
        {
            if (slot < 0 || profile.Split == "full") return null;
            int count = profile.Days.Length;
            bool consecutive = profile.Days.Any(day => profile.Days.Contains((day + 1) % 7));
            if (count <= 3 && profile.Split != "groups" && !consecutive) return null;
            var defaults = count == 4 && FocusGroups(profile.Focus).Any(group => Lower.Contains(group) && group != "core")
                ? new[] { "push-shoulders", "legs", "pull", "posterior" } : DedicatedSchedules[count - 1];
            if (profile.Split != "groups" || (profile.DayThemes == null && profile.DayRoutines == null)) return PresetDay(defaults[slot]);
            return ApplyDayChoices(profile, defaults)[slot];
        }

        private static SplitDay[] ApplyDayChoices(Profile profile, string[] defaults)
        {
            var choices = profile.Days.Select(day => ChosenDay(profile, day)).ToArray();
            var chosenGroups = new HashSet<string>();
            if (profile.DayRoutines == null) chosenGroups.UnionWith(choices.Where(day => day != null).SelectMany(day => day.Groups));
            var themes = defaults.Select((key, index) => choices[index] ?? PresetDay(key)).ToArray();
            for (int index = 0; index < themes.Length; index++)
            {
                if (choices[index] != null)
                {
                    chosenGroups.UnionWith(choices[index].Groups);
                    continue;
                }
                if (themes[index] == null) continue;
                var originalGroups = themes[index].Groups.Distinct().ToArray();
                int overlap = originalGroups.Count(chosenGroups.Contains);
                if (overlap == 0) continue;

                var otherGroups = new HashSet<string>(themes.Where((theme, position) => position != index && theme != null)
                    .SelectMany(theme => theme.Groups));
                var replacement = SuggestedThemes.Select(key => new { Day = SplitDays[key], Groups = SplitDays[key].Groups.Distinct().ToArray() })
                    .Where(option => option.Groups.Count(chosenGroups.Contains) < overlap)
                    .OrderBy(option => option.Groups.Count(chosenGroups.Contains))
                    .ThenBy(option => option.Groups.Count(otherGroups.Contains))
                    .ThenByDescending(option => option.Groups.Count(originalGroups.Contains))
                    .ThenByDescending(option => option.Groups.Count(group => !otherGroups.Contains(group)))
                    .FirstOrDefault();
                if (replacement != null) themes[index] = replacement.Day;
            }
            return themes;
        }

        private DayPlan PlanFor(DateTime date, Profile profile, int slot)
        {
            if (profile.Split == null && date.Date < Data.SplitStart) return LegacyPlanGroups(profile, slot);
            var split = SplitFor(profile, slot);
            if (split != null && split.Manual) return ManualPlan(profile, slot, split);
            string[] groups = split != null ? split.Groups : slot % 2 == 0
                ? new[] { "quads", "chest", "back", "hamstrings", "shoulders", "core", "calves", "biceps" }
                : new[] { "glutes", "back", "chest", "quads", "core", "shoulders", "triceps", "adductors" };
            var family = (split == null ? Catalog.Groups : groups).Distinct()
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
                    if (secondary < 0 && split == null && planned.Count >= 5)
                        secondary = planned.FindLastIndex(group => !focus.Contains(group));
                    if (secondary >= 0) planned[secondary] = preferred;
                }
            }
            return new DayPlan {
                Title = split == null ? "Corpo inteiro" : split.Name, Family = family, Groups = planned,
                LegDay = split == SplitDays["legs"] || split == SplitDays["posterior"]
            };
        }

        private DayPlan ManualPlan(Profile profile, int slot, SplitDay split)
        {
            var focus = FocusGroups(profile.Focus);
            var available = split.Groups.Select(group => new {
                Group = group, Count = Catalog.All.Count(exercise => exercise.Group == group && Eligible(exercise, profile))
            }).Where(option => option.Count > 0).OrderByDescending(option => focus.Contains(option.Group)).ToArray();
            var planned = Enumerable.Range(0, 3).SelectMany(round => available.Where(option => option.Count > round).Select(option => option.Group))
                .Take(Math.Max(available.Length, ExerciseLimit(profile, slot))).ToList();
            return new DayPlan {
                Title = split.Name, Family = available.Select(option => option.Group).ToArray(), Groups = planned,
                LegDay = split.Groups.All(Lower.Contains) && split.Groups.Any(group => group != "core")
            };
        }
    }
}
