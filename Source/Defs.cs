using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;

namespace AutomatedVat
{
    public class ModExtension_AutomatedVat : DefModExtension
    {
        /// <summary>
        /// Insert ingredients, prducts and work amount (in ticks, 60 ticks is 1 work).
        /// </summary>
        public int workAmount = -1;

        /// <summary>
        /// Insert ingredients, prducts and work amount (in ticks, 60 ticks is 1 work).
        /// </summary>
        public List<ThingCountClass> ingredients = new List<ThingCountClass>();

        /// <summary>
        /// Insert ingredients, prducts and work amount (in ticks, 60 ticks is 1 work).
        /// </summary>
        public List<ThingCountClass> products = new List<ThingCountClass>();

        /// <summary>
        /// Describes how often the machine ticks (1 in x times). Has no effect on work speed. Recommended number is 35 to be in line with Industrial Rollers
        /// </summary>
        public int tickRateDivisor = 35;

        /// <summary>
        /// How fast the machine works. Has no effect on tick speed.
        /// </summary>
        public float workSpeedMultiplier = 1f;

        /// <summary>
        /// Fields about temperature.
        /// </summary>
        public TemperatureManagementProperties temperatureManagement = new TemperatureManagementProperties();

        /// <summary>
        /// Allows you to replace some of the translation strings with your own.
        /// </summary>
        public List<DefTranslationOverride> overrides = new List<DefTranslationOverride>();
    }

    public class DefTranslationOverride
    {
        public string original;
        public string modified;
    }

    public static class DefTranslationOverrideUtility
    {
        public static string AdvancedTranslate(this string original, ModExtension_AutomatedVat instance,  params object[] args)
        {
            var overrideString = instance.overrides.Find(s => s.original == original);
            if (overrideString != null)
            {
                return overrideString.modified.Translate(args);
            }
            else
            {
                return original.Translate(args);
            }
        }
    }

    public class TemperatureManagementProperties
    {
        /// <summary>
        /// If this is set to false, the building is at the mercy of the environment.
        /// </summary>
        public bool hasTemperatureManagement;
        /// <summary>
        /// How much juice the temperature regulator has got against the elements. 50 means it can survive temperature ranges from (minimum - 50) to (maximum + 50).
        /// </summary>
        public float temperatureManagementStrength = 50f;
        /// <summary>
        /// The amount of power the temperature regulator takes.
        /// </summary>
        public float powerConsumptionExtra = 250;
    }
}
