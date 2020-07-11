using System;
using RimWorld;
using Verse;
using Verse.AI;

namespace AchtungMod.Components
{
    public class AchtungHealthCompProperties : CompProperties
    {
        public AchtungHealthCompProperties()
        {
            this.compClass = typeof(AchtungHealthComp);
        }
    }

    public class AchtungHealthComp : ThingComp
    {
        private bool derefrenced = false;
        private Pawn pawn = null;

        public override void CompTickRare()
        {
            if (derefrenced) { return; }

            if (pawn == null) { pawn = (Pawn)this.parent; }

            if (!pawn.IsColonist || pawn.Dead) { derefrenced = true; return; }

            if ((pawn.health.hediffSet.HasTendableInjury() && pawn.health.HasHediffsNeedingTendByPlayer()) || pawn.needs.food.Starving || pawn.health.hediffSet.HasTemperatureInjury(TemperatureInjuryStage.Serious))
                RemoveForcedWork(pawn);
        }

        public static void RemoveForcedWork(Pawn p)
        {
            var forcedWork = Find.World.GetComponent<ForcedWork>();

            if (forcedWork == null)
                return;

            if (forcedWork.HasForcedJob(p))
            {
                p.Map.pawnDestinationReservationManager.ReleaseAllClaimedBy(p);
                forcedWork.Remove(p);
                p.jobs?.EndCurrentJob(JobCondition.InterruptForced, true);
            }
        }
    }
}
