using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace EvenMoreLinkables
{
    public class EvenMoreLinkablesMod : Mod
    {
        public EvenMoreLinkablesMod(ModContentPack pack) : base(pack)
        {
			new Harmony("EvenMoreLinkablesMod").PatchAll();
        }
    }

    public class RequiredLinkablesToCraft : DefModExtension
    {
        public List<ThingDef> requiredLinkables;
    }


    [HarmonyPatch(typeof(RecipeDef), "AvailableOnNow")]
    public static class RecipeDef_AvailableOnNow_Patch
    {
        public static void Postfix(RecipeDef __instance, Thing thing, ref bool __result)
        {
            if (__result)
            {
                var extension = __instance.ProducedThingDef?.GetModExtension<RequiredLinkablesToCraft>();
                if (extension != null && thing is Building_WorkTable worktable)
                {
                    var comp = worktable.GetComp<CompAffectedByFacilities>();
                    if (comp != null)
                    {
                        if (comp.LinkedFacilitiesListForReading.Any(x => extension.requiredLinkables.Contains(x.def)) is false)
                        {
                            __result = false;
                        }
                    }
                }
            }
        }
    }

    [HarmonyPatch(typeof(Bill_Production), "ShouldDoNow")]
    public static class Bill_Production_ShouldDoNow_Patch
    {
        public static void Postfix(Bill_Production __instance, ref bool __result)
        {
            if (__result)
            {
                var extension = __instance.recipe.ProducedThingDef?.GetModExtension<RequiredLinkablesToCraft>();
                if (extension != null && __instance.billStack.billGiver is Building_WorkTable worktable)
                {
                    var comp = worktable.GetComp<CompAffectedByFacilities>();
                    if (comp != null)
                    {
                        if (comp.LinkedFacilitiesListForReading.Any(x => extension.requiredLinkables.Contains(x.def)) is false)
                        {
                            __result = false;
                        }
                    }
                }
            }
        }
    }

    [HarmonyPatch(typeof(QualityUtility), "GenerateQualityCreatedByPawn", [typeof(Pawn), typeof(SkillDef)])]
    public static class QualityUtility_GenerateQualityCreatedByPawn_Patch
    {
        public static void Postfix(ref QualityCategory __result, Pawn pawn)
        {
			if (pawn.CurJob?.bill?.billStack?.billGiver is Building_WorkTable worktable)
			{
				var comp = worktable.GetComp<CompAffectedByFacilities>();
				if (comp != null)
				{
					foreach (var link in comp.LinkedFacilitiesListForReading)
					{
						var props = link.TryGetComp<CompQualityOffset>();
						if (props != null)
						{
							__result = (QualityCategory)((int)__result + props.Props.qualityOffset);
							__result = (QualityCategory)Mathf.Clamp((int)__result, (int)QualityCategory.Awful, (int)QualityCategory.Legendary);
							return;
						}
					}
				}
			}
		}
    }

    public class CompProperties_QualityOffset : CompProperties
    {
        public int qualityOffset;
        public CompProperties_QualityOffset()
        {
            this.compClass = typeof(CompQualityOffset);
        }
    }
    public class CompQualityOffset : ThingComp
    {
        public CompProperties_QualityOffset Props => props as CompProperties_QualityOffset;
    }
}
