﻿using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using System.Reflection;
using System;
using Harmony;
using System.Linq;
using System.Reflection.Emit;

namespace AchtungMod
{
	[StaticConstructorOnStartup]
	public class AchtungLoader
	{
		public static bool IsSameSpotInstalled;

		static AchtungLoader()
		{
			Controller.GetInstance().InstallDefs();

			// HarmonyInstance.DEBUG = true;
			var harmony = HarmonyInstance.Create("net.pardeike.rimworld.mods.achtung");
			harmony.PatchAll(Assembly.GetExecutingAssembly());

			const string sameSpotId = "net.pardeike.rimworld.mod.samespot";
			IsSameSpotInstalled = harmony.GetPatchedMethods()
				.Any(method => harmony.GetPatchInfo(method).Transpilers.Any(transpiler => transpiler.owner == sameSpotId));
		}
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
			Settings.DoWindowContents(inRect);
		}

		public override string SettingsCategory()
		{
			return "Achtung!";
		}
	}

	// build-in "Ignore Me Passing" functionality
	//
	[HarmonyPatch(typeof(GenConstruct))]
	[HarmonyPatch(nameof(GenConstruct.BlocksConstruction))]
	static class GenConstruct_BlocksConstruction_Patch
	{
		static bool Prefix(ref bool __result, Thing constructible, Thing t)
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
		static bool Prefix(Pawn p, Thing t, bool forced, ref bool __result)
		{
			var forcedWork = Find.World.GetComponent<ForcedWork>();
			if (forced || forcedWork.HasForcedJob(p))
			{
				__result = true;
				return false;
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
		static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			var instr = instructions.ToList();

			var f_NotInHomeAreaTrans = AccessTools.Field(typeof(WorkGiver_FixBrokenDownBuilding), "NotInHomeAreaTrans");
			var i = instr.FirstIndexOf(inst => inst.opcode == OpCodes.Ldsfld && inst.operand == f_NotInHomeAreaTrans);
			if (i > 0)
			{
				object label = null;
				for (var j = i - 1; j >= 0; j--)
					if (instr[j].opcode == OpCodes.Brtrue)
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

	// ignore reservations for forced jobs
	/*
	[HarmonyPatch(typeof(ReservationManager))]
	[HarmonyPatch(nameof(ReservationManager.CanReserve))]
	static class ReservationUtility_CanReserve_Patch
	{
		static bool Prefix(Pawn claimant, LocalTargetInfo target, bool ignoreOtherReservations, ref bool __result)
		{
			var forcedWork = Find.World.GetComponent<ForcedWork>();
			if (forcedWork.HasForcedJob(claimant))
			{
				__result = true;
				return false;
			}
			return true;
		}
	}*/

	// ignore forbidden for forced jobs
	// TODO: make this optional
	//
	[HarmonyPatch(typeof(ForbidUtility))]
	[HarmonyPatch(nameof(ForbidUtility.IsForbidden))]
	[HarmonyPatch(new Type[] { typeof(Thing), typeof(Pawn) })]
	static class ForbidUtility_IsForbidden_Patch
	{
		static bool Prefix(Thing t, Pawn pawn, ref bool __result)
		{
			var forcedWork = Find.World.GetComponent<ForcedWork>();
			if (forcedWork.HasForcedJob(pawn))
			{
				__result = false;
				return false;
			}
			return true;
		}
	}

	// teleporting does not end current job with error condition
	//
	[HarmonyPatch(typeof(Pawn_PathFollower))]
	[HarmonyPatch(nameof(Pawn_PathFollower.TryRecoverFromUnwalkablePosition))]
	static class Pawn_PathFollower_TryRecoverFromUnwalkablePosition_Patch
	{
		static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			var m_Notify_Teleported = AccessTools.Method(typeof(Pawn), nameof(Pawn.Notify_Teleported));
			var list = instructions.ToList();
			var idx = list.FirstIndexOf(code => code.operand == m_Notify_Teleported);
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
			if (forcedJob != null)
			{
				var targets = forcedJob.GetSortedTargets();
				if (targets.Count > 0)
				{
					if (targets[0].HasThing)
					{
						foreach (var thing in targets.Select(target => target.Thing))
							yield return thing;
						yield break;
					}
				}
			}

			foreach (var thing in GenRadial.RadialDistinctThingsAround(center, map, radius, useCenter))
				yield return thing;
		}

		static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			var m_RadialDistinctThingsAround = AccessTools.Method(typeof(GenRadial), nameof(GenRadial.RadialDistinctThingsAround));
			var m_RadialDistinctThingsAround_Patch = AccessTools.Method(typeof(WorkGiver_ConstructDeliverResources_FindNearbyNeeders_Patch), "RadialDistinctThingsAround_Patch");

			var found = 0;
			var list = instructions.ToList();
			var count = list.Count;
			var idx = 0;
			while (idx < count)
			{
				if (list[idx].operand == m_RadialDistinctThingsAround)
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
		static void Prefix(Pawn pawn, out ForcedWork __state)
		{
			__state = Find.World.GetComponent<ForcedWork>();
			__state.Prepare(pawn);
		}

		static void Postfix(Pawn pawn, ForcedWork __state)
		{
			__state.Unprepare(pawn);
		}

		static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			var c_FloatMenuOption = AccessTools.FirstConstructor(typeof(FloatMenuOption), c => c.GetParameters().Count() > 1);
			var m_ForcedFloatMenuOption = AccessTools.Method(typeof(ForcedFloatMenuOption), nameof(ForcedFloatMenuOption.CreateForcedMenuItem));

			var list = instructions.ToList();

			var foundCount = 0;
			Enumerable.Range(0, list.Count)
				.DoIf(i => list[i].opcode == OpCodes.Isinst && list[i].operand == typeof(WorkGiver_Scanner), i =>
				{
					var index = i + 1;
					if (list[index].opcode == OpCodes.Stloc_S)
					{
						var localVar = list[index].operand;

						index = list.FindIndex(index, code => code.opcode == OpCodes.Newobj && code.operand == c_FloatMenuOption);
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
	//
	[HarmonyPatch(typeof(Pawn_JobTracker))]
	[HarmonyPatch(nameof(Pawn_JobTracker.EndJob))]
	static class Pawn_JobTracker_EndJob_Patch
	{
		static bool Prefix(Pawn_JobTracker __instance, Pawn ___pawn, JobCondition condition)
		{
			var forcedWork = Find.World.GetComponent<ForcedWork>();
			if (forcedWork.HasForcedJob(___pawn))
			{
				__instance.EndCurrentJob(condition, true);
				return false;
			}
			return true;
		}
	}
	//
	[HarmonyPatch(typeof(Pawn_JobTracker))]
	[HarmonyPatch(nameof(Pawn_JobTracker.EndCurrentJob))]
	static class Pawn_JobTracker_EndCurrentJob_Patch
	{
		static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			var m_CleanupCurrentJob = AccessTools.Method(typeof(Pawn_JobTracker), "CleanupCurrentJob");
			var m_ContinueJob = AccessTools.Method(typeof(ForcedJob), "ContinueJob");
			var f_pawn = AccessTools.Field(typeof(Pawn_JobTracker), "pawn");

			var instrList = instructions.ToList();
			for (var i = 0; i < instrList.Count; i++)
			{
				var instruction = instrList[i];
				yield return instruction;

				if (instruction.operand != m_CleanupCurrentJob)
					continue;

				if (instrList[i + 1].opcode != OpCodes.Ldarg_2 || instrList[i + 2].opcode != OpCodes.Brfalse)
				{
					Log.Error("Unexpected opcodes while transpiling Pawn_JobTracker.EndCurrentJob");
					continue;
				}

				var endLabel = instrList[i + 2].operand;

				yield return instrList[++i];
				yield return instrList[++i];

				yield return new CodeInstruction(OpCodes.Ldarg_0);
				yield return new CodeInstruction(OpCodes.Ldloc_1);
				yield return new CodeInstruction(OpCodes.Ldarg_0);
				yield return new CodeInstruction(OpCodes.Ldfld, f_pawn);
				yield return new CodeInstruction(OpCodes.Ldarg_1);
				yield return new CodeInstruction(OpCodes.Call, m_ContinueJob);

				yield return new CodeInstruction(OpCodes.Brtrue, endLabel);
			}
		}
	}

	// check mental levels
	//
	[HarmonyPatch(typeof(MentalBreaker))]
	[HarmonyPatch(nameof(MentalBreaker.MentalBreakerTick))]
	static class MentalBreaker_MentalBreakerTick_Patch
	{
		static void Postfix(Pawn ___pawn)
		{
			if (___pawn.IsColonist == false || ___pawn.IsHashIntervalTick(120) == false)
				return;

			var forcedWork = Find.World.GetComponent<ForcedWork>();
			if (forcedWork.HasForcedJob(___pawn) == false)
				return;

			var breakNote = Tools.PawnOverBreakLevel(___pawn);
			if (breakNote != null)
			{
				___pawn.Map.pawnDestinationReservationManager.ReleaseAllClaimedBy(___pawn);
				var jobName = ___pawn.jobs.curJob.GetReport(___pawn).CapitalizeFirst();
				var label = "JobInterruptedLabel".Translate(jobName);
				Find.LetterStack.ReceiveLetter(LetterMaker.MakeLetter(label, "JobInterruptedBreakdown".Translate(___pawn.Name.ToStringShort, breakNote), LetterDefOf.NegativeEvent, ___pawn));

				forcedWork.Remove(___pawn);
				___pawn.jobs.EndCurrentJob(JobCondition.InterruptForced, true);
				return;
			}

			if (Tools.PawnOverHealthLevel(___pawn))
			{
				___pawn.Map.pawnDestinationReservationManager.ReleaseAllClaimedBy(___pawn);
				var jobName = ___pawn.jobs.curJob.GetReport(___pawn).CapitalizeFirst();
				var label = "JobInterruptedLabel".Translate(jobName);
				Find.LetterStack.ReceiveLetter(LetterMaker.MakeLetter(label, "JobInterruptedBadHealth".Translate(___pawn.Name.ToStringShort), LetterDefOf.NegativeEvent, ___pawn));

				forcedWork.Remove(___pawn);
				___pawn.jobs.EndCurrentJob(JobCondition.InterruptForced, true);
				return;
			}
		}
	}

	// release forced work when pawn disappears
	//
	[HarmonyPatch(typeof(Pawn))]
	[HarmonyPatch(nameof(Pawn.DeSpawn))]
	static class Pawn_DeSpawn_Patch
	{
		static void Postfix(Pawn __instance)
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

		static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> gizmos, Pawn ___pawn)
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
				activateSound = SoundDefOf.Designate_AreaAdd,
				action = delegate
				{
					forcedJob = forcedWork.GetForcedJob(___pawn);
					forcedJob?.ChangeCellRadius(1);
				}
			};

			yield return new Command_Action
			{
				defaultLabel = "DecreaseForceRadius".Translate(),
				defaultDesc = "DecreaseForceRadiusDesc".Translate(radius),
				icon = radius > 0 ? ForceRadiusShrink : ForceRadiusShrinkOff,
				activateSound = radius > 0 ? SoundDefOf.Designate_AreaAdd : SoundDefOf.Designate_Failed,
				action = delegate
				{
					forcedJob = forcedWork.GetForcedJob(___pawn);
					if (forcedJob != null && forcedJob.cellRadius > 0)
						forcedJob.ChangeCellRadius(-1);
				}
			};
		}
	}

	// ignore think treee when building uninterrupted
	//
	[HarmonyPatch(typeof(Pawn_JobTracker))]
	[HarmonyPatch("ShouldStartJobFromThinkTree")]
	static class Pawn_JobTracker_ShouldStartJobFromThinkTree_Patch
	{
		static void Postfix(Pawn ___pawn, ref bool __result)
		{
			var forcedWork = Find.World.GetComponent<ForcedWork>();
			if (__result && forcedWork.HasForcedJob(___pawn))
				__result = false;
		}
	}

	// handle events early
	//
	[HarmonyPatch(typeof(MainTabsRoot))]
	[HarmonyPatch(nameof(MainTabsRoot.HandleLowPriorityShortcuts))]
	static class MainTabsRoot_HandleLowPriorityShortcuts_Patch
	{
		static void Prefix()
		{
			Controller.GetInstance().HandleEvents();
		}
	}

	// handle drawing
	//
	[HarmonyPatch(typeof(SelectionDrawer))]
	[HarmonyPatch(nameof(SelectionDrawer.DrawSelectionOverlays))]
	static class SelectionDrawer_DrawSelectionOverlays_Patch
	{
		static void Postfix()
		{
			Controller.GetInstance().HandleDrawing();
		}
	}

	// handle gui
	//
	[HarmonyPatch(typeof(ThingOverlays))]
	[HarmonyPatch(nameof(ThingOverlays.ThingOverlaysOnGUI))]
	static class ThingOverlays_ThingOverlaysOnGUI_Patch
	{
		static void Postfix()
		{
			Controller.GetInstance().HandleDrawingOnGUI();
		}
	}

	// turn reservation error into warning inject
	//
	[HarmonyPatch(typeof(ReservationManager))]
	[HarmonyPatch("LogCouldNotReserveError")]
	static class ReservationManager_LogCouldNotReserveError_Patch
	{
		static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			var fromMethod = AccessTools.Method(typeof(Log), "Error", new Type[] { typeof(string), typeof(bool) });
			var toMethod = AccessTools.Method(typeof(Log), "Warning", new Type[] { typeof(string), typeof(bool) });
			return instructions.MethodReplacer(fromMethod, toMethod);
		}
	}

	// pawn inspector panel
	//
	[HarmonyPatch(typeof(Pawn))]
	[HarmonyPatch(nameof(Pawn.GetInspectString))]
	static class Pawn_GetInspectString_Patch
	{
		static void Postfix(Pawn __instance, ref string __result)
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
		static void Postfix(List<FloatMenuOption> __result, Vector3 clickPos, Pawn pawn)
		{
			if (pawn != null && pawn.Drafted == false)
				__result.AddRange(Controller.GetInstance().AchtungChoicesAtFor(clickPos, pawn));
		}
	}
}