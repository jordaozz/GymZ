using System;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;

namespace GymZ
{
    internal sealed partial class GymApp
    {
        private static string CoverageLabel(int status) { return status == 2 ? "Meta atingida" : status == 1 ? "Parcial" : "Sem trabalho registrado"; }
        private static string CoverageFill(int status) { return status == 2 ? "CoverageGood" : status == 1 ? "CoveragePartial" : "CoverageNone"; }
        private static string CoverageInk(int status) { return status == 2 ? "Good" : status == 1 ? "Warn" : "Bad"; }

        private Border CoverageReport(WeekReport report)
        {
            var root = new StackPanel();
            root.Children.Add(Text("Cobertura muscular da semana", 19, null, true));
            Add(root, Text("Clique em uma região ou no nome do músculo para ver as séries e os exercícios concluídos.", 13, "Muted"), 5);
            bool future = week > training.Today;
            var legend = new WrapPanel();
            foreach (int status in new[] { 2, 1, 0 })
            {
                var key = Row();
                var dot = new Border { Width = 12, Height = 12, CornerRadius = new CornerRadius(4), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) };
                dot.SetResourceReference(Border.BackgroundProperty, CoverageFill(status));
                key.Children.Add(dot);
                key.Children.Add(Text(CoverageLabel(status), 12, future ? "Muted" : CoverageInk(status), true));
                AddChoice(legend, key);
            }
            Add(root, legend, 10);
            var front = new StackPanel();
            front.Children.Add(Center(Text("Frente", 12, "Muted", true)));
            Add(front, CreateBodyMap(false, 245, future ? null : report.Coverage, key => MuscleDetail(key, report)), 5);
            var back = new StackPanel();
            back.Children.Add(Center(Text("Costas", 12, "Muted", true)));
            Add(back, CreateBodyMap(true, 245, future ? null : report.Coverage, key => MuscleDetail(key, report)), 5);
            var bodies = Columns(front, back, 1, 1, 3);

            var chart = new StackPanel();
            chart.Children.Add(Text("Séries diretas por região", 15, null, true));
            int max = report.Coverage.Values.Max(g => g.DirectSets);
            foreach (var item in report.Coverage.OrderByDescending(g => g.Value.DirectSets).ThenByDescending(g => g.Value.SupportSets))
            {
                string key = item.Key;
                var score = item.Value;
                var line = new DockPanel();
                var count = Text(score.DirectSets.ToString(), 12, future ? "Muted" : CoverageInk(score.Status), true);
                count.Margin = new Thickness(6, 0, 0, 0);
                DockPanel.SetDock(count, Dock.Right);
                line.Children.Add(count);
                line.Children.Add(Text(Catalog.Names[key], 12));
                var row = new StackPanel();
                row.Children.Add(line);
                Add(row, Bar(score.DirectSets, max, CoverageFill(score.Status)), 3);
                var details = Button(Catalog.Names[key], () => MuscleDetail(key, report), "Nav");
                details.Content = row;
                details.HorizontalContentAlignment = HorizontalAlignment.Stretch;
                details.MinHeight = 28;
                details.Padding = new Thickness(3, 2, 3, 2);
                AutomationProperties.SetName(details, Catalog.Names[key] + " · " + score.DirectSets + " séries diretas · " + CoverageLabel(score.Status));
                chart.Children.Add(details);
            }
            Add(root, Columns(Top(Card(bodies, 10, "Input")), chart, 1.07, 1, 13), 8);
            Add(root, Text(future ? "Semana futura. O mapa ganha as cores conforme você registra os treinos." :
                "Verde marca pelo menos 75% das séries diretas previstas até a data, com mínimo de 2. Amarelo indica parte da meta ou trabalho auxiliar. " +
                "Ao concluir um exercício sem informar séries, o relatório usa as séries previstas para ele.", 12, "Muted"), 9);
            return Card(root, 15);
        }

        private void MuscleDetail(string group, WeekReport report)
        {
            StackPanel panel;
            var dialog = Dialog("Cobertura · " + Catalog.Names[group], out panel);
            panel.Children.Add(Button("‹  Voltar", dialog.Close));
            Add(panel, Text(Catalog.Names[group], 24, null, true), 8);
            var score = report.Coverage[group];
            Add(panel, Text(CoverageLabel(score.Status), 14, CoverageInk(score.Status), true), 5);
            Add(panel, Text(CoachingText.DirectDose(score) + " · meta de cobertura: " + score.Target + " séries · " + score.SupportSets + " séries como apoio", 13, "Muted"), 6);
            string advice = CoachingText.GroupAdvice(group, score, training.At(week.AddDays(6) > training.Today ? training.Today : week.AddDays(6)));
            if (advice != "") Add(panel, Card(Text(advice, 13), 10, "Input"), 9);
            if (score.DirectSets + score.SupportSets == 0) Add(panel, Text("Nenhum exercício concluído para esta região no período.", 13, "Muted"), 9);
            for (int offset = 0; offset < 7; offset++)
            {
                var date = week.AddDays(offset);
                if (date > training.Today) break;
                foreach (var item in training.Get(date).Items.Where(m => m.Done))
                {
                    var exercise = Catalog.ById[item.Id];
                    if (exercise.Group != group && !exercise.Other.Contains(group)) continue;
                    var record = new StackPanel();
                    record.Children.Add(Text(exercise.Name, 14, null, true));
                    Add(record, Text(Range(date) + " · " + (exercise.Group == group ? "Foco direto" : "Trabalho auxiliar") + " · " +
                        (item.ActualSets > 0 ? item.ActualSets : item.Sets) + " séries", 12, "Muted"), 3);
                    Add(panel, Card(record, 9, "Input"), 7);
                }
            }
            Add(panel, Button("Ver exercícios no mapa muscular", () => {
                dialog.Close();
                muscle = group;
                libraryAll = false;
                backView = !BodyArtwork.MapGroupsFront.Contains(group);
                search = libraryGear = "";
                Go(Section.Muscles);
            }, "Selected"), 12);
            dialog.ShowDialog();
        }
    }
}
