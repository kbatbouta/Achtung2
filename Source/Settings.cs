﻿using UnityEngine;
using Verse;

namespace AchtungMod
{
    public enum AchtungModKey
    {
        None,
        Alt,
        Ctrl,
        Shift,
        Meta
    }

    public enum CommandMenuMode
    {
        Auto,
        PressForMenu,
        PressForPosition
    }

    public enum BreakLevel
    {
        None,
        Minor,
        Major,
        AlmostExtreme,
        Extreme
    }

    public enum HealthLevel
    {
        None,
        ShouldBeTendedNow,
        PrefersMedicalRest,
        NeedsMedicalRest,
        InPainShock
    }

    public class AchtungSettings : ModSettings
    {
        public bool positioningEnabled = true;
        public AchtungModKey achtungKey = AchtungModKey.Alt;
        public CommandMenuMode forceCommandMenuMode = CommandMenuMode.PressForMenu;
        public AchtungModKey forceCommandMenuKey = AchtungModKey.Ctrl;
        public BreakLevel breakLevel = BreakLevel.AlmostExtreme;
        public HealthLevel healthLevel = HealthLevel.InPainShock;
        public bool ignoreForbidden = false;
        public bool ignoreRestrictions = false;
        public bool ignoreAssignments = false;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref positioningEnabled, "positioningEnabled", true, true);
            Scribe_Values.Look(ref achtungKey, "achtungKey", AchtungModKey.Alt, true);
            Scribe_Values.Look(ref forceCommandMenuMode, "forceCommandMenuMode", CommandMenuMode.Auto, true);
            Scribe_Values.Look(ref forceCommandMenuKey, "forceCommandMenuKey", AchtungModKey.Ctrl, true);
            Scribe_Values.Look(ref breakLevel, "BreakLevel", BreakLevel.AlmostExtreme, true);
            Scribe_Values.Look(ref healthLevel, "HealthLevel", HealthLevel.InPainShock, true);
            Scribe_Values.Look(ref ignoreForbidden, "ignoreForbidden", false, true);
            Scribe_Values.Look(ref ignoreRestrictions, "ignoreRestrictions", false, true);
            Scribe_Values.Look(ref ignoreAssignments, "ignoreAssignments", false, true);
        }

        public static void DoWindowContents(Rect canvas)
        {
            var columnWidth = canvas.width * 0.6f;
            var list = new Listing_Standard {ColumnWidth = columnWidth};
            list.Begin(canvas);
            list.Gap(8);

            list.CheckboxEnhanced("PositioningEnabled", ref Achtung.Settings.positioningEnabled);
            list.Gap(10);
            list.ValueLabeled("AchtungModifier", ref Achtung.Settings.achtungKey);
            list.Gap(10);
            list.ValueLabeled("ForceCommandMenuMode", ref Achtung.Settings.forceCommandMenuMode);
            if (Achtung.Settings.forceCommandMenuMode != CommandMenuMode.Auto)
            {
                list.Gap(-2);
                list.ValueLabeled("ForceCommandMenuKey", ref Achtung.Settings.forceCommandMenuKey);
            }

            list.Gap(10);
            list.ValueLabeled("BreakLevel", ref Achtung.Settings.breakLevel);
            list.Gap(10);
            list.ValueLabeled("HealthLevel", ref Achtung.Settings.healthLevel);
            list.Gap(10);
            list.CheckboxEnhanced("IgnoreForbidden", ref Achtung.Settings.ignoreForbidden);
            list.CheckboxEnhanced("IgnoreRestrictions", ref Achtung.Settings.ignoreRestrictions);
            list.CheckboxEnhanced("IgnoreAssignments", ref Achtung.Settings.ignoreAssignments);

            list.NewColumn();
            list.ColumnWidth = canvas.width - columnWidth;

            list.Gap(10);
            list.Note("Notes");
            list.Gap(2);
            list.Note("Note1");
            list.Gap(2);
            list.Note("Note2");

            list.End();
        }
    }
}