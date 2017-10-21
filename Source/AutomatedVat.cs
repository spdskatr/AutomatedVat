using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using Verse;

namespace AutomatedVat
{
    public class Building_AutomatedVat : Building
    {
        public class _ThingCountClass : IExposable
        {
            public ThingDef thingDef;

            public int count;

            public _ThingCountClass()
            {
            }

            public _ThingCountClass(ThingDef thingDef, int count)
            {
                this.thingDef = thingDef;
                this.count = count;
            }

            public override string ToString()
            {
                return string.Concat(new object[]
                {
                "(",
                count,
                "x ",
                (thingDef == null) ? "null" : thingDef.defName,
                ")"
                });
            }

            public override int GetHashCode()
            {
                return thingDef.shortHash + count << 16;
            }

            public void ExposeData()
            {
                Scribe_Defs.Look(ref thingDef, "thingDef");
                Scribe_Values.Look(ref count, "count");
            }

            public static implicit operator _ThingCountClass(ThingCountClass original)
            {
                return new _ThingCountClass(original.thingDef, original.count);
            }

            public static List<_ThingCountClass> ListToSaveable(List<ThingCountClass> original)
            {
                var result = new List<_ThingCountClass>();
                original.ForEach(t => result.Add(t));//Implicit cast - remember
                return result;
            }
        }

        public List<_ThingCountClass> localRecord;

        public int workLeft;

        public bool PowerOn
        {
            get
            {
                return GetComp<CompPowerTrader>()?.PowerOn ?? true;
            }
        }

        public bool Ruined
        {
            get
            {
                return GetComp<CompTemperatureRuinable>()?.Ruined ?? false;
            }
        }
        public virtual bool HasProducts => Extension.products.Any();

        public ModExtension_AutomatedVat Extension => def.GetModExtension<ModExtension_AutomatedVat>();

        public virtual bool CollectingIngredients => localRecord.Any(x => x.count > 0);

        public virtual IEnumerable<IntVec3> AdjacentCells => GenAdj.CellsAdjacent8Way(this);

        public override void PostMake()
        {
            base.PostMake();
            if (Extension == null)
            {
                Log.Error("Automated vat building needs <modExtensions> <li Class=\"FFModExtension_AutomatedVat\"> to define input and output, and will cause errors without. Destroying.");
                Destroy(DestroyMode.Deconstruct);
                return;
            }
            InitializeNewRecipe();
        }

        public virtual void InitializeNewRecipe()
        {
            workLeft = Extension.workAmount;
            localRecord = _ThingCountClass.ListToSaveable(Extension.ingredients);
        }

        public override string GetInspectString()
        {
            var stringBuilder = new StringBuilder();
	        string inspect = base.GetInspectString();
	        if (!inspect.NullOrEmpty())
	        {
                stringBuilder.AppendLine(base.GetInspectString());
	        }
            CompTemperatureRuinable compTemperatureRuinable = GetComp<CompTemperatureRuinable>();
            if (compTemperatureRuinable != null)
            {
                var amount = (float)typeof(CompTemperatureRuinable).GetField("ruinedPercent", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(compTemperatureRuinable);
                if (amount > 0f)
                    stringBuilder.AppendLine("(" + amount.ToStringPercent() + ") "
                        + ((Extension.temperatureManagement.hasTemperatureManagement) ? "AVInspect_BadTempManaged".AdvancedTranslate(Extension) : "AVInspect_BadTempUnmanaged".AdvancedTranslate(Extension)));
            }
            string str = GetStringIngredients();
            if (!str.NullOrEmpty())
                stringBuilder.AppendLine(str);
            string str2 = GetStringWorking();
            if (!str2.NullOrEmpty())
            {
                stringBuilder.AppendLine(str2);
            }
            return stringBuilder.ToString().TrimEndNewlines();
        }

        public virtual string GetStringIngredients()
        {
            var stringBuilder = new StringBuilder();
            stringBuilder.Append("AVInspect_IngredientsLeft".AdvancedTranslate(Extension));
            foreach (var t in localRecord)
                stringBuilder.Append(t.ToString());
            return localRecord.NullOrEmpty() ? string.Empty : stringBuilder.ToString();
        }

        public virtual string GetStringWorking()
        {
            return localRecord.Any(t => t.count > 0) ? string.Empty : "AVInspect_WorkLeft".AdvancedTranslate(Extension, ((float)workLeft).ToStringWorkAmount());
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref localRecord, "localRecord", LookMode.Deep);
            Scribe_Values.Look(ref workLeft, "workLeft");
        }

        public override void DrawExtraSelectionOverlays()
        {
            base.DrawExtraSelectionOverlays();
            GenDraw.DrawFieldEdges(AdjacentCells.ToList());
        }

        public override void Tick()
        {
            base.Tick();
            if (Ruined)
            {
                InitializeNewRecipe();
                if (AmbientTemperature > GetComp<CompTemperatureRuinable>().Props.maxSafeTemperature || AmbientTemperature < GetComp<CompTemperatureRuinable>().Props.minSafeTemperature)
                {
                    ManageTemperature();
                    return;
                }
            }
            else if (PowerOn && Find.TickManager.TicksGame % Extension.tickRateDivisor == 0 )
            {
                if (Extension.temperatureManagement.hasTemperatureManagement)
                    ManageTemperature();
                else if (GetComp<CompPowerTrader>() != null)
                {
                    GetComp<CompPowerTrader>().powerOutputInt = -def.GetCompProperties<CompProperties_Power>().basePowerConsumption;
                }

                if (CollectingIngredients)
                    AcceptIngredientsForNextRecipe();
                else if (workLeft > 0)
                    DoWork(Mathf.RoundToInt(Extension.workSpeedMultiplier * Extension.tickRateDivisor));
                else
                    MakeProductsAndStartNewRecipe();
            }
        }

        public void ManageTemperature()
        {
            var ruinableComp = GetComp<CompTemperatureRuinable>();
            var powerComp = GetComp<CompPowerTrader>();
            FieldInfo fieldInfo = typeof(CompTemperatureRuinable).GetField("ruinedPercent", BindingFlags.NonPublic | BindingFlags.Instance);
            if (ruinableComp != null && powerComp != null &&
                (float)fieldInfo
                .GetValue(ruinableComp) > 0f)
            {
                powerComp.powerOutputInt = -def.GetCompProperties<CompProperties_Power>().basePowerConsumption - Extension.temperatureManagement.powerConsumptionExtra;

                //Temperature management takes a cetain amount of ruin percent away
                fieldInfo.SetValue(ruinableComp, (float)fieldInfo.GetValue(ruinableComp) - (ruinableComp.Props.progressPerDegreePerTick * Extension.temperatureManagement.temperatureManagementStrength * Extension.tickRateDivisor));
            }
        }

        public virtual void MakeProductsAndStartNewRecipe()
        {
            if (HasProducts)
                MakeProducts();
            InitializeNewRecipe();
        }


        public virtual void DoWork(int amount)
        {
            workLeft -= amount;
            if (workLeft <= 0)
            {
                workLeft = 0;
            }
        }

        public virtual void AcceptIngredientsForNextRecipe()
        {
            var cells = AdjacentCells.ToList();
            var items = cells.SelectMany(c => c.GetThingList(Map));
            foreach (var item in items)
            {
                foreach (var count in localRecord.Where(x => x.count > 0))
                {
                    if (item.def == count.thingDef)
                    {
                        if (count.count >= item.stackCount)
                        { 
                            count.count -= item.stackCount;
                            item.Destroy();
                            goto DoubleBreak;
                        }
                        else
                        {
                            item.stackCount -= count.count;
                            count.count = 0;
                            goto DoubleBreak;
                        }
                    }
                }
            }
            DoubleBreak:;
        }

        public virtual void MakeProducts()
        {
            foreach (ThingCountClass x in Extension.products)
            {
                var thing = ThingMaker.MakeThing(x.thingDef);
                thing.stackCount = x.count;
                var ingredietsComp = thing.TryGetComp<CompIngredients>();
                if (ingredietsComp != null)
                {
                    ingredietsComp.ingredients.AddRange(from i in Extension.ingredients select i.thingDef);
                }
                GenPlace.TryPlaceThing(thing, InteractionCell, Map, ThingPlaceMode.Near);
            }
        }
    }
}
