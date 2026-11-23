using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace GymZ
{
    internal sealed class Storage : IDisposable
    {
        public const string DataName = "gymz.json";
        public const string BackupName = "gymz-backup.json";
        public const string FolderName = "GymZ";
        private static readonly string[] Themes = { "dark", "light" };
        internal static readonly string[] Accents = { "blue", "teal", "violet", "rose", "amber" };
        internal static readonly int[] Scales = { 100, 110, 125 };
        private readonly FileStream gate;
        private readonly string path;
        public string Folder { get; private set; }

        public Storage(string folder)
        {
            Folder = folder;
            Directory.CreateDirectory(folder);
            path = Path.Combine(folder, DataName);
            gate = new FileStream(Path.Combine(folder, "app.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }

        public GymData Open(DateTime today)
        {
            return File.Exists(path) ? Read(path) : GymData.Create(today);
        }

        public void Save(GymData data)
        {
            Write(path, data, true);
        }

        public string Backup(string parent, GymData data, string executable)
        {
            string folder = Path.Combine(parent, "GymZ-backup-" + DateTime.Now.ToString("yyyy-MM-dd-HHmmss-fff", CultureInfo.InvariantCulture));
            Directory.CreateDirectory(folder);
            Write(Path.Combine(folder, BackupName), data, false);
            File.Copy(executable, Path.Combine(folder, "GymZ.exe"));
            return folder;
        }

        public string Restore(GymData oldData, GymData replacement)
        {
            string history = Path.Combine(Folder, "Backups");
            Directory.CreateDirectory(history);
            string previous = Path.Combine(history, "antes-de-restaurar-" + DateTime.Now.ToString("yyyy-MM-dd-HHmmss-fff", CultureInfo.InvariantCulture) + ".json");
            Write(previous, oldData, false);
            Save(replacement);
            return previous;
        }

        private static DataContractJsonSerializer Serializer()
        {
            return new DataContractJsonSerializer(typeof(GymData), new DataContractJsonSerializerSettings {
                DateTimeFormat = new DateTimeFormat("yyyy-MM-dd", CultureInfo.InvariantCulture), UseSimpleDictionaryFormat = true
            });
        }

        internal static string Pack(GymData data)
        {
            using (var buffer = new MemoryStream())
            {
                Serializer().WriteObject(buffer, data);
                return System.Text.Encoding.UTF8.GetString(buffer.ToArray());
            }
        }

        internal static GymData Unpack(byte[] bytes)
        {
            using (var buffer = new MemoryStream(bytes, false))
                return ReadBackup(buffer);
        }

        private static void Write(string filename, GymData data, bool replace)
        {
            string pending = filename + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var stream = new FileStream(pending, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    Serializer().WriteObject(stream, data);
                    stream.Flush(true);
                }
                if (replace && File.Exists(filename)) File.Replace(pending, filename, null);
                else File.Move(pending, filename);
            }
            finally { if (File.Exists(pending)) File.Delete(pending); }
        }

        public static GymData Read(string filename)
        {
            try
            {
                using (var stream = File.OpenRead(filename))
                {
                    return ReadBackup(stream);
                }
            }
            catch (SerializationException ex) { throw new InvalidDataException("O arquivo não contém dados válidos do GymZ.", ex); }
            catch (FormatException ex) { throw new InvalidDataException("O arquivo contém uma data inválida.", ex); }
        }

        private static GymData ReadBackup(Stream stream)
        {
            if (stream.Length > 32 * 1024 * 1024) throw new InvalidDataException("O arquivo de dados excede 32 MB.");
            return Validate((GymData)Serializer().ReadObject(stream));
        }

        private static GymData Validate(GymData data)
        {
            if (data == null || data.Version != 1 || data.Profiles == null || data.Profiles.Count == 0 || data.Sessions == null || data.Preferences == null)
                throw new InvalidDataException("Versão de dados do GymZ desconhecida ou arquivo incompleto.");
            if (data.Avoided == null) data.Avoided = new List<string>();
            if (data.Avoided.Any(id => id == null || !Catalog.ById.ContainsKey(id)))
                throw new InvalidDataException("O arquivo contém preferências por exercícios inválidas.");
            data.Avoided = data.Avoided.Distinct().ToList();
            if (data.Swaps == null) data.Swaps = new List<ExerciseSwap>();
            if (data.Swaps.Count > 1500 || data.Swaps.Any(s => s == null || s.Date.Year < 1900 || s.From == null || s.To == null || s.From == s.To ||
                !Catalog.ById.ContainsKey(s.From) || !Catalog.ById.ContainsKey(s.To)))
                throw new InvalidDataException("O arquivo contém histórico de substituições inválido.");
            var preferences = data.Preferences;
            if (!Themes.Contains(preferences.Theme) || !Accents.Contains(preferences.Accent) || !Scales.Contains(preferences.Scale))
                throw new InvalidDataException("O arquivo contém opções de aparência inválidas.");
            foreach (var revision in data.Profiles)
            {
                if (revision == null || revision.Start.Year < 1900 || revision.Profile == null)
                    throw new InvalidDataException("O arquivo contém um perfil inválido.");
                var profile = revision.Profile;
                if (profile.Cardio == null) profile.Cardio = "auto";
                if (profile.Name == null) profile.Name = "";
                if (profile.FocusMode == null) profile.FocusMode = "balanced";
                if ((profile.Split != null && !Training.SplitModes.ContainsKey(profile.Split)) ||
                    (profile.DayThemes != null && (profile.DayThemes.Length != 7 || !profile.DayThemes.All(Training.IsDayTheme))))
                    throw new InvalidDataException("O arquivo contém uma divisão semanal inválida.");
                if (profile.Days == null || profile.Days.Length == 0 || !profile.Days.SequenceEqual(profile.Days.Distinct().OrderBy(d => d)) ||
                    profile.Days.Any(d => d < 0 || d > 6) || profile.Goal == null || !Catalog.Goals.ContainsKey(profile.Goal) ||
                    profile.Gear == null || !Catalog.ProfileGear.ContainsKey(profile.Gear) || !Catalog.ProfileCardio.ContainsKey(profile.Cardio) || profile.Focus == null ||
                    (!Catalog.FocusOptions.ContainsKey(profile.Focus) && !Catalog.Groups.Contains(profile.Focus)) || !Catalog.FocusModes.ContainsKey(profile.FocusMode) ||
                    profile.Name.Length > 48 || profile.Name.Any(char.IsControl) || profile.Minutes < 20 || profile.Minutes > 90 ||
                    profile.Level < 0 || profile.Level > 1 || profile.Weight < 0 || profile.Weight > 600 || profile.Height < 0 || profile.Height > 300)
                    throw new InvalidDataException("O arquivo contém preferências de treino inválidas.");
            }
            data.Profiles = data.Profiles.OrderBy(p => p.Start).ToList();
            if (data.FirstAccess == default(DateTime)) data.FirstAccess = data.Profiles[0].Start;
            if (data.FirstAccess.Year < 1900 || data.FirstAccess > data.Profiles[0].Start)
                throw new InvalidDataException("A data de início do GymZ é inválida.");
            if (data.SplitStart.HasValue && data.SplitStart.Value < data.FirstAccess)
                throw new InvalidDataException("A data de atualização da divisão semanal é inválida.");
            if (data.Profiles.Select(p => p.Start).Distinct().Count() != data.Profiles.Count)
                throw new InvalidDataException("O arquivo contém perfis duplicados.");
            var origins = new HashSet<string>();
            foreach (var pair in data.Sessions)
            {
                var date = Training.Date(pair.Key);
                var session = pair.Value;
                if (date.Year < 1900 || session == null || string.IsNullOrWhiteSpace(session.Title) || session.Items == null || session.Items.Count > Catalog.All.Count ||
                    (session.Attendance != "" && session.Attendance != "present" && session.Attendance != "missed") || session.MakeupFor == null)
                    throw new InvalidDataException("O arquivo contém um treino inválido.");
                if (session.MakeupFor != "" && (!origins.Add(session.MakeupFor) || Training.Date(session.MakeupFor) >= date ||
                    !data.Sessions.ContainsKey(session.MakeupFor) || !session.Extra || session.Scheduled))
                    throw new InvalidDataException("O arquivo contém uma reposição inválida.");
                if (session.ComplementFor == null) session.ComplementFor = "";
                if (session.ComplementGroup == null) session.ComplementGroup = "";
                if (session.Complement && (!session.Extra || session.Scheduled || session.MakeupFor != "" ||
                    (session.ComplementFor != "" && Training.Date(session.ComplementFor) >= date) ||
                    (session.ComplementGroup != "" && !Catalog.Groups.Contains(session.ComplementGroup))))
                    throw new InvalidDataException("O arquivo contém um treino extra inválido.");
                if (session.Finalized && ((!session.Extra && !session.Scheduled) || session.Items.Count == 0))
                    throw new InvalidDataException("O arquivo contém um treino finalizado inválido.");
                var movements = new HashSet<string>();
                foreach (var item in session.Items)
                {
                    if (item == null || item.Id == null || !Catalog.ById.ContainsKey(item.Id) || !movements.Add(item.Id) || item.Sets < 1 || item.Sets > 12 ||
                        item.ActualSets < 0 || item.ActualSets > 30 || item.ActualReps < 0 || item.ActualReps > 200 || item.Load < 0 || item.Load > 1500 ||
                        item.Rest < 0 || item.Rest > 600 || item.RepsInReserve < 0 || item.RepsInReserve > 10 || string.IsNullOrWhiteSpace(item.Target))
                        throw new InvalidDataException("O arquivo contém um exercício inválido.");
                    if (item.CardioPhase == null) item.CardioPhase = "";
                    if (item.CardioPhase != "" && (Catalog.ById[item.Id].Type != 'A' || (item.CardioPhase != "before" && item.CardioPhase != "after")))
                        throw new InvalidDataException("O arquivo contém uma fase de cardio inválida.");
                    if (item.Efforts == null) continue;
                    if (item.Efforts.Count > 30 || item.ActualSets != item.Efforts.Count || Catalog.ById[item.Id].Type == 'A' ||
                        item.Efforts.Any(s => s == null ||
                            s.Kilograms < 0 || s.Kilograms > 1500 || s.RepsOrSeconds < 1 || s.RepsOrSeconds > 200) ||
                        new LiftRecord { Movement = item }.Peak.GetValueOrDefault() != item.Load)
                        throw new InvalidDataException("O arquivo contém registros de séries ou cargas inválidos.");
                }
            }
            return data;
        }

        public void Dispose() { gate.Dispose(); }
    }
}
