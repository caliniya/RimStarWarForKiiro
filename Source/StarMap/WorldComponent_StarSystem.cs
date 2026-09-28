using RimWorld.Planet;
using Verse;

namespace StarWarKiiro.StarMap
{
    // 世界级存档组件:星系种子随存档一起保存,同一个存档永远对应同一个星系
    public class WorldComponent_StarSystem : WorldComponent
    {
        private int seed = -1;
        private StarSystem system;

        public WorldComponent_StarSystem(World world) : base(world)
        {
        }

        public StarSystem System
        {
            get
            {
                if (system == null)
                {
                    if (seed < 0) seed = global::System.Environment.TickCount & 0x7fffffff;
                    system = StarSystem.Generate(seed);
                    system.AssignHomePlanet(Find.World?.info?.name ?? "未知星球");
                }
                return system;
            }
        }

        // 开发用:重新随机种子(存档后会被固化)
        public void RerollSeed()
        {
            seed = global::System.Environment.TickCount & 0x7fffffff;
            system = null;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref seed, "starSystemSeed", -1);
        }
    }
}
