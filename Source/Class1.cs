using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

/*namespace Hobbes.MoreLinkables {
    [StaticConstructorOnStartup]
    public class LinkModSettings : ModSettings {

        public static Dictionary<string, int> maxSimu;
        public static Dictionary<string, float> maxDist;
        public static Dictionary<string, bool> mustBeAdjacent;

        public static int Lsim;
        public static float Ldist;
        public static bool Ladj;

        static LinkModSettings() {

*//*            maxSimu = new Dictionary<string, int>();
            maxDist = new Dictionary<string, float>();
            mustBeAdjacent = new Dictionary<string, bool>();
            foreach (ThingDef def in LoadedModManager.GetMod<LinkMod>().Content.AllDefs) {
                string name = def.defName;
                maxSimu.Add(name, def.GetCompProperties<CompProperties_Facility>().maxSimultaneous);
                maxDist.Add(name, def.GetCompProperties<CompProperties_Facility>().maxDistance);
                mustBeAdjacent.Add(name, def.GetCompProperties<CompProperties_Facility>().mustBePlacedAdjacent);
            }*//*
        }

        public override void ExposeData() {
            CompProperties_Facility Lcomp = DefDatabase<ThingDef>.GetNamed("HobbesLink_LaserEngraver").GetCompProperties<CompProperties_Facility>();

            Scribe_Values.Look(ref Lsim, "LaserEngraverSimultaneous", Lcomp.maxSimultaneous);
            Scribe_Values.Look(ref Ldist, "LaserEngraverDistance", Lcomp.maxDistance);
            Scribe_Values.Look(ref Ladj, "LaserEngraverAdjacent", Lcomp.mustBePlacedAdjacent);
            base.ExposeData();
        }
    }
    public class LinkMod : Mod {

        LinkModSettings settings;

        public LinkMod(ModContentPack content) : base(content) {
            settings = GetSettings<LinkModSettings>();
        }

        public override void DoSettingsWindowContents(Rect inRect) {
            Listing_Standard standard = new Listing_Standard();
            //standard.Begin();
            base.DoSettingsWindowContents(inRect);
        }

        public override string SettingsCategory() {
            return "Even More Linkables";
        }
    }
}*/

namespace Hobbes.MoreLinkablesProduction {

    [StaticConstructorOnStartup]
    public static class Patches {
        static Harmony harmony = new Harmony("Hobbes.MoreLinkables");
        static Patches() {
            PatchCompFacility();
        }
        static void PatchCompFacility() {
            MethodBase original = typeof(CompFacility).GetProperty("CanBeActive").GetMethod;
            HarmonyMethod postfix = new HarmonyMethod(typeof(Patches).GetMethod("CanBeActive_Postfix"));
            harmony.Patch(original, postfix: postfix);
        }

        public static void CanBeActive_Postfix(CompFacility __instance, ref bool __result) {
            CompWorkHelper comp = __instance.parent.GetComp<CompWorkHelper>();
            if (comp != null && comp.state == CompWorkHelper.State.WORK) {
                __result = false;
            }
        }
    }

    public class CompProperties_SecondLayer : CompProperties {
        public GraphicData graphicData = null;

        //public AltitudeLayer altitude = AltitudeLayer.BuildingOnTop;

        //public float Y => altitude.AltitudeFor() - 0.1f;

        public CompProperties_SecondLayer() {
            compClass = typeof(CompSecondLayer);
        }
    }

    public class CompSecondLayer : ThingComp {

        private Graphic graphicInt;

        private float Y => parent.def.altitudeLayer.AltitudeFor() + 0.2f;

        public Graphic Graphic {
            get {
                if (graphicInt == null) {
                    graphicInt = Props.graphicData.GraphicColoredFor(parent);
                }
                return graphicInt;
            }
        }
        public CompProperties_SecondLayer Props => (CompProperties_SecondLayer) props;

        public override void PostDraw() {
                Graphic.Draw(GenThing.TrueCenter(parent.Position, parent.Rotation, parent.def.size, Y), parent.Rotation, parent);
        }
    }

    public class CompProperties_WorkHelper : CompProperties {
        public float radius = 5f;
        public int tickInterval = 100;
        public int reductionAmt = 100;
        public string workIconPath = "UI/WorkReducer";
        public string boostIconPath = "UI/BoostWork";

        public CompProperties_WorkHelper() {
            compClass = typeof(CompWorkHelper);
        }
    }

    public class CompWorkHelper : ThingComp {

        protected CompPowerTrader compPower;

        private CompProperties_WorkHelper Props => (CompProperties_WorkHelper)props;

        public enum State {
            WORK,
            BOOST
        }

        public State state = State.WORK;

        Texture2D workIcon;
        Texture2D boostIcon;

        public Texture2D WorkIcon {
            get {
                if (workIcon == null) {
                    workIcon = ContentFinder<Texture2D>.Get(Props.workIconPath);
                }
                return workIcon;
            }
        }

        public Texture2D BoostIcon {
            get {
                if (boostIcon == null) {
                    boostIcon = ContentFinder<Texture2D>.Get(Props.boostIconPath);
                }
                return boostIcon;
            }
        }

        private IntVec3[] areaCache;

        private IntVec3[] Area {
            get {
                if (areaCache == null) {
                    areaCache = GenRadial.RadialCellsAround(parent.Position, Props.radius, false).ToArray();
                }
                return areaCache;
            }
        }

        private int ticksActive;

        public override void CompTick() {
            base.CompTick();
            ticksActive++;
            if (compPower.PowerOn && ticksActive >= Props.tickInterval && state == State.WORK) {
                ticksActive = 0;
                DoWork();
            }
        }

        public override void PostSpawnSetup(bool respawningAfterLoad) {
            base.PostSpawnSetup(respawningAfterLoad);
            compPower = parent.GetComp<CompPowerTrader>();
        }

        private void DoWork() {
            IEnumerable<UnfinishedThing> things = WorkTargets().TakeRandom(1);
            foreach (Thing t in things) {
                UnfinishedThing ut = (UnfinishedThing) t;
                float amt = Math.Max(0f, ut.workLeft - Props.reductionAmt * 60);
                string moteMsg = $"Work reduced\nto {Mathf.CeilToInt(amt / 60)}!";
                if (amt == 0) moteMsg = "Work Complete!";
                MoteMaker.ThrowText(ut.TrueCenter(), ut.MapHeld, moteMsg, 2.5f);
                ut.workLeft = amt;
                //Log.Message($"work being done on {t.Label}; work left: {((UnfinishedThing) t).workLeft}");
            }
        }

        private List<UnfinishedThing> WorkTargets() {
            List<UnfinishedThing> targets = new List<UnfinishedThing>();
            for (int i = 0; i < Area.Length; i++) {
                if (!Area[i].InBounds(parent.Map)) {
                    continue;
                }
                foreach (Thing t in Area[i].GetItems(parent.Map)) {
                    if (t is UnfinishedThing ut && ut.workLeft > 0f /*&& (ut.BoundBill == null || !ut.BoundBill.ShouldDoNow() || ut.BoundBill.suspended)*/) targets.Add(ut);
                }
            }
            return targets;
        }

        public override void PostExposeData() {
            base.PostExposeData();
            Scribe_Values.Look(ref state, "state");
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra() {
            Command_Action command_action = new Command_Action();
            State stateToSwitchTo = State.BOOST;
            if (state == State.WORK) {
                command_action.defaultLabel = "Switch to Boost";
                command_action.defaultDesc = "Switch modes to boost productivity of nearby workbenches.";
                command_action.icon = BoostIcon;
                stateToSwitchTo = State.BOOST;
            } else if (state == State.BOOST) {
                command_action.defaultLabel = "Switch to Work";
                command_action.defaultDesc = "Switch modes to reduce the work left on nearby, unfinished things.";
                command_action.icon = WorkIcon;
                stateToSwitchTo = State.WORK;
            }
            command_action.action = delegate {
                state = stateToSwitchTo;
            };
            command_action.activateSound = SoundDefOf.Tick_Tiny;
            yield return command_action;
        }

        public override string CompInspectStringExtra() {
            return $"Current mode: {((state == State.WORK) ? "Work" : "Boost")}";
        }
    }
}

namespace Hobbes.MoreLinkablesPower {

    [StaticConstructorOnStartup]
    class Patches {

        static Harmony harmony = new Harmony("Hobbes.MoreLinkables");
        static Patches() {
            PatchCompPowerPlant();
        }

        static void PatchCompPowerPlant() {
            MethodBase original = typeof(CompPowerPlant).GetMethod("UpdateDesiredPowerOutput");
            HarmonyMethod postfix = new HarmonyMethod(typeof(Patches).GetMethod("UpdateDesiredPowerOutput_Postfix"));
            harmony.Patch(original, postfix: postfix);
        }

        public static void UpdateDesiredPowerOutput_Postfix(CompPowerPlant __instance) {
            CompAffectedByFacilities linkedComp = __instance.parent.GetComp<CompAffectedByFacilities>();
            if (__instance.PowerOutput == 0.0f || linkedComp == null) {
                return;
            }
            __instance.PowerOutput *= LinkedStatDefOf.PowerOutputFactor.Worker.GetValue(__instance.parent);
        }
    }

    [DefOf]
    public static class LinkedStatDefOf {

        public static StatDef PowerOutputFactor;

        static LinkedStatDefOf() {
            DefOfHelper.EnsureInitializedInCtor(typeof(LinkedStatDefOf));
        }
    }

}
