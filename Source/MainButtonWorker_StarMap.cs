using RimWorld;

namespace StarWarKiiro
{
    // 仿照原版 MainButtonWorker_ToggleMechTab:Visible 返回 false 时选项卡整体隐藏
    public class MainButtonWorker_StarMap : MainButtonWorker_ToggleTab
    {
        public override bool Visible =>
            StarWarKiiroDefOf.StarWarKiiro_Astronomy != null
            && StarWarKiiroDefOf.StarWarKiiro_Astronomy.IsFinished;
    }
}
