using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace GymZ
{
    internal sealed partial class GymApp
    {
        private sealed class Option
        {
            public string Key, Label;
            public override string ToString() { return Label; }
        }

        private static Option Choice(string key, string label) { return new Option { Key = key, Label = label }; }

        private static ComboBox Options(IEnumerable<Option> options, string selected)
        {
            var all = options.ToList();
            var combo = new ComboBox { ItemsSource = all };
            combo.SelectedItem = all.FirstOrDefault(o => o.Key == selected) ?? all.FirstOrDefault();
            return combo;
        }

        private static string Selected(ComboBox combo)
        {
            var item = combo.SelectedItem as Option;
            return item == null ? "" : item.Key;
        }

        private static StackPanel Field(string name, Control input)
        {
            var panel = new StackPanel();
            var label = Text(name, 13, "Muted", true);
            panel.Children.Add(label);
            AutomationProperties.SetLabeledBy(input, label);
            Add(panel, input, 5);
            return panel;
        }

        private void ShowProfile()
        {
            var profile = training.At(training.Today);
            bool firstUse = training.NeedsSetup;
            var days = new List<CheckBox>();
            var options = new WrapPanel();
            for (int i = 0; i < 7; i++)
            {
                var check = new CheckBox { Content = Training.DayShort[i], IsChecked = !firstUse && profile.Days.Contains(i) };
                AutomationProperties.SetName(check, Training.DayNames[i]);
                days.Add(check);
                AddChoice(options, Card(check, 7, "Input"));
            }
            var routine = new StackPanel();
            routine.Children.Add(Text("Dias de treino", 19, null, true));
            Add(routine, Text("Marque os dias em que você costuma treinar.", 13, "Muted"), 5);
            Add(routine, options, 11);
            var split = Options(Training.SplitModes.Select(mode => Choice(mode.Key, mode.Value)), profile.Split ?? "auto");
            Add(routine, Field("Divisão da semana", split), 12);
            var splitNote = Text("", 12, "Muted");
            Add(routine, splitNote, 7);
            var themes = Training.DayThemes.Select(theme => Choice(theme.Key, theme.Value)).ToArray();
            var dayThemes = new ComboBox[7];
            var themeFields = new FrameworkElement[7];
            for (int i = 0; i < days.Count; i++)
            {
                dayThemes[i] = Options(new[] { Choice("", "Sugestão do GymZ") }.Concat(themes),
                    profile.DayThemes == null ? "" : profile.DayThemes[i]);
                themeFields[i] = Field(Training.DayNames[i], dayThemes[i]);
                Add(routine, themeFields[i], 8);
            }
            Action updateSplit = () => {
                string mode = Selected(split);
                for (int i = 0; i < days.Count; i++)
                    themeFields[i].Visibility = mode == "groups" && days[i].IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
                splitNote.Text = mode == "groups"
                    ? "Escolha o foco de cada dia marcado. Deixe em Sugestão do GymZ para usar a divisão automática daquele dia."
                    : mode == "full" ? "Cada sessão combina movimentos de pernas e superiores, variando a ênfase ao longo da semana."
                    : "O GymZ sugere sessões por região com quatro dias ou mais, ou quando há treinos em dias seguidos. Com até três dias espaçados, combina pernas e superiores.";
            };
            split.SelectionChanged += delegate { updateSplit(); };
            foreach (var day in days)
            {
                day.Checked += delegate { updateSplit(); };
                day.Unchecked += delegate { updateSplit(); };
            }
            updateSplit();
            var goals = Catalog.Goals.Select(g => Choice(g.Key, g.Value));
            if (firstUse) goals = new[] { Choice("", "Escolha seu objetivo") }.Concat(goals);
            var goal = Options(goals, firstUse ? "" : profile.Goal);
            var focus = Options(Catalog.FocusOptions.Select(g => Choice(g.Key, g.Value)).Concat(Catalog.Groups.Select(g => Choice(g, Catalog.Names[g]))), profile.Focus);
            var focusMode = Options(Catalog.FocusModes.Select(g => Choice(g.Key, g.Value)), profile.FocusMode);
            var gear = Options(Catalog.ProfileGear.Select(g => Choice(g.Key, g.Value)), profile.Gear);
            var cardio = Options(Catalog.ProfileCardio.Select(g => Choice(g.Key, g.Value)), profile.Cardio);
            var level = Options(new[] { Choice("0", "Iniciante ou retomando"), Choice("1", "Intermediário / avançado") }, profile.Level.ToString());
            var minutes = Options(new[] { 25, 30, 45, 60, 75 }.Select(m => Choice(m.ToString(), m + " minutos")), profile.Minutes.ToString());
            var name = new TextBox { Text = profile.Name, MaxLength = 48 };
            var weight = new TextBox { Text = OptionalNumber(profile.Weight) };
            var height = new TextBox { Text = OptionalNumber(profile.Height) };
            var plan = new StackPanel();
            plan.Children.Add(Text("Personalização do plano", 19, null, true));
            Add(plan, Columns(Field("Objetivo", goal), Field("Região prioritária", focus)), 12);
            Add(plan, Columns(Field("Equipamento de musculação", gear), Field("Equipamento de cardio", cardio)), 12);
            Add(plan, Columns(Field("Intensidade da prioridade", focusMode), Field("Experiência", level)), 12);
            Add(plan, Field("Tempo por treino", minutes), 12);
            var explanation = Text("", 12, "Muted");
            Action updateExplanation = () => {
                focusMode.IsEnabled = Selected(focus) != "";
                var choice = new Profile { Goal = Selected(goal), Cardio = Selected(cardio) };
                if (Training.WantsAutoCardio(choice))
                    explanation.Text = choice.Goal == "endurance" ? "O cardio vem primeiro quando a prioridade é melhorar o fôlego." :
                        "O cardio moderado entra depois da musculação nas sessões selecionadas pelo plano.";
                else explanation.Text = "O plano vai priorizar a musculação. Você pode acrescentar cardio manualmente no treino do dia.";
            };
            focus.SelectionChanged += delegate { updateExplanation(); };
            goal.SelectionChanged += delegate { updateExplanation(); };
            cardio.SelectionChanged += delegate { updateExplanation(); };
            updateExplanation();
            Add(plan, explanation, 9);

            var body = new StackPanel();
            body.Children.Add(Text("Suas informações", 19, null, true));
            Add(body, Field("Como você quer ser chamado? (opcional)", name), 11);
            Add(body, Columns(Field("Peso atual (kg)", weight), Field("Altura (cm)", height)), 11);
            Add(body, Text("As medidas ficam no perfil para acompanhamento. Ajuste as cargas pela sua execução e pelo histórico de cada exercício.", 12, "Muted"), 8);
            Add(content, Card(body));
            Add(content, Card(routine), 13);
            Add(content, Card(plan), 13);

            var save = Button(firstUse ? "Montar meu plano" : "Salvar perfil e atualizar plano", () => {
                var checkedDays = Enumerable.Range(0, 7).Where(i => days[i].IsChecked == true).ToArray();
                if (checkedDays.Length == 0) throw new InvalidOperationException("Selecione pelo menos um dia de treino.");
                if (Selected(goal) == "") throw new InvalidOperationException("Escolha seu objetivo de treino.");
                decimal weightValue = Number(weight, "o peso", 20, 600);
                decimal heightValue = Number(height, "a altura em cm", 80, 300);
                if (firstUse && (weightValue == 0 || heightValue == 0)) throw new InvalidOperationException("Preencha peso e altura para montar seu primeiro plano.");
                string displayName = name.Text.Trim();
                if (displayName == displayName.ToLower(Training.Pt)) displayName = Training.Pt.TextInfo.ToTitleCase(displayName);
                var updated = new Profile {
                    Days = checkedDays, Goal = Selected(goal), Focus = Selected(focus), Gear = Selected(gear), Level = int.Parse(Selected(level)),
                    Minutes = int.Parse(Selected(minutes)), Weight = weightValue, Height = heightValue,
                    FocusMode = Selected(focusMode), Name = displayName, Cardio = Selected(cardio), Split = Selected(split),
                    DayThemes = dayThemes.Select(Selected).ToArray()
                };
                Commit(() => training.ChangeProfile(updated), "Perfil salvo. O plano usa suas preferências a partir de hoje.", false);
                Go(Section.Workouts);
            }, "Primary");
            Add(content, save, 13);
            if (Data.Avoided.Count > 0)
            {
                var avoided = new StackPanel();
                avoided.Children.Add(Text("Exercícios evitados nos planos", 19, null, true));
                foreach (string id in Data.Avoided)
                {
                    var excluded = Catalog.ById[id];
                    var allow = Button("Permitir novamente", () => Commit(() => Data.Avoided.Remove(excluded.Id), excluded.Name + " pode voltar aos próximos planos."));
                    Add(avoided, Columns(Text(excluded.Name, 13), Right(allow), 1.25, 1, 8), 8);
                }
                Add(content, Card(avoided), 12);
            }
            var reduced = training.ReducedSuggestions().ToList();
            if (reduced.Count > 0)
            {
                var preference = new StackPanel();
                preference.Children.Add(Text("Exercícios sugeridos com menos frequência", 19, null, true));
                foreach (string id in reduced)
                {
                    var exercise = Catalog.ById[id];
                    var reset = Button("Restaurar preferência", () => Commit(() => Data.Swaps.RemoveAll(s => s.From == exercise.Id), "Preferência atualizada."));
                    Add(preference, Columns(Text(exercise.Name, 13), Right(reset), 1.1, 1, 7), 7);
                }
                Add(content, Card(preference), 12);
            }
            Add(content, Text("Plano geral para adultos. Se sentir dor aguda ou tontura, pare o exercício. Procure orientação profissional para adaptar o treino em caso de lesão ou condição de saúde.", 12, "Muted"), 11);
        }

        private static string OptionalNumber(decimal value) { return value == 0 ? "" : value.ToString("0.##", Training.Pt); }

        private static decimal Number(TextBox box, string label, decimal min, decimal max, bool whole = false)
        {
            var text = box.Text.Trim();
            if (text == "") return 0;
            decimal value;
            if (!decimal.TryParse(text.Replace('.', ','), NumberStyles.AllowDecimalPoint, Training.Pt, out value) || value < min || value > max ||
                (whole && decimal.Truncate(value) != value))
                throw new InvalidOperationException("Informe " + label + " entre " + min + " e " + max + (whole ? ", em número inteiro." : "."));
            return value;
        }

        private void ShowSettings()
        {
            var look = new StackPanel();
            look.Children.Add(Text("Aparência e acessibilidade", 19, null, true));
            Add(look, Text("Tema", 13, "Muted", true), 11);
            var themes = new WrapPanel();
            AddChoice(themes, Button("Escuro", () => SetTheme("dark"), Data.Preferences.Theme == "dark" ? "Selected" : null));
            AddChoice(themes, Button("Claro", () => SetTheme("light"), Data.Preferences.Theme == "light" ? "Selected" : null));
            Add(look, themes, 6);
            Add(look, Text("Cor de destaque", 13, "Muted", true), 7);
            var accents = new WrapPanel();
            for (int i = 0; i < Storage.Accents.Length; i++)
            {
                string accent = Storage.Accents[i];
                var button = Button(AccentNames[i], () => ChangeAppearance(p => p.Accent = accent), Data.Preferences.Accent == accent ? "Selected" : null);
                var row = Row();
                row.Children.Add(new System.Windows.Shapes.Ellipse { Width = 11, Height = 11, Fill = ColorBrush(AccentColors[i][3]), Margin = new Thickness(0, 0, 7, 0) });
                row.Children.Add(Text(AccentNames[i], 13, null, true));
                button.Content = row;
                AddChoice(accents, button);
            }
            Add(look, accents, 6);
            Add(look, Text("Tamanho do texto e controles", 13, "Muted", true), 7);
            var scales = new WrapPanel();
            foreach (int size in Storage.Scales)
            {
                int scale = size;
                AddChoice(scales, Button(scale + "%", () => ChangeAppearance(p => p.Scale = scale), Data.Preferences.Scale == scale ? "Selected" : null));
            }
            Add(look, scales, 6);
            var contrast = new CheckBox { Content = "Reforçar contraste", IsChecked = Data.Preferences.Contrast };
            contrast.Click += delegate { Guard(() => ChangeAppearance(p => p.Contrast = contrast.IsChecked == true)); };
            Add(look, contrast, 6);
            Add(look, Text("Alt+← volta à tela anterior, F1 abre o Início e Ctrl+Home volta a hoje. Use Tab para navegar e Enter ou Espaço para acionar controles.", 12, "Muted"), 9);
            content.Children.Add(Card(look));

            var backups = new StackPanel();
            backups.Children.Add(Text("Backup e restauração", 19, null, true));
            Add(backups, Text("O backup cria uma pasta com o executável, o perfil, as preferências e o histórico. Para usar a cópia em outro computador, abra o GymZ e restaure o arquivo da pasta.", 13, "Muted"), 6);
            var actions = new WrapPanel();
            AddChoice(actions, Button("Salvar backup", SaveBackup, "Primary"));
            AddChoice(actions, Button("Restaurar backup", RestoreBackup));
            AddChoice(actions, Button("Abrir pasta de dados", () => Process.Start(new ProcessStartInfo(storage.Folder) { UseShellExecute = true })));
            Add(backups, actions, 12);
            Add(content, Card(backups), 12);

            var about = new StackPanel();
            about.Children.Add(Text("Sobre o GymZ", 19, null, true));
            Add(about, Text("Versão " + Version + " · Grátis, offline e sem cadastro.", 13, "Muted"), 6);
            var links = new WrapPanel();
            AddChoice(links, Button("Treino de força · ACSM", () => OpenLink(Catalog.Acsm)));
            AddChoice(links, Button("Biblioteca de exercícios · ACE", () => OpenLink(Catalog.Ace)));
            AddChoice(links, Button("Atividade física · CDC", () => OpenLink(Catalog.Cdc)));
            AddChoice(links, Button("Força e composição corporal ↗", () => OpenLink(Catalog.RecompReview)));
            AddChoice(links, Button("Créditos das ilustrações", ExerciseCredits));
            Add(about, links, 11);
            Add(content, Card(about), 12);
        }

        private void SetTheme(string theme) { ChangeAppearance(p => p.Theme = theme); }

        private void ChangeAppearance(Action<Preferences> update)
        {
            Commit(() => update(Data.Preferences), "Aparência atualizada.", false);
            SetPalette();
            ApplyScale();
            Render(true);
        }

        private void SaveBackup()
        {
            using (var picker = new Forms.FolderBrowserDialog())
            {
                picker.Description = "Escolha onde criar a pasta com o backup do GymZ.";
                if (picker.ShowDialog() != Forms.DialogResult.OK) return;
                string folder = storage.Backup(picker.SelectedPath, Data, Assembly.GetExecutingAssembly().Location);
                Notify("Backup salvo em " + folder);
                MessageBox.Show(window, "Guarde a pasta completa para restaurar o GymZ em outro computador.\n\n" + folder, "Backup salvo", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void RestoreBackup()
        {
            string adjacent = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Storage.BackupName);
            var picker = new OpenFileDialog { Title = "Selecione o arquivo do backup", Filter = "Backup do GymZ (*.json)|*.json", CheckFileExists = true };
            if (File.Exists(adjacent)) { picker.InitialDirectory = Path.GetDirectoryName(adjacent); picker.FileName = Storage.BackupName; }
            if (picker.ShowDialog(window) != true) return;
            var replacement = Storage.Read(picker.FileName);
            int records = replacement.Sessions.Count;
            string period = "";
            if (records > 0)
            {
                var dates = replacement.Sessions.Keys.Select(Training.Date).ToList();
                period = " De " + Range(dates.Min()) + " a " + Range(dates.Max()) + ".";
            }
            var confirmation = MessageBox.Show(window, "O backup contém " + records + " dia(s) com dados salvos." + period +
                "\n\nRestaurar esse backup substitui os dados atuais. Uma cópia dos dados anteriores será salva na pasta Backups.",
                "Restaurar backup do GymZ", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
            if (confirmation != MessageBoxResult.Yes) return;
            var restored = new Training(replacement, training.Today);
            storage.Restore(Data, replacement);
            training = restored;
            openGuide = null;
            SetPalette();
            ApplyScale();
            Render(true);
            Notify("Backup restaurado. A cópia anterior está na pasta Backups.");
        }

        private static void OpenLink(string address) { Process.Start(new ProcessStartInfo(address) { UseShellExecute = true }); }

        private Window Dialog(string title, out StackPanel panel, double width = 570)
        {
            panel = new StackPanel { Margin = new Thickness(19) };
            var dialog = new Window {
                Owner = window, Icon = window.Icon, Title = title, Width = Math.Min(width, SystemParameters.WorkArea.Width - 32),
                SizeToContent = SizeToContent.Height, MaxHeight = SystemParameters.WorkArea.Height - 32,
                ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false, WindowStartupLocation = WindowStartupLocation.CenterOwner,
                FontFamily = window.FontFamily, FontSize = window.FontSize
            };
            dialog.SetResourceReference(Window.BackgroundProperty, "Canvas");
            dialog.SetResourceReference(Window.ForegroundProperty, "Ink");
            double scale = Data.Preferences.Scale / 100.0;
            panel.LayoutTransform = new ScaleTransform(scale, scale);
            var viewer = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
            viewer.SetResourceReference(Control.BackgroundProperty, "Canvas");
            dialog.Content = viewer;
            dialog.PreviewKeyDown += delegate(object sender, KeyEventArgs e) {
                if (e.Key == Key.Escape || (e.SystemKey == Key.Left && Keyboard.Modifiers == ModifierKeys.Alt)) { e.Handled = true; dialog.Close(); }
            };
            return dialog;
        }

        private void EditExerciseList(DateTime date, int index)
        {
            training.RequireRecordingDay(date);
            var session = training.Get(date);
            if (session.Finalized) throw new InvalidOperationException("Reabra o treino antes de alterar a lista de exercícios.");
            var profile = training.At(date);
            var original = index < 0 ? null : session.Items[index];
            var exercise = original == null ? null : Catalog.ById[original.Id];
            string title = original == null ? "Adicionar exercício" : "Trocar exercício";
            StackPanel panel;
            var dialog = Dialog(title, out panel);
            panel.Children.Add(Text(title, 23, null, true));
            Add(panel, Text(original == null ? "Escolha uma região para adicionar ao treino deste dia." : exercise.Name + " · " + Catalog.Names[exercise.Group], 13, "Muted"), 6);
            var groups = new[] { Choice("", "Todas as regiões") }.Concat(Catalog.Names.Select(g => Choice(g.Key, g.Value))).ToList();
            if (original != null) groups.Insert(0, Choice("same", "Equivalentes e mesmo grupo"));
            var selector = Options(groups, exercise == null ? "" : "same");
            var gear = Options(new[] { Choice("", "Todo equipamento") }.Concat(Catalog.GearNames.Select(g => Choice(g.Key, g.Value))), "");
            var field = new TextBox();
            var list = new ComboBox();
            var suggestion = Text("", 13, "Muted");
            Add(panel, Columns(Field("Região", selector), Field("Buscar pelo nome", field)), 12);
            Add(panel, Field("Equipamento alternativo", gear), 10);
            Add(panel, Field("Exercício", list), 12);
            Add(panel, suggestion, 8);
            var avoid = new CheckBox { Content = "Evitar este exercício nos próximos planos" };
            var temporary = new CheckBox { Content = "Trocar só neste treino" };
            if (original != null)
            {
                Add(panel, Text("O GymZ aprende com suas escolhas. Para uma máquina ocupada, use a troca só neste treino.", 12, "Muted"), 10);
                Add(panel, temporary, 4);
                Add(panel, avoid, 4);
                temporary.Checked += delegate { avoid.IsChecked = false; };
                avoid.Checked += delegate { temporary.IsChecked = false; };
            }

            Action<Exercise> apply = chosen => {
                Commit(() => {
                    var entry = training.Save(date);
                    var newItem = training.PrescribeForSession(date, entry, chosen, original != null && original.Priority && chosen.Group == exercise.Group);
                    if (index < 0) entry.Items.Add(newItem);
                    else entry.Items[index] = newItem;
                    Training.OrderCardio(entry);
                    if (original != null) training.LearnSwap(exercise.Id, chosen.Id, temporary.IsChecked == true);
                    if (original != null && avoid.IsChecked == true) training.Avoid(exercise.Id);
                }, index < 0 ? "Exercício adicionado." : "Exercício substituído.");
                dialog.Close();
            };

            var actions = new WrapPanel();
            var save = Button(index < 0 ? "Adicionar exercício" : "Confirmar troca", () => {
                if (Selected(list) != "") apply(Catalog.ById[Selected(list)]);
            }, "Primary");
            var auto = Button("Trocar automaticamente", () => {
                if (Selected(list) != "") apply(Catalog.ById[((Option)list.Items[0]).Key]);
            }, "Selected");
            AddChoice(actions, save);
            if (index >= 0) AddChoice(actions, auto);
            AddChoice(actions, Button("Voltar", dialog.Close));
            Add(panel, actions, 12);
            Action filter = () => {
                string group = Selected(selector);
                string equipment = Selected(gear);
                string query = Normalize(field.Text.Trim());
                var results = (group == "same" ? training.Alternatives(date, session, original) : Catalog.All.AsEnumerable())
                    .Where(e => training.Eligible(e, profile) && training.FitsSession(date, session, e) && !session.Items.Any(m => m.Id == e.Id) &&
                        (group == "" || group == "same" || e.Group == group) && (equipment == "" || e.Gear == equipment) &&
                        (query == "" || Normalize(e.Name).Contains(query))).ToList();
                list.ItemsSource = results.Select(e => Choice(e.Id, e.Name)).ToList();
                list.SelectedIndex = results.Count > 0 ? 0 : -1;
                save.IsEnabled = auto.IsEnabled = results.Count > 0;
            };
            list.SelectionChanged += delegate {
                string id = Selected(list);
                if (id == "") { suggestion.Text = "Nenhum exercício disponível para esses filtros."; return; }
                var chosen = Catalog.ById[id];
                var match = exercise == null ? "" : Training.Equivalent(exercise, chosen) ? "Mesma função  ·  " : exercise.Group == chosen.Group ?
                    "Mesmo grupo, movimento diferente  ·  " : "Outra região  ·  ";
                suggestion.Text = match + Catalog.Names[chosen.Group] + "  ·  " + Catalog.GearNames[chosen.Gear];
            };
            selector.SelectionChanged += delegate { filter(); };
            gear.SelectionChanged += delegate { filter(); };
            field.TextChanged += delegate { filter(); };
            filter();
            dialog.ShowDialog();
        }

        private void ScheduleExerciseDialog(Exercise exercise)
        {
            DateTime date = training.Today;
            var session = training.Get(date);
            if (!session.Scheduled && !session.Extra)
                throw new InvalidOperationException("Hoje é descanso. Abra Treinos e escolha Montar treino extra para editar uma sessão complementar.");
            if (Training.Complete(session)) throw new InvalidOperationException("O treino de hoje está concluído.");
            if (training.Makeup(date).HasValue)
                throw new InvalidOperationException("O treino de hoje foi transferido para uma reposição.");
            if (session.Items.Any(m => m.Id == exercise.Id)) throw new InvalidOperationException("Este exercício já está na lista de hoje.");
            if (!Catalog.Available(exercise, training.At(date)) || Data.Avoided.Contains(exercise.Id) || !training.FitsSession(date, session, exercise))
                throw new InvalidOperationException("Este exercício não combina com o equipamento, as preferências ou a recuperação prevista para hoje.");
            StackPanel panel;
            var dialog = Dialog("Adicionar ao treino de hoje", out panel);
            panel.Children.Add(Text(exercise.Name, 22, null, true));
            Add(panel, Text(Range(date) + " · " + session.Title, 13, "Muted"), 8);
            if (exercise.Level > training.At(date).Level)
                Add(panel, Text("Exercício intermediário. Confira a técnica e comece com uma carga leve.", 13, "Warn"), 7);
            var save = Button("Adicionar", () => {
                if (date != training.Today) throw new InvalidOperationException("O dia mudou. Abra o treino de hoje para adicionar o exercício.");
                Commit(() => {
                    var entry = training.Save(date);
                    entry.Items.Add(training.PrescribeForSession(date, entry, exercise, false));
                    Training.OrderCardio(entry);
                }, "Exercício adicionado.", false);
                dialog.Close();
                Go(Section.Workouts);
            }, "Primary");
            var actions = new WrapPanel();
            AddChoice(actions, save);
            AddChoice(actions, Button("Voltar", dialog.Close));
            Add(panel, actions, 13);
            dialog.ShowDialog();
        }

        private void MakeupDialog(DateTime date)
        {
            StackPanel panel;
            var dialog = Dialog("Planejar reposição", out panel);
            panel.Children.Add(Text("Repor treino de " + Range(date), 20, null, true));
            Add(panel, Text("Os primeiros dias da lista dão preferência a mais espaço entre os treinos. A frequência será atualizada quando você registrar a reposição.", 13, "Muted"), 6);
            var options = training.MakeupOptions(date).ToList();
            if (options.Count == 0) return;
            var choices = Options(options.Select(d => Choice(Training.Key(d), Training.DayNames[Training.Day(d)] + " · " + Range(d))), Training.Key(options[0]));
            Add(panel, Field("Dia livre para repor", choices), 12);
            var save = Button("Agendar reposição", () => {
                var destination = Training.Date(Selected(choices));
                Commit(() => training.Compensate(date, destination), "Reposição agendada para " + Range(destination) + ".");
                dialog.Close();
            }, "Primary");
            var actions = new WrapPanel();
            AddChoice(actions, save);
            AddChoice(actions, Button("Voltar", dialog.Close));
            Add(panel, actions, 12);
            dialog.ShowDialog();
        }

        private void UndoMakeup(DateTime date)
        {
            var session = training.Get(date);
            string question = Training.Present(session) ? "Desvincular a reposição do dia original e manter este treino como extra?" :
                "Cancelar a reposição agendada? A falta permanece registrada no dia original.";
            if (MessageBox.Show(window, question, "Reposição", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes) return;
            Commit(() => training.CancelMakeup(date), "Reposição atualizada.");
        }

        private void ExportReport()
        {
            var save = new SaveFileDialog { Title = "Exportar relatório da semana", Filter = "CSV (*.csv)|*.csv", FileName = "GymZ-" + Training.Key(week) + ".csv" };
            if (save.ShowDialog(window) != true) return;
            using (var csv = new StreamWriter(save.FileName, false, new UTF8Encoding(true)))
            {
                csv.WriteLine("Data;Dia;Presença;Treino;Exercício;Grupo principal;Grupos auxiliares;Séries registradas;Repetições ou tempo;Unidade;Maior carga (kg);Detalhes por série;Resultado do exercício");
                for (int i = 0; i < 7; i++)
                {
                    var date = week.AddDays(i);
                    var session = training.Get(date);
                    string[] day = { Range(date), Training.DayNames[i], StatusFor(date, session).Label, session.Title };
                    var completed = session.Items.Where(m => m.Done && date <= training.Today).ToList();
                    var pending = training.PendingExercises(date, session);
                    if (completed.Count == 0 && pending.Count == 0) csv.WriteLine(string.Join(";", day.Select(CsvCell)) + ";;;;;;;;;");
                    foreach (var item in completed)
                    {
                        var ex = Catalog.ById[item.Id];
                        var peak = new LiftRecord { Movement = item }.Peak;
                        string[] fields = { ex.Name, Catalog.Names[ex.Group], string.Join(", ", ex.Other.Select(g => Catalog.Names[g])),
                            OptionalNumber(item.ActualSets), OptionalNumber(item.ActualReps), ex.Type == 'A' ? "min" : ex.Type == 'T' ? "s" : "repetições",
                            peak.HasValue ? peak.Value.ToString("0.##", Training.Pt) : "",
                            item.Efforts == null ? "" : string.Join(" / ", item.Efforts.Select((set, index) => "Série " + (index + 1) + " · " + SetLabel(set, ex))), "Concluído" };
                        csv.WriteLine(string.Join(";", day.Concat(fields).Select(CsvCell)));
                    }
                    foreach (var item in pending)
                    {
                        var ex = Catalog.ById[item.Id];
                        string[] fields = { ex.Name, Catalog.Names[ex.Group], string.Join(", ", ex.Other.Select(g => Catalog.Names[g])),
                            "", "", "", "", "", "Ficou pendente" };
                        csv.WriteLine(string.Join(";", day.Concat(fields).Select(CsvCell)));
                    }
                }
            }
            Notify("Relatório exportado para " + save.FileName);
        }

        private static string CsvCell(string value)
        {
            if (value.Length > 0 && "=+-@\t\r".Contains(value[0])) value = "'" + value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
    }
}
