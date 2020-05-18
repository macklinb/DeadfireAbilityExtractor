using System;
using System.IO;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using DeadfireAbilityExtractor;
using System.Linq;
using System.Text;

namespace DeadfireAbility.Infobox
{
    class Program
    {
        // Typical use is
        // DeadfireAbilityInfobox.exe"D:\System\Steam\steamapps\common\Pillars of Eternity II\DeadfireAbilityExtractor\extracted" "D:\System\Steam\steamapps\common\Pillars of Eternity II\DeadfireAbilityExtractor\infobox"

        static void Main(string[] args)
        {
            if (args == null || args.Length == 0)
            {
                Console.WriteLine("DeadfireAbilityInfobox.exe <path to extracted data>.json");
                return;
            }

            string inputPath = args[0];

            // Serialize entire directory
            if (Directory.Exists(inputPath))
            {
                string outputDir = string.Empty;

                if (args.Length == 2)
                {
                    outputDir = args[1];

                    if (!Directory.Exists(outputDir))
                    {
                        try
                        {
                            Directory.CreateDirectory(outputDir);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine("Output path invalid");
                            return;
                        }
                    }
                }
                else
                {
                    Console.WriteLine("Output path missing");
                    return;
                }

                foreach (string filePath in Directory.EnumerateFiles(inputPath, "*.json"))
                {
                    string outputPath = Path.Combine(outputDir, Path.GetFileName(filePath));
                    outputPath = Path.ChangeExtension(outputPath, ".txt");

                    LoadAbilityAndSavePageText(filePath, outputPath);
                }
            }

            // Serialize single file
            else if (File.Exists(inputPath))
            {
                string outputPath = Path.ChangeExtension(inputPath, ".txt");
                LoadAbilityAndSavePageText(inputPath, outputPath);
            }
        }

        static void LoadAbilityAndSavePageText(string inputPath, string outputPath)
        {
            // Load ability data from json file
            var data = LoadAbilityData(inputPath);

            if (data != null)
            {
                // Create wiki page from json
                string text = CreatePage(data);

                // Create the output directory if it doesn't exist
                string outputPathFolder = Path.GetDirectoryName(outputPath);

                if (!Directory.Exists(outputPathFolder))
                    Directory.CreateDirectory(outputPathFolder);

                File.WriteAllText(outputPath, text);
            }
        }

        static ExtractedAbilityData LoadAbilityData(string filePath)
        {
            string json = string.Empty;
            ExtractedAbilityData data = null;

            // Attempt to load file
            try
            {
                if (!File.Exists(filePath))
                {
                    Console.WriteLine("File not found!");
                    return null;
                }

                json = File.ReadAllText(filePath);
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception while loading file\n" + e.ToString());
                return null;
            }

            // Attempt to deserialize file
            try
            {
                data = JsonConvert.DeserializeObject<ExtractedAbilityData>(json);
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception occurred while deserializing\n" + e.ToString());
                return null;
            }

            if (data == null || data.guid == Guid.Empty)
                return null;

            return data;
        }

        static string CreatePage(ExtractedAbilityData data)
        {
            string turnBasedDescription = "";

            if (!string.IsNullOrEmpty(data.turnBasedDescription))
                turnBasedDescription = "'''Turn-based mode description:'''\n" + data.turnBasedDescription;

            string infoboxText = CreateInfobox(data);
            string pageText = PAGE_TEMPLATE.Replace("{0}", infoboxText).Replace("{1}", turnBasedDescription);

            return pageText;
        }

        static string CreateInfobox(ExtractedAbilityData data)
        {
            if (data == null)
                return null;

            // This is a bit of a mess, but it does the job
            
            string infobox = INFOBOX_TEMPLATE;
            AppendInfoboxField("name", data.name, ref infobox);
            AppendInfoboxField("icon", data.icon, ref infobox);
            AppendInfoboxField("description", data.description, ref infobox);
            AppendInfoboxField("added_in", DetermineDLC(data.internalname), ref infobox);
            AppendInfoboxField("class", data.abilityClass, ref infobox);
            // skip subclass
            // skip race
            // skip subrace
            if (data.activation != ActivationType.None)
                AppendInfoboxField("activation", data.activation.ToString(), ref infobox);
            // skip activation_req
            AppendInfoboxField("combat_only", data.combatOnly ? "yes" : "no", ref infobox);
            AppendInfoboxField("ability_type", data.abilityType, ref infobox);
            AppendInfoboxField("ability_level", data.abilityLevel.ToString(), ref infobox);
            if (data.abilityOrigin != AbilityOrigin.None)
                AppendInfoboxField("ability_origin", data.abilityOrigin.ToString(), ref infobox);
            AppendInfoboxField("modal_group", data.modalGroup, ref infobox);
            if (data.learnType != LearnType.None)
                AppendInfoboxField("learn_type", data.learnType.ToString(), ref infobox);
            if (data.learnLevel != null)
                AppendInfoboxField("learn_level", data.learnLevel.ToString(), ref infobox);
            if (data.learnLevelMc != null)
                AppendInfoboxField("learn_level_mc", data.learnLevelMc.ToString(), ref infobox);
            AppendInfoboxField("upgrades_from", data.upgradesFrom, ref infobox);
            if (data.upgradesTo != null && data.upgradesTo.Length > 0)
                AppendInfoboxField("upgrades_to", string.Join(";", data.upgradesTo), ref infobox);
            if (data.keywords != null && data.keywords.Length > 0)
                AppendInfoboxField("keywords", string.Join(", ", data.keywords), ref infobox);
            if (data.counters != null && data.counters.Length > 0)
                AppendInfoboxField("counters", string.Join(", ", data.counters), ref infobox);
            AppendInfoboxField("source", data.source, ref infobox);
            AppendInfoboxField("source_cost", data.sourceCost.ToString(), ref infobox);
            if (data.uses != null && (int)data.uses > 0)
                AppendInfoboxField("uses", data.uses.ToString(), ref infobox);
            if (data.restoration != RestorationType.None)
                AppendInfoboxField("restoration", data.restoration.ToString(), ref infobox);
            AppendInfoboxField("cast_time", string.Format("{0:0.0}", data.castTime), ref infobox);
            AppendInfoboxField("recovery_time", string.Format("{0:0.0}", data.recoveryTime), ref infobox);
            AppendInfoboxField("range", data.range, ref infobox);

            // Trim trailing unit 'm' from end
            if (data.areaOfEffect != null && float.TryParse(data.areaOfEffect.TrimEnd('m'), out float aoe))
                data.areaOfEffect = data.areaOfEffect.TrimEnd('m');

            AppendInfoboxField("area_of_effect", data.areaOfEffect, ref infobox);
            AppendInfoboxField("duration", string.Format("{0:0.0}", data.duration), ref infobox);
            AppendInfoboxField("linger", string.Format("{0:0.0}", data.linger), ref infobox);
            AppendInfoboxField("noise_use", data.noiseUse, ref infobox);
            AppendInfoboxField("noise_impact", data.noiseImpact, ref infobox);

            if (data.effectBlocks != null && data.effectBlocks.Count > 0)
            {
                if (data.effectBlocks.Count == 1)
                {
                    AppendInfoboxField("target", data.effectBlocks[0].target, ref infobox);
                    AppendInfoboxField("effects", AddLinksToEffects(data.effectBlocks[0].effects), ref infobox);
                }
                else
                {
                    for (int i = 0; i < data.effectBlocks.Count; i++)
                    {
                        string suffix = "_" + (i + 1);
                        AddNewInfoboxField("target" + suffix, data.effectBlocks[i].target, ref infobox, "rel_quests", true);
                        AddNewInfoboxField("effects" + suffix, AddLinksToEffects(data.effectBlocks[i].effects), ref infobox, "rel_quests", true);
                    }
                }
            }

            if (data.statBlockPairsUnused != null && data.statBlockPairsUnused.Count > 0)
            {
                int index = 0;

                foreach (var pair in data.statBlockPairsUnused)
                {
                    AddNewInfoboxField("label_" + (index + 1), pair.Key, ref infobox, "rel_quests", true);
                    AddNewInfoboxField("data_" + (index + 1), pair.Value, ref infobox, "rel_quests", true);
                    index++;
                }
            }

            if (data.relatedItems != null)
                AppendInfoboxField("rel_items", string.Join(";", data.relatedItems), ref infobox);

            AppendInfoboxField("internalname", data.internalname, ref infobox);
            AppendInfoboxField("guid", data.guid.ToString(), ref infobox);

            return infobox;
        }

        static string AddLinksToEffects(string effects)
        {
            // Misc keywords
            effects = effects.ReplaceLinkText("Accuracy");
            effects = effects.ReplaceLinkText("Health");
            effects = effects.ReplaceLinkText("Resistance");
            effects = effects.ReplaceLinkText("Weakness");
            effects = effects.ReplaceLinkText("Concentration");
            effects = effects.ReplaceLinkText("Interrupt");
            effects = effects.ReplaceLinkText("Recovery");
            effects = effects.ReplaceLinkText("Recovery Time");
            effects = effects.ReplaceLinkText("Penetration");
            effects = effects.ReplaceLinkText("Stride");
            effects = effects.ReplaceLinkText("Poison");

            effects = effects.ReplaceLinkText("Focus");
            effects = effects.ReplaceLinkText("Guile");

            effects = effects.ReplaceLinkText("Flanked");
            effects = effects.ReplaceLinkText("Engaged");

            effects = effects.ReplaceLinkText("Armor Rating");

            // Damage types
            effects = effects.ReplaceLinkText("Slash Damage");
            effects = effects.ReplaceLinkText("Slash");
            effects = effects.ReplaceLinkText("Pierce Damage");
            effects = effects.ReplaceLinkText("Pierce");
            effects = effects.ReplaceLinkText("Crush Damage");
            effects = effects.ReplaceLinkText("Crush");
            effects = effects.ReplaceLinkText("Shock Damage");
            effects = effects.ReplaceLinkText("Shock");
            effects = effects.ReplaceLinkText("Burn Damage");
            effects = effects.ReplaceLinkText("Burn");
            effects = effects.ReplaceLinkText("Freeze Damage");
            effects = effects.ReplaceLinkText("Freeze");
            effects = effects.ReplaceLinkText("Corrode Damage");
            effects = effects.ReplaceLinkText("Corrode");
            effects = effects.ReplaceLinkText("Raw Damage");
            effects = effects.ReplaceLinkText("Raw");

            // Status effects
            effects = effects.ReplaceLinkText("Sickened", false, "Status effects (Deadfire)#");
            effects = effects.ReplaceLinkText("Weakened", false, "Status effects (Deadfire)#");
            effects = effects.ReplaceLinkText("Enfeebled", false, "Status effects (Deadfire)#");
            effects = effects.ReplaceLinkText("Hobbled", false, "Status effects (Deadfire)#");
            effects = effects.ReplaceLinkText("Immobilized", false, "Status effects (Deadfire)#");
            effects = effects.ReplaceLinkText("Paralyzed", false, "Status effects (Deadfire)#");
            effects = effects.ReplaceLinkText("Petrified", false, "Status effects (Deadfire)#");
            effects = effects.ReplaceLinkText("Staggered", false, "Status effects (Deadfire)#");
            effects = effects.ReplaceLinkText("Dazed", false, "Status effects (Deadfire)#");
            effects = effects.ReplaceLinkText("Stunned", false, "Status effects (Deadfire)#");
            effects = effects.ReplaceLinkText("Confused", false, "Status effects (Deadfire)#");
            effects = effects.ReplaceLinkText("Charmed", false, "Status effects (Deadfire)#");
            effects = effects.ReplaceLinkText("Dominated", false, "Status effects (Deadfire)#");
            effects = effects.ReplaceLinkText("Distracted", false, "Status effects (Deadfire)#");
            effects = effects.ReplaceLinkText("Disoriented", false, "Status effects (Deadfire)#");
            effects = effects.ReplaceLinkText("Blinded", false, "Status effects (Deadfire)#");
            effects = effects.ReplaceLinkText("Shaken", false, "Status effects (Deadfire)#");
            effects = effects.ReplaceLinkText("Frightened", false, "Status effects (Deadfire)#");
            effects = effects.ReplaceLinkText("Terrified", false, "Status effects (Deadfire)#");

            effects = effects.ReplaceLinkText("Fit", false, "Status effects (Deadfire)#");
            effects = effects.ReplaceLinkText("Hardy", false, "Status effects (Deadfire)#");
            effects = effects.ReplaceLinkText("Robust", false, "Status effects (Deadfire)#");
            effects = effects.ReplaceLinkText("Quick", false, "Status effects (Deadfire)#");
            effects = effects.ReplaceLinkText("Nimble", false, "Status effects (Deadfire)#");
            effects = effects.ReplaceLinkText("Swift", false, "Status effects (Deadfire)#");
            effects = effects.ReplaceLinkText("Strong", false, "Status effects (Deadfire)#");
            effects = effects.ReplaceLinkText("Tenacious", false, "Status effects (Deadfire)#");
            effects = effects.ReplaceLinkText("Energized", false, "Status effects (Deadfire)#");
            effects = effects.ReplaceLinkText("Smart", false, "Status effects (Deadfire)#");
            effects = effects.ReplaceLinkText("Acute", false, "Status effects (Deadfire)#");
            effects = effects.ReplaceLinkText("Brilliant", false, "Status effects (Deadfire)#");
            effects = effects.ReplaceLinkText("Insightful", false, "Status effects (Deadfire)#");
            effects = effects.ReplaceLinkText("Aware", false, "Status effects (Deadfire)#");
            effects = effects.ReplaceLinkText("Intuitive", false, "Status effects (Deadfire)#");
            effects = effects.ReplaceLinkText("Steadfast", false, "Status effects (Deadfire)#");
            effects = effects.ReplaceLinkText("Resolute", false, "Status effects (Deadfire)#");
            effects = effects.ReplaceLinkText("Courageous", false, "Status effects (Deadfire)#");

            // Afflictions
            effects = effects.ReplaceLinkText("Body Affliction", true);
            effects = effects.ReplaceLinkText("Body Afflictions", true);
            effects = effects.ReplaceLinkText("Mind Affliction", true);
            effects = effects.ReplaceLinkText("Mind Afflictions", true);
            effects = effects.ReplaceLinkText("Constitution Affliction", true);
            effects = effects.ReplaceLinkText("Constitution Afflictions", true);
            effects = effects.ReplaceLinkText("Dexterity Affliction", true);
            effects = effects.ReplaceLinkText("Dexterity Afflictions", true);
            effects = effects.ReplaceLinkText("Might Affliction", true);
            effects = effects.ReplaceLinkText("Might Afflictions", true);
            effects = effects.ReplaceLinkText("Intellect Affliction", true);
            effects = effects.ReplaceLinkText("Intellect Afflictions", true);
            effects = effects.ReplaceLinkText("Perception Affliction", true);
            effects = effects.ReplaceLinkText("Perception Afflictions", true);
            effects = effects.ReplaceLinkText("Resolve Affliction", true);
            effects = effects.ReplaceLinkText("Resolve Afflictions", true);

            // Inspirations
            effects = effects.ReplaceLinkText("Body Inspiration", true);
            effects = effects.ReplaceLinkText("Body Inspirations", true);
            effects = effects.ReplaceLinkText("Mind Inspiration", true);
            effects = effects.ReplaceLinkText("Mind Inspirations", true);
            effects = effects.ReplaceLinkText("Constitution Inspiration", true);
            effects = effects.ReplaceLinkText("Constitution Inspirations", true);
            effects = effects.ReplaceLinkText("Dexterity Inspiration", true);
            effects = effects.ReplaceLinkText("Dexterity Inspirations", true);
            effects = effects.ReplaceLinkText("Might Inspiration", true);
            effects = effects.ReplaceLinkText("Might Inspirations", true);
            effects = effects.ReplaceLinkText("Intellect Inspiration", true);
            effects = effects.ReplaceLinkText("Intellect Inspirations", true);
            effects = effects.ReplaceLinkText("Perception Inspiration", true);
            effects = effects.ReplaceLinkText("Perception Inspirations", true);
            effects = effects.ReplaceLinkText("Resolve Inspiration", true);
            effects = effects.ReplaceLinkText("Resolve Inspirations", true);

            // Defenses
            effects = effects.ReplaceLinkText("Defenses");
            effects = effects.ReplaceLinkText("Deflection");
            effects = effects.ReplaceLinkText("Fortitude");
            effects = effects.ReplaceLinkText("Reflex");
            effects = effects.ReplaceLinkText("Will");

            // Attributers
            effects = effects.ReplaceLinkText("Might");
            effects = effects.ReplaceLinkText("Constitution");
            effects = effects.ReplaceLinkText("Dexterity");
            effects = effects.ReplaceLinkText("Perception");
            effects = effects.ReplaceLinkText("Intellect");
            effects = effects.ReplaceLinkText("Resolve");

            // Skills - Active
            effects = effects.ReplaceLinkText("Alchemy");
            effects = effects.ReplaceLinkText("Arcana");
            effects = effects.ReplaceLinkText("Athletics");
            effects = effects.ReplaceLinkText("Explosives");
            effects = effects.ReplaceLinkText("Mechanics");
            effects = effects.ReplaceLinkText("Sleight of Hand");
            effects = effects.ReplaceLinkText("Stealth");

            // Skills - Passive
            effects = effects.ReplaceLinkText("Bluff");
            effects = effects.ReplaceLinkText("Diplomacy");
            effects = effects.ReplaceLinkText("History");
            effects = effects.ReplaceLinkText("Insight");
            effects = effects.ReplaceLinkText("Intimidate");
            effects = effects.ReplaceLinkText("Metaphysics");
            effects = effects.ReplaceLinkText("Religion");
            effects = effects.ReplaceLinkText("Streetwise");
            effects = effects.ReplaceLinkText("Survival");

            // Hit results
            effects = effects.ReplaceLinkText("Miss");
            effects = effects.ReplaceLinkText("Misses");
            effects = effects.ReplaceLinkText("Graze");
            effects = effects.ReplaceLinkText("Grazes");
            effects = effects.ReplaceLinkText("Hit");
            effects = effects.ReplaceLinkText("Hits");
            effects = effects.ReplaceLinkText("Critical Hit");
            effects = effects.ReplaceLinkText("Critical Hits");
            effects = effects.ReplaceLinkText("Crit");
            effects = effects.ReplaceLinkText("Crits");

            return effects;
        }

        static void AppendInfoboxField(string field, string value, ref string infoboxText)
        {
            infoboxText = AppendInfoboxField(infoboxText, field, value);
        }

        static string AppendInfoboxField(string infoboxText, string field, string value)
        {
            if (string.IsNullOrEmpty(value) || string.IsNullOrWhiteSpace(value))
                return infoboxText;
            
            string pattern = REGEX_INFOBOX_APPEND.Replace("{0}", field);
            var match = Regex.Match(infoboxText, pattern, RegexOptions.IgnoreCase);

            if (match.Success)
            {
                string capture = match.Groups[1].Value;
                infoboxText = infoboxText.Replace(capture, capture + value);
            }

            return infoboxText;
        }

        static void AddNewInfoboxField(string field, string value, ref string infoboxText, string belowField = null, bool above = false)
        {
            infoboxText = AddNewInfoboxField(infoboxText, field, value, belowField, above);
        }

        static string AddNewInfoboxField(string infoboxText, string field, string value, string belowField = null, bool above = false)
        {
            int startIndex = infoboxText.Length - 2;
            string pattern = REGEX_INFOBOX_LINE.Replace("{0}", belowField);

            if (!string.IsNullOrEmpty(belowField))
            {
                var match = Regex.Match(infoboxText, pattern, RegexOptions.IgnoreCase);
                if (match.Success) startIndex = infoboxText.IndexOf(match.Value) + (above ? 0 : match.Value.Length);
            }

            return infoboxText.Insert(startIndex, "| " + field.PadRight(15) + "= " + value + "\r\n");
        }

        // Determine the DLC given the DebugName prefix (unfortunately I don't think there's a way to determine the extracted folder from the GameData, since it's all loaded at runtime)
        static string DetermineDLC(string internalName)
        {
            int underscoreIndex = internalName.IndexOf('_');

            if (underscoreIndex == -1)
                return "poe2";

            switch (internalName.Substring(0, underscoreIndex))
            {
                case "LAX01": return "lax1";
                case "LAX02": return "lax2";
                case "LAX03": return "lax3";
                default: return "poe2";
            }
        }

        const string REGEX_INFOBOX_APPEND = @"(\|\s{0}\s*=\s*)\r\n";
        const string REGEX_INFOBOX_LINE = @"(\|\s{0}\s*=.*)\n";
        const string PAGE_TEMPLATE = @"{0}
'''{{Pagename nd}}''' is an [[Pillars of Eternity II: Deadfire abilities|ability]] in {{poe2}}.

==Description==
{{Description|{{#var:description}}}}
{1}
==Effects==
{{#var:effects_formatted}}";
        const string INFOBOX_TEMPLATE =
@"{{Infobox ability poe2
| name           = 
| icon           = 
| description    = 
| added_in       = 
| class          = 
| subclass       = 
| race           = 
| subrace        = 
| activation     = 
| activation_req = 
| combat_only    = 
| ability_type   = 
| ability_level  = 
| ability_origin = 
| modal_group    = 
| learn_type     = 
| learn_level    = 
| learn_level_mc = 
| upgrades_from  = 
| upgrades_to    = 
| keywords       = 
| counters       = 
| source         = 
| source_cost    = 
| uses           = 
| restoration    = 
| cast_time      = 
| recovery_time  = 
| range          = 
| area_of_effect = 
| duration       = 
| linger         = 
| noise_use      = 
| noise_impact   = 
| target         = 
| effects        = 
| rel_quests     = 
| rel_abilities  = 
| rel_items      = 
| rel_characters = 
| internalname   = 
| guid           = 
}}";
    }
}
