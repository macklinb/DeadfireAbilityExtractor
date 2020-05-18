using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace DeadfireAbilityExtractor
{
    [JsonObject(MemberSerialization.OptOut)]
    public class ExtractedAbilityDataCollection
    {
        public List<ExtractedAbilityData> data = new List<ExtractedAbilityData>();
    }

    [JsonObject(MemberSerialization.OptOut), Serializable]
    public class ExtractedAbilityData
    {
        // Info
        public Guid guid;
        public string internalname;
        public string name;
        public string description;
        public string turnBasedDescription;
        public string icon;

        public SerializedRect iconRect;

        // Ability info
        public string abilityClass;
        public int? abilityLevel;
        public string abilityType;
        public string modalGroup;

        [JsonConverter(typeof(StringEnumConverter))]
        public AbilityOrigin abilityOrigin;

        // Learning and upgrades
        [JsonConverter(typeof(StringEnumConverter))]
        public LearnType learnType;

        public int? learnLevel;
        public int? learnLevelMc;
        public string[] upgradesTo;
        public string upgradesFrom;

        public string[] keywords;
        public string[] counters;

        // These arrays are not always parallel with the above arrays
        public string[] keywordIds;
        public string[] counterIds;

        // Activation
        [JsonConverter(typeof(StringEnumConverter))]
        public ActivationType activation;

        public string source;
        public int? sourceCost;
        public int? uses;

        [JsonConverter(typeof(StringEnumConverter))]
        public RestorationType restoration;

        // Restrictions/requirements

        public bool combatOnly;

        // Casting conditionals
        // Remove [JsonIgnore] to include these in serialization. Keep in mind that they cannot be deserialized without a custom JSON.Net deserializer contract
        [JsonIgnore] public OEIFormats.FlowCharts.ConditionalExpression activationPrerequisites;
        [JsonIgnore] public OEIFormats.FlowCharts.ConditionalExpression applicationPrerequisites;
        [JsonIgnore] public OEIFormats.FlowCharts.ConditionalExpression deactivationPrerequisites;

        // Unlock conditionals
        [JsonIgnore] public List<OEIFormats.FlowCharts.ConditionalExpression> unlockPrerequisites = new List<OEIFormats.FlowCharts.ConditionalExpression>();
        [JsonIgnore] public List<OEIFormats.FlowCharts.ConditionalExpression> visibilityPrerequisites = new List<OEIFormats.FlowCharts.ConditionalExpression>();

        // Effects and casting

        public float? castTime;
        public float? recoveryTime;
        public string range;
        public string areaOfEffect;
        public float? duration;
        public float? linger;
        public string noiseUse;
        public string noiseImpact;

        public List<EffectBlock> effectBlocks;

        // Related (of those that can be easily inferred from game data)

        public string[] relatedItems;

        // Stack block from inspector

        public string statBlockRaw;
        public Dictionary<string, string> statBlockPairsAll;
        public Dictionary<string, string> statBlockPairsUnused;

        // Debug

        public List<string> gameDataReferencingThisAbility;

        public string ToJson()
        {
            var settings = new JsonSerializerSettings()
            {
                NullValueHandling = NullValueHandling.Ignore
            };

            return JsonConvert.SerializeObject(this, Formatting.Indented, settings);
        }
    }

    [JsonObject(MemberSerialization.OptOut), Serializable]
    public class EffectBlock
    {
        public string target;
        public string effects;
        public string effectsUnformatted;
    }
}
