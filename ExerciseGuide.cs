using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace GymZ
{
    internal static class ExerciseArt
    {
        internal const string Source = "https://github.com/bryllim/workout-guide";
        internal const string License = "https://creativecommons.org/licenses/by-sa/4.0/";
        private const string ResourcePrefix = "exercise-art.";
        private static readonly Assembly Resources = Assembly.GetExecutingAssembly();
        private static readonly Dictionary<string, BitmapFrame> Images = new Dictionary<string, BitmapFrame>();
        private static readonly Dictionary<string, BitmapSource> Thumbnails = new Dictionary<string, BitmapSource>();

        internal sealed class Art
        {
            public string[] Sources;
            public string[] Captions;
            public int[] Frames;

            public bool FromWorkoutGuide;
            public int Count { get { return Frames.Length; } }

            public string Caption(int phase)
            {
                if (phase < Captions.Length) return Captions[phase];
                if (Count == 1) return "Posição";
                return phase == 0 ? "Posição A" : "Posição B";
            }
        }

        internal static readonly Dictionary<string, Art> ById = Load();

        private static Dictionary<string, Art> Load()
        {
            var art = new Dictionary<string, Art>();
            using (var reader = new StreamReader(Resources.GetManifestResourceStream(ResourcePrefix + "Attribution.txt")))
            {
                for (string line = reader.ReadLine(); line != null; line = reader.ReadLine())
                {
                    if (string.IsNullOrWhiteSpace(line) || line[0] == '#') continue;
                    var fields = line.Split('|');
                    art.Add(fields[0], new Art {
                        FromWorkoutGuide = fields[1].Length > 0,
                        Frames = fields[2].Split(',').Select(int.Parse).ToArray(),
                        Sources = fields[3].Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries),
                        Captions = fields.Skip(4).ToArray()
                    });
                }
            }
            return art;
        }

        internal static ImageSource ImageFor(string id, int frame)
        {
            string filename = ResourcePrefix + id + "-" + frame + ".png";
            BitmapFrame bitmap;
            if (Images.TryGetValue(filename, out bitmap)) return bitmap;
            using (var stream = Resources.GetManifestResourceStream(filename))
            {
                bitmap = BitmapFrame.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                bitmap.Freeze();
            }
            Images.Add(filename, bitmap);
            return bitmap;
        }

        internal static ImageSource ThumbnailFor(string id, int frame)
        {
            BitmapSource thumbnail;
            string key = id + ":" + frame;
            if (Thumbnails.TryGetValue(key, out thumbnail)) return thumbnail;
            var bitmap = (BitmapSource)ImageFor(id, frame);
            var image = new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);
            int width = image.PixelWidth, height = image.PixelHeight, stride = width * 4;
            var pixels = new byte[stride * height];
            image.CopyPixels(pixels, stride, 0);
            int left = width, top = height, right = -1, bottom = -1;
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    if (pixels[y * stride + x * 4 + 3] < 40) continue;
                    left = Math.Min(left, x); top = Math.Min(top, y); right = Math.Max(right, x); bottom = Math.Max(bottom, y);
                }
            thumbnail = right < left ? bitmap : new CroppedBitmap(bitmap, new Int32Rect(left, top, right - left + 1, bottom - top + 1));
            thumbnail.Freeze();
            Thumbnails.Add(key, thumbnail);
            return thumbnail;
        }
    }

    internal sealed partial class GymApp
    {
        private static ExerciseEquipment.EquipmentGuide FindEquipment(Exercise exercise) { return ExerciseEquipment.Find(exercise); }

        private void FillExerciseGuide(Panel panel, Exercise exercise)
        {
            panel.Children.Add(Text(Catalog.Names[exercise.Group] + "  ·  " + Catalog.GearNames[exercise.Gear], 13, "Accent", true));
            if (exercise.Other.Length > 0)
                Add(panel, Text("Também trabalha " + string.Join(", ", exercise.Other.Select(g => Catalog.Names[g].ToLower(Training.Pt))) + ".", 13, "Muted"), 5);
            var art = ExerciseArt.ById[exercise.Id];
            var first = PoseCard(exercise, 0, art.Caption(0));
            Add(panel, art.Count == 1 ? (UIElement)first : Columns(first, PoseCard(exercise, 1, art.Caption(1)), 1, 1, 8), 12);

            var equipment = FindEquipment(exercise);
            var apparatus = new StackPanel();
            apparatus.Children.Add(Text(equipment.Title, 15, null, true));
            Add(apparatus, Text(equipment.Description, 13, "Muted"), 5);
            if (equipment.Source != null)
            {
                var link = Button("Ver fotos do fabricante ↗", () => OpenLink(equipment.Source), "Selected");
                link.HorizontalAlignment = HorizontalAlignment.Left;
                Add(apparatus, link, 8);
            }
            var sketch = VisualPanel(Sketch(null, equipment.Picture), 76);
            AutomationProperties.SetName(sketch, "Esquema · " + equipment.Title);
            Add(panel, Card(Columns(Top(sketch), apparatus, .85, 2.15, 11), 12), 10);

            var instructions = new StackPanel();
            instructions.Children.Add(Text("Como executar", 18, null, true));
            for (int i = 0; i < exercise.Steps.Length; i++) Add(instructions, Text((i + 1) + ".  " + exercise.Steps[i], 14), 8);
            Add(instructions, Text(exercise.Tip, 13, "Accent", true), 10);
            Add(panel, Card(instructions, 13), 10);
            if (exercise.Id == "leg-press" || exercise.Id == "leg-press-horizontal") Add(panel, FootPlacement(), 10);
            var links = new WrapPanel();
            AddChoice(links, Button("Ver evolução", () => HistoryDialog(exercise, training.Today), "Selected"));
            AddChoice(links, Button("Biblioteca técnica · ACE ↗", () => OpenLink(exercise.Id == "incline-db" ? Catalog.Ace + "25/incline-chest-press/" : Catalog.Ace)));
            if (art.FromWorkoutGuide)
            {
                AddChoice(links, Button("Arte · Workout Guide ↗", () => OpenLink(ExerciseArt.Source)));
                AddChoice(links, Button("CC BY-SA 4.0 ↗", () => OpenLink(ExerciseArt.License)));
                string author = art.Sources.Any(s => s.Contains("Everkinetic")) ? "Everkinetic e Bryl Lim" : "Bryl Lim";
                Add(panel, Text("Ilustrações de " + author + " · CC BY-SA 4.0.", 11, "Muted"), 10);
            }
            else Add(panel, Text("Ilustração de execução do GymZ.", 11, "Muted"), 10);
            Add(panel, links, 7);
        }

        private UIElement FootPlacement()
        {
            var panel = new StackPanel();
            panel.Children.Add(Text("Posição dos pés no leg press", 18, null, true));
            var placements = new System.Windows.Controls.Primitives.UniformGrid { Columns = 3 };
            foreach (var entry in new[] { new { Title = "Mais alto", Y = 35 }, new { Title = "Centro", Y = 60 }, new { Title = "Mais baixo", Y = 85 } })
            {
                string feet = "M84," + (entry.Y - 10) + " Q79," + (entry.Y - 12) + " 78," + entry.Y + " Q77," + (entry.Y + 11) + " 83," + (entry.Y + 11) +
                    " Q89," + (entry.Y + 11) + " 89," + entry.Y + " Q89," + (entry.Y - 10) + " 84," + (entry.Y - 10) +
                    " M116," + (entry.Y - 10) + " Q111," + (entry.Y - 10) + " 111," + entry.Y + " Q111," + (entry.Y + 11) + " 117," + (entry.Y + 11) +
                    " Q123," + (entry.Y + 11) + " 122," + entry.Y + " Q121," + (entry.Y - 12) + " 116," + (entry.Y - 10);
                var marker = new StackPanel { Margin = new Thickness(0, 0, 6, 0) };
                marker.Children.Add(VisualPanel(Sketch(feet, "M57,14 H143 V110 H57 Z M61,37 H72 M129,37 H139 M61,62 H72 M129,62 H139 M61,87 H72 M129,87 H139"), 63));
                Add(marker, Center(Text(entry.Title, 11, "Muted")), 4);
                placements.Children.Add(marker);
            }
            Add(panel, placements, 9);
            Add(panel, Text("Centro · Comece com os pés inteiros apoiados e os joelhos acompanhando a direção dos dedos. É uma base para trabalhar quadríceps com participação de glúteos.", 13), 8);
            Add(panel, Text("Mais alto · Em alguns testes a 45°, aumentou a participação dos glúteos ou dos posteriores. O efeito varia conforme a máquina, a amplitude e a carga.", 13), 8);
            Add(panel, Text("Mais baixo · Pode aumentar a demanda dos quadríceps em algumas condições. Preserve o apoio dos calcanhares. A posição central ou aberta por si só não garante foco na parte interna da coxa.", 13), 8);
            Add(panel, Text("Use o ajuste que permite movimento controlado. Para dar foco direto aos posteriores ou aos adutores, combine com mesa flexora ou cadeira adutora.", 13, "Muted"), 8);
            var links = new WrapPanel();
            AddChoice(links, Button("Estudo de variações ↗", () => OpenLink(Catalog.FootStudy)));
            AddChoice(links, Button("Revisão de evidências ↗", () => OpenLink(Catalog.FootReview)));
            Add(panel, links, 9);
            return Card(panel, 13);
        }

        private void ExerciseCredits()
        {
            StackPanel panel;
            var dialog = Dialog("Créditos das ilustrações", out panel);
            panel.Children.Add(Text("Créditos das ilustrações", 22, null, true));
            Add(panel, Text("Arte de exercício de Everkinetic, com expansão de Bryl Lim para o Workout Guide. As imagens selecionadas são distribuídas em seu formato original, sob CC BY-SA 4.0.", 14, "Muted"), 10);
            Add(panel, Text("As demais poses foram criadas para o GymZ com geração de imagens e revisão das posições. Os esquemas de aparelhos acompanham links para os catálogos oficiais.", 13, "Muted"), 10);
            var links = new WrapPanel();
            AddChoice(links, Button("Workout Guide ↗", () => OpenLink(ExerciseArt.Source)));
            AddChoice(links, Button("Everkinetic ↗", () => OpenLink("https://github.com/everkinetic/data")));
            AddChoice(links, Button("CC BY-SA 4.0 ↗", () => OpenLink(ExerciseArt.License)));
            AddChoice(links, Button("Voltar", dialog.Close));
            Add(panel, links, 11);
            dialog.ShowDialog();
        }

        private Border PoseCard(Exercise exercise, int phase, string label)
        {
            var stack = new StackPanel();
            stack.Children.Add(VisualPanel(Pose(exercise, phase), 160));
            Add(stack, Center(Text(label, 12, "Muted", true)), 7);
            return Card(stack, 8, "Input");
        }

        private FrameworkElement Pose(Exercise exercise, int phase, bool thumbnail = false)
        {
            var art = ExerciseArt.ById[exercise.Id];
            ImageSource source = thumbnail ? ExerciseArt.ThumbnailFor(exercise.Id, art.Frames[phase]) : ExerciseArt.ImageFor(exercise.Id, art.Frames[phase]);
            FrameworkElement pose = new Image { Source = source, Stretch = Stretch.Uniform };
            RenderOptions.SetBitmapScalingMode(pose, BitmapScalingMode.HighQuality);
            AutomationProperties.SetName(pose, exercise.Name + " · " + art.Caption(phase));
            return pose;
        }

        private Border ExerciseThumbnail(Exercise exercise, double size)
        {
            var thumbnail = VisualPanel(Pose(exercise, 0, true), size);
            thumbnail.Width = size;
            thumbnail.VerticalAlignment = VerticalAlignment.Top;
            return thumbnail;
        }

        private static Border VisualPanel(FrameworkElement illustration, double height)
        {
            return new Border {
                Height = height, Padding = new Thickness(4), Child = illustration, CornerRadius = new CornerRadius(8),
                Background = ColorBrush("#172B42"), BorderBrush = ColorBrush("#35506D"), BorderThickness = new Thickness(1)
            };
        }

        private static Viewbox Sketch(string figure, string gear)
        {
            var canvas = new Canvas { Width = 200, Height = 128 };
            if (!string.IsNullOrWhiteSpace(gear)) AddSketch(canvas, gear, "#91B8D9", 2.6);
            if (!string.IsNullOrWhiteSpace(figure)) AddSketch(canvas, figure, "#F0F6FF", 3.25);
            return new Viewbox { Child = canvas, Stretch = Stretch.Uniform };
        }

        private static void AddSketch(Canvas canvas, string path, string color, double thickness)
        {
            var shape = new System.Windows.Shapes.Path {
                Data = Geometry.Parse(path), Stroke = ColorBrush(color), StrokeThickness = thickness, Stretch = Stretch.None,
                StrokeLineJoin = PenLineJoin.Round, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round
            };
            canvas.Children.Add(shape);
        }
    }
}
