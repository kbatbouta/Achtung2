using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace AchtungMod
{
    public static class Initialization
    {
        private static IEnumerable<ThingDef> DefPawns;

        private static ThinkTreeDef DefStupidPawns;

        public static void Init()
        {
            Prepare();
            AddCompProp();
        }

        private static void Prepare()
        {
            DefStupidPawns = DefDatabase<ThinkTreeDef>.GetNamedSilentFail("Zombie");

            DefPawns = from def in DefDatabase<ThingDef>.AllDefs
                where def.race?.intelligence == Intelligence.Humanlike
                      && !def.defName.Contains("AIPawn") && !def.defName.Contains("Robot")
                      && !def.defName.Contains("ChjDroid") && !def.defName.Contains("ChjBattleDroid")
                      && (DefStupidPawns == null || def.race.thinkTreeMain != DefStupidPawns)
                select def;
        }

        private static void AddCompProp()
        {
            var comps_types = new List<Type>(new[]
            {
                typeof(Components.AchtungHealthCompProperties), typeof(Components.AchtungMentalCompProperties)
            });
            foreach (var t in DefPawns)
            {
                if (t.comps == null)
                    t.comps = new List<CompProperties>();

                foreach (var prop_type in comps_types)
                {
                    t.comps.Add((CompProperties) Activator.CreateInstance(prop_type));
#if true
                    Log.Message("" + prop_type.Name);
#endif
                }
            }
        }
    }
}