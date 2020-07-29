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
<<<<<<< HEAD
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
=======
            Initialization.Prepare();
            Initialization.AddCompProp();
        }

        static void Prepare()
        {
            DefStupidPawns = DefDatabase<ThinkTreeDef>.GetNamedSilentFail("Zombie");

            DefPawns = (
                    from def in DefDatabase<ThingDef>.AllDefs
                    where def.race?.intelligence == Intelligence.Humanlike
                    && !def.defName.Contains("AIPawn") && !def.defName.Contains("Robot")
                    && !def.defName.Contains("ChjDroid") && !def.defName.Contains("ChjBattleDroid")
                    && (DefStupidPawns == null || def.race.thinkTreeMain != DefStupidPawns)
                    select def
                );
        }

        static void AddCompProp()
        {
            List<Type> comps_types = new List<Type>(new[] {
                typeof(Components.AchtungHealthCompProperties), typeof(Components.AchtungMentalCompProperties)
            });
            foreach (ThingDef t in DefPawns)
>>>>>>> dcbc746fc2ff73b0938ab79d41ae99e2f6c0f5aa
            {
                if (t.comps == null)
                    t.comps = new List<CompProperties>();

<<<<<<< HEAD
                foreach (var prop_type in comps_types)
                {
                    t.comps.Add((CompProperties) Activator.CreateInstance(prop_type));
=======
                foreach (Type prop_type in comps_types)
                {
                    t.comps.Add((CompProperties)Activator.CreateInstance(prop_type));
>>>>>>> dcbc746fc2ff73b0938ab79d41ae99e2f6c0f5aa
#if true
                    Log.Message("" + prop_type.Name);
#endif
                }
            }
        }
    }
<<<<<<< HEAD
}
=======
}
>>>>>>> dcbc746fc2ff73b0938ab79d41ae99e2f6c0f5aa
