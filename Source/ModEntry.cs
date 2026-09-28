using Verse;

namespace StarWarKiiro
{
    [StaticConstructorOnStartup]
    public static class ModEntry
    {
        static ModEntry()
        {
            Log.Message("[StarWarKiiro] 模组代码已加载!");
        }
    }
}
