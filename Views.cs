using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;

namespace GymZ
{
    internal sealed partial class GymApp
    {
        private Viewbox heroArt;
        private Grid heroLayout;
        private DateTime? reviewingCompletedDay;

        private void ShowOverview()
        {
            WeekControls();
            var report = training.Report(week);
            var start = week > training.Today ? week : training.Today;
            var next = NextWorkout(start);
            var featured = training.Get(next);
            var actionColor = ((SolidColorBrush)Application.Current.Resources["Action"]).Color;
            var heroDark = Mix(ColorBrush("#111B31").Color, actionColor, .66);
            var heroBright = Mix(ColorBrush("#14283F").Color, actionColor, .92);
            var softWhite = Mix(Colors.White, actionColor, .21);
            var copy = new StackPanel();
            string name = training.At(training.Today).Name;
            var white = Text(name != "" ? "Treino de " + name : next == training.Today ? "TREINO DE HOJE" : "PRÓXIMO TREINO", 12, null, true);
            white.Foreground = new SolidColorBrush(softWhite);
            copy.Children.Add(white);
            white = Text(featured.Title, 33, null, true);
            white.Foreground = Brushes.White;
            Add(copy, white, 9);
            string summary = Training.DayNames[Training.Day(next)] + ", " + next.ToString("dd/MM") + "  ·  " + featured.Items.Count + " exercícios" +
                (featured.MakeupFor != "" ? "  ·  reposição" : "");
            white = Text(summary, 14);
            white.Foreground = new SolidColorBrush(softWhite);
            Add(copy, white, 4);
            var heroActions = new WrapPanel();
            var open = Button(next > training.Today ? "Ver treino" : "Abrir treino", () => SelectDay(next, Section.Workouts));
            open.Background = Brushes.White;
            open.BorderBrush = Brushes.White;
            open.Foreground = new SolidColorBrush(heroDark);
            AddChoice(heroActions, open);
            if (featured.Items.Count > 0)
            {
                var why = Button("Como o treino foi montado", () => PlanDetails(next, featured));
                why.Background = new SolidColorBrush(heroDark);
                why.BorderBrush = new SolidColorBrush(Mix(actionColor, Colors.White, .4));
                why.Foreground = Brushes.White;
                AddChoice(heroActions, why);
            }
            if (!Data.Configured)
            {
                var setup = Button("Personalizar plano", () => Go(Section.Profile));
                setup.Background = new SolidColorBrush(heroDark);
                setup.BorderBrush = new SolidColorBrush(Mix(actionColor, Colors.White, .4));
                setup.Foreground = Brushes.White;
                AddChoice(heroActions, setup);
            }
            Add(copy, heroActions, 17);
            heroArt = HeroArt(actionColor);
            heroLayout = Columns(copy, heroArt, 2.2, 1, 14);
            var banner = Card(heroLayout, 22);
            banner.Background = new LinearGradientBrush(heroDark, heroBright, 12);
            banner.BorderBrush = new SolidColorBrush(Mix(actionColor, Colors.White, .25));
            Add(content, banner);
            AdjustHero();

            var stats = new UniformGrid { Columns = 3 };
            AddMetric(stats, "Treinos realizados", report.Attended.ToString(), report.Scheduled + " previstos na semana", "Good");
            AddMetric(stats, "Frequência", report.Rate.HasValue ? report.Rate + "%" : "—", report.Compensated + " de " + report.Due + " previstos até agora", "Accent");
            int groups = report.Direct.Keys.Count(g => g != "cardio");
            AddMetric(stats, "Exercícios feitos", report.Done.ToString(), groups + (groups == 1 ? " grupo com foco direto" : " grupos com foco direto"), "Accent");
            Add(content, stats, 15);

            var schedule = new StackPanel();
            schedule.Children.Add(Text("Sua semana", 19, null, true));
            Add(schedule, DayStrip(Section.Workouts), 12);
            Add(schedule, Text("Selecione um dia para consultar o treino. O registro fica disponível na data do treino.", 12, "Muted"), 10);
            Add(content, Card(schedule), 1);

            var trend = new StackPanel();
            trend.Children.Add(Text("Frequência nas últimas 4 semanas", 19, null, true));
            for (int i = 3; i >= 0; i--)
            {
                var startOfWeek = week.AddDays(-7 * i);
                var values = training.Report(startOfWeek);
                string label = startOfWeek.ToString("dd/MM") + " – " + startOfWeek.AddDays(6).ToString("dd/MM");
                var row = new StackPanel();
                var legends = Columns(Text(label, 12, "Muted"), Right(Text(values.Rate.HasValue ? values.Rate + "% · " + values.Attended + " treino(s)" : "Sem previsão vencida", 12, "Muted")), 1, 1, 4);
                row.Children.Add(legends);
                Add(row, Bar(values.Rate ?? 0, 100), 5);
                Add(trend, row, 11);
            }
            var links = new StackPanel();
            links.Children.Add(Text("Cobertura muscular", 19, null, true));
            Add(links, Text(report.Done == 0 ? "Os músculos aparecem aqui conforme você marca os exercícios como feitos."
                : string.Join("  ·  ", Catalog.Groups.Where(g => report.Direct.ContainsKey(g)).Select(g => Catalog.Names[g]).DefaultIfEmpty("Apenas cardio nesta semana")), 13, "Muted"), 10);
            var linkRow = new WrapPanel();
            AddChoice(linkRow, Button("Ver relatório", () => Go(Section.Reports), "Selected"));
            AddChoice(linkRow, Button("Registrar presença", () => Go(Section.Attendance)));
            Add(links, linkRow, 13);
            Add(content, Columns(Card(trend), Top(Card(links))), 13);
        }

        private DateTime NextWorkout(DateTime start)
        {
            for (int offset = 0; offset < 15; offset++)
            {
                var date = start.AddDays(offset);
                var session = training.Get(date);
                if ((session.Scheduled || session.Extra) && !PresentOrSkipped(date, session)) return date;
            }
            return start;
        }

        private bool PresentOrSkipped(DateTime day, Session session)
        {
            return training.Makeup(day).HasValue || session.Attendance == "missed" ||
                Training.Complete(session);
        }

        private void AdjustHero()
        {
            if (heroArt == null || heroLayout == null) return;
            bool show = window.ActualWidth <= 0 || window.ActualWidth / (Data.Preferences.Scale / 100.0) >= 900;
            heroArt.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            heroLayout.ColumnDefinitions[1].Width = new GridLength(show ? 14 : 0);
            heroLayout.ColumnDefinitions[2].Width = show ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        }

        private static Color Mix(Color from, Color to, double weight)
        {
            return Color.FromRgb((byte)(from.R + (to.R - from.R) * weight), (byte)(from.G + (to.G - from.G) * weight), (byte)(from.B + (to.B - from.B) * weight));
        }

        private static Viewbox HeroArt(Color color)
        {
            var canvas = new Canvas { Width = 300, Height = 158, IsHitTestVisible = false };
            for (int i = 0; i < 3; i++)
            {
                double size = 100 + 30 * i;
                var halo = new Ellipse { Width = size, Height = size, StrokeThickness = 1, Stroke = new SolidColorBrush(Mix(color, Colors.White, .4)), Opacity = .57 - i * .12 };
                Canvas.SetLeft(halo, 155 - size / 2);
                Canvas.SetTop(halo, 79 - size / 2);
                canvas.Children.Add(halo);
            }
            var weight = new Canvas { Width = 248, Height = 116, RenderTransformOrigin = new Point(.5, .5), RenderTransform = new RotateTransform(-24) };
            Canvas.SetLeft(weight, 31);
            Canvas.SetTop(weight, 20);
            DrawPlate(weight, 20, 51, 208, 14, color, .63);
            DrawPlate(weight, 35, 29, 28, 58, color, .24);
            DrawPlate(weight, 59, 15, 33, 86, color, .37);
            DrawPlate(weight, 156, 15, 33, 86, color, .37);
            DrawPlate(weight, 185, 29, 28, 58, color, .24);
            canvas.Children.Add(weight);
            return new Viewbox { Child = canvas, MaxHeight = 160, VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false };
        }

        private static void DrawPlate(Canvas canvas, double x, double y, double width, double height, Color color, double light)
        {
            var shape = new Rectangle { Width = width, Height = height, RadiusX = 7, RadiusY = 7, StrokeThickness = 1.5,
                Fill = new SolidColorBrush(Mix(color, Colors.White, light)), Stroke = new SolidColorBrush(Mix(color, Colors.White, Math.Min(.88, light + .30))) };
            Canvas.SetLeft(shape, x);
            Canvas.SetTop(shape, y);
            canvas.Children.Add(shape);
        }

        private static void AddMetric(Panel panel, string title, string value, string sub, string brush)
        {
            var stack = new StackPanel();
            stack.Children.Add(Text(title, 13, "Muted", true));
            Add(stack, Text(value, 29, brush, true), 5);
            Add(stack, Text(sub, 12, "Muted"), 4);
            var block = Card(stack, 14);
            block.Margin = new Thickness(panel.Children.Count == 0 ? 0 : 7, 0, 0, 12);
            panel.Children.Add(block);
        }

        private UniformGrid DayStrip(Section target)
        {
            var strip = new UniformGrid { Columns = 7 };
            for (int i = 0; i < 7; i++)
            {
                var day = week.AddDays(i);
                var status = StatusFor(day, training.Get(day));
                var button = Button(Training.DayShort[i], () => SelectDay(day, target), day == selectedDate && section != Section.Overview ? "Selected" : null);
                button.Padding = new Thickness(2, 7, 2, 7);
                button.Margin = new Thickness(i > 0 ? 4 : 0, 0, 0, 0);
                var cell = new StackPanel();
                cell.Children.Add(Center(Text(Training.DayShort[i], 11, "Muted", true)));
                Add(cell, Center(Text(day.Day.ToString(), 17, null, true)), 1);
                Add(cell, Center(Text(status.Label, 10, status.Brush)), 3);
                button.Content = cell;
                button.ToolTip = Training.DayNames[i] + " · " + Range(day) + " · " + status.Label;
                AutomationProperties.SetName(button, button.ToolTip.ToString());
                strip.Children.Add(button);
            }
            return strip;
        }

        private static TextBlock Center(TextBlock text) { text.TextAlignment = TextAlignment.Center; return text; }
        private static T Right<T>(T element) where T : FrameworkElement { element.HorizontalAlignment = HorizontalAlignment.Right; return element; }
        private static T Top<T>(T element) where T : FrameworkElement { element.VerticalAlignment = VerticalAlignment.Top; return element; }

        private static void AddChoice(Panel panel, FrameworkElement element)
        {
            element.Margin = new Thickness(0, 0, 7, 6);
            panel.Children.Add(element);
        }

        private sealed class DayStatus
        {
            public readonly string Label, Brush;
            public DayStatus(string label, string brush) { Label = label; Brush = brush; }
        }

        private DayStatus StatusFor(DateTime day, Session session)
        {
            if (!training.IsTrackedDate(day)) return new DayStatus("Antes do início", "Muted");
            if (day > training.Today)
                return session.Scheduled || session.Extra || training.Makeup(day).HasValue ? new DayStatus("Previsto", "Accent") : new DayStatus("Descanso", "Muted");
            if (Training.Present(session)) return new DayStatus("Treinado", "Good");
            var makeup = training.Makeup(day);
            if (makeup.HasValue) return Training.Present(training.Get(makeup.Value)) && makeup.Value <= training.Today
                ? new DayStatus("Reposto", "Good") : new DayStatus("A repor", "Warn");
            if (session.Attendance == "missed") return new DayStatus("Falta", "Bad");
            if (session.Scheduled || session.Extra)
                return day < training.Today ? new DayStatus("Sem registro", "Warn") : new DayStatus(session.Extra ? "Extra" : "Previsto", "Accent");
            return new DayStatus("Descanso", "Muted");
        }

        private void ShowAttendance()
        {
            WeekControls();
            var report = training.Report(week);
            var stats = new UniformGrid { Columns = 3 };
            AddMetric(stats, "Presenças", report.Attended.ToString(), "dias com treino registrado", "Good");
            AddMetric(stats, "Faltas", report.Missed.ToString(), report.Unmarked + " previstos sem registro", "Bad");
            AddMetric(stats, "Frequência", report.Rate.HasValue ? report.Rate + "%" : "—", report.Compensated + " de " + report.Due + " compromissos cumpridos", "Accent");
            content.Children.Add(stats);
            var calendar = new StackPanel();
            calendar.Children.Add(Text("Calendário semanal", 19, null, true));
            Add(calendar, DayStrip(Section.Attendance), 12);
            var profile = training.At(training.Today);
            var schedule = new WrapPanel();
            var dayText = Text("Dias fixos: " + string.Join(", ", profile.Days.Select(d => Training.DayShort[d])), 13, "Muted");
            AddChoice(schedule, dayText);
            AddChoice(schedule, Button("Alterar dias de treino", () => Go(Section.Profile)));
            Add(calendar, schedule, 12);
            content.Children.Add(Card(calendar));

            var entry = training.Get(selectedDate);
            var status = StatusFor(selectedDate, entry);
            var date = selectedDate;
            var detail = new StackPanel();
            detail.Children.Add(Text(Training.DayNames[Training.Day(date)] + " · " + Range(date), 20, null, true));
            Add(detail, Text(entry.Title + "  ·  " + status.Label, 14, status.Brush, true), 5);
            var actions = new WrapPanel();
            if (date == training.Today && training.IsTrackedDate(date))
            {
                var mark = Button("Treinei", () => Commit(() => training.Attendance(date, "present"), "Presença registrada."), "Primary");
                mark.IsEnabled = !Training.Present(entry) && !training.Makeup(date).HasValue;
                AddChoice(actions, mark);
                if (entry.Scheduled || entry.Extra)
                {
                    var miss = Button("Faltei", () => Commit(() => training.Attendance(date, "missed"), "Falta registrada."));
                    miss.IsEnabled = entry.Attendance != "missed" && !entry.Finalized && !entry.Items.Any(i => i.Done);
                    miss.ToolTip = "Reabra o treino e desmarque os exercícios concluídos antes de registrar uma falta.";
                    AddChoice(actions, miss);
                }
                if (entry.Attendance != "" && !training.Makeup(date).HasValue)
                    AddChoice(actions, Button("Limpar marcação", () => Commit(() => training.Attendance(date, ""), "Marcação manual removida.")));
            }
            else Add(detail, Text(!training.IsTrackedDate(date) ? "O acompanhamento começa em " + Range(Data.FirstAccess) + "." :
                date > training.Today ? "Você pode registrar presença nessa data." : "Dias anteriores ficam disponíveis para consulta.", 13, "Muted"), 8);
            if (entry.Scheduled || entry.Extra) AddChoice(actions, Button(date > training.Today ? "Consultar treino previsto" : "Abrir treino do dia", () => SelectDay(date, Section.Workouts)));
            Add(detail, actions, 12);

            var makeup = training.Makeup(date);
            if (makeup.HasValue) AddMakeupStatus(detail, makeup.Value);
            else if (entry.Scheduled && date <= training.Today && !Training.Present(entry))
            {
                if (training.MakeupOptions(date).Any()) Add(detail, Button("Escolher dia para repor", () => MakeupDialog(date), "Selected"), 5);
                else Add(detail, Text("Nos próximos dias não há um horário livre para repor este treino.", 13, "Muted"), 7);
            }
            if (entry.MakeupFor != "")
            {
                Add(detail, Text("Reposição do treino de " + Range(Training.Date(entry.MakeupFor)) + ".", 13, "Muted"), 7);
                Add(detail, Button(Training.Present(entry) ? "Desvincular reposição" : "Cancelar reposição", () => UndoMakeup(date)), 6);
            }
            Add(content, Card(detail), 13);
            Add(content, Text("A frequência considera o período desde o primeiro acesso. Reposições feitas contam para a frequência no dia original.", 12, "Muted"), 10);
        }

        private void AddMakeupStatus(Panel panel, DateTime makeup)
        {
            var row = new WrapPanel();
            AddChoice(row, Text("Reposição em " + Range(makeup) + ".", 13, "Warn"));
            AddChoice(row, Button("Ver reposição", () => SelectDay(makeup, Section.Workouts), "Selected"));
            Add(panel, row, 7);
        }

        private void ShowWorkouts(DateTime date)
        {
            var entry = training.Get(date);
            bool isToday = date == training.Today && training.IsTrackedDate(date);
            bool future = date > training.Today;
            bool complete = !future && Training.Complete(entry);
            if (isToday && complete && reviewingCompletedDay != date)
            {
                content.Children.Add(CompletedWorkout(date, entry));
                return;
            }
            var info = new StackPanel();
            info.Children.Add(Text(entry.Title, 26, null, true));
            var moved = training.Makeup(date);
            if (moved.HasValue)
            {
                Add(info, Text("O treino deste dia foi transferido.", 13, "Muted"), 6);
                AddMakeupStatus(info, moved.Value);
                content.Children.Add(Card(info));
                return;
            }
            if (!entry.Scheduled && !entry.Extra)
            {
                if (isToday)
                {
                    Add(info, Text("Se for à academia hoje, você pode montar uma sessão complementar leve.", 14, "Muted"), 6);
                    var start = Button("Montar treino extra", () => Commit(() => training.EnableExtra(date), null), "Primary");
                    start.HorizontalAlignment = HorizontalAlignment.Left;
                    Add(info, start, 13);
                }
                content.Children.Add(Card(info, 21));
                return;
            }

            string name = training.At(date).Name;
            if (name != "") Add(info, Text("Treino de " + name, 12, "Muted"), 4);
            if (entry.MakeupFor != "") Add(info, Text("Reposição de " + Range(Training.Date(entry.MakeupFor)), 12, "Muted"), 5);
            if (entry.Complement)
            {
                if (entry.ComplementFor != "" && entry.ComplementGroup != "")
                {
                    var origin = Training.Date(entry.ComplementFor);
                    var next = training.NextGroup(date, entry.ComplementGroup);
                    string muscle = Catalog.Names[entry.ComplementGroup].ToLower(Training.Pt);
                    string reason = "O treino de " + DayAndDate(origin) + " ficou pendente. Hoje há um exercício leve para " + muscle + ".";
                    if (next.HasValue) reason += " O próximo treino dessa região é " + DayAndDate(next.Value) + ".";
                    Add(info, Text(reason, 13, "Muted"), 7);
                }
                Add(info, Text("Use carga leve e termine com pelo menos 4 repetições em reserva. Mantenha o cardio em ritmo confortável.", 12, "Accent"), 7);
            }
            var menu = new WrapPanel();
            if (isToday && !complete) AddChoice(menu, Button("Adicionar exercício", () => EditExerciseList(date, -1), "Selected"));
            if (isToday && entry.Complement && !complete && !entry.Items.Any(Training.HasLog))
                AddChoice(menu, Button("Voltar ao descanso", () => Commit(() => training.CancelComplement(date), null)));
            if (isToday && !complete && Training.Present(entry) && entry.Items.Count > 0)
                headerActions.Children.Add(Button("Finalizar treino", () => Commit(() => { reviewingCompletedDay = null; training.Finish(date); }, null), "Primary"));
            if (complete)
            {
                Add(info, Text("Treino concluído", 13, "Good", true), 7);
                if (isToday && entry.Finalized) AddChoice(menu, Button("Reabrir treino", () => Commit(() => training.Reopen(date), null)));
                if (isToday) AddChoice(menu, Button("Voltar à conclusão", () => { reviewingCompletedDay = null; Render(false); }));
            }
            if (menu.Children.Count > 0) Add(info, menu, 10);
            content.Children.Add(Card(info, 15));

            for (int i = 0; i < entry.Items.Count; i++)
                Add(content, MovementCard(date, entry, i), 8);
            if (entry.Items.Count == 0 && isToday) Add(content, Text("Adicione um exercício para montar o treino deste dia.", 13, "Muted"), 12);
        }

        private Border CompletedWorkout(DateTime date, Session entry)
        {
            var body = new StackPanel { Margin = new Thickness(6, 23, 6, 23) };
            var symbol = Center(Text("✓", 34, "Good", true));
            var badge = Card(symbol, 1, "Input");
            badge.HorizontalAlignment = HorizontalAlignment.Center;
            badge.Width = badge.Height = 61;
            badge.CornerRadius = new CornerRadius(31);
            badge.SetResourceReference(Border.BorderBrushProperty, "CoverageGood");
            body.Children.Add(badge);
            Add(body, Center(Text("Treino concluído", 33, null, true)), 12);
            Add(body, Center(Text("Você cumpriu a meta de hoje.", 16, "Good")), 5);
            if (entry.Finalized && entry.Items.Any(m => !m.Done))
                Add(body, Center(Text("Os exercícios que ficaram pendentes estão no relatório.", 12, "Muted")), 7);
            var review = Button("Rever exercícios", () => { reviewingCompletedDay = date; Render(false); });
            review.HorizontalAlignment = HorizontalAlignment.Center;
            Add(body, review, 17);
            return Card(body, 18);
        }

        private Border MovementCard(DateTime date, Session entry, int index)
        {
            var item = entry.Items[index];
            var exercise = Catalog.ById[item.Id];
            var block = new StackPanel();
            bool future = date > training.Today;
            bool editable = date == training.Today && !entry.Finalized;
            if (future) block.Children.Add(Text(exercise.Name, 16, null, true));
            else
            {
                var toggle = new CheckBox { IsChecked = item.Done, IsEnabled = editable };
                toggle.Content = Text(exercise.Name, 16, null, true);
                AutomationProperties.SetName(toggle, "Concluir " + exercise.Name);
                toggle.Click += delegate { Guard(() => Commit(() => {
                    var session = training.Save(date);
                    session.Items[index].Done = toggle.IsChecked == true;
                    if (session.Items[index].Done && session.Attendance == "missed") session.Attendance = "";
                    if (Training.Complete(session)) reviewingCompletedDay = null;
                }, null)); };
                block.Children.Add(toggle);
            }
            string plan = exercise.Type == 'A' ? item.Target : item.Sets + " × " + item.Target;
            string detail = Catalog.Names[exercise.Group] + "  ·  " + Catalog.GearNames[exercise.Gear] + "  ·  " + plan;
            Add(block, Text(detail, 12, "Muted"), 4);
            if (!future)
            {
                if (exercise.Type == 'A')
                {
                    string order = item.CardioPhase == "before" ? "Antes da musculação" : item.CardioPhase == "after" ? "Depois da musculação" : "Cardio";
                    Add(block, Text(order + "  ·  " + CoachingText.Rhythm(exercise, entry.Complement), 12, "Accent", true), 5);
                }
                else Add(block, Text(CoachingText.Rhythm(exercise, entry.Complement), 12, "Muted"), 5);
                var pauses = new List<string>();
                if (exercise.Type != 'A' && item.Sets > 1) pauses.Add("Entre séries: " + IntervalLabel(item.Rest));
                if (index + 1 < entry.Items.Count) pauses.Add("Antes do próximo exercício: " + IntervalLabel(Math.Max(60, Math.Max(item.Rest, entry.Items[index + 1].Rest))));
                if (pauses.Count > 0) Add(block, Text(string.Join("  ·  ", pauses), 12, "Accent", true), 4);
            }
            string progress = LiftLabel(item, exercise);
            if (!future && progress != "") Add(block, Text("Registro: " + progress, 12, "Good"), 4);
            if (!future)
            {
                if (editable && progress == "" && !entry.Complement)
                {
                    var previous = training.PreviousLoad(date, item.Id);
                    if (previous != null) Add(block, Text("Última carga: " + Kg(previous.Peak.Value), 12, "Muted"), 4);
                }
                var options = new WrapPanel();
                var view = Button("Ver exercício", () => OpenExerciseGuide(exercise), "Selected");
                AutomationProperties.SetName(view, "Ver exercício · " + exercise.Name);
                AddChoice(options, view);
                if (editable) AddChoice(options, Button(exercise.Type == 'A' ? "Registrar tempo" : "Registrar carga", () => LogDialog(date, index)));
                if (editable && !Training.HasLog(item))
                {
                    AddChoice(options, Button("Trocar", () => EditExerciseList(date, index)));
                    AddChoice(options, Button("Remover", () => Commit(() => training.Save(date).Items.RemoveAt(index), null)));
                }
                Add(block, options, 9);
            }
            var row = new DockPanel();
            var thumbnail = ExerciseThumbnail(exercise, 65);
            thumbnail.Margin = new Thickness(0, 2, 12, 0);
            DockPanel.SetDock(thumbnail, Dock.Left);
            row.Children.Add(thumbnail);
            row.Children.Add(block);
            return Card(row, 13);
        }

        private static string IntervalLabel(int seconds)
        {
            if (seconds < 60) return seconds + " s";
            var minutes = seconds / 60 + " min";
            return seconds % 60 == 0 ? minutes : minutes + " " + seconds % 60 + " s";
        }

        private static string LiftLabel(Movement item, Exercise exercise)
        {
            var fields = new List<string>();
            if (item.ActualSets > 0) fields.Add(item.ActualSets + " séries");
            if (item.ActualReps > 0) fields.Add(item.ActualReps + (exercise.Type == 'A' ? " min" : exercise.Type == 'T' ? " s por série" : " rep. por série"));
            var peak = new LiftRecord { Movement = item }.Peak;
            if (peak.HasValue) fields.Add((item.Efforts != null && item.Efforts.Select(s => s.Kilograms).Distinct().Count() > 1 ? "pico " : "") + Kg(peak.Value));
            if (item.RepsInReserve.HasValue) fields.Add(item.RepsInReserve.Value + " rep. em reserva");
            return string.Join("  ·  ", fields);
        }

        private void ShowReports()
        {
            WeekControls();
            var report = training.Report(week);
            var stats = new UniformGrid { Columns = 3 };
            AddMetric(stats, "Dias treinados", report.Attended.ToString(), report.Scheduled + " dias previstos", "Good");
            AddMetric(stats, "Exercícios feitos", report.Done.ToString(), "na semana selecionada", "Accent");
            AddMetric(stats, "Séries de força", report.Sets.ToString(), "registradas ou previstas dos exercícios feitos", "Accent");
            content.Children.Add(stats);

            Add(content, CoverageReport(report), 1);
            if (report.Done > 0) Add(content, WeeklyAdvice(report), 13);

            var list = new StackPanel();
            list.Children.Add(Text("Exercícios concluídos", 19, null, true));
            if (report.Done == 0) Add(list, Text("Os exercícios que você marcar como feitos aparecem aqui.", 13, "Muted"), 8);
            else Add(list, DoneByDay(), 9);
            for (int i = 0; i < 7; i++)
            {
                var date = week.AddDays(i);
                if (date > training.Today) break;
                var entry = training.Get(date);
                var items = entry.Items.Where(m => m.Done).ToList();
                if (!Training.Present(entry) && items.Count == 0) continue;
                Add(list, Line(), 11);
                Add(list, Text(Training.DayNames[i] + " · " + date.ToString("dd/MM"), 13, "Accent", true), 9);
                if (items.Count == 0) Add(list, Text("Presença registrada", 12, "Muted"), 4);
                foreach (var item in items)
                {
                    var ex = Catalog.ById[item.Id];
                    Add(list, Text(ex.Name, 13, null, true), 6);
                    Add(list, Text(Catalog.Names[ex.Group] + (LiftLabel(item, ex) == "" ? "" : "  ·  " + LiftLabel(item, ex)), 12, "Muted"), 2);
                }
            }
            Add(list, Button("Exportar relatório CSV", ExportReport), 13);
            if (report.Coverage.Values.Any(g => g.DoneExercises > 0))
            {
                var upcoming = new StackPanel();
                upcoming.Children.Add(Text("Preparar próxima semana", 18, null, true));
                var gaps = report.Coverage.Where(pair => pair.Value.Expected > 0 && pair.Value.Status < 2)
                    .OrderBy(pair => pair.Value.Status).ThenByDescending(pair => pair.Value.Target - pair.Value.DirectSets).Select(pair => Catalog.Names[pair.Key]).ToList();
                string note = gaps.Count > 0 ? "Abaixo da meta da semana: " + string.Join(", ", gaps) + ". Os próximos treinos dão prioridade a essas regiões quando possível." :
                    "As regiões previstas atingiram a meta de cobertura deste plano. Veja como seus treinos ficaram distribuídos para a próxima semana.";
                Add(upcoming, Text(note, 13, "Muted"), 6);
                Add(upcoming, Button("Ver próxima semana", () => SelectDay(PreferredDay(week.AddDays(7)), Section.Workouts), "Selected"), 10);
                Add(content, Columns(Top(Card(list)), Top(Card(upcoming)), 1.15, 1), 13);
            }
            else Add(content, Card(list), 13);
            if (report.Pending.Count > 0) Add(content, PendingWorkouts(report), 13);
            Add(content, MonthlySummary(selectedDate), 13);
        }

        private Border MonthlySummary(DateTime date)
        {
            var month = training.ReportMonth(date);
            var card = new StackPanel();
            card.Children.Add(Text("Resumo de " + date.ToString("MMMM yyyy", Training.Pt), 18, null, true));
            var first = new DateTime(date.Year, date.Month, 1);
            string period = first.AddMonths(1) <= Data.FirstAccess ? "O acompanhamento começou em " + Range(Data.FirstAccess) + "." :
                first < Data.FirstAccess ? "Contagem a partir de " + Range(Data.FirstAccess) + "." :
                "Treinos previstos e registrados neste mês.";
            Add(card, Text(period, 12, "Muted"), 6);
            string frequency = month.Rate.HasValue ? month.Rate.Value + "% de frequência" : "Frequência disponível após o primeiro compromisso";
            Add(card, Text(month.Attended + " dias treinados  ·  " + month.Compensated + " de " + month.Due + " compromissos cumpridos  ·  " + frequency, 13), 8);
            return Card(card, 15);
        }

        private Border PendingWorkouts(WeekReport report)
        {
            var pending = new StackPanel();
            pending.Children.Add(Text("Exercícios que ficaram pendentes", 19, null, true));
            Add(pending, Text("A cobertura muscular considera os exercícios concluídos.", 12, "Muted"), 5);
            foreach (var day in report.Pending.OrderBy(p => p.Key))
            {
                var date = day.Key;
                Add(pending, Line(), 10);
                Add(pending, Text(WorkoutDateLabel(date), 13, "Warn", true), 8);
                foreach (var movement in day.Value)
                {
                    var exercise = Catalog.ById[movement.Id];
                    Add(pending, Text(exercise.Name + "  ·  " + Catalog.Names[exercise.Group], 13), 5);
                }
                var open = Button("Ver treino do dia", () => SelectDay(date, Section.Workouts));
                open.HorizontalAlignment = HorizontalAlignment.Left;
                Add(pending, open, 7);
            }
            return Card(pending, 15);
        }

        private UIElement DoneByDay()
        {
            var chart = new UniformGrid { Columns = 7 };
            int[] counts = Enumerable.Range(0, 7).Select(i => week.AddDays(i)).Select(date => date > training.Today ? 0 : training.Get(date).Items.Count(m => m.Done)).ToArray();
            int max = Math.Max(1, counts.Max());
            for (int i = 0; i < 7; i++)
            {
                var item = new StackPanel { Margin = new Thickness(2) };
                item.Children.Add(Center(Text(counts[i].ToString(), 11, counts[i] > 0 ? "Accent" : "Muted", true)));
                var frame = new Grid { Height = 30 };
                var bar = new Border { CornerRadius = new CornerRadius(3, 3, 0, 0), Height = Math.Max(1, 30.0 * counts[i] / max),
                    Width = 15, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom };
                bar.SetResourceReference(Border.BackgroundProperty, counts[i] > 0 ? "Accent" : "Line");
                frame.Children.Add(bar);
                Add(item, frame, 2);
                Add(item, Center(Text(Training.DayShort[i], 10, "Muted")), 3);
                string label = Training.DayNames[i] + " · " + counts[i] + " exercícios concluídos";
                item.ToolTip = label;
                AutomationProperties.SetName(item, label);
                chart.Children.Add(item);
            }
            return chart;
        }
    }
}
