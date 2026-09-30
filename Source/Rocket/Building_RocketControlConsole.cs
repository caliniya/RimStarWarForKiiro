using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace StarWarKiiro
{
    // 火箭控制台:下达发射指令的指挥终端。
    // 真正的发射流程待实装;当前只做界面与前置校验。
    public class Building_RocketControlConsole : Building
    {
        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (var g in base.GetGizmos())
                yield return g;

            yield return MakeLaunchCommand();
            yield return MakeAbortCommand();
        }

        private Command_Action MakeLaunchCommand()
        {
            var cmd = new Command_Action
            {
                defaultLabel = "下达发射指令",
                defaultDesc = LaunchDesc(),
                icon = ContentFinder<Texture2D>.Get("UI/Commands/StarWarKiiro_LaunchRocket"),
                action = TryLaunch
            };

            if (!Spawned)
            {
                cmd.Disable("控制台尚未就位");
            }
            else if (Faction == null || !Faction.IsPlayer)
            {
                cmd.Disable("控制台不属于你");
            }
            else if (!StarWarKiiroDefOf.IsFinished(StarWarKiiroDefOf.StarWarKiiro_BasicOrbitalResearch))
            {
                cmd.Disable("尚未完成「基础轨道研究技术」");
            }
            else if (GetComp<CompPowerTrader>() is { PowerOn: false })
            {
                cmd.Disable("未通电");
            }
            else if (FindReadyPads().Count == 0)
            {
                cmd.Disable("没有已组装完成的运载火箭\n请先在发射台上组装");
            }

            return cmd;
        }

        private Command_Action MakeAbortCommand()
        {
            return new Command_Action
            {
                defaultLabel = "中止发射",
                defaultDesc = "取消当前发射流程。\n（发射流程待实装）",
                icon = ContentFinder<Texture2D>.Get("UI/Designators/Cancel"),
                action = () =>
                {
                    Messages.Message("发射流程已中止（发射功能待实现）。",
                        MessageTypeDefOf.NeutralEvent, historical: false);
                }
            };
        }

        private static string LaunchDesc()
        {
            return "向火箭下达发射指令。\n"
                 + "· 需要完成「基础轨道研究技术」\n"
                 + "· 需要有发射台上的火箭处于「已就绪」\n"
                 + "· 真正的点火升空流程待实装";
        }

        private void TryLaunch()
        {
            if (!StarWarKiiroDefOf.IsFinished(StarWarKiiroDefOf.StarWarKiiro_BasicOrbitalResearch))
            {
                Messages.Message("发射需要「基础轨道研究技术」。",
                    MessageTypeDefOf.RejectInput, historical: false);
                return;
            }

            var pads = FindReadyPads();
            if (pads.Count == 0)
            {
                Messages.Message("没有已组装完成的运载火箭，请先在发射台上组装。",
                    MessageTypeDefOf.RejectInput, historical: false);
                return;
            }

            // 占位:真正升空、入轨与载荷处理待实装
            Messages.Message(
                $"已向 {pads.Count} 枚运载火箭下达发射指令。（点火升空功能待实现）",
                MessageTypeDefOf.PositiveEvent, historical: false);
        }

        private List<Building_RocketLaunchPad> FindReadyPads()
        {
            if (Map == null) return new List<Building_RocketLaunchPad>();
            return Map.listerBuildings.AllBuildingsColonistOfClass<Building_RocketLaunchPad>()
                .Where(p => p.Faction == Faction && p.RocketReady)
                .ToList();
        }
    }
}
