using Verse;

namespace RimKiior
{
    // StaticConstructorOnStartup:游戏加载完所有 Def 后自动执行一次,是模组代码的标准入口
    [StaticConstructorOnStartup]
    public static class ModEntry
    {
        static ModEntry()
        {
            Log.Message("[RimKiior] 模组代码已加载!");
        }
    }
}
