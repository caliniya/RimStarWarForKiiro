using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace StarWarKiiro
{
    // 必须带 [DefOf],启动时才会自动绑定 JobDef;否则 job.def 为 null,小人接单即崩
    [DefOf]
    public static class JobDefOf_StarWarKiiro
    {
        public static JobDef StarWarKiiro_LoadLaunchPad;

        static JobDefOf_StarWarKiiro()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(JobDefOf_StarWarKiiro));
        }
    }

    // 把指定物资搬到火箭发射台并放入台体
    public class JobDriver_LoadLaunchPad : JobDriver
    {
        private Thing HaulThing => job.targetA.Thing;
        private Building_RocketLaunchPad Pad => job.targetB.Thing as Building_RocketLaunchPad;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            var haul = HaulThing;
            var pad = Pad;
            if (haul == null || pad == null) return false;
            // 物资独占;发射台一次只让一名小人装载。
            // 注意:发射台是非堆叠物(stackCount 恒为 1),Reserve 的 stackCount 参数必须传 -1,
            // 传 8 会让 ReservationManager 判定"要预留的数量超过实际存在数量"而永远失败
            return pawn.Reserve(haul, job, 1, -1, null, errorOnFailed)
                   && pawn.Reserve(pad, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            // 注意:拾取后 targetA 会被 DeSpawn,不能用 FailOnDespawnedNullOrForbidden(A)
            this.FailOnDestroyedOrNull(TargetIndex.A);
            this.FailOnDestroyedOrNull(TargetIndex.B);
            this.FailOn(() => Pad is not { State: Building_RocketLaunchPad.PadState.Loading });

            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch);
            yield return Toils_Haul.StartCarryThing(TargetIndex.A, putRemainderInQueue: false,
                subtractNumTakenFromJobCount: true);
            yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.Touch);
            yield return Toils_General.Do(() =>
            {
                var carried = pawn.carryTracker?.CarriedThing;
                var pad = Pad;
                if (carried != null && pad != null)
                    pad.ReceiveIngredient(carried);
            });
            // 物资栈比需求大时会剩下一截,卸回地面,别让小人一直揣着
            yield return Toils_General.Do(() =>
            {
                var ct = pawn.carryTracker;
                if (ct != null && ct.CarriedThing != null)
                    ct.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out _);
            });
        }
    }

    // Loading 状态下:给小人派活,把缺的物资搬进发射台
    public class WorkGiver_LoadLaunchPad : WorkGiver_Scanner
    {
        public override ThingRequest PotentialWorkThingRequest =>
            ThingRequest.ForDef(StarWarKiiroDefOf.StarWarKiiro_RocketLaunchPad);

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (t is not Building_RocketLaunchPad pad) return null;
            if (pad.State != Building_RocketLaunchPad.PadState.Loading) return null;
            if (pad.Faction != pawn.Faction) return null;
            if (JobDefOf_StarWarKiiro.StarWarKiiro_LoadLaunchPad == null) return null;

            var missing = pad.NextMissingIngredient();
            if (missing == null) return null;

            Thing found = GenClosest.ClosestThing_Global_Reachable(
                pawn.Position,
                pawn.Map,
                pad.Map.listerThings.ThingsOfDef(missing.Value.def),
                PathEndMode.ClosestTouch,
                TraverseParms.For(pawn),
                9999f,
                thing => !thing.IsForbidden(pawn)
                         && !thing.IsBurning()
                         && thing.stackCount > 0
                         && pawn.CanReserve(thing));

            if (found == null) return null;
            // 先确认发射台没被别的小人占住;同样不能用 (pad, 1, 8) 这种写法
            if (!pawn.CanReserve(pad)) return null;

            var job = JobMaker.MakeJob(JobDefOf_StarWarKiiro.StarWarKiiro_LoadLaunchPad, found, pad);
            job.count = Math.Min(found.stackCount, missing.Value.needed);
            return job;
        }
    }
}
