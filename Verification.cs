using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace GymZ
{
    internal sealed partial class GymApp
    {
        private static class Verify
        {
            public static int Run(string output)
            {
                Directory.CreateDirectory(output);
                var results = new List<string>();
                int code = 0;
                try
                {
                    var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                    var first = new DateTime(2026, 11, 16);
                    var testFolder = Path.Combine(output, "teste-" + Guid.NewGuid().ToString("N"));
                    CheckFirstUse(output, testFolder + "-novo", first);
                    CheckWorkoutFlow(output, testFolder + "-treino", first);
                    CheckRecordingDates(first);
                    CheckComplement(first);
                    CheckCardioAndEquipment(first);
                    results.Add("Primeiro acesso: tela de perfil sem treino ou agenda padrão, cadastro de nome, medidas, dias e plano personalizado.");
                    results.Add("Treino de hoje: abre direto na lista, guia na mesma janela, retorno à lista, finalização parcial, conclusão automática e virada do dia.");
                    results.Add("Datas e descanso: bloqueio de dias passados e futuros, contagem desde o acesso e sessão extra leve com ajuste ao próximo treino.");
                    results.Add("Equipamentos e cardio: musculação filtrada, bicicleta/esteira/transport e cardio sem aparelho, duração, ordem e opção sem cardio automático.");
                    var data = GymData.Create(first);
                    using (var storage = new Storage(testFolder))
                    {
                        CheckCatalog();
                        CaptureIllustrations(output);
                        CheckPreferences(first);
                        CheckWeeklySplit(first);
                        results.Add("Catálogo: " + Catalog.All.Count + " exercícios com imagens, equipamento, instruções e cobertura dos 12 grupos.");
                        var training = new Training(data, first);
                        training.ChangeProfile(new Profile { Name = "Sam", Weight = 75, Height = 175 });
                        var monday = training.Save(first);
                        Require(monday.Title == "Peito e tríceps" && monday.Items[0].Id == "incline-db" && monday.Items[1].Id == "cable-fly", "Treino de segunda padrão");
                        var alternate = training.Alternatives(first, monday, monday.Items[0]).First();
                        Require(Catalog.ById[monday.Items[0].Id].Group == alternate.Group && !monday.Items.Any(i => i.Id == alternate.Id), "Substituição equivalente");
                        monday.Items[0].Done = true;
                        monday.Items[0].ActualSets = 2;
                        monday.Items[0].ActualReps = 10;
                        monday.Items[0].Load = 12;
                        bool conflict = false;
                        try { training.Attendance(first, "missed"); }
                        catch (InvalidOperationException) { conflict = true; }
                        Require(conflict && Training.Present(monday), "A presença deve refletir os exercícios concluídos");
                        training.Today = first.AddDays(1);
                        training.Attendance(first.AddDays(1), "missed");
                        training.Today = first.AddDays(6);
                        var report = training.Report(first);
                        Require(report.Due == 4 && report.Attended == 1 && report.Missed == 1 && report.Unmarked == 2 && report.Rate == 25 &&
                            report.Direct["chest"] == 1 && report.Indirect.Contains("triceps") && report.Sets == 2, "Presença e cobertura devem usar os registros reais");

                        DateTime makeup = first.AddDays(6);
                        Require(training.MakeupOptions(first.AddDays(1)).Contains(makeup), "Sugestão de reposição em dia livre");
                        training.Compensate(first.AddDays(1), makeup);
                        training.Attendance(makeup, "present");
                        report = training.Report(first);
                        Require(report.Attended == 2 && report.Rate == 50 && report.Missed == 0 && report.Due == 4, "Reposição deve contar uma vez");
                        Require(training.Makeup(first.AddDays(1)) == makeup && !training.Get(makeup).Items.Any(i => i.Done), "Reposição vinculada sem copiar conclusões");
                        training.CancelMakeup(makeup);
                        Require(!training.Makeup(first.AddDays(1)).HasValue && Training.Present(training.Get(makeup)), "Desvincular preserva presença do treino extra");

                        var adapt = Enumerable.Range(7, 7).Select(d => training.Get(first.AddDays(d))).SelectMany(s => s.Items).ToList();
                        Require(adapt.Any(i => i.Priority && Catalog.ById[i.Id].Group == "shoulders"), "A semana seguinte prioriza ombros após o treino omitido");
                        Require(training.Report(first).Coverage["shoulders"].Status == 1 && training.Report(first).Coverage["calves"].Status == 0,
                            "Cor amarela para atuação auxiliar e vermelha sem registro");
                        training.Today = first.AddDays(3);
                        var second = training.Save(first.AddDays(3));
                        second.Items.RemoveAll(i => Catalog.ById[i.Id].Group == "chest");
                        second.Items.Add(training.Prescribe(Catalog.ById["incline-db"], training.At(first), false));
                        training.RecordPerformance(first.AddDays(3), second.Items.Count - 1, new List<LiftSet> {
                            new LiftSet { Kilograms = 12.5m, RepsOrSeconds = 12 }, new LiftSet { Kilograms = 14, RepsOrSeconds = 8 }
                        }, 0, 4, true);
                        training.Today = first.AddDays(6);
                        var chest = training.Report(first).Coverage["chest"];
                        Require(chest.DirectDays.Count == 2 && chest.DirectSets == 4 && chest.EasyEfforts == 1 && chest.Status == 1,
                            "Cobertura distingue volume, dias e esforço");
                        Require(training.LiftHistory("incline-db", first.AddDays(6)).Select(r => r.Peak).SequenceEqual(new decimal?[] { 14m, 12m }),
                            "Histórico reúne pico por dia e conserva carga antiga");
                        string legacyFile = Path.Combine(testFolder, "legacy.json");
                        var oldStorageFile = Path.Combine(testFolder, Storage.DataName);
                        storage.Save(data);
                        var legacy = File.ReadAllText(oldStorageFile);
                        legacy = Regex.Replace(legacy, "\"Avoided\":\\[\\],|,\"(?:FirstAccess|FocusMode|Name|SplitStart)\":\"[^\"]*\"|,\"Split\":\"auto\"|,\"Swaps\":\\[\\]", "");
                        File.WriteAllText(legacyFile, legacy);
                        var migrated = Storage.Read(legacyFile);
                        Require(migrated.FirstAccess == first && migrated.Profiles[0].Profile.FocusMode == "balanced" &&
                            migrated.Profiles[0].Profile.Split == null && !migrated.SplitStart.HasValue &&
                            migrated.Avoided.Count == 0 && migrated.Swaps.Count == 0, "Data de início e preferências em backups 1.0");
                        training.Today = first.AddDays(7);
                        var profile = new Profile { Name = "Sam", Focus = "glutes", Goal = "muscle", Gear = "body", Weight = 72.5m, Height = 172, Days = new[] { 0, 2, 4 } };
                        training.ChangeProfile(profile);
                        var newDay = training.Get(training.Today);
                        Require(newDay.Items.Count(m => Catalog.ById[m.Id].Group == "glutes") >= 2 && newDay.Items.All(m => Catalog.ById[m.Id].Gear == "body") &&
                            training.Get(first).Items[0].Done && training.PreviousLoad(training.Today, "incline-db").Peak == 14, "Perfil, ênfase, equipamento e histórico");
                        results.Add("Planejamento: recomposição, ênfase, leg press horizontal, trocas equivalentes, preferência aprendida, variedade limitada e retorno de ombros.");
                        results.Add("Presença e cargas: faltas, reposição, frequência, cobertura, carga por série, esforço e histórico anterior.");

                        data.Preferences = new Preferences { Theme = "light", Accent = "teal", Scale = 110, Contrast = true };
                        storage.Save(data);
                        var reopened = storage.Open(training.Today);
                        Require(reopened.Sessions[Training.Key(first)].Items[0].Load == 12 && reopened.Preferences.Accent == "teal" &&
                            reopened.Profiles.Last().Profile.Weight == 72.5m && reopened.Sessions[Training.Key(first.AddDays(3))].Items.Last().Efforts[0].Kilograms == 12.5m,
                            "Persistência de cargas distintas e leitura local");
                        var package = storage.Backup(output, reopened, Assembly.GetExecutingAssembly().Location);
                        Require(File.Exists(Path.Combine(package, "GymZ.exe")), "Backup inclui o executável");
                        var backup = Storage.Read(Path.Combine(package, Storage.BackupName));
                        var previousFile = storage.Restore(GymData.Create(first), backup);
                        Require(File.Exists(previousFile) && storage.Open(training.Today).Preferences.Contrast, "Restauração preserva a cópia anterior");
                        string invalid = Path.Combine(testFolder, "invalid.json");
                        File.WriteAllText(invalid, "{\"Version\":9}");
                        bool rejected = false;
                        try { Storage.Read(invalid); }
                        catch (InvalidDataException) { rejected = true; }
                        Require(rejected && storage.Open(training.Today).Sessions.Count > 0, "Backup inválido recusado sem perda de dados");
                        results.Add("Dados: persistência, backup portátil, restauração e recusa de arquivo inválido.");

                        var gym = new GymApp(storage, first.AddDays(6), false);
                        gym.dateTimer.Stop();
                        gym.window.Show();
                        Flush(gym.window);
                        gym.dateTimer.Stop();
                        foreach (Section page in gym.nav.Keys)
                        {
                            Invoke(gym.nav[page]);
                            Flush(gym.window);
                            Require(gym.section == page && gym.content.Children.Count > 0 && gym.Get<TextBlock>("PageTitle").Text ==
                                (page == Section.Workouts ? "Treino de hoje" : Titles[(int)page]), "Navegação " + page);
                        }
                        gym.ChangeAppearance(p => { p.Theme = "dark"; p.Accent = "blue"; p.Scale = 100; p.Contrast = false; });
                        gym.Get<Border>("Notice").Visibility = Visibility.Collapsed;
                        gym.Go(Section.Overview);
                        Capture(gym.window, Path.Combine(output, "Visao-geral.png"));
                        gym.SelectDay(first, Section.Workouts);
                        Capture(gym.window, Path.Combine(output, "Treinos.png"));
                        gym.OpenExerciseGuide(Catalog.ById["leg-press-horizontal"]);
                        Capture(gym.window, Path.Combine(output, "Guia-leg-180.png"));
                        gym.GoBack();
                        InspectDialog(gym, () => gym.HistoryDialog(Catalog.ById["incline-db"], gym.training.Today), Path.Combine(output, "Evolucao.png"));
                        gym.RefreshDate(first.AddDays(3));
                        InspectDialog(gym, () => gym.LogDialog(first.AddDays(3), gym.training.Get(first.AddDays(3)).Items.Count - 1), Path.Combine(output, "Registrar-carga.png"));
                        gym.RefreshDate(first.AddDays(6));
                        gym.Get<Border>("Notice").Visibility = Visibility.Collapsed;
                        gym.Go(Section.Attendance);
                        Capture(gym.window, Path.Combine(output, "Presenca.png"));
                        InspectDialog(gym, gym.OpenCalendar, Path.Combine(output, "Calendario-mensal.png"), dialog => {
                            Invoke(FindButton(dialog, "‹  Mês anterior"));
                            Require(Descendants(dialog).OfType<TextBlock>().Any(t => t.Text.StartsWith("Outubro")), "Calendário percorre meses");
                            Invoke(FindButton(dialog, "Mês atual"));
                            Require(Descendants(dialog).OfType<TextBlock>().Any(t => t.Text.StartsWith("Novembro")), "Mês atual volta ao mês do relógio");
                        });
                        gym.Go(Section.Muscles);
                        Capture(gym.window, Path.Combine(output, "Mapa-frente.png"));
                        var mapButton = Descendants(gym.content).OfType<Button>().First(b => b.Content is System.Windows.Shapes.Path && (string)b.ToolTip == "Quadríceps");
                        Invoke(mapButton);
                        Require(gym.muscle == "quads" && gym.search == "", "Região clicável atualiza exercícios");
                        gym.FlipMap(true);
                        Capture(gym.window, Path.Combine(output, "Mapa-costas.png"));
                        gym.Go(Section.Reports);
                        Capture(gym.window, Path.Combine(output, "Relatorios.png"));
                        gym.GoBack();
                        Require(gym.section == Section.Reports && gym.selectedDate == gym.training.Today, "Voltar retorna à raiz de Relatórios sem percorrer abas anteriores");
                        gym.Go(Section.Attendance);
                        gym.SelectDay(gym.training.Today.AddDays(3), Section.Workouts);
                        gym.SelectDay(gym.training.Today.AddDays(4), Section.Workouts);
                        gym.GoBack();
                        Require(gym.section == Section.Attendance && gym.selectedDate == gym.training.Today, "Voltar retorna à aba de origem mesmo após várias prévias");
                        gym.Go(Section.Muscles);
                        gym.OpenExerciseGuide(Catalog.ById["band-squat"]);
                        Capture(gym.window, Path.Combine(output, "Guia-agachamento-elastico.png"));
                        gym.GoBack();
                        Require(gym.section == Section.Muscles && gym.openGuide == null, "Voltar após o guia do mapa permanece na aba Mapa muscular");
                        gym.OpenExerciseGuide(Catalog.ById["band-lateral"]);
                        Capture(gym.window, Path.Combine(output, "Guia-elevacao-lateral-elastico.png"));
                        gym.GoBack();
                        gym.OpenExerciseGuide(Catalog.ById["wrist-extension"]);
                        Capture(gym.window, Path.Combine(output, "Guia-extensao-de-punho.png"));
                        gym.Home();
                        Require(gym.section == Section.Workouts && gym.selectedDate == gym.training.Today, "Início abre o treino de hoje");
                        gym.ChangeAppearance(p => { p.Theme = "light"; p.Accent = "teal"; p.Scale = 125; p.Contrast = true; });
                        gym.window.Width = 875;
                        gym.Go(Section.Settings);
                        Capture(gym.window, Path.Combine(output, "Acessibilidade.png"));
                        gym.Go(Section.Profile);
                        Capture(gym.window, Path.Combine(output, "Perfil.png"));
                        var split = Descendants(gym.content).OfType<ComboBox>().Single(box =>
                            ((TextBlock)AutomationProperties.GetLabeledBy(box)).Text == "Divisão da semana");
                        split.SelectedItem = split.Items.Cast<Option>().Single(option => option.Key == "groups");
                        Flush(gym.window);
                        Require(Descendants(gym.content).OfType<ComboBox>().Count(box => box.IsVisible &&
                            box.Items.Cast<Option>().Any(option => option.Key == "chest-biceps")) == gym.training.At(gym.training.Today).Days.Length,
                            "A divisão por grupos mostra edição só para os dias selecionados");
                        gym.window.Close();
                        app.Shutdown();
                        results.Add("Interface: 7 telas, Voltar e Início, mês atual, guias dos exercícios, histórico, relatório visual e opções de acessibilidade.");
                    }
                }
                catch (Exception ex) { results.Add(ex.ToString()); code = 1; }
                File.WriteAllLines(Path.Combine(output, "resultado.txt"), results);
                return code;
            }

            private static void CheckCatalog()
            {
                Require(Catalog.All.Count >= 60, "Catálogo com variedade");
                foreach (var exercise in Catalog.All)
                {
                    Require(Catalog.Names.ContainsKey(exercise.Group) && exercise.Other.All(Catalog.Names.ContainsKey) && Catalog.GearNames.ContainsKey(exercise.Gear), "Grupos e equipamento: " + exercise.Id);
                    Require(exercise.Steps.Length >= 3 && exercise.Tip.Length > 10 && "CITA".Contains(exercise.Type), "Técnica: " + exercise.Id);
                    var art = ExerciseArt.ById[exercise.Id];
                    Require(art.Count > 0 && art.Count <= 2 && art.Frames.Distinct().Count() == art.Count, "Posições: " + exercise.Id);
                    if (!art.FromWorkoutGuide) Require(art.Captions.Length == art.Count, "Legendas: " + exercise.Id);
                    foreach (int frame in art.Frames)
                    {
                        var picture = (BitmapSource)ExerciseArt.ImageFor(exercise.Id, frame);
                        var crop = (BitmapSource)ExerciseArt.ThumbnailFor(exercise.Id, frame);
                        Require(crop.PixelWidth >= 48 && crop.PixelHeight >= 48 &&
                            (crop.PixelWidth < picture.PixelWidth || crop.PixelHeight < picture.PixelHeight), "Ilustração transparente: " + exercise.Id);
                    }
                    Require(!Geometry.Parse(FindEquipment(exercise).Picture).Bounds.IsEmpty, "Reconhecimento do aparelho " + exercise.Id);
                }
                foreach (string group in Catalog.Groups)
                    Require(Catalog.All.Count(e => e.Group == group) >= 3 && Catalog.All.Any(e => e.Group == group && e.Gear == "body"), "Variedade e alternativa sem aparelho para " + group);
            }

            private static void CaptureIllustrations(string output)
            {
                for (int batch = 0; batch * 12 < Catalog.All.Count; batch++)
                {
                    var sample = Catalog.All.Skip(batch * 12).Take(12).ToArray();
                    var view = new DrawingVisual();
                    using (var draw = view.RenderOpen())
                    {
                        draw.DrawRectangle(ColorBrush("#141F30"), null, new Rect(0, 0, 1170, 660));
                        for (int i = 0; i < sample.Length; i++)
                        {
                            var exercise = sample[i];
                            var art = ExerciseArt.ById[exercise.Id];
                            int left = (i % 3) * 390, top = (i / 3) * 165;
                            var caption = new FormattedText(exercise.Name, Training.Pt, FlowDirection.LeftToRight,
                                new Typeface("Segoe UI"), 14, Brushes.White, 1);
                            draw.DrawText(caption, new Point(left + 12, top + 9));
                            for (int phase = 0; phase < art.Count; phase++)
                            {
                                var figure = ExerciseArt.ImageFor(exercise.Id, art.Frames[phase]);
                                double scale = Math.Min(173.0 / figure.Width, 118.0 / figure.Height);
                                double width = figure.Width * scale, height = figure.Height * scale;
                                draw.DrawImage(figure, new Rect(left + 20 + phase * 177 + (173 - width) / 2, top + 33 + (118 - height) / 2, width, height));
                            }
                        }
                    }
                    var rendered = new RenderTargetBitmap(1170, 660, 96, 96, PixelFormats.Pbgra32);
                    rendered.Render(view);
                    var png = new PngBitmapEncoder();
                    png.Frames.Add(BitmapFrame.Create(rendered));
                    using (var stream = File.Create(Path.Combine(output, "Ilustracoes-" + (batch + 1) + ".png"))) png.Save(stream);
                }
            }

            private static void CheckPreferences(DateTime first)
            {
                var personalized = new Training(GymData.Create(first), first);
                personalized.ChangeProfile(new Profile { Goal = "recomp", Focus = "glutes-legs", FocusMode = "priority", Level = 0 });
                var tuesday = personalized.Get(first.AddDays(1));
                var friday = personalized.Get(first.AddDays(4));
                Require(friday.Items.Any(m => Catalog.ById[m.Id].Group == "hamstrings") && personalized.Get(first).Items.Any(m => Catalog.ById[m.Id].Type == 'A'),
                    "Recomposição combina força e cardio");
                var swapSession = new Session { Title = "Pernas" };
                var press = personalized.Prescribe(Catalog.ById["leg-press"], personalized.At(first), false);
                swapSession.Items.Add(press);
                Require(personalized.Alternatives(first.AddDays(1), swapSession, press).First().Id == "leg-press-horizontal" && tuesday.Scheduled,
                    "Leg press horizontal é oferecido como troca direta do inclinado");
                Require(personalized.NextGroup(first, "chest") == first.AddDays(7), "Peito volta na mesma divisão na semana seguinte");

                var baseline = new Training(GymData.Create(first), first);
                baseline.ChangeProfile(new Profile());
                var original = baseline.Get(first).Items;
                var weekThree = baseline.Get(first.AddDays(14)).Items;
                Require(original.Count == weekThree.Count && original.Zip(weekThree, (a, b) => a.Id != b.Id).Count(changed => changed) == 1 &&
                    weekThree.Count(m => m.Varied) == 1, "Rotação troca apenas um exercício acessório");
                baseline.LearnSwap("incline-db", "chest-machine", true);
                Require(baseline.Get(first.AddDays(7)).Items[0].Id == "incline-db" && baseline.Data.Swaps.Count == 0, "Troca temporária não altera preferência");
                baseline.LearnSwap("incline-db", "chest-machine", false);
                Require(baseline.Get(first.AddDays(7)).Items[0].Id == "chest-machine" && baseline.ReducedSuggestions().Contains("incline-db"),
                    "Troca aprendida favorece escolha e reduz sugestão rejeitada");
                baseline.Avoid("leg-press-horizontal");
                Require(!baseline.Eligible(Catalog.ById["leg-press-horizontal"], baseline.At(first)), "Bloqueio explícito de exercício");
            }

            private static void CheckWeeklySplit(DateTime monday)
            {
                var training = new Training(GymData.Create(monday), monday);
                training.ChangeProfile(new Profile { Goal = "recomp" });
                var friday = monday.AddDays(4);
                var sessions = new[] { 0, 1, 3, 4 }.Select(offset => training.Get(monday.AddDays(offset))).ToArray();
                Require(sessions.Select(entry => entry.Title).SequenceEqual(new[] {
                    "Peito e tríceps", "Pernas completas", "Costas e bíceps", "Ombros e abdômen"
                }), "Divisão padrão de quatro dias");
                var push = new[] { "chest", "triceps", "cardio" };
                Require(sessions[0].Items.All(item => push.Contains(Catalog.ById[item.Id].Group)),
                    "Dia de peito preserva apenas os grupos diretos daquela sessão");
                var logged = training.Save(monday);
                logged.Items[0].Done = true;
                var themes = new[] { "chest-biceps", "legs", "", "back-triceps", "shoulders", "", "" };
                training.ChangeProfile(new Profile { Goal = "recomp", Split = "groups", DayThemes = themes });
                Require(training.Get(monday).Items[0].Done && training.Get(monday.AddDays(7)).Title == "Peito e bíceps" &&
                    training.Get(monday.AddDays(3)).Title == "Costas e tríceps" && training.Get(friday).Title == "Ombros e abdômen",
                    "Escolha por dia muda a previsão e preserva a sessão registrada");
                training.Get(monday.AddDays(7)).Items.ForEach(item =>
                    Require(new[] { "chest", "biceps", "cardio" }.Contains(Catalog.ById[item.Id].Group), "Ajustes semanais respeitam o tema escolhido"));
                training.ChangeProfile(new Profile { Split = "full" });
                Require(training.Get(friday).Title == "Corpo inteiro" && training.Get(friday).Items.Any(item => Catalog.ById[item.Id].Group == "back") &&
                    training.Get(friday).Items.Any(item => Catalog.ById[item.Id].Group == "glutes"), "Corpo inteiro continua disponível");

                var legacy = GymData.Create(monday.AddDays(-14));
                legacy.Configured = true;
                legacy.Profiles[0].Profile.Split = null;
                var migrated = new Training(legacy, monday);
                Require(migrated.Get(monday.AddDays(-7)).Items.Any(item => Catalog.ById[item.Id].Group == "back") &&
                    migrated.Get(monday).Items.All(item => push.Contains(Catalog.ById[item.Id].Group)), "A atualização preserva o plano histórico de perfis antigos");
                migrated.Today = monday.AddDays(1);
                Require(migrated.Get(monday).Items.All(item => push.Contains(Catalog.ById[item.Id].Group)),
                    "Passar a data não reverte o plano atualizado");

                var adjacent = new Training(GymData.Create(monday), monday);
                adjacent.ChangeProfile(new Profile { Days = new[] { 0, 1 } });
                Require(adjacent.Get(monday).Title == "Superiores completos" && adjacent.Get(monday.AddDays(1)).Title == "Pernas completas",
                    "Dois dias consecutivos alternam superiores e pernas");
            }

            private static void CheckRecordingDates(DateTime monday)
            {
                var wednesday = monday.AddDays(2);
                var thursday = wednesday.AddDays(1);
                var training = new Training(GymData.Create(wednesday), wednesday);
                training.ChangeProfile(new Profile { Days = new[] { 0, 1, 2, 3 } });
                Require(!training.Get(monday).Scheduled && !training.Get(monday.AddDays(1)).Scheduled, "Dias anteriores ao acesso ficam fora da agenda");
                RequireRejected(() => training.Attendance(monday, "present"), "Bloqueia presença anterior à instalação");
                RequireRejected(() => training.Attendance(new DateTime(2026, 12, 4), "present"), "Bloqueia presença futura inclusive em outro mês");
                RequireRejected(() => training.EnableExtra(thursday), "Bloqueia criação antecipada de treino extra");
                RequireRejected(() => training.Save(thursday), "Bloqueia alterações no treino futuro");
                RequireRejected(() => training.Finish(thursday), "Bloqueia finalização futura");
                RequireRejected(() => training.RecordPerformance(thursday, 0, new List<LiftSet>(), 0, null, true), "Bloqueia desempenho futuro");
                training.Attendance(wednesday, "present");
                training.Today = thursday.AddDays(1);
                RequireRejected(() => training.Attendance(thursday, "present"), "Bloqueia novo registro em dia anterior sem presença");
                RequireRejected(() => training.Save(thursday), "Bloqueia preenchimento histórico de exercício");
                var week = training.Report(monday);
                var month = training.ReportMonth(monday);
                Require(week.Due == 2 && week.Attended == 1 && week.Unmarked == 1 && month.Due == week.Due && month.Attended == week.Attended,
                    "Relatórios mensal e semanal contam apenas a partir da quarta-feira");
            }

            private static void CheckComplement(DateTime monday)
            {
                var training = new Training(GymData.Create(monday), monday);
                training.ChangeProfile(new Profile { Goal = "strength", Level = 1 });
                var tuesday = monday.AddDays(1);
                training.Today = tuesday;
                training.Attendance(tuesday, "missed");
                training.Today = monday.AddDays(4);
                training.Attendance(training.Today, "present");
                training.Today = monday.AddDays(6);
                training.Data.Sessions[Training.Key(training.Today)] = new Session {
                    Title = "Extra antigo", Extra = true, Items = new List<Movement> { training.Prescribe(Catalog.ById["hip-thrust"], training.At(tuesday), false) }
                };
                Require(!training.Get(training.Today).Extra && training.Get(training.Today).Items.Count == 0, "Dia de descanso não abre extras antigos sem atividade");
                var session = training.EnableExtra(training.Today);
                Require(session.Complement && session.ComplementFor == Training.Key(tuesday) && session.ComplementGroup == "quads" &&
                    session.Items.First().Id == "chair-squat" && session.Items.First().Sets == 1 && session.Items.First().Target.Contains("leves"),
                    "Extra de domingo prioriza pernas após a falta, com uma série leve antes do próximo treino");
                Require(training.NextGroup(training.Today, "quads") == monday.AddDays(8) && training.Get(tuesday).Attendance == "missed" &&
                    !Training.Present(session) && !training.Makeup(tuesday).HasValue, "Extra não registra presença ou repõe falta automaticamente");
                Require(session.Items.Where(m => Catalog.ById[m.Id].Type != 'A').All(m => training.FitsSession(training.Today, session, Catalog.ById[m.Id])) &&
                    !training.FitsSession(training.Today, session, Catalog.ById["incline-db"]), "Complementar protege o peito previsto para segunda");
                training.CancelComplement(training.Today);
                Require(training.Get(training.Today).Title == "Descanso", "Cancelamento de extra sem registro volta ao descanso");
            }

            private static void CheckWorkoutFlow(string output, string directory, DateTime date)
            {
                using (var storage = new Storage(directory))
                {
                    var data = GymData.Create(date);
                    new Training(data, date).ChangeProfile(new Profile { Name = "Sam" });
                    storage.Save(data);
                    var gym = new GymApp(storage, date, false);
                    gym.window.Show();
                    Flush(gym.window);
                    Require(gym.section == Section.Workouts && gym.selectedDate == date &&
                        gym.Get<TextBlock>("PageSubtitle").Text == WorkoutDateLabel(date) && gym.headerActions.Children.Count == 0 &&
                        ReferenceEquals(gym.Get<StackPanel>("Navigation").Children[0], gym.nav[Section.Workouts]), "Treinos é a primeira aba e abre a lista do dia");
                    Require(Descendants(gym.content).OfType<TextBlock>().Any(t => t.Text.Contains("Entre séries:") && t.Text.Contains("Antes do próximo exercício:")),
                        "Lista de hoje explica as pausas");
                    Capture(gym.window, Path.Combine(output, "Treino-de-hoje.png"));
                    gym.scroll.ScrollToVerticalOffset(96);
                    Flush(gym.window);
                    var listOffset = gym.scroll.VerticalOffset;
                    var view = Descendants(gym.content).OfType<Button>().First(b => (b.Content as string) == "Ver exercício");
                    Invoke(view);
                    Require(gym.openGuide != null && Window.GetWindow(gym.content) == gym.window && gym.window.OwnedWindows.Cast<Window>().All(w => !w.IsVisible),
                        "Guia abre na mesma janela, sem caixa modal");
                    Capture(gym.window, Path.Combine(output, "Guia-supino.png"));
                    Invoke(FindButton(gym.Get<WrapPanel>("PageNavigation"), "‹  Voltar"));
                    Flush(gym.window);
                    Require(gym.openGuide == null && gym.section == Section.Workouts && listOffset > 0 && gym.scroll.VerticalOffset == 0, "Voltar leva ao início da aba Treinos");

                    ApplyCheck(Descendants(gym.content).OfType<CheckBox>().First());
                    Require(Training.Present(gym.training.Get(date)) && gym.training.Report(date).Attended == 1, "Primeiro exercício já registra presença");
                    Invoke(FindButton(gym.headerActions, "Finalizar treino"));
                    var finished = gym.training.Get(date);
                    Require(finished.Finalized && gym.training.Report(date).Pending[date].Count == finished.Items.Count - 1 &&
                        gym.training.Report(date).Done == 1 && Descendants(gym.content).OfType<TextBlock>().Any(t => t.Text == "Treino concluído"),
                        "Finalização manual mantém exercícios pendentes fora da cobertura");
                    Capture(gym.window, Path.Combine(output, "Treino-concluido.png"));
                    gym.window.Close();

                    gym = new GymApp(storage, date, false);
                    gym.window.Show();
                    Flush(gym.window);
                    Require(Descendants(gym.content).OfType<TextBlock>().Any(t => t.Text == "Treino concluído"), "Estado concluído persiste ao reabrir");
                    gym.Go(Section.Reports);
                    Flush(gym.window);
                    gym.scroll.ScrollToEnd();
                    Flush(gym.window);
                    Require(Descendants(gym.content).OfType<TextBlock>().Any(t => t.Text == "Exercícios que ficaram pendentes") &&
                        Descendants(gym.content).OfType<TextBlock>().Any(t => t.Text.StartsWith("Crucifixo na polia")), "Relatório lista exercícios pulados");
                    gym.Go(Section.Workouts);
                    Flush(gym.window);
                    Invoke(FindButton(gym.content, "Rever exercícios"));
                    Invoke(FindButton(gym.content, "Reabrir treino"));
                    for (int i = 1; i < gym.training.Get(date).Items.Count; i++)
                        ApplyCheck(Descendants(gym.content).OfType<CheckBox>().ElementAt(i));
                    Require(Training.Complete(gym.training.Get(date)) && !gym.training.Get(date).Finalized &&
                        Descendants(gym.content).OfType<TextBlock>().Any(t => t.Text == "Treino concluído"), "Conclusão automática ao marcar o último exercício");
                    gym.RefreshDate(date.AddDays(1));
                    Flush(gym.window);
                    Require(gym.selectedDate == date.AddDays(1) && Descendants(gym.content).OfType<CheckBox>().Any() &&
                        !Descendants(gym.content).OfType<TextBlock>().Any(t => t.Text == "Treino concluído"), "Virada do dia mostra a nova lista");
                    gym.SelectDay(date.AddDays(3), Section.Workouts);
                    Flush(gym.window);
                    Require(!Descendants(gym.content).OfType<Button>().Any() && !Descendants(gym.content).OfType<CheckBox>().Any(), "Prévia futura bloqueia todos os controles de treino");
                    Capture(gym.window, Path.Combine(output, "Treino-previsto.png"));
                    gym.Go(Section.Workouts);
                    gym.RefreshDate(date.AddDays(2));
                    Flush(gym.window);
                    Require(gym.training.Get(date.AddDays(2)).Title == "Descanso" && !Descendants(gym.content).OfType<CheckBox>().Any(), "Quarta abre em descanso sem lista");
                    Capture(gym.window, Path.Combine(output, "Descanso.png"));
                    Invoke(FindButton(gym.content, "Montar treino extra"));
                    Require(gym.training.Get(date.AddDays(2)).Complement, "Extra simples depende de ação do usuário");
                    Capture(gym.window, Path.Combine(output, "Treino-extra.png"));
                    gym.window.Close();
                }
            }

            private static void CheckCardioAndEquipment(DateTime date)
            {
                var mixed = new Training(GymData.Create(date), date);
                var machinesAndWeights = new Profile { Goal = "recomp", Gear = "machines-dumbbell", Cardio = "elliptical" };
                mixed.ChangeProfile(machinesAndWeights);
                var monday = mixed.Get(date);
                Require(monday.Items.Last().Id == "elliptical" && monday.Items.Last().CardioPhase == "after" &&
                    monday.Items.Last().Target == "8–12 min" && monday.Items.Where(m => Catalog.ById[m.Id].Type != 'A').All(m =>
                        new[] { "machine", "cable", "dumbbell" }.Contains(Catalog.ById[m.Id].Gear)), "Perfil misto usa equipamento selecionado e transport no final");
                Require(!mixed.Eligible(Catalog.ById["bridge"], machinesAndWeights) && !mixed.Eligible(Catalog.ById["chair-squat"], machinesAndWeights) &&
                    !mixed.Eligible(Catalog.ById["bike"], machinesAndWeights), "Preferências filtram força sem aparelho e cardio incompatível");

                mixed.ChangeProfile(new Profile { Goal = "endurance", Gear = "machines", Cardio = "treadmill", Minutes = 25 });
                var cardioFirst = mixed.Get(date);
                Require(cardioFirst.Items.First().Id == "treadmill" && cardioFirst.Items.First().CardioPhase == "before" &&
                    cardioFirst.Items.First().Target == "10–15 min" && cardioFirst.Items.Skip(1).All(m => new[] { "machine", "cable" }.Contains(Catalog.ById[m.Id].Gear)),
                    "Fôlego coloca esteira antes da musculação e limita duração na sessão curta");
                mixed.ChangeProfile(new Profile { Goal = "weight", Gear = "machines-dumbbell", Cardio = "gym" });
                var legs = mixed.Get(date.AddDays(1));
                Require(legs.Items.Last().Id == "bike" && legs.Items.Last().Target == "8–12 min", "Dia de pernas tem pedalada mais curta se disponível");
                mixed.ChangeProfile(new Profile { Goal = "fitness", Gear = "dumbbell-only", Cardio = "floor", Days = new[] { 0 } });
                var floor = mixed.Get(date).Items;
                Require(floor.Any(m => Catalog.ById[m.Id].Type == 'A' && Catalog.ById[m.Id].Gear == "body") &&
                    floor.Where(m => Catalog.ById[m.Id].Type != 'A').All(m => Catalog.ById[m.Id].Gear == "dumbbell"), "Uma sessão semanal pode ter halteres e cardio sem aparelho");
                mixed.ChangeProfile(new Profile { Goal = "strength", Gear = "machines", Cardio = "auto" });
                Require(!mixed.Get(date).Items.Any(m => Catalog.ById[m.Id].Type == 'A'), "O foco em força não força cardio automático");
                mixed.ChangeProfile(new Profile { Goal = "weight", Gear = "machines", Cardio = "none" });
                Require(!mixed.Get(date).Items.Any(m => Catalog.ById[m.Id].Type == 'A'), "Modo Sem cardio automático prevalece em qualquer objetivo");
            }

            private static void ApplyCheck(CheckBox checkbox)
            {
                Require(checkbox.IsEnabled, "Marcação disponível para hoje");
                typeof(CheckBox).GetMethod("OnClick", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(checkbox, null);
                Flush(checkbox);
            }

            private static void RequireRejected(Action action, string label)
            {
                try { action(); }
                catch (InvalidOperationException) { return; }
                throw new InvalidOperationException("Falhou: " + label);
            }

            private static void CheckFirstUse(string output, string folder, DateTime date)
            {
                using (var storage = new Storage(folder))
                {
                    var gym = new GymApp(storage, date, false);
                    gym.window.Show();
                    Flush(gym.window);
                    gym.dateTimer.Stop();
                    Require(gym.training.NeedsSetup && gym.section == Section.Profile && !gym.nav[Section.Workouts].IsEnabled &&
                        !gym.training.Get(date).Scheduled && gym.Data.Sessions.Count == 0, "Primeira abertura exige preenchimento antes da agenda");
                    Capture(gym.window, Path.Combine(output, "Primeiro-acesso.png"));
                    FieldByName<TextBox>(gym.content, "Como você quer ser chamado? (opcional)").Text = "sam";
                    FieldByName<TextBox>(gym.content, "Peso atual (kg)").Text = "79,5";
                    FieldByName<TextBox>(gym.content, "Altura (cm)").Text = "178";
                    var goals = FieldByName<ComboBox>(gym.content, "Objetivo");
                    goals.SelectedItem = goals.Items.Cast<Option>().First(o => o.Key == "recomp");
                    var days = Descendants(gym.content).OfType<CheckBox>().Take(7).ToArray();
                    foreach (int index in new[] { 0, 1, 3, 4 }) days[index].IsChecked = true;
                    Invoke(FindButton(gym.content, "Montar meu plano"));
                    Require(!gym.training.NeedsSetup && gym.section == Section.Workouts && gym.training.At(date).Name == "Sam" &&
                        gym.training.At(date).Weight == 79.5m && gym.training.At(date).Goal == "recomp", "Salvamento do perfil personalizado pela interface");
                    Require(Descendants(gym.content).OfType<TextBlock>().Any(t => t.Text == "Treino de Sam"), "Nome usado no treino");
                    gym.window.Close();
                }
            }

            private static T FieldByName<T>(DependencyObject root, string label) where T : Control
            {
                return Descendants(root).OfType<T>().First(input => (AutomationProperties.GetLabeledBy(input) as TextBlock) != null &&
                    ((TextBlock)AutomationProperties.GetLabeledBy(input)).Text == label);
            }

            private static Button FindButton(DependencyObject root, string title)
            {
                return Descendants(root).OfType<Button>().First(button => (button.Content as string) == title);
            }

            private static void InspectDialog(GymApp gym, Action open, string filename, Action<Window> inspect = null)
            {
                bool opened = false;
                Exception error = null;
                var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(70) };
                timer.Tick += delegate {
                    var dialog = gym.window.OwnedWindows.Cast<Window>().FirstOrDefault(w => w.IsVisible);
                    if (dialog == null) return;
                    timer.Stop();
                    opened = true;
                    try { if (inspect != null) inspect(dialog); Capture(dialog, filename); }
                    catch (Exception ex) { error = ex; }
                    finally { dialog.Close(); }
                };
                timer.Start();
                try { open(); }
                finally { timer.Stop(); }
                if (error != null) throw new InvalidOperationException("Falhou: " + Path.GetFileName(filename), error);
                Require(opened, "Janela aberta: " + Path.GetFileName(filename));
            }

            private static void Invoke(Button button)
            {
                var peer = new ButtonAutomationPeer(button);
                ((IInvokeProvider)peer).Invoke();
                Flush(button);
            }

            private static IEnumerable<DependencyObject> Descendants(DependencyObject node)
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
                {
                    var child = VisualTreeHelper.GetChild(node, i);
                    yield return child;
                    foreach (var nested in Descendants(child)) yield return nested;
                }
            }

            private static void Require(bool condition, string description)
            {
                if (!condition) throw new InvalidOperationException("Falhou: " + description);
            }

            private static void Flush(FrameworkElement target)
            {
                target.Dispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(() => target.UpdateLayout()));
            }

            private static void Capture(Window window, string file)
            {
                Flush(window);
                var surface = (UIElement)window.Content;
                var bounds = surface.RenderSize;
                var image = new RenderTargetBitmap((int)Math.Ceiling(bounds.Width), (int)Math.Ceiling(bounds.Height), 96, 96, PixelFormats.Pbgra32);
                image.Render(surface);
                var png = new PngBitmapEncoder();
                png.Frames.Add(BitmapFrame.Create(image));
                using (var stream = File.Create(file)) png.Save(stream);
            }
        }
    }
}
