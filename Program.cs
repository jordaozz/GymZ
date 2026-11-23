using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

[assembly: AssemblyTitle("GymZ")]
[assembly: AssemblyDescription("Planejador e diário de treinos")]
[assembly: AssemblyProduct("GymZ")]
[assembly: AssemblyVersion("1.3.1.0")]
[assembly: AssemblyFileVersion("1.3.1.0")]

namespace GymZ
{
    internal sealed partial class GymApp
    {
        private enum Section { Overview, Attendance, Workouts, Muscles, Reports, Profile, Settings, WorkoutRecord }
        private const string Version = "1.3.1";
        private readonly Window window;
        private readonly Storage storage;
        private Training training;
        private GymData Data { get { return training.Data; } }
        private DateTime week, selectedDate;
        private Section section;
        private string muscle = "chest", search = "", libraryGear = "";
        private bool backView, libraryAll;
        private readonly StackPanel content, headerActions;
        private readonly ScrollViewer scroll;
        private readonly DispatcherTimer noticeTimer, dateTimer;
        private readonly Dictionary<Section, Button> nav = new Dictionary<Section, Button>();
        private static readonly Section[] SidebarOrder = { Section.Workouts, Section.Overview, Section.Attendance, Section.Muscles, Section.Reports, Section.Profile, Section.Settings };
        private static readonly string[] Titles = { "Visão geral", "Presença", "Treinos", "Mapa muscular", "Relatórios", "Meu perfil", "Configurações" };
        private static readonly string[] Subtitles = {
            "Acompanhe sua frequência e veja qual é o próximo treino.",
            "Consulte o calendário e registre os dias em que treinou ou faltou.",
            "Siga a lista do dia, marque exercícios e registre sua evolução.",
            "Selecione uma região do corpo para encontrar exercícios e ver como executá-los.",
            "Veja o que foi feito e quais grupos musculares foram trabalhados na semana.",
            "Ajuste o plano aos seus objetivos e à sua rotina.",
            "Personalize a aparência, ajuste a leitura ou faça um backup."
        };
        private static readonly string[] Symbols = { "\uE80F", "\uE787", "\uE9D5", "\uE81E", "\uE9D2", "\uE77B", "\uE713" };
        private static readonly string[] AccentNames = { "Azul", "Turquesa", "Violeta", "Rosa", "Âmbar" };
        private static readonly string[][] AccentColors = {
            new[] { "#83B8FF", "#214171", "#30568D", "#225FC6", "#1754AE", "#E6F0FF", "#A4C4F1" },
            new[] { "#70E4CE", "#173F40", "#306B65", "#08776A", "#006B5C", "#E0F5EF", "#84CDC0" },
            new[] { "#C6B0FF", "#3A2D5C", "#5B4A83", "#6551BC", "#5733AA", "#EFE9FF", "#C6B2EF" },
            new[] { "#FDAFCB", "#542B44", "#814767", "#9F386D", "#8B2557", "#FCE9F1", "#E4A8C1" },
            new[] { "#F4CC82", "#4A371E", "#796036", "#875500", "#784A00", "#FFF1DA", "#DFBD79" }
        };

        [STAThread]
        private static int Main(string[] args)
        {
            Thread.CurrentThread.CurrentCulture = Training.Pt;
            Thread.CurrentThread.CurrentUICulture = Training.Pt;
            if (args.Length == 2 && args[0] == "--self-test") return Verify.Run(args[1]);
            var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
            try
            {
                string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Storage.FolderName);
                using (var store = new Storage(folder))
                {
                    var gym = new GymApp(store, DateTime.Today);
                    return app.Run(gym.window);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Não foi possível abrir o GymZ. Confira se ele já está aberto em outra janela e tente novamente.\n\n" + ex.GetBaseException().Message,
                    "GymZ", MessageBoxButton.OK, MessageBoxImage.Error);
                return 1;
            }
        }

        internal GymApp(Storage storage, DateTime today, bool followsClock = true)
        {
            this.storage = storage;
            training = new Training(storage.Open(today), today);
            storage.Save(Data);
            section = training.NeedsSetup ? Section.Profile : Section.Workouts;
            week = Training.Monday(today);
            selectedDate = today;
            var colors = new ResourceDictionary();
            Application.Current.Resources = colors;
            SetPalette();
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Main.xaml")) window = (Window)XamlReader.Load(stream);
            Application.Current.Resources.MergedDictionaries.Add(window.Resources);
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("GymZ.png"))
            {
                var icon = BitmapFrame.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                window.Icon = icon;
                Get<Image>("Brand").Source = icon;
            }
            content = Get<StackPanel>("Content");
            headerActions = Get<StackPanel>("HeaderActions");
            scroll = Get<ScrollViewer>("Scroll");
            foreach (Section target in SidebarOrder)
            {
                Section page = target;
                var button = Button(Titles[(int)page], () => Navigate(page, training.Today), "Nav");
                var row = Row();
                var glyph = Text(Symbols[(int)page], 15, "Accent");
                glyph.FontFamily = new FontFamily("Segoe MDL2 Assets");
                glyph.Width = 26;
                row.Children.Add(glyph);
                row.Children.Add(Text(Titles[(int)page], 13, null, true));
                button.Content = row;
                button.Margin = new Thickness(0, 0, 0, 4);
                nav.Add(page, button);
                Get<StackPanel>(page == Section.Settings ? "SettingsNav" : "Navigation").Children.Add(button);
            }
            window.InputBindings.Add(new KeyBinding(new UiCommand(() => ResetWeek()), Key.Home, ModifierKeys.Control));
            window.InputBindings.Add(new KeyBinding(new UiCommand(() => Go(Section.Settings)), Key.F9, ModifierKeys.None));
            window.InputBindings.Add(new KeyBinding(new UiCommand(Home), Key.F1, ModifierKeys.None));
            window.InputBindings.Add(new KeyBinding(new UiCommand(GoBack), Key.Left, ModifierKeys.Alt));
            window.SizeChanged += delegate { if (section == Section.Overview) AdjustHero(); };
            noticeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            noticeTimer.Tick += delegate { Get<Border>("Notice").Visibility = Visibility.Collapsed; noticeTimer.Stop(); };
            dateTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            dateTimer.Tick += delegate { RefreshDate(DateTime.Today); };
            if (followsClock) window.Loaded += delegate { dateTimer.Start(); };
            window.Activated += delegate { if (dateTimer.IsEnabled) RefreshDate(DateTime.Today); };
            window.Closed += delegate { dateTimer.Stop(); noticeTimer.Stop(); };
            ApplyScale();
            Render(false);
        }

        private void RefreshDate(DateTime today)
        {
            today = today.Date;
            if (today == training.Today) return;
            var old = training.Today;
            training.Today = today;
            if (section == Section.Workouts || section != Section.WorkoutRecord && selectedDate == old)
            {
                selectedDate = today;
                week = Training.Monday(today);
            }
            if (section != Section.Profile && section != Section.Settings && section != Section.Muscles) Render(true);
        }

        private T Get<T>(string name) where T : class { return (T)window.FindName(name); }

        private void SetPalette()
        {
            var preferences = Data.Preferences;
            bool dark = preferences.Theme == "dark";
            var palette = dark ? new[] { "#101723", "#0D1523", "#182333", "#202E41", "#121C2B", "#2E3C50", "#EFF4FC", "#A9B9CE", "#75DEC2", "#FFABAA", "#F3CA82" }
                : new[] { "#F3F6FB", "#EAF0F9", "#FFFFFF", "#EFF3FA", "#FAFCFF", "#CCD7E6", "#182740", "#4F627D", "#09664F", "#9F2F35", "#7D4C00" };
            if (preferences.Contrast)
                palette = dark ? new[] { "#080C13", "#080F19", "#111B29", "#172539", "#101A28", "#708199", "#FFFFFF", "#D8E4F6", "#8BFFDE", "#FFB7B7", "#FFDC9C" }
                    : new[] { "#F5F8FC", "#E5EEF9", "#FFFFFF", "#E8EFF9", "#FFFFFF", "#657B98", "#101827", "#2A405E", "#005940", "#821024", "#684200" };
            string[] keys = { "Canvas", "Sidebar", "Surface", "Raised", "Input", "Line", "Ink", "Muted", "Good", "Bad", "Warn" };
            for (int i = 0; i < keys.Length; i++) Application.Current.Resources[keys[i]] = ColorBrush(palette[i]);
            var accent = AccentColors[Array.IndexOf(Storage.Accents, preferences.Accent)];
            Application.Current.Resources["Accent"] = ColorBrush(accent[dark ? 0 : 4]);
            Application.Current.Resources["AccentSoft"] = ColorBrush(accent[dark ? 1 : 5]);
            Application.Current.Resources["AccentLine"] = ColorBrush(accent[dark ? 2 : 6]);
            Application.Current.Resources["Action"] = ColorBrush(accent[3]);
            Application.Current.Resources["CoverageGood"] = ColorBrush(dark ? "#23795F" : "#5BB792");
            Application.Current.Resources["CoveragePartial"] = ColorBrush(dark ? "#947126" : "#ECCD70");
            Application.Current.Resources["CoverageNone"] = ColorBrush(dark ? "#913E4B" : "#E68B91");
        }

        private void ApplyScale()
        {
            double scale = Data.Preferences.Scale / 100.0;
            Get<Grid>("Layout").LayoutTransform = new ScaleTransform(scale, scale);
        }

        private void ResetWeek()
        {
            if (section == Section.WorkoutRecord || openGuide != null)
            {
                Go(Section.Workouts);
                return;
            }
            week = Training.Monday(training.Today);
            selectedDate = training.Today;
            Render(false);
        }

        private DateTime PreferredDay(DateTime start)
        {
            var days = Enumerable.Range(0, 7).Select(i => start.AddDays(i)).ToArray();
            var active = days.Where(day => { var session = training.Get(day); return session.Scheduled || session.Extra; }).ToArray();
            var next = active.FirstOrDefault(day => day >= training.Today);
            if (next != default(DateTime)) return next;
            if (active.Length > 0) return active[0];
            return days.Contains(training.Today) ? training.Today : start;
        }

        private void Render(bool keepScroll)
        {
            double offset = scroll.VerticalOffset;
            var activeSide = section == Section.WorkoutRecord ? Section.Workouts : section;
            foreach (var item in nav)
            {
                item.Value.IsEnabled = CanOpen(item.Key);
                if (item.Key == activeSide)
                {
                    item.Value.SetResourceReference(Control.BackgroundProperty, "AccentSoft");
                    item.Value.SetResourceReference(Control.BorderBrushProperty, "AccentLine");
                }
                else { item.Value.ClearValue(Control.BackgroundProperty); item.Value.ClearValue(Control.BorderBrushProperty); }
            }
            content.Children.Clear();
            headerActions.Children.Clear();
            ShowPageNavigation();
            if (openGuide != null)
            {
                Get<TextBlock>("PageTitle").Text = openGuide.Name;
                Get<TextBlock>("PageSubtitle").Text = "Guia de execução e equipamento";
                FillExerciseGuide(content, openGuide);
                scroll.ScrollToVerticalOffset(keepScroll ? offset : 0);
                return;
            }
            if (section == Section.WorkoutRecord)
            {
                Get<TextBlock>("PageTitle").Text = selectedDate > training.Today ? "Treino previsto" : "Treino do dia";
                Get<TextBlock>("PageSubtitle").Text = WorkoutDateLabel(selectedDate) + (selectedDate > training.Today ? " · Disponível para registro nessa data" : "");
                headerActions.Children.Add(Button("Treino de hoje", () => Go(Section.Workouts), "Selected"));
                ShowWorkouts(selectedDate);
                scroll.ScrollToVerticalOffset(keepScroll ? offset : 0);
                return;
            }
            Get<TextBlock>("PageTitle").Text = Titles[(int)section];
            Get<TextBlock>("PageSubtitle").Text = Subtitles[(int)section];
            if (section == Section.Profile && training.NeedsSetup)
            {
                Get<TextBlock>("PageTitle").Text = "Monte seu primeiro treino";
                Get<TextBlock>("PageSubtitle").Text = "Preencha suas medidas, seus objetivos e os dias que combinam com sua rotina.";
            }
            switch (section)
            {
                case Section.Overview: ShowOverview(); break;
                case Section.Attendance: ShowAttendance(); break;
                case Section.Workouts:
                    Get<TextBlock>("PageTitle").Text = "Treino de hoje";
                    Get<TextBlock>("PageSubtitle").Text = WorkoutDateLabel(training.Today);
                    ShowWorkouts(training.Today);
                    break;
                case Section.Muscles: ShowMuscles(); break;
                case Section.Reports: ShowReports(); break;
                case Section.Profile: ShowProfile(); break;
                case Section.Settings: ShowSettings(); break;
            }
            scroll.ScrollToVerticalOffset(keepScroll ? offset : 0);
        }

        private void MoveWeek(int days)
        {
            week = week.AddDays(days);
            selectedDate = PreferredDay(week);
            Render(false);
        }

        private void Commit(Action action, string message, bool redraw = true)
        {
            action();
            try { storage.Save(Data); }
            catch
            {
                training = new Training(storage.Open(training.Today), training.Today);
                if (redraw) Render(true);
                throw;
            }
            if (redraw) Render(true);
            if (!string.IsNullOrEmpty(message)) Notify(message);
        }

        private void Notify(string message)
        {
            Get<TextBlock>("NoticeText").Text = message;
            Get<Border>("Notice").Visibility = Visibility.Visible;
            noticeTimer.Stop();
            noticeTimer.Start();
        }

        private void Guard(Action action)
        {
            try
            {
                if (dateTimer.IsEnabled) RefreshDate(DateTime.Today);
                action();
            }
            catch (Exception ex) { MessageBox.Show(window, ex.GetBaseException().Message, "GymZ", MessageBoxButton.OK, MessageBoxImage.Information); }
        }

        private Button Button(string label, Action action, string style = null)
        {
            var button = new Button { Content = label, VerticalAlignment = VerticalAlignment.Center };
            if (style != null) button.Style = (Style)window.FindResource(style);
            AutomationProperties.SetName(button, label);
            button.Click += delegate { Guard(action); };
            return button;
        }

        private static SolidColorBrush ColorBrush(string hex)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            brush.Freeze();
            return brush;
        }

        private static StackPanel Row() { return new StackPanel { Orientation = Orientation.Horizontal }; }
        private static TextBlock Text(string value, double size = 14, string brush = null, bool strong = false)
        {
            var text = new TextBlock { Text = value, FontSize = size, FontWeight = strong ? FontWeights.SemiBold : FontWeights.Normal, VerticalAlignment = VerticalAlignment.Center };
            if (brush != null) text.SetResourceReference(TextBlock.ForegroundProperty, brush);
            return text;
        }

        private static void Add(Panel panel, UIElement element, double space = 0)
        {
            var child = element as FrameworkElement;
            if (space > 0 && child != null) child.Margin = new Thickness(child.Margin.Left, space, child.Margin.Right, child.Margin.Bottom);
            panel.Children.Add(element);
        }

        private static Border Card(UIElement child, double padding = 18, string fill = "Surface")
        {
            var border = new Border { Child = child, CornerRadius = new CornerRadius(13), Padding = new Thickness(padding), BorderThickness = new Thickness(1) };
            border.SetResourceReference(Border.BackgroundProperty, fill);
            border.SetResourceReference(Border.BorderBrushProperty, "Line");
            return border;
        }

        private static Grid Columns(UIElement left, UIElement right, double first = 1, double second = 1, double gap = 13)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(first, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(gap) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(second, GridUnitType.Star) });
            grid.Children.Add(left);
            grid.Children.Add(right);
            Grid.SetColumn(right, 2);
            return grid;
        }

        private static Border Line()
        {
            var line = new Border { Height = 1 };
            line.SetResourceReference(Border.BackgroundProperty, "Line");
            return line;
        }

        private static ProgressBar Bar(double value, double max, string brush = null)
        {
            var bar = new ProgressBar { Maximum = Math.Max(max, 1), Value = value };
            if (brush != null) bar.SetResourceReference(Control.ForegroundProperty, brush);
            return bar;
        }

        private static string Range(DateTime day) { return day.ToString("dd/MM/yyyy", Training.Pt); }

        private static string WorkoutDateLabel(DateTime day) { return Training.DayNames[Training.Day(day)] + " · " + Range(day); }

        private sealed class UiCommand : ICommand
        {
            private readonly Action action;
            public UiCommand(Action action) { this.action = action; }
            public event EventHandler CanExecuteChanged { add { } remove { } }
            public bool CanExecute(object parameter) { return true; }
            public void Execute(object parameter) { action(); }
        }
    }
}
