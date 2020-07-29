using System;
using RimWorld;
using Verse;
using Verse.AI;

namespace AchtungMod.Components
{
    public class AchtungMentalCompProperties : CompProperties
    {
        public AchtungMentalCompProperties()
        {
            compClass = typeof(AchtungMentalComp);
        }
    }

    public class AchtungMentalComp : ThingComp
    {
        private bool derefrenced = false;
        private Pawn pawn = null;

        public override void CompTickRare()
        {
            if (derefrenced) return;

            if (pawn == null) pawn = (Pawn) parent;

            if (!pawn.IsColonist || pawn.Dead)
            {
                derefrenced = true;
                return;
            }

            if (pawn.mindState.mentalBreaker.BreakExtremeIsApproaching ||
                pawn.mindState.mentalBreaker.BreakMajorIsImminent)
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