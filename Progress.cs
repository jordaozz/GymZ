using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace GymZ
{
    internal sealed partial class GymApp
    {
        private static string Kg(decimal value) { return value.ToString("0.##", Training.Pt) + " kg"; }

        private static string LoadConvention(Exercise exercise)
        {
            switch (exercise.Gear)
            {
                case "dumbbell": return "Anote o peso de cada halter, mesmo quando usa dois.";
                case "barbell": return "Anote o peso total, incluindo a barra e as anilhas dos dois lados.";
                case "machine":
                case "cable": return "Use o valor indicado no aparelho. Ao mudar de máquina, confira a unidade e os ajustes antes de comparar cargas.";
                case "band": return "Se o elástico só indica cor ou resistência, registre as repetições e deixe a carga em branco.";
                default: return "Registre a carga extra se houver. Para uma série só com seu peso corporal, deixe o campo vazio ou informe 0 kg.";
            }
        }

        private sealed class SetFields
        {
            public TextBox Load, Reps;
            public CheckBox Done;

            public LiftSet Read(int series)
            {
                decimal? kg = string.IsNullOrWhiteSpace(Load.Text) ? (decimal?)null : Number(Load, "a carga da série " + series, 0, 1500);
                int? count = string.IsNullOrWhiteSpace(Reps.Text) ? (int?)null : (int)Number(Reps, "o valor da série " + series, 1, 200, true);
                return kg.HasValue || count.HasValue || Done.IsChecked == true ? new LiftSet { RepsOrSeconds = count, Kilograms = kg } : null;
            }
        }

        private void LogDialog(DateTime date, int index)
        {
            training.RequireRecordingDay(date);
            var session = training.Get(date);
            var item = session.Items[index];
            var exercise = Catalog.ById[item.Id];
            StackPanel panel;
            var dialog = Dialog(exercise.Type == 'A' ? "Registrar tempo" : "Registrar carga e séries", out panel);
            var top = new WrapPanel();
            AddChoice(top, Button("‹  Voltar", dialog.Close));
            panel.Children.Add(top);
            Add(panel, Text(exercise.Name, 23, null, true), 5);
            Add(panel, Text(Range(date), 13, "Muted"), 4);
            var previous = training.PreviousLoad(date, item.Id);
            var series = new List<SetFields>();
            var minutes = new TextBox { Text = OptionalNumber(item.ActualReps) };
            var reserve = new TextBox { Text = item.RepsInReserve.HasValue ? item.RepsInReserve.Value.ToString() : "" };
            var done = new CheckBox { Content = "Marcar exercício como concluído", IsChecked = true };
            if (exercise.Type == 'A') Add(panel, Field("Minutos praticados", minutes), 13);
            else
            {
                Add(panel, Text(LoadConvention(exercise), 13, "Muted"), 7);
                if (session.Complement) Add(panel, Text("Esta é uma sessão leve. Ajuste a carga para terminar com pelo menos 4 repetições em reserva.", 13, "Accent"), 5);
                Add(panel, Text(item.Sets + " séries planejadas · Preencha os campos ou marque as séries feitas.", 13, "Muted"), 5);
                if (item.Efforts == null && item.ActualSets == 0 && (item.Load > 0 || item.ActualReps > 0))
                    Add(panel, Text("O registro anterior não informava quantas séries foram feitas. Confira os campos antes de salvar.", 12, "Warn"), 5);

                var rows = new StackPanel();
                var buttons = new WrapPanel();
                Button extra = null;
                Action<LiftSet> addRow = value => {
                    int number = series.Count + 1;
                    var row = new SetFields {
                        Load = new TextBox { Text = value == null || !value.Kilograms.HasValue ? "" : value.Kilograms.Value.ToString("0.##", Training.Pt) },
                        Reps = new TextBox { Text = value == null || !value.RepsOrSeconds.HasValue ? "" : value.RepsOrSeconds.Value.ToString() },
                        Done = new CheckBox { Content = "Série " + number, IsChecked = value != null }
                    };
                    series.Add(row);
                    var inputs = Columns(Field(exercise.Type == 'T' ? "Segundos" : "Repetições", row.Reps), Field("Carga (kg)", row.Load), 1, 1, 8);
                    AutomationProperties.SetName(row.Reps, "Série " + number + (exercise.Type == 'T' ? ", segundos" : ", repetições"));
                    AutomationProperties.SetName(row.Load, "Série " + number + ", carga em kg");
                    Add(rows, Columns(row.Done, inputs, .8, 2.2, 6), 9);
                    if (extra != null) extra.IsEnabled = series.Count < 30;
                };
                int existing = item.Efforts != null ? item.Efforts.Count : item.ActualSets;
                if (existing == 0 && item.Efforts == null && (item.ActualReps > 0 || item.Load > 0)) existing = 1;
                int count = Math.Max(existing, item.Sets);
                for (int i = 0; i < count; i++)
                {
                    LiftSet value = null;
                    if (item.Efforts != null && i < item.Efforts.Count) value = item.Efforts[i];
                    else if (item.Efforts == null && i < existing)
                        value = new LiftSet { Kilograms = item.Load > 0 ? (decimal?)item.Load : null, RepsOrSeconds = item.ActualReps > 0 ? (int?)item.ActualReps : null };
                    addRow(value);
                }
                if (previous != null && !session.Complement)
                {
                    Add(panel, Text("Última carga: " + Kg(previous.Peak.Value) + " em " + Range(previous.Date), 13, "Accent", true), 9);
                    AddChoice(buttons, Button("Copiar pesos anteriores", () => {
                        var prior = previous.Movement;
                        var loads = prior.Efforts == null ? Enumerable.Repeat((decimal?)prior.Load, Math.Max(1, prior.ActualSets)).ToArray() :
                            prior.Efforts.Select(set => set.Kilograms).ToArray();
                        for (int i = 0; i < Math.Min(series.Count, loads.Length); i++)
                            if (loads[i].HasValue && string.IsNullOrWhiteSpace(series[i].Load.Text))
                                series[i].Load.Text = loads[i].Value.ToString("0.##", Training.Pt);
                    }, "Selected"));
                }
                extra = Button("Adicionar série", () => { addRow(null); series.Last().Reps.Focus(); });
                extra.IsEnabled = series.Count < 30;
                AddChoice(buttons, extra);
                Add(panel, rows, 4);
                Add(panel, buttons, 12);
                if (exercise.Type != 'T')
                {
                    Add(panel, Field("Repetições que ainda conseguiria fazer na última série (opcional)", reserve), 9);
                    Add(panel, Text("Use 0 se não sobrava nenhuma repetição com boa técnica. Use 10 se sobravam 10 ou mais.", 12, "Muted"), 6);
                }
            }
            Add(panel, done, 12);
            var save = Button("Salvar registro", () => {
                int duration = exercise.Type == 'A' ? (int)Number(minutes, "os minutos", 0, 200, true) : 0;
                var efforts = exercise.Type == 'A' ? null : series.Select((row, i) => row.Read(i + 1)).Where(s => s != null).ToList();
                int? effort = string.IsNullOrWhiteSpace(reserve.Text) ? (int?)null : (int)Number(reserve, "as repetições em reserva", 0, 10, true);
                Commit(() => training.RecordPerformance(date, index, efforts, duration, effort, done.IsChecked == true), "Desempenho salvo.");
                dialog.Close();
            }, "Primary");
            save.IsDefault = true;
            Add(panel, save, 13);
            dialog.ShowDialog();
        }

        private void HistoryDialog(Exercise exercise, DateTime through)
        {
            StackPanel panel;
            var dialog = Dialog("Evolução · " + exercise.Name, out panel, 665);
            panel.Children.Add(Button("‹  Voltar", dialog.Close));
            FillHistory(panel, exercise, through);
            dialog.ShowDialog();
        }

        private void FillHistory(Panel panel, Exercise exercise, DateTime through)
        {
            Add(panel, Text(exercise.Name, 23, null, true), 9);
            var records = training.LiftHistory(exercise.Id, through);
            if (records.Count == 0)
            {
                Add(panel, Text("Seus registros aparecem aqui depois que você concluir o exercício com carga, repetições ou tempo informados.", 14, "Muted"), 11);
                return;
            }
            var weighted = records.Where(row => row.Peak.HasValue).ToList();
            if (weighted.Count > 0)
            {
                var current = weighted[0];
                var latest = new StackPanel();
                latest.Children.Add(Text("Última carga", 12, "Muted", true));
                Add(latest, Text(Kg(current.Peak.Value), 23, "Accent", true), 3);
                Add(latest, Text(Range(current.Date), 12, "Muted"), 3);
                var highest = new StackPanel();
                highest.Children.Add(Text("Maior carga registrada", 12, "Muted", true));
                Add(highest, Text(Kg(weighted.Max(row => row.Peak.Value)), 23, "Good", true), 3);
                Add(highest, Text("Pico por treino", 12, "Muted"), 3);
                Add(panel, Columns(Card(latest, 11), Card(highest, 11), 1, 1, 7), 13);
                if (weighted.Count > 1) Add(panel, Card(LoadChart(weighted.Take(8).Reverse().ToList()), 12), 10);
                Add(panel, Text(LoadConvention(exercise), 12, "Muted"), 8);
            }
            Add(panel, Text("Histórico por data", 17, null, true), 14);
            foreach (var record in records)
            {
                var entry = new StackPanel();
                entry.Children.Add(Text(Range(record.Date), 13, "Accent", true));
                if (record.Movement.Efforts != null)
                {
                    for (int i = 0; i < record.Movement.Efforts.Count; i++)
                    {
                        string detail = SetLabel(record.Movement.Efforts[i], exercise);
                        Add(entry, Text("Série " + (i + 1) + "  ·  " + detail, 13), 4);
                    }
                }
                else Add(entry, Text(LiftLabel(record.Movement, exercise), 13), 5);
                if (record.Movement.RepsInReserve.HasValue)
                    Add(entry, Text(record.Movement.RepsInReserve.Value + " rep. em reserva na última série", 12, "Muted"), 5);
                Add(panel, Card(entry, 10, "Input"), 6);
            }
        }

        private static string SetLabel(LiftSet set, Exercise exercise)
        {
            var parts = new List<string>();
            if (set.RepsOrSeconds.HasValue) parts.Add(set.RepsOrSeconds.Value + (exercise.Type == 'T' ? " s" : " rep."));
            if (set.Kilograms.HasValue) parts.Add(Kg(set.Kilograms.Value));
            return parts.Count == 0 ? "Concluída" : string.Join("  ·  ", parts);
        }

        private static Panel LoadChart(IList<LiftRecord> points)
        {
            var chart = new StackPanel();
            chart.Children.Add(Text("Evolução da maior carga por treino", 14, null, true));
            var grid = new UniformGrid { Columns = points.Count };
            decimal max = Math.Max(points.Max(p => p.Peak.Value), 1);
            foreach (var point in points)
            {
                var column = new StackPanel { Margin = new Thickness(4, 0, 4, 0) };
                string value = point.Peak.Value.ToString("0.##", Training.Pt);
                column.Children.Add(Center(Text(value, 11, "Muted", true)));
                var background = new Grid { Height = 66 };
                var bar = new Border { CornerRadius = new CornerRadius(4, 4, 0, 0), MaxWidth = 36, Height = Math.Max(2, 66 * (double)(point.Peak.Value / max)), VerticalAlignment = VerticalAlignment.Bottom };
                bar.SetResourceReference(Border.BackgroundProperty, "Accent");
                background.Children.Add(bar);
                Add(column, background, 4);
                Add(column, Center(Text(point.Date.ToString("dd/MM"), 11, "Muted")), 5);
                column.ToolTip = Range(point.Date) + " · " + Kg(point.Peak.Value);
                AutomationProperties.SetName(column, column.ToolTip.ToString());
                grid.Children.Add(column);
            }
            Add(chart, grid, 12);
            return chart;
        }
    }
}
