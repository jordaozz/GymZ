using Microsoft.JSInterop;

namespace GymZ.Mobile;

public partial class App
{
    public sealed class Series
    {
        public bool Done { get; set; }
        public string Kg { get; set; } = "";
        public string Reps { get; set; } = "";
        public bool HasData => Done || !string.IsNullOrWhiteSpace(Kg) || !string.IsNullOrWhiteSpace(Reps);
    }

    public sealed class WorkoutDraft
    {
        public string BaseVersion { get; set; }
        public List<Series> Rows { get; set; } = new();
        public string Minutes { get; set; } = "";
        public string Reserve { get; set; } = "";
        public bool Quick { get; set; }
        public bool ReserveOpen { get; set; }
    }

    private WorkoutDraft draft;
    private string draftId, draftDay, quickId, lastSavedId;
    private string draftStatus = "", entryError = "";
    private readonly Dictionary<string, string> fieldErrors = new();
    private bool autoRest, openingEntry;
    private int quickRow;
    private RestTimer restTimer;

    private static string CardId(string id) => "workout-" + id;
    private static string QuickPanelId(string id) => "quick-" + id;
    private static string EntryId(string field, int row = -1) => row < 0 ? "entry-" + field : $"entry-{field}-{row}";
    private string EntryErrorFor(string field, int row = -1) => fieldErrors.GetValueOrDefault(EntryId(field, row)) ?? "";
    private static int RecordedRows(Movement item) => item.Efforts?.Count ??
        Math.Max(item.ActualSets, item.Load > 0 || item.ActualReps > 0 ? 1 : 0);
    private static string EntryVersion(Movement item) =>
        FormattableString.Invariant($"{item.Load}|{item.ActualReps}|{item.ActualSets}|{item.RepsInReserve}|") +
        string.Join(",", item.Efforts?.Select(s => FormattableString.Invariant($"{s.Kilograms}/{s.RepsOrSeconds}")) ?? Array.Empty<string>());

    private static WorkoutDraft NewDraft(Movement item, bool cardio)
    {
        var initial = new WorkoutDraft {
            BaseVersion = EntryVersion(item),
            Minutes = cardio ? NumberLabel(item.ActualReps) : "",
            Reserve = item.RepsInReserve?.ToString(Training.Pt) ?? "",
            ReserveOpen = item.RepsInReserve.HasValue
        };
        if (!cardio)
        {
            for (int i = 0; i < RecordedRows(item); i++)
            {
                var history = item.Efforts == null
                    ? new LiftSet { Kilograms = item.Load > 0 ? item.Load : null, RepsOrSeconds = item.ActualReps > 0 ? item.ActualReps : null }
                    : item.Efforts[i];
                initial.Rows.Add(new Series {
                    Done = true, Kg = history.Kilograms?.ToString("0.##", Training.Pt) ?? "",
                    Reps = history.RepsOrSeconds?.ToString(Training.Pt) ?? ""
                });
            }
        }
        return initial;
    }

    private async Task OpenLog(int index, bool quick = false)
    {
        if (busy || openingEntry) return;
        openingEntry = true;
        try
        {
            var day = date;
            var view = page;
            var activeTab = tab;
            var entry = training.Get(day);
            var item = entry.Items[index];
            var target = Catalog.ById[item.Id];
            WorkoutDraft restored = null;
            try { restored = await JS.InvokeAsync<WorkoutDraft>("gymz.draft", Training.Key(day), item.Id); }
            catch (JSException) { /* A fresh form remains available if draft storage is temporarily inaccessible. */ }
            if (day != date || view != page || activeTab != tab || training.Get(day).Items.ElementAtOrDefault(index)?.Id != item.Id) return;

            if (restored?.BaseVersion != EntryVersion(item) || restored?.Rows == null) restored = null;
            draft = restored ?? NewDraft(item, target.Type == 'A');
            draftId = item.Id;
            draftDay = Training.Key(day);
            bool showQuick = quick && !item.Done && target.Type != 'A' && RecordedRows(item) < 30 &&
                (restored == null || restored.Quick);
            draft.Quick = showQuick;
            fieldErrors.Clear();
            entryError = "";
            draftStatus = restored == null ? "" : "Rascunho recuperado.";
            editing = index;
            exercise = target;
            quickId = showQuick ? item.Id : null;
            if (showQuick)
            {
                quickRow = RecordedRows(item);
                var row = quickRow < draft.Rows.Count ? draft.Rows[quickRow] : new Series();
                if (restored == null) SuggestSeries(row, entry, item, quickRow);
                if (quickRow == draft.Rows.Count) draft.Rows.Add(row);
                if (restored == null && row.HasData) draftStatus = "Valores anteriores sugeridos. Edite se precisar.";
                fieldFocus = QuickPanelId(item.Id);
                return;
            }
            if (target.Type != 'A')
                while (draft.Rows.Count < item.Sets) draft.Rows.Add(new Series());
            Sub(Page.Log, CardId(item.Id));
        }
        finally { openingEntry = false; }
    }

    private void SuggestSeries(Series row, Session entry, Movement item, int index)
    {
        var current = item.Efforts?.LastOrDefault();
        var prior = entry.Complement ? null : training.PreviousLoad(date, item.Id)?.Movement;
        var previous = prior?.Efforts?.ElementAtOrDefault(index) ?? prior?.Efforts?.LastOrDefault();
        var kg = current?.Kilograms ?? previous?.Kilograms ?? (prior?.Load > 0 ? prior.Load : null);
        var reps = current?.RepsOrSeconds ?? previous?.RepsOrSeconds ?? (prior?.ActualReps > 0 ? prior.ActualReps : null);
        row.Kg = kg?.ToString("0.##", Training.Pt) ?? "";
        row.Reps = reps?.ToString(Training.Pt) ?? "";
    }

    private async Task UpdateEntry(Action update, string field = null)
    {
        update();
        if (field != null) fieldErrors.Remove(field);
        entryError = "";
        try
        {
            await JS.InvokeVoidAsync("gymz.writeDraft", draftDay, draftId, draft);
            draftStatus = "Rascunho guardado";
        }
        catch (JSException) { draftStatus = "Salve o registro antes de sair para guardar estes valores."; }
    }

    private void CloseQuick()
    {
        if (busy || quickId == null) return;
        fieldFocus = CardId(quickId);
        quickId = null;
    }

    private decimal? EntryNumber(string value, string field, string label, decimal min, decimal max, bool whole = false)
    {
        try { return Parse(value, label, min, max, whole); }
        catch (InvalidOperationException ex) { fieldErrors[field] = ex.Message; return null; }
    }

    private LiftSet ReadSeries(Series row, int index)
    {
        var kg = EntryNumber(row.Kg, EntryId("kg", index), "a carga", 0, 1500);
        var reps = (int?)EntryNumber(row.Reps, EntryId("reps", index), exercise.Type == 'T' ? "os segundos" : "as repetições", 1, 200, true);
        return new LiftSet { Kilograms = kg, RepsOrSeconds = reps };
    }

    private bool EntryHasData => draft != null && (exercise.Type == 'A' ? !string.IsNullOrWhiteSpace(draft.Minutes) :
        draft.Rows.Any(row => row.HasData) || !string.IsNullOrWhiteSpace(draft.Reserve));

    private async Task SaveEntry(bool complete, bool unrecorded = false)
    {
        if (busy) return;
        var entry = training.Get(date);
        var item = entry.Items[editing];
        bool quick = quickId != null;
        bool cardio = exercise.Type == 'A';
        int pastRows = cardio ? 0 : RecordedRows(item);
        var id = item.Id;
        fieldErrors.Clear();
        entryError = "";
        List<LiftSet> efforts = null;
        int duration = 0;
        int? reserve = null;

        if (!unrecorded)
        {
            if (quick)
            {
                var value = ReadSeries(draft.Rows[quickRow], quickRow);
                if (!value.RepsOrSeconds.HasValue)
                    fieldErrors.TryAdd(EntryId("reps", quickRow), exercise.Type == 'T' ? "Informe os segundos." : "Informe as repetições.");
                efforts = draft.Rows.Take(quickRow).Select(ReadSeries).Append(value).ToList();
                reserve = item.RepsInReserve;
            }
            else if (cardio)
            {
                duration = (int)(EntryNumber(draft.Minutes, EntryId("minutes"), "os minutos", 1, 200, true) ?? 0);
                if (duration == 0) fieldErrors.TryAdd(EntryId("minutes"), "Informe os minutos praticados.");
            }
            else
            {
                efforts = draft.Rows.Select((row, i) => (row, i)).Where(pair => pair.row.HasData)
                    .Select(pair => ReadSeries(pair.row, pair.i)).ToList();
                if (exercise.Type != 'T')
                    reserve = (int?)EntryNumber(draft.Reserve, EntryId("reserve"), "as repetições em reserva", 0, 10, true);
                if (efforts.Count == 0) entryError = "Preencha ou marque pelo menos uma série.";
            }
        }
        if (fieldErrors.Count > 0 || entryError != "")
        {
            fieldFocus = fieldErrors.Keys.FirstOrDefault() ?? (quick ? QuickPanelId(id) : "entry-feedback");
            return;
        }

        await TodayChange(() => {
            if (unrecorded)
            {
                var stored = training.Save(date).Items[editing];
                stored.Done = true;
                if (training.Get(date).Attendance == "missed") training.Attendance(date, "");
            }
            else training.RecordPerformance(date, editing, efforts, duration, reserve, complete || item.Done);
        }, clearDraft: id);
        if (error != "")
        {
            entryError = error;
            error = "";
            fieldFocus = quick ? QuickPanelId(id) : "entry-feedback";
            return;
        }

        bool sessionDone = Training.Complete(training.Get(date));
        if (sessionDone) review = false;
        if (quick) CloseQuick();
        else if (page == Page.Log) Back();
        lastSavedId = id;
        var next = training.Get(date).Items.FirstOrDefault(m => !m.Done);
        if (sessionDone) fieldFocus = "workout-finished";
        else if (autoRest && !cardio && !unrecorded && efforts?.Count > pastRows && restTimer != null)
            await restTimer.Start(complete && next != null ? Between(item, next) : item.Rest, exercise.Name);
    }

    private Task CopyLoads()
    {
        var previous = training.PreviousLoad(date, exercise.Id)?.Movement;
        if (previous == null) return Task.CompletedTask;
        return UpdateEntry(() => {
            var loads = previous.Efforts?.Select(s => s.Kilograms).ToArray() ??
                Enumerable.Repeat<decimal?>(previous.Load, Math.Max(1, previous.ActualSets)).ToArray();
            for (int i = 0; i < Math.Min(draft.Rows.Count, loads.Length); i++)
                if (draft.Rows[i].Kg == "" && loads[i].HasValue) draft.Rows[i].Kg = loads[i].Value.ToString("0.##", Training.Pt);
        });
    }

    private static int Between(Movement from, Movement to) => Math.Max(60, Math.Max(from.Rest, to.Rest));

    private async Task SetAutoRest(bool value)
    {
        try
        {
            await JS.InvokeVoidAsync("gymz.setAutoRest", value);
            autoRest = value;
        }
        catch (JSException) { error = "O navegador não conseguiu salvar a preferência do timer."; }
    }
}
