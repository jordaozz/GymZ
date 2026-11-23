using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace GymZ
{
    internal sealed partial class GymApp
    {
        private static string DayAndDate(DateTime date)
        {
            return Training.DayNames[Training.Day(date)].ToLower(Training.Pt) + ", " + date.ToString("dd/MM");
        }

        private void PlanDetails(DateTime date, Session session)
        {
            StackPanel panel;
            var dialog = Dialog("Como o treino foi montado", out panel, 670);
            panel.Children.Add(Button("‹  Voltar", dialog.Close));
            var profile = training.At(date);
            Add(panel, Text(profile.Name == "" ? "Seu plano de treino" : "Treino de " + profile.Name, 24, null, true), 8);
            Add(panel, Text(Catalog.Goals[profile.Goal] + "  ·  " + Catalog.ProfileGear[profile.Gear] + "  ·  " + profile.Minutes + " min", 13, "Accent", true), 5);
            var summary = new StackPanel();
            summary.Children.Add(Text("Distribuição na semana", 17, null, true));
            var monday = Training.Monday(date);
            foreach (var day in Enumerable.Range(0, 7).Select(offset => monday.AddDays(offset)).Where(d => training.At(d).Days.Contains(Training.Day(d))))
            {
                var entry = training.Get(day);
                if (entry.Scheduled) Add(summary, Columns(Text(Training.DayNames[Training.Day(day)], 13, null, true),
                    Text(entry.Title, 13, "Muted"), .7, 1.3, 6), 6);
            }
            Add(summary, Text("As sessões por grupos reservam exercícios diretos para o foco de cada dia. Músculos auxiliares ainda participam, como os ombros no supino e o bíceps nas puxadas.", 13), 7);
            Add(summary, Text("Com séries semanais semelhantes, corpo inteiro e treino dividido podem produzir ganhos parecidos. Distribuir as séries de uma região em mais de um dia pode facilitar a execução e a recuperação quando o volume aumenta.", 13, "Muted"), 7);
            Add(panel, Card(summary, 13), 13);

            var aerobic = new StackPanel();
            aerobic.Children.Add(Text("Cardio na rotina", 17, null, true));
            var blocks = session.Items.Where(m => Catalog.ById[m.Id].Type == 'A').ToList();
            foreach (var block in blocks)
            {
                var exercise = Catalog.ById[block.Id];
                string when = block.CardioPhase == "before" ? "Antes da musculação" : block.CardioPhase == "after" ? "Depois da musculação" : "Cardio previsto";
                Add(aerobic, Text(when + " · " + exercise.Name + " · " + block.Target, 13, "Accent", true), 7);
                Add(aerobic, Text(CoachingText.CardioDeviceNote(exercise.Id), 13, "Muted"), 4);
            }
            if (blocks.Count == 0)
                Add(aerobic, Text("Esta sessão prioriza a musculação. Um aquecimento leve de 5–10 minutos, seguido de séries leves do primeiro exercício, é uma opção para começar.", 13, "Muted"), 7);
            else
                Add(aerobic, Text(profile.Goal == "endurance" ? "Com foco no fôlego, faça o bloco aeróbico enquanto está descansado. Aumente a duração aos poucos." :
                    "Fazer o bloco aeróbico depois preserva energia para a musculação. No cardio moderado, você consegue conversar, mas cantar fica difícil.", 13), 7);
            Add(aerobic, Text("Bicicleta, caminhada e transport trabalham o condicionamento. Para ganhar força ou massa, mantenha a musculação. Para saúde, a referência geral é acumular 150 minutos moderados por semana, em ritmo adequado a você.", 12, "Muted"), 9);
            var cardioSources = new WrapPanel();
            AddChoice(cardioSources, Button("Intensidade e tempo · CDC ↗", () => OpenLink(Catalog.CardioIntensity)));
            AddChoice(cardioSources, Button("Aquecimento · AHA ↗", () => OpenLink(Catalog.Warmup)));
            AddChoice(cardioSources, Button("Ordem da sessão ↗", () => OpenLink(Catalog.CardioOrder)));
            AddChoice(cardioSources, Button("Cardio e musculação ↗", () => OpenLink(Catalog.ConcurrentTraining)));
            Add(aerobic, cardioSources, 9);
            Add(panel, Card(aerobic, 13), 10);

            var schedule = new StackPanel();
            schedule.Children.Add(Text("Quando a região volta ao plano", 17, null, true));
            foreach (var group in session.Items.Select(m => Catalog.ById[m.Id].Group).Where(g => g != "cardio").Distinct())
            {
                var next = training.NextGroup(date, group);
                var label = next.HasValue ? DayAndDate(next.Value) : "Sem nova sessão direta nos próximos 14 dias";
                Add(schedule, Columns(Text(Catalog.Names[group], 13, null, true), Text(label, 13, "Muted"), .7, 1.3, 6), 8);
            }
            Add(panel, Card(schedule, 13), 10);
            Add(panel, Text("Os exercícios principais dão continuidade ao progresso. Algumas sessões ganham uma alternativa para a mesma região ao longo das semanas. Suas trocas e os exercícios que você costuma fazer orientam as próximas escolhas.", 13, "Muted"), 10);
            var sources = new WrapPanel();
            AddChoice(sources, Button("Diretrizes ACSM ↗", () => OpenLink(Catalog.Acsm)));
            AddChoice(sources, Button("Divisão dos treinos ↗", () => OpenLink(Catalog.FrequencyTrial)));
            AddChoice(sources, Button("Estudo sobre variação ↗", () => OpenLink(Catalog.VariationTrial)));
            AddChoice(sources, Button("Descanso entre séries ↗", () => OpenLink(Catalog.RestReview)));
            Add(panel, sources, 10);
            dialog.ShowDialog();
        }

        private Border WeeklyAdvice(WeekReport report)
        {
            var stack = new StackPanel();
            var current = week == Training.Monday(training.Today);
            var profile = training.At(week.AddDays(6) > training.Today ? training.Today : week.AddDays(6));
            string name = training.At(training.Today).Name;
            stack.Children.Add(Text(name != "" ? name + ", o que ajustar no treino" : "O que ajustar no treino", 19, null, true));
            Add(stack, Text(current ? "Análise dos treinos concluídos até hoje." : "Análise dos treinos concluídos na semana.", 13, "Muted"), 5);
            var focus = Training.FocusGroups(profile.Focus);
            var feedback = report.Coverage.Where(p => p.Value.Expected > 0 || focus.Contains(p.Key) || p.Value.EasyEfforts > 0)
                .OrderByDescending(p => focus.Contains(p.Key)).ThenBy(p => p.Value.Status).ThenByDescending(p => p.Value.Expected)
                .Select(p => new { Group = p.Key, Score = p.Value, Suggestion = CoachingText.GroupAdvice(p.Key, p.Value, profile) })
                .Where(p => p.Suggestion != "").Take(3).ToList();
            foreach (var item in feedback)
            {
                Add(stack, Line(), 10);
                Add(stack, Text(Catalog.Names[item.Group] + " · " + CoachingText.DirectDose(item.Score), 13, CoverageInk(item.Score.Status), true), 8);
                Add(stack, Text(item.Suggestion, 13), 4);
                if (item.Score.Status < 2)
                {
                    var next = training.NextGroup(training.Today.AddDays(-1), item.Group);
                    if (next.HasValue)
                    {
                        var movement = training.Get(next.Value).Items.First(m => Catalog.ById[m.Id].Group == item.Group);
                        Add(stack, Text("Próxima oportunidade · " + Catalog.ById[movement.Id].Name + " na " + DayAndDate(next.Value), 12, "Accent"), 4);
                    }
                }
            }
            if (feedback.Count == 0)
                Add(stack, Text("Você cumpriu a cobertura dos grupos previstos até aqui. Compare as repetições e o esforço com os últimos registros dos mesmos exercícios.", 13), 8);
            return Card(stack, 15);
        }

    }
}
