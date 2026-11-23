using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace GymZ
{
    internal sealed partial class GymApp
    {
        private void ShowMuscles()
        {
            var map = new StackPanel();
            map.Children.Add(Text("Explore por região", 19, null, true));
            var sides = new WrapPanel();
            AddChoice(sides, Button("Frente", () => FlipMap(false), !backView ? "Selected" : null));
            AddChoice(sides, Button("Costas", () => FlipMap(true), backView ? "Selected" : null));
            Add(map, sides, 11);
            Add(map, CreateBodyMap(), 6);
            var chips = new WrapPanel();
            foreach (var group in Catalog.Groups)
            {
                string key = group;
                AddChoice(chips, Button(Catalog.Names[key], () => ChooseMuscle(key), muscle == key && !libraryAll ? "Selected" : null));
            }
            Add(map, chips, 14);

            var library = new StackPanel();
            string title = libraryAll ? "Biblioteca completa" : Catalog.Names[muscle];
            library.Children.Add(Text(title, 22, null, true));
            Add(library, Button(libraryAll ? "Filtrar pela região" : "Ver biblioteca completa", () => { libraryAll = !libraryAll; search = ""; Render(false); }, "Selected"), 8);

            var field = new TextBox { Text = search, ToolTip = "Busque por nome ou músculo" };
            var selector = Options(new[] { Choice("", "Todo equipamento") }.Concat(Catalog.GearNames.Select(g => Choice(g.Key, g.Value))), libraryGear);
            Add(library, Columns(Field("Buscar exercício", field), Field("Equipamento", selector), 1.15, 1, 7), 9);
            var list = new StackPanel();
            Add(library, list, 11);
            field.TextChanged += delegate { search = field.Text; FillLibrary(list); };
            selector.SelectionChanged += delegate { libraryGear = Selected(selector); FillLibrary(list); };
            FillLibrary(list);
            content.Children.Add(Columns(Top(Card(map, 14)), Top(Card(library, 15)), .8, 1.2, 12));
        }

        private void FlipMap(bool rear)
        {
            backView = rear;
            if (!libraryAll && !(rear ? BodyArtwork.MapGroupsBack : BodyArtwork.MapGroupsFront).Contains(muscle)) muscle = rear ? "back" : "chest";
            Render(true);
        }

        private void ChooseMuscle(string key)
        {
            muscle = key;
            libraryAll = false;
            if (!(backView ? BodyArtwork.MapGroupsBack : BodyArtwork.MapGroupsFront).Contains(muscle)) backView = !backView;
            search = "";
            Render(true);
        }

        private void FillLibrary(Panel list)
        {
            list.Children.Clear();
            var today = training.Get(training.Today);
            bool canAdd = (today.Scheduled || today.Extra) && !Training.Complete(today) && !training.Makeup(training.Today).HasValue;
            var query = Normalize(search.Trim());
            var exercises = Catalog.All.Where(e => (libraryAll || e.Group == muscle || e.Other.Contains(muscle)) &&
                (libraryGear == "" || e.Gear == libraryGear) && (query == "" || Normalize(e.Name + " " + Catalog.Names[e.Group]).Contains(query)))
                .OrderByDescending(e => libraryAll ? false : e.Group == muscle).ThenBy(e => e.Name, StringComparer.Create(Training.Pt, true)).ToList();
            list.Children.Add(Text(exercises.Count == 1 ? "1 exercício" : exercises.Count + " exercícios", 12, "Muted"));
            if (exercises.Count == 0) Add(list, Text("Nenhum exercício encontrado com esses filtros.", 13, "Muted"), 9);
            foreach (var exercise in exercises)
            {
                var row = new StackPanel();
                row.Children.Add(Text(exercise.Name, 15, null, true));
                string label = Catalog.GearNames[exercise.Gear] + "  ·  " + Catalog.Names[exercise.Group];
                if (!libraryAll && exercise.Group != muscle) label += "  ·  Atua como apoio";
                if (exercise.Level > 0) label += "  ·  Intermediário";
                Add(row, Text(label, 12, "Muted"), 3);
                var buttons = new WrapPanel();
                var view = Button("Ver exercício", () => OpenExerciseGuide(exercise), "Selected");
                AutomationProperties.SetName(view, "Ver exercício · " + exercise.Name);
                AddChoice(buttons, view);
                AddChoice(buttons, Button("Evolução", () => HistoryDialog(exercise, training.Today)));
                var add = Button("Adicionar ao treino", () => ScheduleExerciseDialog(exercise));
                add.IsEnabled = canAdd && !today.Items.Any(m => m.Id == exercise.Id);
                add.ToolTip = "Disponível para a lista de hoje. Em dias de descanso, monte uma sessão extra em Treinos.";
                System.Windows.Controls.ToolTipService.SetShowOnDisabled(add, true);
                AddChoice(buttons, add);
                Add(row, buttons, 7);
                var layout = new DockPanel();
                var art = ExerciseThumbnail(exercise, 61);
                art.Margin = new Thickness(0, 0, 10, 0);
                DockPanel.SetDock(art, Dock.Left);
                layout.Children.Add(art);
                layout.Children.Add(row);
                Add(list, Card(layout, 10, "Input"), 6);
            }
        }

        private Viewbox CreateBodyMap()
        {
            return CreateBodyMap(backView, 330, null, ChooseMuscle);
        }

        private Viewbox CreateBodyMap(bool rear, double maxHeight, Dictionary<string, MuscleWeek> coverage, Action<string> select)
        {
            var canvas = new Canvas { Width = 204, Height = 380, Margin = new Thickness(4, 0, 4, 0) };
            canvas.Children.Add(MapShape(BodyArtwork.Silhouette, "Raised", "Line", 1.4));
            var shapes = rear ? BodyArtwork.Back : BodyArtwork.Front;
            foreach (var item in shapes)
            {
                var key = item.Key;
                foreach (var data in item.Value)
                {
                    var geometry = Geometry.Parse(data);
                    AddRegion(canvas, geometry, key, coverage, select);
                    if (key == "back" || key == "core") continue;
                    var mirrored = geometry.Clone();
                    mirrored.Transform = new MatrixTransform(-1, 0, 0, 1, 204, 0);
                    AddRegion(canvas, mirrored, key, coverage, select);
                }
            }
            var lines = MapShape(rear ? BodyArtwork.BackLines : BodyArtwork.FrontLines, null, "Line", 1.2);
            lines.IsHitTestVisible = false;
            canvas.Children.Add(lines);
            return new Viewbox { MaxHeight = maxHeight, Child = canvas, Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Center };
        }

        private void AddRegion(Canvas canvas, Geometry geometry, string group, Dictionary<string, MuscleWeek> coverage, Action<string> select)
        {
            var bounds = geometry.Bounds;
            var map = new GeometryGroup { Transform = new TranslateTransform(-bounds.Left, -bounds.Top), Children = { geometry } };
            var shape = new Path { Data = map, StrokeThickness = 1.1, Stretch = Stretch.None, StrokeLineJoin = PenLineJoin.Round };
            bool selected = coverage == null && group == muscle && !libraryAll;
            string fill = selected ? "Action" : "AccentSoft";
            string line = selected ? "Accent" : "AccentLine";
            string label = Catalog.Names[group];
            if (coverage != null)
            {
                var score = coverage[group];
                fill = CoverageFill(score.Status);
                line = CoverageInk(score.Status);
                label += " · " + CoverageLabel(score.Status) + " · " + score.DirectSets + " séries diretas";
            }
            shape.SetResourceReference(Shape.FillProperty, fill);
            shape.SetResourceReference(Shape.StrokeProperty, line);
            var button = Button(label, () => select(group), "MuscleRegion");
            button.Content = shape;
            button.ToolTip = label;
            button.Width = bounds.Width + 1.5;
            button.Height = bounds.Height + 1.5;
            button.MouseEnter += delegate { shape.SetResourceReference(Shape.StrokeProperty, "Ink"); shape.StrokeThickness = 2; };
            button.MouseLeave += delegate { shape.SetResourceReference(Shape.StrokeProperty, line); shape.StrokeThickness = 1.1; };
            AutomationProperties.SetHelpText(button, coverage == null ? "Mostrar exercícios de " + Catalog.Names[group] : "Mostrar detalhes da semana");
            Canvas.SetLeft(button, bounds.Left - .75);
            Canvas.SetTop(button, bounds.Top - .75);
            canvas.Children.Add(button);
        }

        private static Path MapShape(string data, string fill, string stroke, double thickness)
        {
            var path = new Path { Data = Geometry.Parse(data), StrokeThickness = thickness, StrokeLineJoin = PenLineJoin.Round };
            if (fill != null) path.SetResourceReference(Shape.FillProperty, fill);
            path.SetResourceReference(Shape.StrokeProperty, stroke);
            return path;
        }

        private static string Normalize(string value)
        {
            var result = new StringBuilder();
            foreach (char ch in value.Normalize(NormalizationForm.FormD))
                if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark) result.Append(char.ToLowerInvariant(ch));
            return result.ToString();
        }
    }
}
