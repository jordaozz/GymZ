using System.Linq;

namespace GymZ
{
    internal static class CoachingText
    {
        internal static string Rhythm(Exercise exercise, bool light)
        {
            if (exercise.Type == 'A') return light ? "Ritmo leve, com conversa confortável." : "Ritmo moderado: converse durante o exercício.";
            return exercise.Type == 'T' ? "Mantenha a posição respirando normalmente." :
                "Ritmo: cerca de 1–2 s no esforço e 2 s na volta, com repetições contínuas.";
        }

        internal static string CardioDeviceNote(string exerciseId)
        {
            switch (exerciseId)
            {
                case "bike": return "Na bicicleta você pedala sentado e regula a resistência. Depois de pernas, mantenha a resistência confortável.";
                case "elliptical": return "No transport, os pés ficam apoiados nos pedais ao longo do movimento. Ajuste a resistência e use as alças para se equilibrar.";
                case "treadmill": return "A esteira permite controlar velocidade e inclinação. Comece caminhando e ajuste uma variável de cada vez.";
                case "standing-march": return "A marcha no lugar permite ajustar o ritmo dos passos com pouco espaço e sem saltos.";
                default: return "Na caminhada, ajuste o passo ao seu fôlego. Comece em terreno regular.";
            }
        }

        internal static string DirectDose(MuscleWeek score)
        {
            return score.DirectSets + (score.DirectSets == 1 ? " série direta em " : " séries diretas em ") + score.DirectDays.Count +
                (score.DirectDays.Count == 1 ? " dia" : " dias");
        }

        internal static string GroupAdvice(string group, MuscleWeek score, Profile profile)
        {
            var focus = Training.FocusGroups(profile.Focus);
            bool majorFocus = focus.Contains(group) || (focus.Length == 0 && (profile.Goal == "muscle" || profile.Goal == "recomp"));
            bool maintenance = profile.FocusMode == "priority" && focus.Length > 0 && !focus.Contains(group);
            if (score.Expected > 0 && score.DirectSets == 0)
            {
                string assist = score.SupportSets > 0 ? "A região participou como apoio. " : "";
                return assist + "Inclua um exercício de foco direto para a região na próxima oportunidade do plano.";
            }
            if (score.EffortLogs > 0 && score.EasyEfforts == score.EffortLogs)
                return "Você registrou quatro ou mais repetições em reserva. Se repetir o topo da faixa com boa técnica, experimente o menor aumento de carga disponível.";
            if (score.Expected > 0 && score.Status == 1)
                return "Faltaram " + (score.Target - score.DirectSets) + " séries diretas para a meta de cobertura. Confira o que cabe nas sessões previstas.";
            if (maintenance && score.Status == 2)
                return "Esta região está em manutenção no seu plano. O volume extra foi reservado aos grupos prioritários.";
            if (majorFocus && profile.Days.Length >= 2 && score.DirectDays.Count == 1 && score.PlannedDays.Count >= 2)
                return "A região teve trabalho direto em um dia. Distribuir as séries pelos dias previstos pode ajudar a manter a qualidade das repetições.";
            if (majorFocus && score.DirectSets > 0 && score.EffortLogs == 0)
                return "Registre as repetições em reserva para comparar o esforço. Os quilos ajudam na comparação quando você repete o mesmo movimento e equipamento.";
            return "";
        }
    }
}
