using System.Reflection;

namespace GymZ.Mobile;

internal static class Artwork
{
    internal sealed record Plate(int[] Frames, string[] Captions, bool Licensed, string[] Sources)
    {
        public string Caption(int i) => i < Captions.Length ? Captions[i] :
            Frames.Length == 1 ? "Posição" : i == 0 ? "Posição A" : "Posição B";
    }

    private static readonly Dictionary<string, Plate> Plates = Load();
    internal static Plate For(Exercise exercise) => Plates[exercise.Id];
    internal static string Image(Exercise exercise, int phase = 0) => $"exercise-art/{exercise.Id}-{For(exercise).Frames[phase]}.png";

    private static Dictionary<string, Plate> Load()
    {
        var result = new Dictionary<string, Plate>();
        using var reader = new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream("ExerciseAttribution.txt"));
        while (reader.ReadLine() is { } line)
        {
            if (line.Length == 0 || line[0] == '#') continue;
            var fields = line.Split('|');
            result.Add(fields[0], new Plate(fields[2].Split(',').Select(int.Parse).ToArray(), fields.Skip(4).ToArray(),
                fields[1].Length > 0, fields[3].Split(", ", StringSplitOptions.RemoveEmptyEntries)));
        }
        return result;
    }
}
