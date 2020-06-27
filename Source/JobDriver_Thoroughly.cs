﻿using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;

namespace AchtungMod
{
    public abstract class JobDriver_Thoroughly : JobDriver
    {
        public HashSet<IntVec3> workLocations = null;
        public LocalTargetInfo currentItem = null;
        public bool isMoving = false;
        public float subCounter = 0;
        public float currentWorkCount = -1f;
        public float totalWorkCount = -1f;

        public virtual string GetPrefix()
        {
            return "DoThoroughly";
        }

        public virtual EffecterDef GetWorkIcon()
        {
            return null;
        }

        public string GetLabel()
        {
            return (GetPrefix() + "Label").Translate();
        }

        public JobDef MakeDef()
        {
            var def = new JobDef
            {
                driverClass = GetType(),
                collideWithPawns = false,
                defName = GetPrefix(),
                label = GetLabel(),
                reportString = (GetPrefix() + "InfoText").Translate(),
                description = (GetPrefix() + "Description").Translate(),
                playerInterruptible = true,
                checkOverrideOnDamage = CheckJobOverrideOnDamageMode.Always,
                suspendable = true,
                alwaysShowWeapon = false,
                neverShowWeapon = true,
                casualInterruptible = true
            };
            return def;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref workLocations, "workLocations");
            Scribe_Values.Look(ref isMoving, "isMoving", false, false);
            Scribe_Values.Look(ref subCounter, "subCounter", 0, false);
            Scribe_Values.Look(ref currentWorkCount, "currentWorkCount", -1f, false);
            Scribe_Values.Look(ref totalWorkCount, "totalWorkCount", -1f, false);
        }

        public virtual IEnumerable<LocalTargetInfo> CanStart(Pawn thePawn, LocalTargetInfo clickCell)
        {
            pawn = thePawn;
            return null;
        }

        public List<Job> SameJobTypesOngoing()
        {
            var jobs = new List<Job>();
            if (pawn.jobs == null) return jobs;
            var queue = pawn.jobs.jobQueue;
            if (queue == null) return jobs;
            for (var i = -1; i < queue.Count; i++)
            {
                var aJob = i == -1 ? pawn.CurJob : queue[i].job;
                if (aJob?.def.driverClass.IsInstanceOfType(this) ?? false)
                    jobs.Add(aJob);
            }
            return jobs;
        }

        public void StartJob(Pawn targetPawn, LocalTargetInfo target, LocalTargetInfo extra)
        {
            var newJob = JobMaker.MakeJob(MakeDef(), target, extra);
            newJob.playerForced = true;
            _ = targetPawn.jobs.TryTakeOrderedJob(newJob);
        }

        public float Progress()
        {
            if (currentWorkCount <= 0f || totalWorkCount <= 0f) return 0f;
            return (totalWorkCount - currentWorkCount) / totalWorkCount;
        }

        public virtual void UpdateVerbAndWorkLocations()
        {
        }

        public virtual LocalTargetInfo FindNextWorkItem()
        {
            return null;
        }

        public override void Notify_PatherArrived()
        {
            isMoving = false;
        }

        public virtual void InitAction()
        {
            workLocations = new HashSet<IntVec3>() { TargetA.Cell };
            currentItem = null;
            isMoving = false;
            subCounter = 0;
            currentWorkCount = -1f;
            totalWorkCount = -1f;
        }

        public virtual bool DoWorkToItem()
        {
            return true;
        }

        public virtual void CleanupLastItem()
        {
        }

        public bool CurrentItemInvalid()
        {
            return
                currentItem == null ||
                (currentItem.HasThing && currentItem.Thing.Destroyed) ||
                currentItem.Cell.IsValid == false ||
                (currentItem.Cell.x == 0 && currentItem.Cell.z == 0);
        }

        public void CheckJobCancelling()
        {
            if (pawn.Dead || pawn.Downed || pawn.HasAttachment(ThingDefOf.Fire))
            {
                pawn.Map.pawnDestinationReservationManager.ReleaseAllClaimedBy(pawn);
                EndJobWith(JobCondition.Incompletable);
                return;
            }

            if (Tools.PawnOverHealthLevel(pawn))
            {
                pawn.Map.pawnDestinationReservationManager.ReleaseAllClaimedBy(pawn);
                EndJobWith(JobCondition.Incompletable);
                var jobName = (GetPrefix() + "Label").Translate();
                Find.LetterStack.ReceiveLetter(LetterMaker.MakeLetter("JobInterruptedLabel".Translate(jobName), "JobInterruptedBadHealth".Translate(pawn.Name.ToStringShort), LetterDefOf.NegativeEvent, pawn));
                return;
            }
        }

        public void TickAction()
        {
            CheckJobCancelling();
            UpdateVerbAndWorkLocations();

            if (CurrentItemInvalid())
            {
                currentItem = FindNextWorkItem();
                if (CurrentItemInvalid() == false)
                {
                    pawn.Map.reservationManager.Reserve(pawn, job, currentItem);
                    pawn.CurJob?.SetTarget(TargetIndex.A, currentItem);
                }
            }
            if (CurrentItemInvalid())
            {
                EndJobWith(JobCondition.Succeeded);
                return;
            }

            if (pawn.Position.AdjacentTo8WayOrInside(currentItem))
            {
                var itemCompleted = DoWorkToItem();
                if (itemCompleted) currentItem = null;
            }
            else if (!isMoving)
            {
                pawn.pather.StartPath(currentItem, PathEndMode.Touch);
                isMoving = true;
            }
        }

        public override string GetReport()
        {
            return (GetPrefix() + "Report").Translate(Math.Floor(Progress() * 100f) + "%");
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            var effecterProgresBar = EffecterDefOf.ProgressBar.Spawn();
            var effecterWorkIcon = GetWorkIcon()?.Spawn();

            var toil = new Toil
            {
                initAction = InitAction,
                tickAction = TickAction
            };

            toil.AddPreTickAction(delegate
            {
                effecterProgresBar.EffectTick(toil.actor, TargetInfo.Invalid);
                var mote = ((SubEffecter_ProgressBar)effecterProgresBar.children[0]).mote;
                if (mote != null)
                {
                    mote.progress = Mathf.Clamp01(Progress());
                    mote.Position = toil.actor.Position;
                    mote.offsetZ = -1.1f;
                }

                if (effecterWorkIcon != null)
                {
                    effecterWorkIcon.EffectTick(toil.actor, TargetInfo.Invalid);
                    var interactSymbol = (SubEffecter_InteractSymbol)effecterWorkIcon.children[0];
                    var dualMode = Traverse.Create(interactSymbol).Field("interactMote").GetValue<MoteDualAttached>();
                    dualMode.Attach(toil.actor, currentItem.ToTargetInfo(toil.actor.Map));
                }
            });

            toil.defaultCompleteMode = ToilCompleteMode.Never;
            toil.AddFinishAction(CleanupLastItem);

            toil.AddFinishAction(delegate
            {
                if (effecterProgresBar != null)
                {
                    effecterProgresBar.Cleanup();
                    effecterProgresBar = null;
                }
                if (effecterWorkIcon != null)
                {
                    effecterWorkIcon.Cleanup();
                    effecterWorkIcon = null;
                }
            });

            yield return toil;
        }
    }
}