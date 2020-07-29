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
<<<<<<< HEAD
            compClass = typeof(AchtungMentalComp);
=======
            this.compClass = typeof(AchtungMentalComp);
>>>>>>> dcbc746fc2ff73b0938ab79d41ae99e2f6c0f5aa
        }
    }

    public class AchtungMentalComp : ThingComp
    {
        private bool derefrenced = false;
        private Pawn pawn = null;

        public override void CompTickRare()
        {
<<<<<<< HEAD
            if (derefrenced) return;

            if (pawn == null) pawn = (Pawn) parent;

            if (!pawn.IsColonist || pawn.Dead)
            {
                derefrenced = true;
                return;
            }

            if (pawn.mindState.mentalBreaker.BreakExtremeIsApproaching ||
                pawn.mindState.mentalBreaker.BreakMajorIsImminent)
=======
            if (derefrenced) { return; }

            if (pawn == null) { pawn = (Pawn)this.parent; }

            if (!pawn.IsColonist || pawn.Dead) { derefrenced = true; return; }

            if (pawn.mindState.mentalBreaker.BreakExtremeIsApproaching || pawn.mindState.mentalBreaker.BreakMajorIsImminent)
>>>>>>> dcbc746fc2ff73b0938ab79d41ae99e2f6c0f5aa
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
<<<<<<< HEAD
}
=======
}
>>>>>>> dcbc746fc2ff73b0938ab79d41ae99e2f6c0f5aa
