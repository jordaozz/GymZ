using System.Collections.Generic;

namespace GymZ
{
    internal static class ExerciseEquipment
    {
        private const string LifeMachines = "https://www.lifefitness.com/en-us/catalog/strength-training/selectorized/insignia-series-";
        private const string LifeCable = "https://www.lifefitness.com/en-us/catalog/strength-training/cable-machines-functional-trainers/life-fitness-dual-adjustable-pulley";
        private const string LifeSupport = "https://support.lifefitness.com/hc/en-us/articles/";
        private const string Technogym = "https://www.technogym.com/en-US/category/selection/";

        internal sealed class EquipmentGuide
        {
            public string Title, Description, Picture, Source;
        }

        private const string Tower = "M31,110 H169 M41,110 V18 H159 V110 M46,96 H66 V60 H46 Z M134,96 H154 V60 H134 Z M46,71 H66 M46,80 H66 M46,88 H66 M134,71 H154 M134,80 H154 M134,88 H154 M41,25 L70,49 L77,46 L72,58 Z M159,25 L130,49 L123,46 L128,58 Z";
        private const string Bench = "M44,105 L53,72 L102,72 M101,72 L151,48 L156,57 L107,83 H51 L46,78 Z M107,83 L121,102 M42,105 H65 M111,105 H135";
        private const string Dumbbell = "M49,62 V44 H63 V81 H49 Z M62,41 V36 H76 V87 H62 Z M76,58 H124 M76,66 H124 M124,36 H138 V87 H124 Z M138,44 H151 V80 H138 Z";
        private const string Band = "M47,63 C47,38 151,36 155,61 C153,84 49,87 47,63 Z M56,62 C56,52 143,50 145,62 C142,72 58,72 56,62 Z";
        private const string Barbell = "M23,61 H50 M150,61 H177 M70,61 H130 M50,44 H59 V78 H50 Z M59,36 H70 V86 H59 Z M130,36 H141 V86 H130 Z M141,44 H150 V78 H141 Z";
        private const string Rack = "M43,102 V22 H157 V102 M30,106 H65 M141,106 H173 M49,37 H53 M49,53 H53 M49,68 H53 M147,37 H151 M147,53 H151 M147,68 H151";
        private const string Stack = "M138,102 V25 H167 V102 Z M144,57 H161 V94 H144 Z M144,64 H161 M144,72 H161 M144,80 H161 M144,88 H161 M125,105 H177";
        private const string Seated = "M48,104 H123 M78,104 V76 M55,67 H95 V76 H55 Z M60,69 L48,34 L57,31 L70,66 M93,74 L100,101";
        private const string Mat = "M27,89 L65,60 H173 L136,89 Z M27,89 V94 H136 L173,64 V60";
        private const string Bike = "M48,103 H164 M62,102 L74,84 H139 L154,103 M77,84 L83,57 M71,54 H96 V59 H71 Z M134,84 L139,46 L157,41 M134,45 H147 M99,82 A13,13 0 1 0 125,82 A13,13 0 1 0 99,82 M112,82 L126,75 H135";

        private static EquipmentGuide Gear(string title, string description, string picture, string source = null)
        {
            return new EquipmentGuide { Title = title, Description = description, Picture = picture, Source = source };
        }

        private static readonly Dictionary<string, EquipmentGuide> EquipmentByExercise = new Dictionary<string, EquipmentGuide> {
            { "incline-db", Gear("Banco inclinado e dois halteres", "Procure um banco com encosto ajustável. Incline pouco o encosto e separe dois halteres iguais.", Bench + " M33,46 H51 M34,40 V52 M50,39 V53") },
            { "bench", Gear("Banco reto com suporte de barra", "O banco tem encosto horizontal e dois apoios fixos ou ajustáveis onde a barra descansa.", Rack + " M57,70 H133 V78 H57 Z M77,78 V101 M126,78 V101 M35,45 H164 M53,31 V59 M145,31 V59") },
            { "hip-thrust", Gear("Banco firme, barra e proteção", "Apoie a parte alta das costas no banco. Use uma proteção na barra e, se necessário, anilhas que deixem espaço para posicioná-la.", Bench + " M97,94 H162 M108,84 V103 M151,84 V103") },
            { "barbell-squat", Gear("Rack de agachamento e barra", "Procure uma estrutura com ganchos e travas de segurança. Regule a altura dos ganchos antes de colocar carga.", Rack + " M23,49 H176 M60,37 V61 M140,37 V61 M52,78 H147") },
            { "chest-machine", Gear("Máquina de supino sentado", "O assento tem encosto e alças à frente, na altura do peito. Procure a etiqueta Chest Press ou Supino.", Seated + Stack + " M135,32 H119 L110,62 H96 M94,56 V70 M134,33 L125,72 H110", LifeMachines + "chest-press") },
            { "pec-deck", Gear("Voador peitoral (Pec Deck)", "Procure uma cadeira com encosto e dois braços laterais que giram para a frente, com alças ou almofadas para os antebraços.", Tower + " M75,44 V75 M125,44 V75 M87,61 H112 V77 H87 Z M99,78 V107", LifeMachines + "pectoral-fly-rear-deltoid") },
            { "pulldown", Gear("Puxada alta com barra longa", "Procure a barra suspensa acima do assento e os rolos que seguram as coxas enquanto você puxa o cabo.", Stack + " M138,25 H78 V43 M57,45 H99 M54,46 L48,51 M102,46 L108,51 M61,90 H94 M79,90 V104 M92,76 H114 M112,77 V102 M41,105 H119", LifeMachines + "pulldown") },
            { "seated-row", Gear("Polia baixa de remada", "Procure o cabo que sai perto do chão, um banco e apoios inclinados para os pés. A alça em V é comum.", Stack + " M34,95 H123 M42,72 H94 V80 H42 Z M60,80 V95 M94,92 L116,71 M110,95 L128,69 M138,88 L102,66 L88,74 Z", Technogym) },
            { "chest-row", Gear("Remada com apoio no peito", "Identifique a almofada à frente do assento e as alças acessíveis por trás ou pelos lados dela. Há versões com placas e com anilhas.", Stack + " M47,105 H126 M66,103 V78 M44,74 H81 V81 H44 Z M87,67 L101,43 L110,47 L96,72 Z M105,87 L124,66 L115,52 M113,50 H125", LifeMachines + "row") },
            { "machine-shoulder", Gear("Máquina de desenvolvimento", "Assento com encosto e alças próximas aos ombros. A trajetória leva as alças para cima.", Seated + Stack + " M133,28 H110 L98,52 H75 M77,47 V61 M122,27 V44 L106,58", LifeMachines + "shoulder-press") },
            { "leg-press", Gear("Leg press inclinado (45°)", "Procure o encosto reclinado e uma plataforma que sobe em trilhos diagonais. Identifique as travas antes de começar.", "M32,103 H166 M39,94 L145,49 L157,65 L56,105 M43,82 L59,61 L66,65 L55,85 L80,76 L83,82 L58,94 Z M124,57 L110,40 L116,33 L137,51 M137,52 L151,42 L160,53 L149,63", "https://www.lifefitness.com/en-us/catalog/strength-training/plate-loaded/plate-loaded-linear-leg-press") },
            { "leg-press-horizontal", Gear("Leg press horizontal (180°)", "O assento fica voltado para a plataforma à frente. Procure um trilho quase horizontal, com o assento ou a plataforma móvel de acordo com o modelo.", "M40,43 L64,81 H101 V89 H61 L32,47 Z M64,89 V106 M93,89 V106 M103,95 H173 M144,46 L139,85 H155 L160,46 Z M151,85 L166,96 M29,108 H179", LifeMachines + "seated-leg-press") },
            { "hack-squat", Gear("Agachamento hack", "Procure trilhos inclinados, uma plataforma fixa e um carrinho com apoio para as costas e almofadas para os ombros.", "M25,107 H179 M48,105 L120,22 M66,105 L137,22 M78,73 L111,32 L121,37 L88,79 Z M112,38 L130,47 M114,28 L135,39 M49,98 L83,103", "https://www.lifefitness.com/en-us/catalog/strength-training/plate-loaded") },
            { "step-up", Gear("Banco firme e halteres", "Use um banco estável que permita apoiar o pé inteiro. Comece com uma altura fácil de controlar.", "M47,79 H121 V89 H47 Z M53,89 V107 M113,89 V107 M44,107 H65 M105,107 H126 M125,56 H160 M125,47 V65 M159,47 V65") },
            { "cable-lateral", Gear("Polia baixa com uma alça", "Regule a polia perto do chão. Segure a alça com a mão do lado mais distante da torre.", Tower, LifeCable) },
            { "leg-extension", Gear("Cadeira extensora", "Procure uma cadeira com encosto e um rolo à frente das canelas. Na extensão, você empurra o rolo para a frente.", Seated + Stack + " M98,78 L113,95 H127 M120,88 H135 V101 H120 Z", LifeMachines + "leg-extension") },
            { "leg-curl", Gear("Mesa flexora deitada", "O apoio é comprido, para deitar de barriga para baixo. O rolo fica atrás dos tornozelos.", Stack + " M35,69 L80,60 L127,70 L124,78 L80,69 L37,78 Z M47,78 V103 M99,74 V103 M115,104 L123,85 H134 M125,77 H140 V90 H125 Z M29,105 H132", LifeMachines + "leg-curl") },
            { "seated-curl", Gear("Cadeira flexora sentada", "Procure o assento com encosto, um apoio que prende as coxas e outro rolo que recebe a parte de trás dos tornozelos.", Seated + Stack + " M96,58 H120 V67 H96 Z M112,98 L128,82 M121,74 H135 V88 H121 Z", LifeMachines + "seated-leg-curl") },
            { "abductor", Gear("Cadeira abdutora", "As almofadas ficam por fora dos joelhos. Você afasta as pernas. Em aparelhos duplos, confirme a posição das almofadas.", Seated + Stack + " M72,75 L57,87 H36 M90,75 L111,83 H129 M34,75 V91 H46 V75 Z M121,71 V87 H134 V71 Z", LifeMachines + "hip-abductor-adductor") },
            { "adductor-machine", Gear("Cadeira adutora", "As almofadas ficam entre as pernas. Você aproxima os joelhos. O mesmo aparelho pode ter função abdutora.", Seated + Stack + " M58,74 L66,85 H80 M96,74 L104,85 H120 M62,72 V86 H74 V72 Z M104,72 V86 H116 V72 Z", LifeMachines + "hip-adduction") },
            { "machine-calf", Gear("Panturrilha sentada", "Procure uma alavanca com almofadas sobre as coxas e uma plataforma menor, onde fica a frente dos pés.", "M29,105 H165 M49,80 H87 V86 H49 Z M67,86 V105 M91,73 H125 V82 H91 Z M116,74 L137,93 H157 M120,98 H150 V104 H120 Z M113,82 L108,101", "https://www.lifefitness.com/en-us/catalog/strength-training/plate-loaded/plate-loaded-seated-calf-raise") },
            { "bike", Gear("Bicicleta ergométrica", "Procure o selim ajustável, pedais, guidão e controle de resistência. A ilustração mostra o modelo vertical.", Bike, LifeSupport + "16166380651671-Life-Fitness-Integrity-Bike") },
            { "elliptical", Gear("Aparelho elíptico", "Tem dois pedais compridos e alças móveis. Os pés permanecem apoiados enquanto os pedais deslizam.", "M28,104 H171 M109,87 A20,20 0 1 0 149,87 A20,20 0 1 0 109,87 M109,91 L53,75 H33 M148,87 L88,70 H66 M125,74 L145,40 H164 M107,85 L86,59 L81,24 H66 M126,77 L108,56 L106,23 H121", LifeSupport + "16173749213207") },
            { "treadmill", Gear("Esteira", "Procure a lona móvel sob os pés, as barras laterais e o painel para velocidade. Identifique a trava de segurança.", "M26,100 H163 V107 H26 Z M43,100 H140 M142,101 L133,44 H155 L159,34 H137 L130,43 M130,51 H105 L96,62", LifeSupport + "16165431493015-Life-Fitness-Integrity-Treadmill") },
            { "pushdown", Gear("Polia alta com corda", "Regule a polia acima da cabeça e encaixe a corda com duas pontas na presilha.", Tower, LifeCable) },
            { "cable-fly", Gear("Duas polias ajustáveis com alças", "Procure duas torres voltadas uma para a outra. Coloque uma alça em cada cabo e ajuste-os à altura aproximada do peito.", Tower, LifeCable) },
            { "kickback", Gear("Polia baixa e tornozeleira", "Regule a saída do cabo perto do chão e conecte a tornozeleira à presilha. Use um apoio firme para o equilíbrio.", Tower, LifeCable) },
            { "face-pull", Gear("Polia e corda", "Prenda a corda à presilha e posicione a saída do cabo perto da altura do rosto.", Tower, LifeCable) },
            { "cable-curl", Gear("Polia baixa com barra curta", "Procure a torre com cabo ajustável. Regule a saída perto do chão e escolha uma barra curta.", Tower, LifeCable) },
            { "cable-crunch", Gear("Polia alta com corda", "A corda fica acima da cabeça. Deixe espaço à frente da torre para ajoelhar com estabilidade.", Tower, LifeCable) },
            { "pallof", Gear("Polia na altura do peito", "Prenda uma alça curta ao cabo. Fique de lado para a torre, com espaço para estender as mãos à frente.", Tower, LifeCable) },
            { "chair-squat", Gear("Cadeira ou banco firme", "Use um assento estável, sem rodinhas. Ajuste a altura para poder sentar e levantar com controle.", "M59,99 V31 H72 V61 H125 V99 M61,62 H126 V73 H61 M79,75 V102 M111,75 V102") },
            { "grip-towel", Gear("Toalha dobrada", "Enrole uma toalha pequena e firme para formar um rolo que caiba na mão.", "M56,45 H135 Q149,45 149,62 Q149,78 135,78 H56 Q46,78 46,62 Q46,45 56,45 M56,45 Q69,45 69,62 Q69,78 56,78 M56,53 Q62,53 62,62 Q62,69 54,69") },
            { "wall-slide", Gear("Parede livre", "Use uma parede lisa com espaço para elevar os braços. Afaste os pés o suficiente para manter boa base.", "M50,22 H152 V102 H50 Z M63,35 H138 M63,43 H138 M63,51 H138 M41,102 H164") }
        };

        private static readonly Dictionary<string, EquipmentGuide> EquipmentByType = new Dictionary<string, EquipmentGuide> {
            { "body", Gear("Espaço para o movimento", "Use uma superfície firme e espaço livre. Um colchonete pode dar conforto nos movimentos deitados.", Mat) },
            { "dumbbell", Gear("Halteres", "Escolha o peso indicado no corpo do halter. Se usar apoio ou banco, verifique se está estável.", Dumbbell) },
            { "band", Gear("Elástico de resistência", "Verifique a faixa e a fixação antes de aplicar tensão. Use uma faixa de comprimento adequado à posição mostrada.", Band) },
            { "barbell", Gear("Barra, anilhas e presilhas", "Confira o peso da barra e use presilhas para prender as anilhas dos dois lados.", Barbell) },
            { "pullup", Gear("Barra fixa", "Procure uma barra horizontal alta e firme, com espaço livre para suspender os pés.", Rack + " M41,27 H160") }
        };

        internal static EquipmentGuide Find(Exercise exercise)
        {
            EquipmentGuide guide;
            return EquipmentByExercise.TryGetValue(exercise.Id, out guide) ? guide : EquipmentByType[exercise.Gear];
        }
    }
}
