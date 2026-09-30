using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace StarWarKiiro
{
    // 火箭发射台:点「组装」后进入装载,小人把物资搬进台体,装齐一次性扣除并开始组装。
    public class Building_RocketLaunchPad : Building
    {
        public enum PadState : byte
        {
            Empty,
            Loading,
            Assembling,
            Ready
        }

        private const int AssembleDurationTicks = 3600;

        private PadState state = PadState.Empty;
        private int ticksLeft;

        // 装载阶段暂存的物资(尚未扣除)
        private List<Thing> heldIngredients = new List<Thing>();

        public PadState State => state;
        public bool RocketReady => state == PadState.Ready;
        public bool IsAssembling => state == PadState.Assembling;
        public bool IsLoading => state == PadState.Loading;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref state, "padState", PadState.Empty);
            Scribe_Values.Look(ref ticksLeft, "assembleTicksLeft", 0);
            // 暂存的物资已从地图和一切容器中移除,Reference 会因拿不到 loadID 而丢失;
            // 用 Deep 把它们的数据就地写进存档,读档后仍然是完整可用的 Thing
            Scribe_Collections.Look(ref heldIngredients, "heldIngredients", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
                heldIngredients?.RemoveAll(t => t == null);
        }

        protected override void Tick()
        {
            base.Tick();

            if (state == PadState.Loading && Find.TickManager.TicksGame % 60 == 0)
                TryFinishLoadingIfComplete();

            if (state != PadState.Assembling) return;

            var power = GetComp<CompPowerTrader>();
            if (power != null && !power.PowerOn) return;

            ticksLeft--;
            if (ticksLeft > 0) return;

            state = PadState.Ready;
            ticksLeft = 0;
            Messages.Message($"运载火箭组装完成：{LabelShortCap}",
                MessageTypeDefOf.PositiveEvent, historical: false);
        }

        public static IEnumerable<(ThingDef def, int count)> GetAssembleCost()
        {
            yield return (ThingDefOf.Steel, 10);
            yield return (StarWarKiiroDefOf.StarWarKiiro_HighExplosive, 1);
        }

        public string CostLabel()
        {
            return string.Join("、", GetAssembleCost().Select(c => $"{c.def.LabelCap} ×{c.count}"));
        }

        // 还缺哪种物资,缺多少(WorkGiver 用)
        public (ThingDef def, int needed)? NextMissingIngredient()
        {
            foreach (var (def, count) in GetAssembleCost())
            {
                int have = heldIngredients.Where(t => t != null && t.def == def).Sum(t => t.stackCount);
                if (have < count) return (def, count - have);
            }
            return null;
        }

        public bool LoadingComplete => NextMissingIngredient() == null;

        // 诊断用:地图上是否存在该物资(只查存在性,不考虑可达性与是否被别人预留)
        public bool IngredientOnMap(ThingDef def)
        {
            if (Map == null || def == null) return false;
            return Map.listerThings.ThingsOfDef(def).Any(t => t != null && !t.Destroyed && t.stackCount > 0);
        }

        // 小人把物资放进台体(暂存,不立刻销毁)
        public void ReceiveIngredient(Thing thing)
        {
            if (thing == null || state != PadState.Loading) return;

            var missing = NextMissingIngredient();
            if (missing == null || thing.def != missing.Value.def) return;

            int take = thing.stackCount;
            if (take > missing.Value.needed) take = missing.Value.needed;

            var taken = thing.SplitOff(take);
            if (taken == null) return;

            // 整栈投入时 SplitOff 返回原物(它正躺在小人的搬运容器里),切分时才返回新建的那一段
            // 两种情况都必须脱离父容器,否则小人会一直"持有"已经被消耗的物资
            if (taken.Spawned) taken.DeSpawn();
            taken.holdingOwner?.Remove(taken);
            heldIngredients.Add(taken);

            if (LoadingComplete)
                FinishLoading();
        }

        // 装齐:一次性扣除暂存物资,开始组装
        private void FinishLoading()
        {
            foreach (var t in heldIngredients)
            {
                if (t != null && !t.Destroyed) t.Destroy();
            }
            heldIngredients.Clear();
            state = PadState.Assembling;
            ticksLeft = AssembleDurationTicks;
            Messages.Message($"运载火箭开始组装，预计耗时 {AssembleDurationTicks / 2500f:0.#} 小时。",
                MessageTypeDefOf.PositiveEvent, historical: false);
        }

        private void TryFinishLoadingIfComplete()
        {
            if (LoadingComplete) FinishLoading();
        }

        private void DropHeldIngredients()
        {
            foreach (var t in heldIngredients)
            {
                if (t == null || t.Destroyed) continue;
                IntVec3 pos = Position;
                if (!GenPlace.TryPlaceThing(t, pos, Map, ThingPlaceMode.Near))
                    t.Destroy();
            }
            heldIngredients.Clear();
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (var g in base.GetGizmos())
                yield return g;

            switch (state)
            {
                case PadState.Empty:
                    yield return MakeStartCommand();
                    break;
                case PadState.Loading:
                    yield return MakeAbortLoadingCommand();
                    break;
                case PadState.Assembling:
                    yield return MakeAbortCommand();
                    break;
                case PadState.Ready:
                    yield return MakeScrapCommand();
                    break;
            }
        }

        private Command_Action MakeStartCommand()
        {
            var cmd = new Command_Action
            {
                defaultLabel = "组装运载火箭",
                defaultDesc = "开始装载并组装运载火箭。\n"
                              + $"需要装入：{CostLabel()}\n"
                              + "小人会把物资搬进发射台，装齐后一次性扣除并开始组装。",
                icon = ContentFinder<Texture2D>.Get("UI/Commands/StarWarKiiro_LaunchRocket"),
                action = () =>
                {
                    state = PadState.Loading;
                    Messages.Message("开始装载运载火箭物资，请指派小人进行「装载」工作。",
                        MessageTypeDefOf.PositiveEvent, historical: false);
                }
            };

            if (Faction == null || !Faction.IsPlayer)
                cmd.Disable("发射台不属于你");
            else if (GetComp<CompPowerTrader>() is { PowerOn: false })
                cmd.Disable("未通电");

            return cmd;
        }

        private Command_Action MakeAbortLoadingCommand()
        {
            return new Command_Action
            {
                defaultLabel = "取消装载",
                defaultDesc = "取消装载，已放入的物资会丢在发射台旁。",
                icon = ContentFinder<Texture2D>.Get("UI/Designators/Cancel"),
                action = () =>
                {
                    DropHeldIngredients();
                    state = PadState.Empty;
                    Messages.Message("已取消运载火箭装载。", MessageTypeDefOf.NeutralEvent, historical: false);
                }
            };
        }

        private Command_Action MakeAbortCommand()
        {
            return new Command_Action
            {
                defaultLabel = "中止组装",
                defaultDesc = "中止当前组装流程。\n已消耗的物资不会返还。",
                icon = ContentFinder<Texture2D>.Get("UI/Designators/Cancel"),
                action = () =>
                {
                    state = PadState.Empty;
                    ticksLeft = 0;
                    Messages.Message("运载火箭组装已中止。", MessageTypeDefOf.NeutralEvent, historical: false);
                }
            };
        }

        private Command_Action MakeScrapCommand()
        {
            return new Command_Action
            {
                defaultLabel = "拆解运载火箭",
                defaultDesc = "将组装好的运载火箭就地拆解，清空发射台。",
                icon = ContentFinder<Texture2D>.Get("UI/Designators/Deconstruct"),
                action = () =>
                {
                    state = PadState.Empty;
                    ticksLeft = 0;
                    Messages.Message("运载火箭已拆解。", MessageTypeDefOf.NeutralEvent, historical: false);
                }
            };
        }

        public override string GetInspectString()
        {
            var sb = new StringBuilder(base.GetInspectString());
            switch (state)
            {
                case PadState.Empty:
                    sb.AppendLine("运载火箭：未组装");
                    sb.Append("通过「组装运载火箭」开始装载。");
                    break;
                case PadState.Loading:
                {
                    sb.AppendLine("运载火箭：装载中");
                    foreach (var (def, count) in GetAssembleCost())
                    {
                        int have = heldIngredients.Where(t => t != null && t.def == def).Sum(t => t.stackCount);
                        // 找不到货源时直接标出来,免得不知道卡在哪一步
                        string tail = have < count && !IngredientOnMap(def) ? "（地图上找不到）" : "";
                        sb.AppendLine($"  {def.LabelCap} {have}/{count}{tail}");
                    }
                    sb.Append("装齐后自动扣除并开始组装。");
                    break;
                }
                case PadState.Assembling:
                {
                    float p = 1f - (float)ticksLeft / AssembleDurationTicks;
                    sb.AppendLine($"运载火箭：组装中（{p:P0}）");
                    sb.Append($"剩余约 {ticksLeft / 2500f:0.#} 小时");
                    break;
                }
                case PadState.Ready:
                    sb.Append("运载火箭：已就绪，可在火箭控制台下达发射指令。");
                    break;
            }
            return sb.ToString();
        }
    }
}
