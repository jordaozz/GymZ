using System;
using System.Collections.Generic;
using System.Linq;

namespace GymZ
{
    internal sealed partial class Training
    {
        private static readonly string[] LightExercises = {
            "chair-squat", "bridge", "heel-dig", "standing-calf", "side-leg", "side-adduction",
            "wall-slide", "incline-db", "pushup", "prone-row", "band-row", "self-curl",
            "band-triceps", "grip-towel", "dead-bug", "side-plank", "leg-press-horizontal", "leg-extension",
            "seated-curl", "abductor", "adductor-machine", "machine-calf", "chest-machine", "seated-row", "pulldown",
            "cable-lateral", "pushdown", "cable-curl", "pallof", "db-row", "goblet", "floor-press", "db-bridge", "wrist-extension"
        };

        private Session BuildComplement(DateTime day, string attendance)
        {
            var profile = At(day);
            var protectedGroups = ProtectedGroups(day);
            var near = GroupsFor(day.AddDays(2), false);
            var completed = Enumerable.Range(1, 7).Select(i => day.AddDays(-i)).SelectMany(d => Get(d).Items)
                .Where(m => m.Done).Select(m => Catalog.ById[m.Id].Group).ToLookup(g => g);
            var habit = Familiarity(day);
            var extra = new Session { Title = "Treino extra leve", Extra = true, Complement = true, Attendance = attendance };
            var candidates = LightExercises.Select(id => Catalog.ById[id]).Where(e => Eligible(e, profile) &&
                habit[e.Id].ChoiceScore >= 0 && !Touches(e, protectedGroups)).ToList();

            var gaps = Enumerable.Range(1, 7).Select(i => day.AddDays(-i)).Select(d => new { Date = d, Session = Get(d), Makeup = Makeup(d) })
                .Where(p => p.Session.Scheduled && !Present(p.Session) &&
                    (!p.Makeup.HasValue || p.Makeup.Value < day && !Present(Get(p.Makeup.Value))))
                .OrderByDescending(p => p.Session.Attendance == "missed");
            foreach (var gap in gaps)
            {
                var target = gap.Session.Items.Select(m => Catalog.ById[m.Id]).Where(e => e.Type != 'A' && !protectedGroups.Contains(e.Group))
                    .OrderByDescending(e => e.Type == 'C').ThenBy(e => completed[e.Group].Count()).ToList();
                Exercise match = null;
                foreach (var missed in target)
                {
                    match = candidates.Where(e => e.Group == missed.Group).OrderByDescending(e => Equivalent(missed, e)).ThenByDescending(e => habit[e.Id].ChoiceScore).FirstOrDefault();
                    if (match != null) break;
                }
                if (match == null) continue;
                extra.ComplementFor = Key(gap.Date);
                extra.ComplementGroup = match.Group;
                extra.Items.Add(PrescribeLight(match, profile, true));
                break;
            }

            var chosen = new HashSet<string>(extra.Items.Select(m => Catalog.ById[m.Id].Group));
            var helpers = candidates.Where(e => !chosen.Contains(e.Group)).OrderBy(e => Touches(e, near))
                .ThenBy(e => completed[e.Group].Count()).ThenBy(e => e.Group == extra.ComplementGroup)
                .ThenByDescending(e => habit[e.Id].ChoiceScore);
            foreach (var exercise in helpers)
            {
                if (!chosen.Add(exercise.Group)) continue;
                extra.Items.Add(PrescribeLight(exercise, profile, Touches(exercise, near)));
                if (extra.Items.Count == 3) break;
            }

            if (WantsAutoCardio(profile))
            {
                var cardio = PickCardio(profile, 0, true, habit);
                if (cardio != null) extra.Items.Add(PrescribeLight(cardio, profile, false));
            }
            return extra;
        }

        public bool FitsSession(DateTime date, Session session, Exercise exercise)
        {
            return !session.Complement || exercise.Type == 'A' || (exercise.Level == 0 && !Touches(exercise, ProtectedGroups(date)));
        }

        public Movement PrescribeForSession(DateTime date, Session session, Exercise exercise, bool priority)
        {
            var profile = At(date);
            if (!session.Complement) return Prescribe(exercise, profile, priority);
            if (!FitsSession(date, session, exercise)) throw new InvalidOperationException("Escolha um exercício leve que poupe os músculos treinados ontem ou previstos para amanhã.");
            return PrescribeLight(exercise, profile, exercise.Group == session.ComplementGroup || Touches(exercise, GroupsFor(date.AddDays(2), false)));
        }

        private Movement PrescribeLight(Exercise exercise, Profile profile, bool singleSet)
        {
            var movement = Prescribe(exercise, profile, false);
            movement.Sets = singleSet || exercise.Type == 'A' ? 1 : 2;
            movement.Target = exercise.Type == 'A' ? "8–12 min leves" : exercise.Type == 'T' ? "15–20 s" : "8–12 rep. leves";
            movement.Rest = exercise.Type == 'A' ? 0 : 90;
            if (exercise.Type == 'A') movement.CardioPhase = "after";
            return movement;
        }

        private HashSet<string> ProtectedGroups(DateTime date)
        {
            var protectedGroups = GroupsFor(date.AddDays(-1), true);
            protectedGroups.UnionWith(GroupsFor(date.AddDays(1), false));
            return protectedGroups;
        }

        private HashSet<string> GroupsFor(DateTime date, bool completedOnly)
        {
            var groups = new HashSet<string>();
            if (Makeup(date).HasValue) return groups;
            var session = Get(date);
            bool inferFromPresence = completedOnly && Present(session) && !session.Items.Any(m => m.Done);
            foreach (var movement in session.Items.Where(m => !completedOnly || inferFromPresence || m.Done))
            {
                var exercise = Catalog.ById[movement.Id];
                if (exercise.Type == 'A') continue;
                groups.Add(exercise.Group);
                groups.UnionWith(exercise.Other);
            }
            return groups;
        }

        private static bool Touches(Exercise exercise, HashSet<string> groups)
        {
            return groups.Contains(exercise.Group) || exercise.Other.Any(groups.Contains);
        }
    }
}
