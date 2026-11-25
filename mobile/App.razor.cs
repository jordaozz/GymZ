using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;

namespace GymZ.Mobile;

public partial class App : IDisposable
{
    private enum Tab { Workouts, Attendance, Map, Reports, More, Overview, Profile, Settings }
    private enum Page { Root, Workout, Guide, History, Log, Edit, Calendar, Makeup, Plan, Coverage, Regions, Notes }

    private static readonly Dictionary<string, string> ExperienceOptions = new() {
        ["0"] = "Iniciante ou retomando", ["1"] = "Intermediário / avançado"
    };
    private static readonly Dictionary<string, string> SwapModeOptions = new() {
        ["learn"] = "Usar nas sugestões futuras", ["temporary"] = "Trocar só nesta ficha", ["avoid"] = "Evitar o exercício original"
    };

    [Inject] private IJSRuntime JS { get; set; }
    private Training training;
    private GymData Data => training.Data;
    private DateTime Today => training.Today;
    private Tab tab = Tab.Workouts;
    private Page page;
    private DateTime date, week, month;
    private string saved, notice = "", error = "", startupError = "";
    private bool busy, review, rear, offlineReady, canInstall;
    private string selected = "", search = "", detailGroup = "";
    private Exercise exercise;
    private Profile form;
    private string weight = "", height = "";
    private string calendarFrom = "";
    private string editGroup = "", editGear = "", editQuery = "", editMode = "learn";
    private string swapScope = "workout";
    private string editOriginalId;
    private int? profileRegionDay;
    private HashSet<string> regionDraft = new();
    private string workoutNotes;
    private int editing;
    private GymData importData;
    private bool hasRestorePoint;
    private DotNetObjectReference<App> listener;
    private bool returnToWorkout, moveScreen;
    private string workoutCard, screenFocus, fieldFocus;
    private int ScreenDepth => page == Page.Root ? 0 : returnToWorkout ? 2 : 1;

    protected override async Task OnInitializedAsync()
    {
        var today = Training.Date(await JS.InvokeAsync<string>("gymz.today"));
        try
        {
            saved = await JS.InvokeAsync<string>("gymz.load");
            var data = saved == null ? GymData.Create(today) : Unpack(saved);
            training = new Training(data, today);
            if (training.NeedsSetup) tab = Tab.Profile;
            date = Today;
            week = Training.Monday(Today);
            month = MonthOf(Today);
            PrepareProfile();
            hasRestorePoint = await JS.InvokeAsync<bool>("gymz.hasRestorePoint");
            try { autoRest = await JS.InvokeAsync<bool>("gymz.autoRest"); }
            catch (JSException) { autoRest = false; }
            if (saved == null || Storage.Pack(Data) != saved)
            {
                var initial = Storage.Pack(Data);
                await JS.InvokeVoidAsync("gymz.save", initial, saved, false);
                saved = initial;
            }
        }
        catch
        {
            startupError = "O GymZ não conseguiu acessar seus dados locais. Exporte uma cópia ou recarregue a página.";
            training = null;
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            listener = DotNetObjectReference.Create(this);
            await JS.InvokeVoidAsync("gymz.start", listener);
        }
        if (moveScreen)
        {
            moveScreen = false;
            var target = screenFocus;
            screenFocus = null;
            await JS.InvokeVoidAsync("gymz.screen", ScreenDepth, target);
        }
        if (fieldFocus != null)
        {
            var target = fieldFocus;
            fieldFocus = null;
            await JS.InvokeVoidAsync("gymz.focus", target);
        }
    }

    [JSInvokable]
    public Task DeviceState(string today, bool offline, bool install)
    {
        offlineReady = offline;
        canInstall = install;
        if (training != null && !busy && Training.Date(today) != Today)
        {
            var previous = Today;
            training.Today = Training.Date(today);
            if (week == Training.Monday(previous)) week = Training.Monday(Today);
            if (month == MonthOf(previous)) month = MonthOf(Today);
            if (tab == Tab.Workouts || ((page is Page.Log or Page.Notes) && !training.CanRecordOn(date)))
            {
                page = Page.Root;
                date = Today;
                review = false;
                returnToWorkout = false;
                workoutCard = quickId = lastSavedId = null;
                moveScreen = true;
            }
        }
        return InvokeAsync(StateHasChanged);
    }

    [JSInvokable]
    public Task DeviceBack(int depth)
    {
        if (page != Page.Root) BackTo(depth);
        return InvokeAsync(StateHasChanged);
    }

    private static GymData Unpack(string json) => Storage.Unpack(Encoding.UTF8.GetBytes(json.TrimStart('\uFEFF')));

    private async Task Change(Action action, string confirmation = "", bool restore = false, string clearDraft = null, DateTime? draftDate = null)
    {
        if (busy) return;
        busy = true;
        error = notice = "";
        var snapshot = Storage.Pack(Data);
        try
        {
            if (Training.Date(await JS.InvokeAsync<string>("gymz.today")) != Today)
                throw new InvalidOperationException("O dia mudou. Abra novamente o treino de hoje.");
            action();
            var content = Storage.Pack(Data);
            await JS.InvokeVoidAsync("gymz.save", content, saved, restore, Training.Key(draftDate ?? Today), clearDraft);
            saved = content;
            if (restore) hasRestorePoint = true;
            notice = confirmation;
        }
        catch (InvalidOperationException ex)
        {
            training = new Training(Unpack(snapshot), Today);
            error = ex.Message;
            fieldFocus = "app-error";
        }
        catch (JSException ex)
        {
            training = new Training(Unpack(snapshot), Today);
            error = ex.Message.Contains("gymz-conflict", StringComparison.Ordinal)
                ? "Os dados mudaram em outra janela. Recarregue para continuar com a versão mais recente."
                : "A alteração ficou sem salvar. Verifique o espaço ou as permissões de armazenamento deste navegador.";
            fieldFocus = "app-error";
        }
        finally { busy = false; }
    }

    private void Open(Tab destination)
    {
        if (busy || (training.NeedsSetup && destination is not (Tab.Profile or Tab.Settings))) return;
        tab = destination;
        page = Page.Root;
        date = Today;
        week = Training.Monday(Today);
        month = MonthOf(Today);
        review = false;
        returnToWorkout = false;
        workoutCard = quickId = null;
        notice = error = "";
        if (tab == Tab.Profile) PrepareProfile();
        moveScreen = true;
    }

    private void Sub(Page target, string card = null)
    {
        if (target == Page.Workout) returnToWorkout = false;
        else if (page == Page.Workout) returnToWorkout = true;
        if (card != null) workoutCard = card;
        page = target;
        quickId = null;
        error = notice = "";
        moveScreen = true;
    }

    private void Back()
    {
        if (busy) return;
        if (page != Page.Root) BackTo(ScreenDepth - 1);
        else if (tab is Tab.Settings or Tab.Profile or Tab.Overview)
            Open(training.NeedsSetup ? Tab.Profile : Tab.More);
    }

    private void BackTo(int depth)
    {
        page = returnToWorkout && depth == 1 ? Page.Workout : Page.Root;
        returnToWorkout = false;
        if (page == Page.Root && tab == Tab.Workouts) date = Today;
        if (page == Page.Workout || tab == Tab.Workouts) screenFocus = workoutCard;
        workoutCard = quickId = null;
        notice = error = "";
        moveScreen = true;
    }

    private void PrepareProfile()
    {
        var current = training.At(Today);
        form = new Profile {
            Name = current.Name, Goal = training.NeedsSetup ? "" : current.Goal, Focus = current.Focus,
            Gear = current.Gear, Cardio = current.Cardio, FocusMode = current.FocusMode, Split = current.Split ?? "auto",
            Level = current.Level, Minutes = current.Minutes, Weight = current.Weight, Height = current.Height,
            Days = training.NeedsSetup ? Array.Empty<int>() : current.Days.ToArray(),
            DayRoutines = Training.EditDayRoutines(current)
        };
        weight = NumberLabel(form.Weight);
        height = NumberLabel(form.Height);
    }

    private void ToggleDay(int day)
    {
        form.Days = form.Days.Contains(day) ? form.Days.Where(d => d != day).ToArray() : form.Days.Append(day).OrderBy(d => d).ToArray();
    }

    private void OpenProfileRegions(int day)
    {
        profileRegionDay = day;
        regionDraft = new HashSet<string>(form.DayRoutines[day].Groups);
        Sub(Page.Regions);
    }

    private void OpenWorkoutRegions(DateTime day, Session session)
    {
        date = day;
        profileRegionDay = null;
        regionDraft = new HashSet<string>(Training.PlanRegions(session));
        Sub(Page.Regions);
    }

    private void ToggleRegion(string group)
    {
        if (!regionDraft.Remove(group)) regionDraft.Add(group);
    }

    private async Task ConfirmRegions()
    {
        string[] selectedGroups = Catalog.Groups.Where(regionDraft.Contains).ToArray();
        if (profileRegionDay is { } day)
        {
            form.DayRoutines[day] = new DayRoutine { Manual = true, Groups = selectedGroups };
            Back();
            return;
        }
        await Change(() => training.ChoosePlanRegions(date, selectedGroups));
        if (error == "")
        {
            Back();
            notice = "Ficha gerada para " + ShortDate(date) + ".";
        }
    }

    private async Task RestoreProfilePlan(DateTime day)
    {
        if (!await JS.InvokeAsync<bool>("gymz.confirm", "Substituir as alterações desta ficha pelo plano atual do perfil?")) return;
        await Change(() => training.RestoreProfilePlan(day), "Ficha atualizada com o plano do perfil.");
        if (error == "") fieldFocus = "plan-heading";
    }

    private async Task SaveProfile()
    {
        await Change(() => {
            if (form.Days.Length == 0) throw new InvalidOperationException("Selecione pelo menos um dia de treino.");
            if (!Catalog.Goals.ContainsKey(form.Goal)) throw new InvalidOperationException("Escolha seu objetivo de treino.");
            form.Weight = Parse(weight, "peso", 20, 600) ?? 0;
            form.Height = Parse(height, "altura em centímetros", 80, 300) ?? 0;
            if (training.NeedsSetup && (form.Weight == 0 || form.Height == 0))
                throw new InvalidOperationException("Preencha peso e altura para montar seu primeiro plano.");
            form.Name = form.Name.Trim();
            if (form.Name.Any(char.IsControl)) throw new InvalidOperationException("Confira o nome informado.");
            if (form.Name == form.Name.ToLower(Training.Pt)) form.Name = Training.Pt.TextInfo.ToTitleCase(form.Name);
            training.ChangeProfile(form);
        }, "Perfil salvo.");
        if (error == "") Open(Tab.Workouts);
    }

    private void ViewDate(DateTime day)
    {
        date = day;
        Sub(Page.Workout);
    }

    private void OpenNotes(DateTime day)
    {
        date = day;
        workoutNotes = training.Get(day).Notes ?? "";
        Sub(Page.Notes);
    }

    private async Task SaveNotes()
    {
        await Change(() => training.SaveNotes(date, workoutNotes));
        if (error == "") { Back(); notice = "Observação salva."; }
    }

    private void OpenGuide(Exercise value, bool fromWorkout = false) { exercise = value; Sub(Page.Guide, fromWorkout ? CardId(value.Id) : null); }
    private void OpenHistory(Exercise value, bool fromWorkout = false) { exercise = value; Sub(Page.History, fromWorkout ? CardId(value.Id) : null); }
    private void OpenCoverage(string group) { detailGroup = group; Sub(Page.Coverage); }
    private void OpenMakeup(DateTime day) { date = day; Sub(Page.Makeup); }
    private void OpenPlan(DateTime day) { date = day; Sub(Page.Plan); }
    private void OpenCalendar(string from, DateTime basis) { calendarFrom = from; month = MonthOf(basis); Sub(Page.Calendar); }

    private void SelectMuscle(string group)
    {
        selected = group;
        search = "";
    }

    private void SelectCalendarDay(DateTime value)
    {
        week = Training.Monday(value);
        if (calendarFrom == "attendance") ViewDate(value);
        else Back();
    }

    private Task SetAttendance(DateTime day, string status) =>
        Change(() => training.Attendance(day, status), "Presença atualizada.");

    private async Task ScheduleMakeup(DateTime destination)
    {
        await Change(() => training.Compensate(date, destination));
        if (error == "") { Back(); notice = "Reposição marcada para " + ShortDate(destination) + "."; }
    }

    private async Task CancelMakeup(DateTime day)
    {
        if (!await JS.InvokeAsync<bool>("gymz.confirm", "Alterar esta reposição?")) return;
        await Change(() => training.CancelMakeup(day), "Reposição atualizada.");
    }

    private async Task AddFromGuide(Exercise target)
    {
        if (target.Level > training.At(Today).Level && !await JS.InvokeAsync<bool>("gymz.confirm",
            "Este exercício é intermediário. Confira a técnica e comece com uma carga leve. Adicionar mesmo assim?")) return;
        date = Today;
        await Edit(target, true);
    }

    private async Task RecordChange(Action action, string message = "", string clearDraft = null)
    {
        var day = date;
        await Change(() => { training.RequireRecordingDay(day); action(); }, message, clearDraft: clearDraft, draftDate: day);
    }

    private async Task ToggleDone(int index)
    {
        var id = training.Get(date).Items[index].Id;
        await RecordChange(() => {
            var entry = training.Save(date);
            var item = entry.Items[index];
            item.Done = !item.Done;
            if (item.Done && entry.Attendance == "missed") entry.Attendance = "";
        });
        if (error == "" && Training.Complete(training.Get(date)))
        {
            review = false;
            fieldFocus = "workout-finished";
        }
        if (error == "" && quickId == id) quickId = null;
    }

    private async Task Finish()
    {
        await RecordChange(() => training.Finish(date), "Treino concluído.");
        if (error == "") review = false;
    }

    private void OpenEdit(int index, bool occupied = false)
    {
        editOriginalId = index < 0 ? null : training.Get(date).Items[index].Id;
        editGroup = editGear = editQuery = "";
        swapScope = "workout";
        editMode = occupied || date != Today ? "temporary" : "learn";
        Sub(Page.Edit, editOriginalId == null ? null : CardId(editOriginalId));
    }

    private void SetSwapScope(string scope)
    {
        swapScope = scope;
        editGroup = "";
    }

    private IEnumerable<Exercise> EditChoices()
    {
        var entry = training.Get(date);
        var original = entry.Items.FirstOrDefault(item => item.Id == editOriginalId);
        if (editOriginalId != null && original == null) return Enumerable.Empty<Exercise>();
        var source = original == null ? training.WorkoutChoices(date, entry) : training.SwapChoices(date, entry, original, swapScope == "workout");
        return source.Where(x => (editGroup == "" || x.Group == editGroup) &&
            (editGear == "" || x.Gear == editGear) && Matches(x.Name, editQuery));
    }

    private async Task Edit(Exercise choice, bool library = false)
    {
        var oldId = library ? null : editOriginalId;
        await Change(() => training.EditPlanExercise(date, oldId, choice, allowAdvanced: library,
            temporary: editMode == "temporary", avoid: editMode == "avoid"), clearDraft: training.CanRecordOn(date) ? oldId : null, draftDate: date);
        if (error == "")
        {
            if (library) Open(Tab.Workouts);
            else
            {
                workoutCard = CardId(choice.Id);
                Back();
            }
            notice = "Ficha atualizada.";
        }
    }

    private async Task Remove()
    {
        var id = editOriginalId;
        if (id == null || !await JS.InvokeAsync<bool>("gymz.confirm", "Retirar este exercício desta ficha?")) return;
        var index = training.Get(date).Items.FindIndex(item => item.Id == id);
        await Change(() => training.RemovePlanExercise(date, id), clearDraft: training.CanRecordOn(date) ? id : null, draftDate: date);
        if (error == "")
        {
            var items = training.Get(date).Items;
            workoutCard = items.Count > 0 ? CardId(items[Math.Clamp(index, 0, items.Count - 1)].Id) : null;
            Back();
        }
    }

    private async Task Backup()
    {
        var current = training == null ? await JS.InvokeAsync<string>("gymz.load") : Storage.Pack(Data);
        if (current != null) await Download("gymz-backup.json", "application/json", current);
    }

    private async Task ReadBackup(InputFileChangeEventArgs args)
    {
        error = notice = "";
        try
        {
            using var stream = args.File.OpenReadStream(32 * 1024 * 1024);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            importData = Unpack(await reader.ReadToEndAsync());
        }
        catch
        {
            importData = null;
            error = "O arquivo não é um backup válido do GymZ ou ultrapassa 32 MB.";
        }
    }

    private async Task RestorePrevious()
    {
        try { importData = Unpack(await JS.InvokeAsync<string>("gymz.restorePoint")); }
        catch { error = "Não foi possível ler a cópia anterior."; }
    }

    private async Task Restore()
    {
        if (importData == null) return;
        var replacement = importData;
        await Change(() => training = new Training(replacement, Today), "Backup restaurado.", true);
        if (error == "")
        {
            importData = null;
            PrepareProfile();
        }
    }

    private Task Download(string name, string format, string value) =>
        JS.InvokeVoidAsync("gymz.download", name, format, value).AsTask();

    private Task ExportCsv()
    {
        var csv = new StringBuilder("\uFEFFData;Dia;Presença;Treino;Exercício;Grupo principal;Grupos auxiliares;Séries registradas;Repetições ou tempo;Unidade;Maior carga (kg);Detalhes por série;Resultado do exercício\r\n");
        foreach (var day in DaysOf(week))
        {
            var entry = training.Get(day);
            var fields = new[] { Training.Key(day), Training.DayNames[Training.Day(day)], Status(day, entry), entry.Title };
            var pending = training.PendingExercises(day, entry);
            var items = entry.Items.Where(m => day <= Today && (m.Done || pending.Contains(m))).ToList();
            if (items.Count == 0) csv.AppendLine(string.Join(";", fields.Select(Training.CsvCell)) + ";;;;;;;;;");
            foreach (var item in items)
            {
                var ex = Catalog.ById[item.Id];
                var extra = new[] { ex.Name, Catalog.Names[ex.Group], string.Join(", ", ex.Other.Select(g => Catalog.Names[g])),
                    item.Done ? NumberLabel(item.ActualSets) : "", item.Done ? NumberLabel(item.ActualReps) : "",
                    ex.Type == 'A' ? "min" : ex.Type == 'T' ? "s" : "repetições",
                    item.Done ? new LiftRecord { Movement = item }.Peak?.ToString("0.##", Training.Pt) ?? "" : "",
                    item.Done && item.Efforts != null ? string.Join(" / ", item.Efforts.Select(s => SetText(s, ex))) : "",
                    item.Done ? "Concluído" : "Ficou pendente" };
                csv.AppendLine(string.Join(";", fields.Concat(extra).Select(Training.CsvCell)));
            }
        }
        return Download("GymZ-" + Training.Key(week) + ".csv", "text/csv;charset=utf-8", csv.ToString());
    }

    private static bool Matches(string name, string input) =>
        Training.Pt.CompareInfo.IndexOf(name, input.Trim(), CompareOptions.IgnoreNonSpace | CompareOptions.IgnoreCase) >= 0;
    private static decimal? Parse(string text, string label, decimal min, decimal max, bool whole = false)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        if (!decimal.TryParse(text.Trim().Replace(',', '.'), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value) ||
            value < min || value > max || whole && value != decimal.Truncate(value))
            throw new InvalidOperationException("Informe " + label + " entre " + min + " e " + max + ".");
        return value;
    }
    private static DateTime MonthOf(DateTime day) => new(day.Year, day.Month, 1);
    private static IEnumerable<DateTime> DaysOf(DateTime day) => Enumerable.Range(0, 7).Select(i => day.AddDays(i));
    private static string ShortDate(DateTime day) => day.ToString("dd/MM", Training.Pt);
    private static string NumberLabel(decimal value) => value == 0 ? "" : value.ToString("0.##", Training.Pt);
    private static string NiceDate(DateTime day) => Training.DayNames[Training.Day(day)] + ", " + day.ToString("d 'de' MMMM", Training.Pt);
    private static string SpanLabel(DateTime start) => ShortDate(start) + " a " + ShortDate(start.AddDays(6));
    private static string SetText(LiftSet row, Exercise ex) =>
        string.Join(" · ", new[] { row.RepsOrSeconds.HasValue ? row.RepsOrSeconds + (ex.Type == 'T' ? " s" : " rep.") : "",
            row.Kilograms.HasValue ? row.Kilograms.Value.ToString("0.##", Training.Pt) + " kg" : "" }.Where(v => v != "").DefaultIfEmpty("Registrada"));
    private static string Pause(int seconds) => seconds < 60 ? seconds + " s" : seconds % 60 == 0 ? seconds / 60 + " min" : seconds / 60 + " min " + seconds % 60 + " s";
    private static string Coverage(int status) => status == 2 ? "Meta atingida" : status == 1 ? "Parcial" : "Sem trabalho registrado";
    private static string LoadHint(string gear) => gear switch
    {
        "dumbbell" => "Anote o peso de cada halter.",
        "barbell" => "Anote o total, incluindo barra e anilhas dos dois lados.",
        "machine" or "cable" => "Anote o valor e a unidade indicados no aparelho.",
        "band" => "Quando o elástico indica só resistência ou cor, registre as repetições.",
        _ => "Se houver carga extra, registre o peso. Para peso corporal, deixe em branco ou use 0 kg."
    };
    private string Status(DateTime day, Session entry) => !training.IsTrackedDate(day) ? "Antes do início" :
        training.Makeup(day).HasValue ? "Reposição agendada" : Training.Complete(entry) ? "Concluído" :
        Training.Present(entry) ? "Presente" : entry.Attendance == "missed" ? "Falta" :
        !entry.Scheduled && !entry.Extra ? "Descanso" : day > Today ? "Previsto" : day < Today ? "Sem registro" : "Hoje";

    public void Dispose() => listener?.Dispose();
}
