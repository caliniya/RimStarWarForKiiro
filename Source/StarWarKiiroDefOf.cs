using RimWorld;
using Verse;

namespace StarWarKiiro
{
    // DefOf:游戏启动时按字段名自动绑定同名 Def,之后 StarWarKiiroDefOf.StarWarKiiro_Astronomy 就是那个研究项目
    [DefOf]
    public static class StarWarKiiroDefOf
    {
        public static ResearchProjectDef StarWarKiiro_Astronomy;
        public static ResearchProjectDef StarWarKiiro_AdvancedMathematics;
        public static ResearchProjectDef StarWarKiiro_Optics;
        public static ResearchProjectDef StarWarKiiro_ChemicalPropulsion;
        public static ResearchProjectDef StarWarKiiro_BasicOrbitalResearch;
        public static ResearchProjectDef StarWarKiiro_SpaceElectronics;

        public static ThingDef StarWarKiiro_HighExplosive;
        public static ThingDef StarWarKiiro_RocketLaunchPad;
        public static ThingDef StarWarKiiro_RocketControlConsole;

        public static MainButtonDef StarWarKiiro_StarMap;

        public static bool IsFinished(ResearchProjectDef def) => def != null && def.IsFinished;
    }
}
