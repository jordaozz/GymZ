using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;

namespace GymZ
{
    public sealed class Profile
    {
        public int[] Days = { 0, 1, 3, 4 };
        public string Goal = "fitness";
        public string Name = "";
        public string Focus = "";
        public string Gear = "gym";
        public string Cardio = "auto";
        public string FocusMode = "balanced";
        public string Split;
        public string[] DayThemes;
        public int Level;
        public int Minutes = 45;
        public decimal Weight;
        public decimal Height;
    }

    public sealed class ProfileRevision
    {
        public DateTime Start;
        public Profile Profile;
    }

    public sealed class Preferences
    {
        public string Theme = "dark";
        public string Accent = "blue";
        public int Scale = 100;
        public bool Contrast;
    }

    public sealed class Movement
    {
        public string Id;
        public int Sets;
        public string Target;
        public int Rest;
        public string CardioPhase = "";
        public bool Priority;
        public bool Varied;
        public bool Done;
        public decimal Load;
        public int ActualSets;
        public int ActualReps;
        public int? RepsInReserve;
        public List<LiftSet> Efforts;
    }

    public sealed class LiftSet
    {
        public int? RepsOrSeconds;
        public decimal? Kilograms;
    }

    internal sealed class LiftRecord
    {
        public DateTime Date;
        public Movement Movement;

        public decimal? Peak
        {
            get
            {
                if (Movement.Efforts != null) return Movement.Efforts.Select(set => set.Kilograms).Max();
                return Movement.Load > 0 ? (decimal?)Movement.Load : null;
            }
        }
    }

    public sealed class Session
    {
        public bool Scheduled;
        public bool Extra;
        public bool Complement;
        public bool Finalized;
        public string Title;
        public string Attendance = "";
        public string MakeupFor = "";
        public string ComplementFor = "";
        public string ComplementGroup = "";
        public List<Movement> Items = new List<Movement>();
    }

    public sealed class GymData
    {
        public int Version = 1;
        public DateTime FirstAccess;
        public DateTime? SplitStart;
        public bool Configured;
        public Preferences Preferences = new Preferences();
        public List<ProfileRevision> Profiles = new List<ProfileRevision>();
        public Dictionary<string, Session> Sessions = new Dictionary<string, Session>();
        public List<string> Avoided = new List<string>();
        public List<ExerciseSwap> Swaps = new List<ExerciseSwap>();

        public static GymData Create(DateTime today)
        {
            var data = new GymData { FirstAccess = today.Date };
            data.Profiles.Add(new ProfileRevision { Start = today.Date, Profile = new Profile { Split = "auto" } });
            return data;
        }
    }

    public sealed class ExerciseSwap
    {
        public DateTime Date;
        public string From, To;
    }

    internal sealed class Exercise
    {
        public string Id, Name, Group, Gear, Tip;
        public string[] Other, Steps;
        public char Type;
        public int Level;
    }

    internal static class Catalog
    {
        public const string Ace = "https://www.acefitness.org/resources/everyone/exercise-library/";
        public const string Acsm = "https://acsm.org/resistance-training-guidelines-update-2026/";
        public const string Cdc = "https://www.cdc.gov/physical-activity-basics/guidelines/adults.html";
        public const string FootStudy = "https://pubmed.ncbi.nlm.nih.gov/18545207/";
        public const string FootReview = "https://pmc.ncbi.nlm.nih.gov/articles/PMC7369968/";
        public const string RecompReview = "https://pubmed.ncbi.nlm.nih.gov/34536199/";
        public const string FrequencyTrial = "https://pubmed.ncbi.nlm.nih.gov/38595233/";
        public const string VariationTrial = "https://pubmed.ncbi.nlm.nih.gov/39388663/";
        public const string RestReview = "https://pubmed.ncbi.nlm.nih.gov/28933024/";
        public const string CardioOrder = "https://pmc.ncbi.nlm.nih.gov/articles/PMC5752732/";
        public const string ConcurrentTraining = "https://doi.org/10.1007/s40279-021-01587-7";
        public const string Warmup = "https://www.heart.org/en/healthy-living/exercise-and-physical-activity/fitness-basics/warm-up-cool-down";
        public const string CardioIntensity = "https://www.cdc.gov/physical-activity-basics/adding-adults/what-counts.html";
        public static readonly string[] Groups = { "chest", "back", "shoulders", "biceps", "triceps", "forearms", "core", "glutes", "quads", "hamstrings", "adductors", "calves" };
        public static readonly Dictionary<string, string> Names = new Dictionary<string, string> {
            {"chest", "Peito"}, {"back", "Costas"}, {"shoulders", "Ombros"}, {"biceps", "Bíceps"},
            {"triceps", "Tríceps"}, {"forearms", "Antebraços"}, {"core", "Abdômen"}, {"glutes", "Glúteos"},
            {"quads", "Quadríceps"}, {"hamstrings", "Posterior de coxa"}, {"adductors", "Adutores"},
            {"calves", "Panturrilhas"}, {"cardio", "Cardio"}
        };
        public static readonly Dictionary<string, string> GearNames = new Dictionary<string, string> {
            {"body", "Sem aparelho"}, {"dumbbell", "Halteres"}, {"band", "Elástico"}, {"machine", "Máquina"},
            {"cable", "Polia"}, {"barbell", "Barra e anilhas"}, {"pullup", "Barra fixa"}
        };
        public static readonly Dictionary<string, string> ProfileGear = new Dictionary<string, string> {
            {"gym", "Academia completa"}, {"machines", "Máquinas e polias"}, {"machines-dumbbell", "Máquinas, polias e halteres"},
            {"dumbbell-only", "Só halteres"}, {"home", "Halteres, elásticos e peso corporal"}, {"dumbbell", "Halteres e peso corporal"},
            {"band", "Elásticos e peso corporal"}, {"body", "Somente peso corporal"}
        };
        public static readonly Dictionary<string, string> ProfileCardio = new Dictionary<string, string> {
            {"auto", "Automático conforme o objetivo"}, {"floor", "Sem aparelho"},
            {"gym", "Bicicleta, esteira ou transport"}, {"bike", "Bicicleta ergométrica"}, {"treadmill", "Esteira"},
            {"elliptical", "Elíptico (transport)"}, {"none", "Sem cardio automático"}
        };
        public static readonly Dictionary<string, string> Goals = new Dictionary<string, string> {
            {"fitness", "Condicionamento e saúde"}, {"strength", "Aumentar a força"},
            {"muscle", "Ganhar massa muscular"}, {"weight", "Emagrecer"},
            {"recomp", "Emagrecer e ganhar massa muscular"}, {"endurance", "Melhorar o fôlego e a resistência"}
        };
        public static readonly Dictionary<string, string> FocusOptions = new Dictionary<string, string> {
            {"", "Corpo inteiro"}, {"glutes-legs", "Glúteos e pernas"}, {"lower", "Pernas completas"},
            {"upper", "Superiores completos"}, {"arms", "Braços"}, {"upper-back", "Costas e ombros"}
        };
        public static readonly Dictionary<string, string> FocusModes = new Dictionary<string, string> {
            {"balanced", "Prioridade moderada"}, {"priority", "Mais foco na região, manutenção das outras"}
        };
        public static readonly List<Exercise> All = Load();
        public static readonly Dictionary<string, Exercise> ById = All.ToDictionary(e => e.Id);

        private static List<Exercise> Load()
        {
            var exercises = new List<Exercise>();
            using (var reader = new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream("Exercises.txt")))
            {
                for (string line = reader.ReadLine(); line != null; line = reader.ReadLine())
                {
                    if (line.Length == 0 || line[0] == '#') continue;
                    var parts = line.Split('|');
                    if (parts.Length != 9) throw new InvalidDataException("Linha inválida no catálogo: " + line);
                    exercises.Add(new Exercise {
                        Id = parts[0], Name = parts[1], Group = parts[2], Other = parts[3].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries),
                        Gear = parts[4], Level = int.Parse(parts[5], CultureInfo.InvariantCulture), Type = parts[6][0],
                        Steps = parts[7].Split('~'), Tip = parts[8]
                    });
                }
            }
            return exercises;
        }

        public static bool Available(Exercise exercise, Profile profile)
        {
            if (exercise.Type == 'A')
            {
                string equipment = profile.Cardio;
                if (equipment == "auto" || equipment == "none")
                    equipment = profile.Gear == "gym" || profile.Gear == "machines" || profile.Gear == "machines-dumbbell" ? "gym" : "floor";
                if (equipment == "gym") return exercise.Gear == "machine";
                if (equipment == "floor") return exercise.Gear == "body";
                return equipment == exercise.Id;
            }
            if (profile.Gear == "machines" || profile.Gear == "machines-dumbbell")
                return exercise.Gear == "machine" || exercise.Gear == "cable" || (profile.Gear == "machines-dumbbell" && exercise.Gear == "dumbbell");
            if (profile.Gear == "dumbbell-only") return exercise.Gear == "dumbbell";
            return profile.Gear == "gym" || exercise.Gear == "body" || exercise.Gear == profile.Gear ||
                (profile.Gear == "home" && (exercise.Gear == "dumbbell" || exercise.Gear == "band"));
        }

        public static string Pattern(string id)
        {
            string pattern;
            return Patterns.TryGetValue(id, out pattern) ? pattern : id;
        }

        private static readonly Dictionary<string, string> Patterns = new[] {
            "horizontal-press incline-db chest-machine pushup bench floor-press band-press close-pushup",
            "chest-fly pec-deck cable-fly", "triceps-down pushdown band-triceps", "triceps-extension overhead-db skull",
            "vertical-pull pulldown pullup", "horizontal-pull seated-row db-row band-row prone-row chest-row",
            "rear-delt face-pull reverse-fly", "shoulder-abduction lateral band-lateral cable-lateral",
            "overhead-press shoulder-press machine-shoulder", "curl db-curl hammer cable-curl band-curl self-curl",
            "grip suitcase grip-towel", "wrist-extension wrist-extension band-wrist",
            "hip-extension hip-thrust bridge db-bridge kickback", "hip-abduction abductor band-walk side-leg",
            "knee-compound leg-press leg-press-horizontal goblet chair-squat band-squat split-squat barbell-squat hack-squat step-up sumo",
            "knee-flexion leg-curl seated-curl", "hip-hinge rdl band-rdl", "calf standing-calf db-calf machine-calf band-calf",
            "hip-adduction adductor-machine side-adduction band-adduction", "core-extension dead-bug plank",
            "core-rotation pallof band-pallof", "cardio walk standing-march bike elliptical treadmill"
        }.SelectMany(line => { var keys = line.Split(' '); return keys.Skip(1).Select(id => new { Id = id, Pattern = keys[0] }); })
            .ToDictionary(pair => pair.Id, pair => pair.Pattern);
    }

    internal sealed class WeekReport
    {
        public int Scheduled, Attended, Due, Missed, Unmarked, Compensated, PlannedExercises, Done, Sets;
        public readonly Dictionary<string, int> Direct = new Dictionary<string, int>();
        public readonly HashSet<string> Indirect = new HashSet<string>();
        public readonly Dictionary<string, MuscleWeek> Coverage = Catalog.Groups.ToDictionary(g => g, g => new MuscleWeek());
        public readonly Dictionary<DateTime, List<Movement>> Pending = new Dictionary<DateTime, List<Movement>>();
        public int? Rate { get { return Due == 0 ? (int?)null : Math.Min(100, (int)Math.Round(100.0 * Compensated / Due)); } }
    }

    internal sealed class MuscleWeek
    {
        public int Expected, DirectSets, SupportSets, DoneExercises;
        public int EffortLogs, EasyEfforts;
        public readonly HashSet<DateTime> PlannedDays = new HashSet<DateTime>();
        public readonly HashSet<DateTime> DirectDays = new HashSet<DateTime>();
        public int Target { get { return Math.Max(2, (int)Math.Ceiling(Expected * .75)); } }
        public int Status { get { return DirectSets >= Target ? 2 : (DirectSets + SupportSets > 0 ? 1 : 0); } }
    }

    internal sealed partial class Training
    {
        public static readonly string[] DayNames = { "Segunda-feira", "Terça-feira", "Quarta-feira", "Quinta-feira", "Sexta-feira", "Sábado", "Domingo" };
        public static readonly string[] DayShort = { "Seg", "Ter", "Qua", "Qui", "Sex", "Sáb", "Dom" };
        public static readonly CultureInfo Pt = CultureInfo.GetCultureInfo("pt-BR");
        public readonly GymData Data;
        public DateTime Today;
        public bool NeedsSetup { get { return !Data.Configured && Data.Sessions.Count == 0; } }

        public Training(GymData data, DateTime today)
        {
            Data = data;
            Today = today.Date;
            if (!data.SplitStart.HasValue) data.SplitStart = Today >= data.FirstAccess ? Today : data.FirstAccess;
        }
        public bool IsTrackedDate(DateTime date) { return date.Date >= Data.FirstAccess; }
        public static int Day(DateTime date) { return ((int)date.DayOfWeek + 6) % 7; }
        public static DateTime Monday(DateTime date) { return date.Date.AddDays(-Day(date)); }
        public static string Key(DateTime date) { return date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture); }
        public static DateTime Date(string key) { return DateTime.ParseExact(key, "yyyy-MM-dd", CultureInfo.InvariantCulture); }
        public Profile At(DateTime date)
        {
            var revision = Data.Profiles.LastOrDefault(p => p.Start <= date.Date);
            return (revision ?? Data.Profiles[0]).Profile;
        }

        public void ChangeProfile(Profile profile)
        {
            if (profile.Split == null) profile.Split = "auto";
            if (NeedsSetup)
            {
                Data.Profiles.Clear();
                Data.Profiles.Add(new ProfileRevision { Start = Today, Profile = profile });
                Data.Configured = true;
                return;
            }
            var current = At(Today);
            bool planChanged = !current.Days.SequenceEqual(profile.Days) || current.Goal != profile.Goal || current.Focus != profile.Focus ||
                current.Gear != profile.Gear || current.Level != profile.Level || current.Minutes != profile.Minutes ||
                current.FocusMode != profile.FocusMode || current.Cardio != profile.Cardio || current.Split != profile.Split ||
                !SameDayThemes(current, profile);
            var previous = Data.Profiles.LastOrDefault(p => p.Start == Today);
            if (previous == null) Data.Profiles.Add(new ProfileRevision { Start = Today, Profile = profile });
            else previous.Profile = profile;
            Data.Profiles = Data.Profiles.OrderBy(p => p.Start).ToList();
            if (planChanged)
            {
                foreach (var key in Data.Sessions.Where(p => Date(p.Key) >= Today && p.Value.Scheduled && !p.Value.Finalized && Makeup(Date(p.Key)) == null &&
                    p.Value.Attendance == "" && !p.Value.Items.Any(HasLog)).Select(p => p.Key).ToArray())
                    Data.Sessions.Remove(key);
                foreach (var key in Data.Sessions.Where(p => Date(p.Key) >= Today && p.Value.Extra && !p.Value.Finalized &&
                    p.Value.Attendance == "" && !p.Value.Items.Any(HasLog) && At(Date(p.Key)).Days.Contains(Day(Date(p.Key)))).Select(p => p.Key).ToArray())
                    Data.Sessions.Remove(key);
            }
            Data.Configured = true;
        }

        internal static bool HasLog(Movement item) { return item.Done || HasPerformance(item); }

        private static bool SameDayThemes(Profile first, Profile second)
        {
            if (first.Split != "groups" || second.Split != "groups") return true;
            return first.Days.SequenceEqual(second.Days) && first.Days.All(day =>
                ((first.DayThemes == null ? "" : first.DayThemes[day]) ?? "") ==
                ((second.DayThemes == null ? "" : second.DayThemes[day]) ?? ""));
        }

        private static bool HasPerformance(Movement item)
        {
            return item.Load > 0 || item.ActualReps > 0 || item.ActualSets > 0 || item.RepsInReserve.HasValue || (item.Efforts != null && item.Efforts.Count > 0);
        }

        public Session Get(DateTime date)
        {
            if (!IsTrackedDate(date)) return new Session { Title = "Antes do início" };
            var profile = At(date);
            int slot = Array.IndexOf(profile.Days, Day(date));
            Session saved;
            if (Data.Sessions.TryGetValue(Key(date), out saved))
            {
                if (!saved.Extra || saved.Complement || saved.Finalized || saved.MakeupFor != "" || saved.Items.Any(HasLog)) return saved;
                if (slot < 0 || date.Date < Data.Profiles[0].Start)
                    return new Session { Title = "Descanso", Attendance = saved.Attendance };
                var planned = Build(date, profile, slot);
                planned.Attendance = saved.Attendance;
                return planned;
            }
            if (NeedsSetup) return new Session { Title = "Sem plano" };
            if (slot < 0 || date.Date < Data.Profiles[0].Start)
                return new Session { Title = "Descanso" };
            return Build(date, profile, slot);
        }

        public Session Save(DateTime date)
        {
            RequireRecordingDay(date);
            var entry = Get(date);
            Data.Sessions[Key(date)] = entry;
            return entry;
        }

        public void RequireRecordingDay(DateTime date)
        {
            if (date.Date > Today) throw new InvalidOperationException("Este treino estará disponível na data prevista.");
            if (date.Date < Today || !IsTrackedDate(date)) throw new InvalidOperationException("Os registros são feitos no próprio dia do treino.");
        }

        public static bool Present(Session session) { return session.Attendance == "present" || session.Finalized || session.Items.Any(i => i.Done); }

        public static bool Complete(Session session) { return session.Finalized || (session.Items.Count > 0 && session.Items.All(item => item.Done)); }

        public void Finish(DateTime date)
        {
            RequireRecordingDay(date);
            var session = Get(date);
            if ((!session.Scheduled && !session.Extra) || Makeup(date).HasValue || session.Items.Count == 0 || !Present(session))
                throw new InvalidOperationException("Registre a presença ou conclua um exercício antes de finalizar o treino.");
            session.Finalized = true;
            Data.Sessions[Key(date)] = session;
        }

        public void Reopen(DateTime date)
        {
            RequireRecordingDay(date);
            var session = Get(date);
            if (!session.Finalized) return;
            session.Finalized = false;
            Data.Sessions[Key(date)] = session;
        }

        public void Attendance(DateTime date, string status)
        {
            RequireRecordingDay(date);
            var entry = Get(date);
            if (status == "missed" && (entry.Finalized || entry.Items.Any(i => i.Done)))
                throw new InvalidOperationException("Reabra o treino e desmarque os exercícios concluídos antes de registrar falta.");
            if (status == "present" && Makeup(date).HasValue)
                throw new InvalidOperationException("Cancele ou desvincule a reposição antes de registrar presença no dia original.");
            if (!entry.Scheduled && !entry.Extra && status == "missed")
                throw new InvalidOperationException("Registre faltas nos dias de treino previstos.");
            entry.Attendance = status;
            Data.Sessions[Key(date)] = entry;
        }

        public Session EnableExtra(DateTime date)
        {
            RequireRecordingDay(date);
            var entry = Get(date);
            if (entry.Scheduled || entry.Extra) return entry;
            entry = BuildComplement(date, entry.Attendance);
            Data.Sessions[Key(date)] = entry;
            return entry;
        }

        public void CancelComplement(DateTime date)
        {
            if (date.Date != Today) return;
            var entry = Get(date);
            if (!entry.Complement || entry.Finalized || entry.Items.Any(HasLog)) return;
            if (entry.Attendance == "") Data.Sessions.Remove(Key(date));
            else Data.Sessions[Key(date)] = new Session { Title = "Descanso", Attendance = entry.Attendance };
        }

        public DateTime? Makeup(DateTime origin)
        {
            string key = Key(origin);
            foreach (var entry in Data.Sessions)
                if (entry.Value.MakeupFor == key) return Date(entry.Key);
            return null;
        }

        public IEnumerable<DateTime> MakeupOptions(DateTime origin)
        {
            if (origin.Date > Today || !IsTrackedDate(origin) || Present(Get(origin)) || !Get(origin).Scheduled || Makeup(origin).HasValue) return Enumerable.Empty<DateTime>();
            return Enumerable.Range(0, 8).Select(i => Today.AddDays(i))
                .Where(d => d > origin.Date && !Get(d).Scheduled && !Get(d).Extra && !Present(Get(d)))
                .OrderByDescending(d => RestGap(d, origin)).ThenBy(d => d);
        }

        private bool RestGap(DateTime date, DateTime origin)
        {
            var neighbors = new[] { date.AddDays(-1), date.AddDays(1) };
            return neighbors.All(d => d == origin || (!Get(d).Scheduled && !Get(d).Extra));
        }

        public void Compensate(DateTime origin, DateTime destination)
        {
            if (!MakeupOptions(origin).Contains(destination.Date))
                throw new InvalidOperationException("Escolha um dia livre para a reposição.");
            var source = Get(origin);
            if (origin.Date == Today) source.Attendance = "missed";
            var profile = At(destination);
            var familiarity = Familiarity(destination);
            var used = new HashSet<string>();
            var makeup = new Session { Title = source.Title, Extra = true, MakeupFor = Key(origin) };
            foreach (var original in source.Items)
            {
                var exercise = Catalog.ById[original.Id];
                if (exercise.Type == 'A' && profile.Cardio == "none") continue;
                if (!Eligible(exercise, profile))
                    exercise = Pick(exercise.Group, profile, used, 0, exercise.Type == 'C', familiarity);
                if (exercise == null || !used.Add(exercise.Id)) continue;
                makeup.Items.Add(Prescribe(exercise, profile, original.Priority));
            }
            OrderCardio(makeup);
            Data.Sessions[Key(origin)] = source;
            Data.Sessions[Key(destination)] = makeup;
        }

        public void CancelMakeup(DateTime day)
        {
            var entry = Get(day);
            if (entry.MakeupFor == "") return;
            if (Present(entry) || entry.Items.Any(HasLog)) entry.MakeupFor = "";
            else Data.Sessions.Remove(Key(day));
        }

        public List<Exercise> Alternatives(DateTime date, Session session, Movement old)
        {
            var profile = At(date);
            var exercise = Catalog.ById[old.Id];
            var familiarity = Familiarity(date);
            return Catalog.All.Where(e => Eligible(e, profile) && FitsSession(date, session, e) && !session.Items.Any(item => item.Id == e.Id) &&
                (e.Group == exercise.Group || Equivalent(exercise, e))).OrderByDescending(e => Equivalent(exercise, e))
                .ThenByDescending(e => e.Group == exercise.Group).ThenByDescending(e => IsLegPress(e.Id) && IsLegPress(exercise.Id))
                .ThenByDescending(e => familiarity[e.Id].ChoiceScore)
                .ThenByDescending(e => e.Type == exercise.Type).ThenByDescending(e => e.Other.Intersect(exercise.Other).Count()).ToList();
        }

        public void LearnSwap(string from, string to, bool temporary)
        {
            if (temporary || from == to) return;
            if (Data.Swaps.Count >= 1500) Data.Swaps.RemoveAt(0);
            Data.Swaps.Add(new ExerciseSwap { Date = Today, From = from, To = to });
        }

        public IEnumerable<string> ReducedSuggestions()
        {
            return Familiarity(Today).Where(p => p.Value.Rejections > p.Value.Chosen && !Data.Avoided.Contains(p.Key))
                .OrderByDescending(p => p.Value.Rejections - p.Value.Chosen).Select(p => p.Key);
        }

        private sealed class ExerciseUse
        {
            public int Rejections, Chosen, Done;
            public int ChoiceScore { get { return Math.Min(12, 4 * Chosen) - Math.Min(24, 7 * Rejections) + Math.Min(2, Done); } }
        }

        private Dictionary<string, ExerciseUse> Familiarity(DateTime date)
        {
            var known = Catalog.All.ToDictionary(e => e.Id, e => new ExerciseUse());
            foreach (var swap in Data.Swaps.Where(s => s.Date <= date.Date))
            {
                known[swap.From].Rejections++;
                known[swap.To].Chosen++;
            }
            var week = Monday(date);
            var start = week.AddDays(-42);
            foreach (var entry in Data.Sessions.Where(p => Date(p.Key) >= start && Date(p.Key) < week && Date(p.Key) <= Today))
                foreach (var item in entry.Value.Items.Where(m => m.Done)) known[item.Id].Done++;
            return known;
        }

        public static bool Equivalent(Exercise from, Exercise to)
        {
            if (Catalog.Pattern(from.Id) != Catalog.Pattern(to.Id)) return false;
            return from.Group == to.Group || (to.Other.Contains(from.Group) && from.Other.Contains(to.Group));
        }

        public bool Eligible(Exercise exercise, Profile profile)
        {
            return Catalog.Available(exercise, profile) && exercise.Level <= profile.Level && !Data.Avoided.Contains(exercise.Id);
        }

        public void Avoid(string exerciseId)
        {
            if (Data.Avoided.Contains(exerciseId)) return;
            Data.Avoided.Add(exerciseId);
            foreach (var pair in Data.Sessions.Where(p => Date(p.Key) >= Today))
            {
                var entry = pair.Value;
                if (entry.Finalized) continue;
                int index = entry.Items.FindIndex(item => item.Id == exerciseId && !HasLog(item));
                if (index < 0) continue;
                var day = Date(pair.Key);
                var old = entry.Items[index];
                var replacement = Alternatives(day, entry, old).FirstOrDefault();
                if (replacement != null)
                {
                    entry.Items[index] = PrescribeForSession(day, entry, replacement, old.Priority);
                    OrderCardio(entry);
                }
            }
        }

        private static bool IsLegPress(string id) { return id == "leg-press" || id == "leg-press-horizontal"; }

        public static string[] FocusGroups(string focus)
        {
            switch (focus)
            {
                case "glutes-legs": return new[] { "glutes", "quads", "hamstrings", "adductors", "calves" };
                case "lower": return new[] { "quads", "hamstrings", "glutes", "calves", "adductors" };
                case "upper": return Upper;
                case "arms": return new[] { "biceps", "triceps", "forearms" };
                case "upper-back": return new[] { "back", "shoulders" };
                case "": return new string[0];
                default: return new[] { focus };
            }
        }

        public Movement Prescribe(Exercise exercise, Profile profile, bool priority)
        {
            var movement = new Movement { Id = exercise.Id, Priority = priority, Sets = profile.Level == 0 ? 2 : 3, Target = "8–12 rep.", Rest = 90 };
            if ((profile.Goal == "muscle" || profile.Goal == "recomp") && profile.Level > 0 && FocusGroups(profile.Focus).Contains(exercise.Group)) movement.Sets = 4;
            if (profile.Level > 0 && profile.FocusMode == "priority" && profile.Focus != "" &&
                !FocusGroups(profile.Focus).Contains(exercise.Group) && !priority) movement.Sets = 2;
            if (profile.Goal == "strength" && exercise.Type == 'C')
            {
                movement.Target = profile.Level == 0 ? "6–10 rep." : "4–8 rep.";
                movement.Rest = 150;
            }
            if (exercise.Type == 'I') { movement.Target = "10–15 rep."; movement.Rest = 60; }
            if (exercise.Type == 'T') { movement.Target = "20–30 s"; movement.Rest = 60; }
            if (exercise.Type == 'A')
            {
                movement.Sets = 1;
                movement.Target = CardioDuration(profile, false);
                movement.Rest = 0;
                movement.CardioPhase = profile.Goal == "endurance" ? "before" : "after";
            }
            return movement;
        }

        private static readonly string[] Upper = { "chest", "back", "shoulders", "triceps", "biceps", "forearms" };
        private static readonly string[] Lower = { "quads", "glutes", "hamstrings", "calves", "core", "adductors" };
        private static readonly string[] Push = { "chest", "chest", "triceps", "shoulders", "back", "triceps" };
        private static readonly string[] Pull = { "back", "biceps", "shoulders", "chest", "forearms", "back" };

        private sealed class DayPlan
        {
            public string Title;
            public string[] Family;
            public List<string> Groups;
            public bool LegDay;
        }

        private static int ExerciseLimit(Profile profile, int slot)
        {
            int limit = profile.Minutes <= 30 ? 4 : (profile.Minutes <= 50 ? 5 : 6);
            if (AutoCardio(profile, slot) && (profile.Minutes <= 30 || profile.Goal == "endurance" || profile.Goal == "weight")) limit--;
            return limit;
        }

        private static DayPlan LegacyPlanGroups(Profile profile, int slot)
        {
            string title = slot < 0 ? "Treino extra" : "Corpo inteiro";
            string[] groups = slot % 2 == 0
                ? new[] { "quads", "chest", "back", "glutes", "core", "calves", "biceps", "shoulders" }
                : new[] { "glutes", "back", "chest", "hamstrings", "shoulders", "core", "triceps", "adductors" };
            if (slot >= 0 && profile.Days.Length >= 4)
            {
                switch (slot % 4)
                {
                    case 0: title = "Peito e tríceps"; groups = Push; break;
                    case 1: title = "Pernas e glúteos"; groups = Lower; break;
                    case 2: title = "Costas e bíceps"; groups = Pull; break;
                    case 3: title = "Glúteos e posteriores"; groups = new[] { "glutes", "hamstrings", "quads", "adductors", "core", "calves" }; break;
                }
            }
            if (profile.Days.Length == 3 && slot == 2)
                groups = new[] { "hamstrings", "chest", "back", "calves", "core", "forearms", "adductors" };

            int limit = ExerciseLimit(profile, slot);
            bool lower = groups.All(g => Lower.Contains(g));
            bool upper = groups.All(g => Upper.Contains(g));
            string[] family = lower ? Lower : upper ? Upper : Catalog.Groups;
            string[] focus = FocusGroups(profile.Focus).Where(family.Contains).ToArray();
            var baseGroups = groups.Take(limit).ToList();
            if (profile.FocusMode == "priority" && profile.Focus != "" && focus.Length == 0 && (upper || lower))
                baseGroups = (upper ? (slot % 4 == 0 ? Push : Pull) : Lower).Distinct().Take(Math.Max(3, limit - 1)).ToList();
            if (focus.Length > 0)
            {
                string primary = focus[0];
                int focusIndex = baseGroups.IndexOf(primary);
                if (focusIndex > 0) { baseGroups.RemoveAt(focusIndex); baseGroups.Insert(0, primary); }
                if (focusIndex < 0)
                {
                    baseGroups.RemoveAt(baseGroups.Count - 1);
                    baseGroups.Insert(0, primary);
                }
                var emphasis = focus.Length == 1 ? primary : focus[1];
                if ((profile.Goal == "muscle" || profile.Goal == "recomp" || profile.FocusMode == "priority") &&
                    baseGroups.Count(g => g == emphasis) < (focus.Length == 1 ? 2 : 1) && baseGroups.Last() != primary)
                {
                    baseGroups.RemoveAt(baseGroups.Count - 1);
                    baseGroups.Insert(Math.Min(2, baseGroups.Count), emphasis);
                }
            }
            return new DayPlan { Title = title, Family = family, Groups = baseGroups, LegDay = lower };
        }

        private Session Build(DateTime date, Profile profile, int slot)
        {
            var plan = PlanFor(date, profile, slot);
            var groups = plan.Groups;
            var session = new Session { Scheduled = slot >= 0, Title = plan.Title };
            bool cardio = AutoCardio(profile, slot);
            var priorities = new HashSet<string>();
            var prior = slot < 0 ? null : PreviousCoverage(date);
            if (prior != null)
            {
                priorities.UnionWith(groups.Where(group => prior[group].Expected > 0 && prior[group].DirectSets < prior[group].Target));
                var missing = plan.Family.Where(group => !groups.Contains(group) && prior[group].DirectSets < prior[group].Target)
                    .OrderByDescending(group => prior[group].Expected > 0).ThenByDescending(group => prior[group].Status == 0)
                    .ThenByDescending(group => prior[group].Target - prior[group].DirectSets).ToArray();
                if (missing.Length > 0)
                {
                    var focus = FocusGroups(profile.Focus);
                    if (groups.Count < ExerciseLimit(profile, slot))
                    {
                        groups.Add(missing[0]);
                        priorities.Add(missing[0]);
                    }
                    else
                    {
                        int replacement = groups.FindLastIndex(g => !focus.Contains(g) && groups.Count(x => x == g) > 1);
                        if (replacement < 0 && prior[missing[0]].Expected > 0)
                            replacement = groups.FindLastIndex(g => !focus.Contains(g) && !priorities.Contains(g));
                        if (replacement >= 2)
                        {
                            groups[replacement] = missing[0];
                            priorities.Add(missing[0]);
                        }
                    }
                }
            }

            var used = new HashSet<string>();
            int variation = slot == 0 ? 0 : Math.Max(0, slot) % 3;
            int weeks = (Monday(date) - Monday(Data.Profiles[0].Start)).Days / 7;
            bool variationDay = slot >= 0 && weeks >= 2 && weeks % 2 == 0 && (weeks / 2 - 1) % profile.Days.Length == slot;
            int accessory = groups.FindLastIndex(g => !priorities.Contains(g) && g != groups[0]);
            var familiarity = Familiarity(date);
            for (int position = 0; position < groups.Count; position++)
            {
                string group = groups[position];
                bool compound = position == 0 || group == "back" || group == "hamstrings" || group == "quads" ||
                    (group == "chest" && !session.Items.Any(m => Catalog.ById[m.Id].Group == "chest"));
                var selected = Pick(group, profile, used, variation, compound, familiarity,
                    session.Items.Select(item => Catalog.Pattern(item.Id)).ToArray());
                if (selected == null) continue;
                var baseExercise = selected;
                if (variationDay && position == accessory)
                {
                    var alternative = Catalog.All.Where(e => e.Group == group && e.Id != baseExercise.Id && !used.Contains(e.Id) &&
                        Eligible(e, profile) && familiarity[e.Id].ChoiceScore >= 0)
                        .OrderByDescending(e => Equivalent(baseExercise, e)).ThenByDescending(e => e.Type == baseExercise.Type)
                        .ThenByDescending(e => familiarity[e.Id].ChoiceScore).FirstOrDefault();
                    if (alternative != null) selected = alternative;
                }
                used.Add(selected.Id);
                var movement = Prescribe(selected, profile, priorities.Contains(group));
                movement.Varied = selected.Id != baseExercise.Id;
                session.Items.Add(movement);
            }
            if (cardio)
            {
                bool legDay = plan.LegDay;
                var selected = PickCardio(profile, slot, legDay, familiarity);
                if (selected != null)
                {
                    var aerobic = Prescribe(selected, profile, false);
                    aerobic.Target = CardioDuration(profile, legDay);
                    if (aerobic.CardioPhase == "before") session.Items.Insert(0, aerobic);
                    else session.Items.Add(aerobic);
                }
            }
            return session;
        }

        private Exercise Pick(string group, Profile profile, HashSet<string> used, int variation, bool compound,
            Dictionary<string, ExerciseUse> familiarity, string[] usedPatterns = null)
        {
            var options = Catalog.All.Where(e => e.Group == group && !used.Contains(e.Id) && Eligible(e, profile)).ToArray();
            if (options.Length == 0) return null;
            var accepted = options.Where(e => familiarity[e.Id].ChoiceScore >= 0).ToArray();
            if (accepted.Length > 0) options = accepted;
            if (usedPatterns != null)
            {
                var fresh = options.Where(e => !usedPatterns.Contains(Catalog.Pattern(e.Id))).ToArray();
                if (fresh.Length > 0) options = fresh;
            }
            if (profile.Goal == "strength" && profile.Level > 0 && compound)
            {
                var barbell = options.FirstOrDefault(e => e.Gear == "barbell" && e.Type == 'C');
                if (barbell != null) return barbell;
            }
            var preferred = options.Where(e => compound ? e.Type == 'C' : e.Type != 'C').ToArray();
            if (preferred.Length > 0) options = preferred;
            int best = options.Max(e => familiarity[e.Id].ChoiceScore);
            var potential = options.Where(e => familiarity[e.Id].ChoiceScore == best).ToArray();
            return potential[variation % potential.Length];
        }

        private Dictionary<string, MuscleWeek> PreviousCoverage(DateTime date)
        {
            var start = Monday(date).AddDays(-7);
            var result = Catalog.Groups.ToDictionary(g => g, g => new MuscleWeek());
            for (int offset = 0; offset < 7; offset++)
            {
                var day = start.AddDays(offset);
                if (day > Today) break;
                AddExpected(result, day);
                Session session;
                if (Data.Sessions.TryGetValue(Key(day), out session))
                    foreach (var item in session.Items.Where(m => m.Done)) AddCoverage(result, day, item);
            }
            return result.Values.Any(g => g.DoneExercises > 0) ? result : null;
        }

        private void AddExpected(Dictionary<string, MuscleWeek> coverage, DateTime date)
        {
            if (NeedsSetup || !IsTrackedDate(date) || date < Data.Profiles[0].Start) return;
            var profile = At(date);
            int slot = Array.IndexOf(profile.Days, Day(date));
            if (slot < 0) return;
            var plan = PlanFor(date, profile, slot);
            var focus = FocusGroups(profile.Focus);
            foreach (string group in plan.Groups)
            {
                if (!Catalog.All.Any(e => e.Group == group && Eligible(e, profile))) continue;
                int expected = profile.Level == 0 || (profile.FocusMode == "priority" && profile.Focus != "" && !focus.Contains(group)) ? 2 : 3;
                if (profile.Level > 0 && (profile.Goal == "muscle" || profile.Goal == "recomp") && focus.Contains(group)) expected = 4;
                coverage[group].Expected += expected;
                coverage[group].PlannedDays.Add(date.Date);
            }
        }

        private static void AddCoverage(Dictionary<string, MuscleWeek> coverage, DateTime date, Movement item)
        {
            var exercise = Catalog.ById[item.Id];
            if (exercise.Type == 'A') return;
            int sets = item.ActualSets > 0 ? item.ActualSets : item.Sets;
            coverage[exercise.Group].DirectSets += sets;
            coverage[exercise.Group].DoneExercises++;
            coverage[exercise.Group].DirectDays.Add(date.Date);
            if (item.RepsInReserve.HasValue)
            {
                coverage[exercise.Group].EffortLogs++;
                if (item.RepsInReserve.Value >= 4) coverage[exercise.Group].EasyEfforts++;
            }
            foreach (string group in exercise.Other) coverage[group].SupportSets += sets;
        }

        public WeekReport Report(DateTime date)
        {
            var start = Monday(date);
            return ReportRange(start, start.AddDays(7));
        }

        public WeekReport ReportMonth(DateTime date)
        {
            var start = new DateTime(date.Year, date.Month, 1);
            return ReportRange(start, start.AddMonths(1));
        }

        private WeekReport ReportRange(DateTime start, DateTime end)
        {
            var report = new WeekReport();
            if (start < Data.FirstAccess) start = Data.FirstAccess;
            for (var day = start; day < end; day = day.AddDays(1))
            {
                var session = Get(day);
                if (day <= Today) AddExpected(report.Coverage, day);
                if (session.Scheduled) report.Scheduled++;
                report.PlannedExercises += session.Items.Count;
                if (day > Today) continue;
                if (Present(session)) report.Attended++;
                var pending = PendingExercises(day, session);
                if (pending.Count > 0) report.Pending.Add(day, pending);
                if (session.Scheduled)
                {
                    var makeup = Makeup(day);
                    bool fulfilled = Present(session) || (makeup.HasValue && makeup.Value <= Today && Present(Get(makeup.Value)));
                    if (day < Today || fulfilled || session.Attendance == "missed")
                    {
                        report.Due++;
                        if (fulfilled) report.Compensated++;
                        else if (session.Attendance == "missed") report.Missed++;
                        else report.Unmarked++;
                    }
                }
                foreach (var item in session.Items.Where(m => m.Done))
                {
                    var exercise = Catalog.ById[item.Id];
                    report.Done++;
                    int count;
                    report.Direct.TryGetValue(exercise.Group, out count);
                    report.Direct[exercise.Group] = count + 1;
                    report.Indirect.UnionWith(exercise.Other);
                    AddCoverage(report.Coverage, day, item);
                    report.Sets += exercise.Type == 'A' ? 0 : (item.ActualSets > 0 ? item.ActualSets : item.Sets);
                }
            }
            return report;
        }

        public List<Movement> PendingExercises(DateTime date, Session session)
        {
            if (date.Date > Today || !IsTrackedDate(date) || (!session.Finalized && !(date.Date < Today && Present(session))) || Makeup(date).HasValue)
                return new List<Movement>();
            return session.Items.Where(item => !item.Done).ToList();
        }

        public List<LiftRecord> LiftHistory(string id, DateTime through)
        {
            string key = Key(through < Today ? through : Today);
            string access = Key(Data.FirstAccess);
            return Data.Sessions.Where(p => string.CompareOrdinal(p.Key, access) >= 0 && string.CompareOrdinal(p.Key, key) <= 0).OrderByDescending(p => p.Key)
                .SelectMany(p => p.Value.Items.Where(m => m.Id == id && m.Done && HasPerformance(m))
                    .Select(m => new LiftRecord { Date = Date(p.Key), Movement = m })).ToList();
        }

        public LiftRecord PreviousLoad(DateTime day, string id)
        {
            return LiftHistory(id, day.AddDays(-1)).FirstOrDefault(record => record.Peak.HasValue);
        }

        public DateTime? NextGroup(DateTime date, string group)
        {
            for (int i = 1; i <= 14; i++)
            {
                var day = date.AddDays(i);
                if (Makeup(day).HasValue) continue;
                if (Get(day).Items.Any(item => Catalog.ById[item.Id].Group == group)) return day;
            }
            return null;
        }

        public void RecordPerformance(DateTime date, int index, List<LiftSet> efforts, int minutes, int? repsInReserve, bool done)
        {
            RequireRecordingDay(date);
            if (Get(date).Finalized) throw new InvalidOperationException("Reabra o treino antes de alterar o desempenho.");
            var entry = Save(date);
            var movement = entry.Items[index];
            movement.Efforts = efforts;
            movement.RepsInReserve = repsInReserve;
            movement.ActualSets = efforts == null ? 0 : efforts.Count;
            if (efforts == null) movement.ActualReps = minutes;
            else
            {
                int?[] reps = efforts.Select(set => set.RepsOrSeconds).Distinct().ToArray();
                movement.ActualReps = reps.Length == 1 ? reps[0].GetValueOrDefault() : 0;
            }
            movement.Load = efforts == null ? 0 : new LiftRecord { Movement = movement }.Peak.GetValueOrDefault();
            movement.Done = done;
            if (done && entry.Attendance == "missed") entry.Attendance = "";
        }
    }
}
