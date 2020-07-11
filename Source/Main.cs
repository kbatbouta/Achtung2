﻿using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using UnityEngine.Playables;
using Verse;
using Verse.AI;

namespace AchtungMod
{
    [StaticConstructorOnStartup]
    public static class AchtungLoader
    {
        public static bool IsSameSpotInstalled;

        static AchtungLoader()
        {
            Controller.InstallDefs();
            Initialization.Init();

            Log.Message("Achtung Init");

            var harmony = new Harmony("net.pardeike.rimworld.mods.achtung");
            harmony.PatchAll();

            const string sameSpotId = "net.pardeike.rimworld.mod.samespot";
            IsSameSpotInstalled = harmony.GetPatchedMethods()
                .Any(method => Harmony.GetPatchInfo(method).Transpilers.Any(transpiler => transpiler.owner == sameSpotId));

            // TODO: multiplayer
            /*if (MP.enabled)
			{
				MP.RegisterAll();
				MP.RegisterSyncWorker<Vector3>(Vector3Support);
				MP.RegisterSyncWorker<Type>(TypeSupport);
			}*/
        }

        // TODO: multiplayer
        /*
		static void Vector3Support(SyncWorker sync, ref Vector3 value)
		{
			sync.Bind(ref value.x);
			sync.Bind(ref value.y);
			sync.Bind(ref value.z);
		}

		static void TypeSupport(SyncWorker sync, ref Type value)
		{
			if (sync.isWriting)
				sync.Write(value.FullName);
			else
				value = AccessTools.TypeByName(sync.Read<string>());
		}
		*/
    }

    public class Achtung : Mod
    {
        public static AchtungSettings Settings;

        public Achtung(ModContentPack content) : base(content)
        {
            Settings = GetSettings<AchtungSettings>();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            AchtungSettings.DoWindowContents(inRect);
        }

        public override string SettingsCategory()
        {
            return "Achtung!";
        }
    }

    /* DEBUG
	[HarmonyPatch]
	static class DEBUG
	{
		public static IEnumerable<MethodBase> TargetMethods()
		{
			yield break;
		}

		static MethodBase GetOutsideCaller(int n)
		{
			var frames = new StackTrace(fNeedFileInfo: true).GetFrames();
			return frames[n].GetMethod();
		}

		public static void Postfix()
		{
		}
	}*/


    // build-in "Ignore Me Passing" functionality
    //
    [HarmonyPatch(typeof(GenConstruct))]
    [HarmonyPatch(nameof(GenConstruct.BlocksConstruction))]
    static class GenConstruct_BlocksConstruction_Patch
    {
        public static bool Prefix(ref bool __result, Thing t)
        {
            if (t is Pawn)
            {
                __result = false;
                return false;
            }
            return true;
        }
    }

    // forced hauling outside of allowed area
    //
    [HarmonyPatch(typeof(HaulAIUtility))]
    [HarmonyPatch(nameof(HaulAIUtility.PawnCanAutomaticallyHaulFast))]
    static class HaulAIUtility_PawnCanAutomaticallyHaulFast_Patch
    {
        public static bool Prefix(Pawn p, bool forced, ref bool __result)
        {
            if (Achtung.Settings.ignoreRestrictions)
            {
                var forcedWork = Find.World.GetComponent<ForcedWork>();
                if (forced || forcedWork.HasForcedJob(p))
                {
                    __result = true;
                    return false;
                }
            }
            return true;
        }
    }

    // forced repair outside of allowed area
    //
    [HarmonyPatch(typeof(WorkGiver_Repair))]
    [HarmonyPatch(nameof(WorkGiver_Repair.HasJobOnThing))]
    static class WorkGiver_Repair_HasJobOnThing_Patch
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var instr = instructions.ToList();

            var f_NotInHomeAreaTrans = AccessTools.Field(typeof(WorkGiver_FixBrokenDownBuilding), "NotInHomeAreaTrans");
            if (f_NotInHomeAreaTrans == null) throw new Exception("Cannot find method WorkGiver_FixBrokenDownBuilding.NotInHomeAreaTrans");
            var i = instr.FindIndex(inst => inst.LoadsField(f_NotInHomeAreaTrans));
            if (i > 0)
            {
                object label = null;
                for (var j = i - 1; j >= 0; j--)
                    if (instr[j].opcode == OpCodes.Brtrue || instr[j].opcode == OpCodes.Brtrue_S)
                    {
                        label = instr[j].operand;
                        break;
                    }
                if (label != null)
                {
                    instr.Insert(i++, new CodeInstruction(OpCodes.Ldarg_3));
                    instr.Insert(i++, new CodeInstruction(OpCodes.Brtrue, label));
                }
                else
                    Log.Error("Cannot find Brtrue before NotInHomeAreaTrans");
            }
            else
                Log.Error("Cannot find ldsfld RimWorld.WorkGiver_FixBrokenDownBuilding::NotInHomeAreaTrans");

            foreach (var inst in instr)
                yield return inst;
        }
    }

    [HarmonyPatch(typeof(ReservationManager))]
    [HarmonyPatch(nameof(ReservationManager.Reserve))]
    static class ReservationManager_Reserve_Patch
    {
        public static bool CanReserve(ReservationManager reservationManager, Pawn claimant, LocalTargetInfo target, int maxPawns, int stackCount, ReservationLayerDef layer, bool ignoreOtherReservations)
        {
            if (ignoreOtherReservations)
            {
                var forcedWork = Find.World.GetComponent<ForcedWork>();
                if (forcedWork.HasForcedJob(claimant))
                    return false;
            }
            return reservationManager.CanReserve(claimant, target, maxPawns, stackCount, layer, ignoreOtherReservations);
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var fromMethod = AccessTools.Method(typeof(ReservationManager), nameof(ReservationManager.CanReserve));
            var toMethod = AccessTools.Method(typeof(ReservationManager_Reserve_Patch), nameof(ReservationManager_Reserve_Patch.CanReserve));
            return instructions.MethodReplacer(fromMethod, toMethod);
        }
    }

    // teleporting does not end current job with error condition
    //
    [HarmonyPatch(typeof(Pawn_PathFollower))]
    [HarmonyPatch(nameof(Pawn_PathFollower.TryRecoverFromUnwalkablePosition))]
    static class Pawn_PathFollower_TryRecoverFromUnwalkablePosition_Patch
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var m_Notify_Teleported = AccessTools.Method(typeof(Pawn), nameof(Pawn.Notify_Teleported));
            var list = instructions.ToList();
            var idx = list.FindIndex(code => code.Calls(m_Notify_Teleported));
            if (idx > 0)
            {
                if (list[idx - 2].opcode == OpCodes.Ldc_I4_1)
                    list[idx - 2].opcode = OpCodes.Ldarg_1;
                else
                    Log.Error("Cannot find Ldc_I4_1 before Pawn.Notify_Teleported in Pawn_PathFollower.TryRecoverFromUnwalkablePosition");
            }
            else
                Log.Error("Cannot find Pawn.Notify_Teleported in Pawn_PathFollower.TryRecoverFromUnwalkablePosition");

            foreach (var instruction in list)
                yield return instruction;
        }
    }

    // for forced jobs, do not find work "on the way" to the work cell
    //
    [HarmonyPatch(typeof(WorkGiver_ConstructDeliverResources))]
    [HarmonyPatch("FindNearbyNeeders")]
    static class WorkGiver_ConstructDeliverResources_FindNearbyNeeders_Patch
    {
        public static IEnumerable<Thing> RadialDistinctThingsAround_Patch(IntVec3 center, Map map, float radius, bool useCenter, Pawn pawn)
        {
            var forcedWork = Find.World.GetComponent<ForcedWork>();
            var forcedJob = forcedWork.GetForcedJob(pawn);
            if (forcedJob != null && forcedJob.isThingJob)
            {
                foreach (var thing1 in forcedJob.GetUnsortedTargets())
                    yield return thing1;
            }
            else
            {
                foreach (var thing2 in GenRadial.RadialDistinctThingsAround(center, map, radius, useCenter))
                    yield return thing2;
            }
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var m_RadialDistinctThingsAround = AccessTools.Method(typeof(GenRadial), nameof(GenRadial.RadialDistinctThingsAround));
            var m_RadialDistinctThingsAround_Patch = AccessTools.Method(typeof(WorkGiver_ConstructDeliverResources_FindNearbyNeeders_Patch), "RadialDistinctThingsAround_Patch");
            if (m_RadialDistinctThingsAround_Patch == null) throw new Exception("Cannot find method WorkGiver_ConstructDeliverResources_FindNearbyNeeders_Patch.m_RadialDistinctThingsAround_Patch");

            var found = 0;
            var list = instructions.ToList();
            var count = list.Count;
            var idx = 0;
            while (idx < count)
            {
                if (list[idx].Calls(m_RadialDistinctThingsAround))
                {
                    list[idx].opcode = OpCodes.Call;
                    list[idx].operand = m_RadialDistinctThingsAround_Patch;

                    // add extra 'pawn' before CALL (extra last argument on our method)
                    list.Insert(idx, new CodeInstruction(OpCodes.Ldarg_1));
                    idx++;
                    count++;
                    found++;
                }
                idx++;
            }
            if (found != 2)
                Log.Error("Cannot find both calls to RadialDistinctThingsAround in WorkGiver_ConstructDeliverResources.FindNearbyNeeders");

            foreach (var instruction in list)
                yield return instruction;
        }
    }

    // patch in our menu options
    //
    [HarmonyPatch(typeof(FloatMenuMakerMap))]
    [HarmonyPatch("AddJobGiverWorkOrders")]
    static class FloatMenuMakerMap_AddJobGiverWorkOrders_Patch
    {
        public static void Prefix(Pawn pawn, out ForcedWork __state)
        {
            __state = Find.World.GetComponent<ForcedWork>();
            __state.Prepare(pawn);
        }

        public static void Postfix(Pawn pawn, ForcedWork __state)
        {
            __state.Unprepare(pawn);
        }

        public static int GetPriority(Pawn pawn, WorkTypeDef w)
        {
            if (Achtung.Settings.ignoreAssignments)
                return pawn.WorkTypeIsDisabled(w) ? 0 : 1;
            return pawn.workSettings.GetPriority(w);
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var m_GetPriority = AccessTools.Method(typeof(Pawn_WorkSettings), nameof(Pawn_WorkSettings.GetPriority));
            var c_FloatMenuOption = AccessTools.FirstConstructor(typeof(FloatMenuOption), c => c.GetParameters().Count() > 1);
            var m_ForcedFloatMenuOption = AccessTools.Method(typeof(ForcedFloatMenuOption), nameof(ForcedFloatMenuOption.CreateForcedMenuItem));

            var list = instructions.ToList();

            var foundCount = 0;
            while (true)
            {
                var idx = list.FindIndex(instr => instr.Calls(m_GetPriority));
                if (idx < 2 || idx >= list.Count)
                    break;
                foundCount++;
                list[idx - 2].opcode = OpCodes.Nop;
                list[idx].opcode = OpCodes.Call;
                list[idx].operand = SymbolExtensions.GetMethodInfo(() => GetPriority(null, WorkTypeDefOf.Doctor));
            }

            if (foundCount != 2)
                Log.Error("Cannot find 2x Pawn_WorkSettings.GetPriority in RimWorld.FloatMenuMakerMap::AddJobGiverWorkOrders");

            foundCount = 0;
            Enumerable.Range(0, list.Count)
                .DoIf(i => list[i].opcode == OpCodes.Isinst && (Type)list[i].operand == typeof(WorkGiver_Scanner), i =>
                {
                    var index = i + 1;
                    if (list[index].opcode == OpCodes.Stloc_S)
                    {
                        var localVar = list[index].operand;

                        index = list.FindIndex(index, code => code.opcode == OpCodes.Newobj && (ConstructorInfo)code.operand == c_FloatMenuOption);
                        if (index < 0)
                            Log.Error("Cannot find 'Isinst WorkGiver_Scanner' in RimWorld.FloatMenuMakerMap::AddJobGiverWorkOrders");
                        else
                        {
                            list[index].opcode = OpCodes.Call;
                            list[index].operand = m_ForcedFloatMenuOption;
                            list.InsertRange(index, new CodeInstruction[]
                            {
                                new CodeInstruction(OpCodes.Ldarg_1),
                                new CodeInstruction(OpCodes.Ldarg_0),
                                new CodeInstruction(OpCodes.Ldloc_S, localVar)
                            });

                            foundCount++;
                        }
                    }
                });

            if (foundCount != 2)
                Log.Error("Cannot find 2x 'Isinst WorkGiver_Scanner', 'Stloc_S n' -> 'Newobj FloatMenuOption()' in RimWorld.FloatMenuMakerMap::AddJobGiverWorkOrders");

            foreach (var instruction in list)
                yield return instruction;
        }
    }

    [HarmonyPatch(typeof(Pawn_JobTracker))]
    [HarmonyPatch(nameof(Pawn_JobTracker.EndCurrentJob))]
    static class Pawn_JobTracker_EndCurrentJob_Patch
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var m_CleanupCurrentJob = AccessTools.Method(typeof(Pawn_JobTracker), "CleanupCurrentJob");
            var m_ContinueJob = AccessTools.Method(typeof(ForcedJob), nameof(ForcedJob.ContinueJob));
            var f_pawn = AccessTools.Field(typeof(Pawn_JobTracker), "pawn");
            var f_curJob = AccessTools.Field(typeof(Pawn_JobTracker), "curJob");

            if (m_CleanupCurrentJob == null) throw new Exception("Cannot find method Pawn_JobTracker.CleanupCurrentJob");
            if (f_pawn == null) throw new Exception("Cannot find field Pawn_JobTracker.pawn");

            var instrList = instructions.ToList();
            for (var i = 0; i < instrList.Count; i++)
            {
                var instruction = instrList[i];
                yield return instruction;

                if (instruction.OperandIs(m_CleanupCurrentJob) == false)
                    continue;

                if (instrList[i + 1].opcode != OpCodes.Ldarg_2 || (instrList[i + 2].opcode != OpCodes.Brfalse && instrList[i + 2].opcode != OpCodes.Brfalse_S))
                {
                    Log.Error("Unexpected opcodes while transpiling Pawn_JobTracker.EndCurrentJob");
                    continue;
                }

                var endLabel = instrList[i + 2].operand;

                yield return instrList[++i];
                yield return instrList[++i];

                yield return new CodeInstruction(OpCodes.Ldarg_0);
                yield return new CodeInstruction(OpCodes.Ldarg_0);
                yield return new CodeInstruction(OpCodes.Ldfld, f_curJob);
                yield return new CodeInstruction(OpCodes.Ldarg_0);
                yield return new CodeInstruction(OpCodes.Ldfld, f_pawn);
                yield return new CodeInstruction(OpCodes.Ldarg_1);
                yield return new CodeInstruction(OpCodes.Call, m_ContinueJob);

                yield return new CodeInstruction(OpCodes.Brtrue, endLabel);
            }
        }
    }

    // release forced work when pawn disappears
    //
    [HarmonyPatch(typeof(Pawn))]
    [HarmonyPatch(nameof(Pawn.DeSpawn))]
    static class Pawn_DeSpawn_Patch
    {
        public static void Postfix(Pawn __instance)
        {
            var forcedWork = Find.World.GetComponent<ForcedWork>();
            forcedWork.Remove(__instance);
        }
    }

    // add force radius buttons
    //
    [HarmonyPatch(typeof(PriorityWork))]
    [HarmonyPatch(nameof(PriorityWork.GetGizmos))]
    [StaticConstructorOnStartup]
    static class PriorityWork_GetGizmos_Patch
    {
        public static readonly Texture2D ForceRadiusExpand = ContentFinder<Texture2D>.Get("ForceRadiusExpand", true);
        public static readonly Texture2D ForceRadiusShrink = ContentFinder<Texture2D>.Get("ForceRadiusShrink", true);
        public static readonly Texture2D ForceRadiusShrinkOff = ContentFinder<Texture2D>.Get("ForceRadiusShrinkOff", true);

        // TODO: multiplayer
        //[SyncMethod]
        public static void ActionSynced(Pawn pawn, int delta)
        {
            var forcedWork = Find.World.GetComponent<ForcedWork>();
            var forcedJob = forcedWork.GetForcedJob(pawn);
            if (forcedJob != null && forcedJob.cellRadius + delta >= 0)
                forcedJob.ChangeCellRadius(delta);
        }

        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> gizmos, Pawn ___pawn)
        {
            var gizmoList = gizmos.ToList();
            foreach (var gizmo in gizmos)
                yield return gizmo;

            var forcedWork = Find.World.GetComponent<ForcedWork>();
            var forcedJob = forcedWork.GetForcedJob(___pawn);
            if (forcedJob == null)
                yield break;

            var radius = forcedJob.cellRadius;

            yield return new Command_Action
            {
                defaultLabel = "IncreaseForceRadius".Translate(),
                defaultDesc = "IncreaseForceRadiusDesc".Translate(radius),
                icon = ForceRadiusExpand,
                activateSound = SoundDefOf.Designate_ZoneAdd,
                action = delegate { ActionSynced(___pawn, 1); }
            };

            yield return new Command_Action
            {
                defaultLabel = "DecreaseForceRadius".Translate(),
                defaultDesc = "DecreaseForceRadiusDesc".Translate(radius),
                icon = radius > 0 ? ForceRadiusShrink : ForceRadiusShrinkOff,
                activateSound = radius > 0 ? SoundDefOf.Designate_ZoneAdd : SoundDefOf.Designate_Failed,
                action = delegate { ActionSynced(___pawn, -1); }
            };
        }
    }

    // ignore think tree when building uninterrupted
    //
    [HarmonyPatch(typeof(Pawn_JobTracker))]
    [HarmonyPatch("ShouldStartJobFromThinkTree")]
    static class Pawn_JobTracker_ShouldStartJobFromThinkTree_Patch
    {
        public static void Postfix(Pawn ___pawn, ref bool __result)
        {
            if (__result == false)
                return;

            var forcedWork = Find.World.GetComponent<ForcedWork>();
            if (forcedWork.HasForcedJob(___pawn) == false)
                return;

            var forcedJob = forcedWork.GetForcedJob(___pawn);
            if (forcedJob == null)
                return;

            var workGiver = ___pawn.CurJob?.workGiverDef;
            if (workGiver == null)
                return;

            __result = forcedJob.workgiverDefs.Contains(workGiver) == false;
        }
    }

    // handle events early
    //
    [HarmonyPatch(typeof(Selector))]
    [HarmonyPatch("HandleMapClicks")]
    static class Selector_HandleMapClicks_Patch
    {
        public static bool Prefix()
        {
            return Controller.GetInstance().HandleEvents();
        }
    }

    // handle drawing
    //
    [HarmonyPatch(typeof(SelectionDrawer))]
    [HarmonyPatch(nameof(SelectionDrawer.DrawSelectionOverlays))]
    static class SelectionDrawer_DrawSelectionOverlays_Patch
    {
        public static void Postfix()
        {
            if (WorldRendererUtility.WorldRenderedNow == false)
                Controller.GetInstance().HandleDrawing();
        }
    }

    // turn some errors into warnings
    //
    [HarmonyPatch]
    static class Errors_To_Warnings_Patch
    {
        public static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(ReservationManager), "LogCouldNotReserveError");
            yield return AccessTools.Method(typeof(JobUtility), "TryStartErrorRecoverJob");
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var fromMethod = SymbolExtensions.GetMethodInfo(() => Log.Error("", false));
            var toMethod = SymbolExtensions.GetMethodInfo(() => Log.Warning("", false));
            return instructions.MethodReplacer(fromMethod, toMethod);
        }
    }

    // pawn inspector panel
    //
    [HarmonyPatch(typeof(Pawn))]
    [HarmonyPatch(nameof(Pawn.GetInspectString))]
    static class Pawn_GetInspectString_Patch
    {
        public static void Postfix(Pawn __instance, ref string __result)
        {
            var forcedWork = Find.World.GetComponent<ForcedWork>();
            if (forcedWork.HasForcedJob(__instance))
                __result = __result + "\n" + "ForcedCommandState".Translate();
        }
    }

    // custom context menu
    //
    [HarmonyPatch(typeof(FloatMenuMakerMap))]
    [HarmonyPatch(nameof(FloatMenuMakerMap.ChoicesAtFor))]
    static class FloatMenuMakerMap_ChoicesAtFor_Patch
    {
        public static void Postfix(List<FloatMenuOption> __result, Vector3 clickPos, Pawn pawn)
        {
            if (pawn != null && pawn.Drafted == false)
                if (WorldRendererUtility.WorldRenderedNow == false)
                    __result.AddRange(Controller.AchtungChoicesAtFor(clickPos, pawn));
        }
    }
}