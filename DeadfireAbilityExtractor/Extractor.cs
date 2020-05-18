using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Onyx;
using OEIFormats.FlowCharts;
using Game;
using Game.UI;
using Game.GameData;
using Newtonsoft.Json;
using System.Diagnostics;

namespace DeadfireAbilityExtractor
{
    // This is intended to be used with SharpMonoInjector:
    // smi.exe inject -p PillarsOfEternityII -a DeadfireAbilityExtractor.dll -n DeadfireAbilityExtractor -c Loader -m Load

    // Missing libraries can be re-added from the PillarsOfEternityII install directory, under "Managed"
    // I don't think any of these libraries need to be present next to the dll or exe. Just in case, you can change the "Copy Local" reference property (in the solution explorer) to True to have the offending library copy to the output directory, then merge it into the built DeadfireAbilityExtractor dll with ILMerge

    // Things that seem to work with mono injection:
    // Declarations with "var" - e.g. var myVariable = new MyType()
    // try/catch blocks
    // "as" casting - e.g. myVariableAsOtherType = myVariable as OtherType
    // "is" type comparison
    // Methods with generic type arguments, e.g. Method<Type>() - doesn't include declarations

    // Things that don't seem to work with mono injection:
    // Certain Linq statements (even if the assembly versions are identical). I think it doesn't like linq statements that reference external libraries (or perhaps libraries contained in injected exe?)

    // Keep in mind that injecting this more than once will crash the game
    public class Extractor : MonoBehaviour
    {
        const string BASE_DIR = "DeadfireAbilityExtractor";

        const string IGNORE_LIST = "_ignore.txt";
        const string KEEP_LIST = "_keep.txt";
        const string INDEX = "index.txt";

        const string EXTRACT_DIR = "extracted";
        const string INFOBOX_DIR = "infobox";

        KeywordGameData weaponProficiencyKeyword = ResourceManager.GetGameDataObject<KeywordGameData>(new Guid("bb68a03d-507e-4799-9a4e-abe7377d1e56"));

        List<ProgressionUnlockableGameData> current;
        int currentIndex = -1;

        // List of all ProgressionUnlockables for manual iteration of abilities
        List<ProgressionUnlockableGameData> allProgressionUnlockables;

        // List of all BaseProgressionTableGameData
        ClassProgressionTableGameData talentProgressionTable;
        List<BaseProgressionTableGameData> allProgressionTables;

        // List of all EquippableGameData for lookups of item mods (which mod belongs to which item)
        List<EquippableGameData> allEquippables;

        // List of all ItemModGameData for lookups of item abilities
        List<ItemModGameData> allItemMods;

        // List of all RecipeData for lookups of recipes
        List<RecipeData> allRecipeData;

        // List of all BestiaryEntryGameData for lookups of bestiary abilities
        List<BestiaryEntryGameData> allBestiaryEntries;

        // List of all ConsumableGameData for lookups of consumable abilities
        List<ConsumableGameData> allConsumables;

        string workingDir;
        string extractDir;
        string iconDir;

        Dictionary<Guid, string> ignoreList;
        Dictionary<Guid, string> keepList;

        void Start()
        {
            workingDir = Path.Combine(Environment.CurrentDirectory, BASE_DIR);
            extractDir = Path.Combine(workingDir, EXTRACT_DIR);

            Game.Console.AddMessage("DeadfireAbilityBlockExtractor loaded (Unity v" + Application.unityVersion + ")");
            Game.Console.AddMessage("Working directory is: " + workingDir);
            Game.Console.AddMessage("    Use Enter to show/hide inspect UI");
            Game.Console.AddMessage("    Use LeftArrow and RightArrow to cycle between abilities");
            Game.Console.AddMessage("    Use UP to add to ignore list");
            Game.Console.AddMessage("    Use DOWN to add to keep list");
            Game.Console.AddMessage("    Use Ctrl-S to serialize current ability");
            Game.Console.AddMessage("    Use Ctrl-Shift-S to serialize entire keep list");
            Game.Console.AddMessage("    Use Ctrl-K to switch view to keep list");
            Game.Console.AddMessage("    Use Ctrl-I to switch view to ignore list");
            Game.Console.AddMessage("    Use Ctrl-R to switch view to remainder (default)");
            Game.Console.AddMessage("    Use Ctrl-O to open the current extracted file");
            Game.Console.AddMessage("    Use Ctrl-Shift-O to open the current infobox file");

            LoadIndex();
            LoadListOfIDs(Path.Combine(workingDir, IGNORE_LIST), out ignoreList);
            LoadListOfIDs(Path.Combine(workingDir, KEEP_LIST), out keepList);
            //StartCoroutine(LoadTextureRuntime(@"https://gamepedia.cursecdn.com/eternitywiki/2/28/Poe2_SpellAbilityIcons.png");

            // Load all ProgressionUnlockables
            allProgressionUnlockables = new List<ProgressionUnlockableGameData>();
            ResourceManager.GetGameDataObjects<ProgressionUnlockableGameData>(allProgressionUnlockables);

            // Set current list to remainder (not ignored and not kept)
            current = allProgressionUnlockables.Where(x => !ignoreList.ContainsKey(x.ID) && !keepList.ContainsKey(x.ID)).ToList();

            Game.Console.AddMessage(string.Format("{0} entries (keep: {1}, ignore: {2}, remaining: {3})", allProgressionUnlockables.Count, keepList.Count, ignoreList.Count, current.Count));

            // Pre-fetch all BaseProgressionTableGameData
            allProgressionTables = new List<BaseProgressionTableGameData>();
            ResourceManager.GetGameDataObjects<BaseProgressionTableGameData>(allProgressionTables);

            // Get reference to ClassProgressionTableGameData for talents
            talentProgressionTable = ResourceManager.GetGameDataObject<ClassProgressionTableGameData>(new Guid("25f42f57-6bfb-4c30-bdfa-857a95fd5fd5"));

            // Load all ItemMods
            allItemMods = new List<ItemModGameData>();
            ResourceManager.GetGameDataObjects<ItemModGameData>(allItemMods);

            // Load all RecipeData
            allRecipeData = new List<RecipeData>();
            ResourceManager.GetGameDataObjects<RecipeData>(allRecipeData);

            // Load all Equipabbles
            allEquippables = new List<EquippableGameData>();
            ResourceManager.GetGameDataObjects<EquippableGameData>(allEquippables);

            // Load all BestiaryEntrys
            allBestiaryEntries = new List<BestiaryEntryGameData>();
            ResourceManager.GetGameDataObjects<BestiaryEntryGameData>(allBestiaryEntries);

            // Load all BestiaryEntrys
            allConsumables = new List<ConsumableGameData>();
            ResourceManager.GetGameDataObjects<ConsumableGameData>(allConsumables);

            Application.logMessageReceived += (logString, stackTrace, type) =>
            {
                logString = logString + "\n" + stackTrace;

                if (type == LogType.Warning)
                    logString = "<color=yellow>" + logString + "</color>";
                else if (type == LogType.Error || type == LogType.Exception)
                    logString = "<color=red>" + logString + "</color>";

                Game.Console.AddMessage(logString);
            };
        }

        // Loads a list of guids from the file at <path> into the output Dictionary
        // File must be formatted with one GUID/DebugName pair per line, with a space-pipe-space delimiter
        void LoadListOfIDs(string path, out Dictionary<Guid, string> output)
        {
            output = new Dictionary<Guid, string>();

            try
            {
                // Load ignoreGuids
                if (File.Exists(path))
                {
                    string[] strings = File.ReadAllLines(path);

                    foreach (string str in strings)
                    {
                        output[new Guid(str.Substring(str.IndexOf(" | ") + 3))] = str.Substring(0, str.IndexOf(" | "));
                    }
                }
            }
            catch (Exception e)
            {
                Game.Console.AddMessage("Error loading list\n" + e.ToString());
            }
        }

        void SaveListOfIDs(string path, Dictionary<Guid, string> list)
        {
            List<string> lines = new List<string>();

            foreach (var item in list)
                lines.Add(item.Value + " | " + item.Key.ToString());

            File.WriteAllLines(path, lines.ToArray());
        }

        void LoadIndex()
        {
            string indexPath = Path.Combine(workingDir, INDEX);

            if (File.Exists(indexPath))
            {
                string text = File.ReadAllText(indexPath);

                if (int.TryParse(text, out int loadedIndex))
                {
                    currentIndex = loadedIndex;
                    Game.Console.AddMessage("Resuming from index " + currentIndex);
                }
            }
        }

        void SaveIndex()
        {
            File.WriteAllText(Path.Combine(workingDir, INDEX), currentIndex.ToString());
        }

        #region Game / Unity Callbacks

        void OnDisable()
        {
            SaveListOfIDs(Path.Combine(workingDir, IGNORE_LIST), ignoreList);
            SaveListOfIDs(Path.Combine(workingDir, KEEP_LIST), keepList);
            SaveIndex();
        }

        void Update()
        {
            // Ctrl-Shift-S -> Serialize all abilities in keep list
            if (Input.GetKeyDown(KeyCode.S) && Input.GetKey(KeyCode.LeftControl) && Input.GetKey(KeyCode.LeftShift))
            {
                // Create extract directory if it doesn't exist
                if (!Directory.Exists(extractDir)) Directory.CreateDirectory(extractDir);

                // Parse each line in the file for valid ability IDs
                LoadListOfIDs(Path.Combine(workingDir, KEEP_LIST), out Dictionary<Guid, string> idsToExtract);

                Game.Console.AddMessage("Serializing " + idsToExtract.Count + " abilities");

                var extractedData = new ExtractedAbilityDataCollection();

                foreach (Guid guid in idsToExtract.Keys)
                //foreach (var gameData in allProgressionUnlockables)
                {
                    //if (!(gameData is GenericAbilityGameData || gameData is PhraseGameData))
                    //    continue;

                    //Guid guid = gameData.ID;

                    try
                    {
                        // Serialize ability
                        var data = ExtractAbilityData(guid);
                        extractedData.data.Add(data);

                        // Save json
                        try
                        {
                            string json = data.ToJson();
                            string savePath = Path.Combine(extractDir, data.internalname + ".json");

                            // Save file
                            File.WriteAllText(savePath, json);
                        }
                        catch (Exception e)
                        {
                            Game.Console.AddMessage("Failed to serialize " + guid + ":\n" + e.ToString());
                            continue;
                        }

                        // Save icon
                        /*
                        try
                        {
                            if (!string.IsNullOrEmpty(data.icon))
                                CreateIconAndSave(data.iconRect, data.icon);
                        }
                        catch (Exception e)
                        {
                            Game.Console.AddMessage("Failed to save icon " + data.icon + ":\n" + e.ToString());
                            continue;
                        }
                        */
                    }
                    catch (Exception e)
                    {
                        Game.Console.AddMessage("Failed to extract data " + guid + ":\n" + e.ToString());
                        continue;
                    }
                }

                // Save combined json

                File.WriteAllText(Path.Combine(extractDir, "_abilities.json"), JsonConvert.SerializeObject(extractedData, Formatting.Indented, new JsonSerializerSettings() { NullValueHandling = NullValueHandling.Ignore }));
            }

            // Ctrl-S -> Serialize current ability
            if (Input.GetKeyDown(KeyCode.S) && Input.GetKey(KeyCode.LeftControl) && !Input.GetKey(KeyCode.LeftShift))
            {
                if (current != null && (currentIndex >= 0 && currentIndex < current.Count))
                {
                    var data = ExtractAbilityData(current[currentIndex].ID);
                    string json = JsonConvert.SerializeObject(data, Formatting.Indented, new JsonSerializerSettings() { NullValueHandling = NullValueHandling.Ignore });
                    Game.Console.AddMessage("Serialized " + data.internalname);
                }
            }

            // Ctrl-O -> Open serialized ability
            // Ctrl-Shift-O -> Open infobox
            if (Input.GetKeyDown(KeyCode.O) && Input.GetKey(KeyCode.LeftControl))
            {
                if (currentIndex >= 0 && currentIndex < current.Count)
                {
                    string openPath = null;

                    if (Input.GetKey(KeyCode.LeftShift))
                        openPath = Path.Combine(Path.Combine(workingDir, INFOBOX_DIR), current[currentIndex].DebugName + ".txt");
                    else
                        openPath = Path.Combine(extractDir, current[currentIndex].DebugName + ".json");


                    if (File.Exists(openPath))
                    {
                        Process p = new Process();
                        p.StartInfo.FileName = "explorer";
                        p.StartInfo.Arguments = "\"" + openPath + "\"";
                        p.Start();
                    }
                    else
                    {
                        Game.Console.AddMessage("File doesn't exist at " + openPath);
                    }
                }
            }

            // Enter -> Open or close inspect window

            if (Input.GetKeyDown(KeyCode.Return))
            {
                UIItemInspectManager[] inspectWindows = FindObjectsOfType<UIItemInspectManager>();

                bool anyWindowsOpen = inspectWindows.Any(x => x.IsVisible);

                // If any are open, close all (and don't open)
                if (anyWindowsOpen)
                {
                    foreach (UIItemInspectManager inspectWindow in inspectWindows)
                    {
                        if (inspectWindow.name.EndsWith("(Clone)"))
                            ((UIHudWindow)inspectWindow).HideWindow(true);
                    }
                }
                else
                {
                    SuspendLastShowCurrent();
                }
            }

            // Left arrow -> Previous ability
            if (Input.GetKeyDown(KeyCode.LeftArrow) && current != null)
            {
                currentIndex--;
                if (currentIndex < 0) currentIndex = 0;
                SuspendLastShowCurrent();
            }

            // Right arrow -> Next ability
            if (Input.GetKeyDown(KeyCode.RightArrow) && current != null)
            {
                currentIndex++;
                if (currentIndex >= current.Count) currentIndex = current.Count - 1;
                SuspendLastShowCurrent();
            }

            // Up arrow -> Add to ignore table
            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                if (currentIndex >= 0 && currentIndex < current.Count)
                {
                    Guid id = current[currentIndex].ID;
                    string debugName = current[currentIndex].DebugName;

                    // Remove from keep table
                    if (keepList.ContainsKey(id))
                        keepList.Remove(id);

                    // Add to ignore list
                    ignoreList[id] = debugName;
                    Game.Console.AddMessage("Ignoring\n");

                    currentIndex++;
                    SuspendLastShowCurrent();
                }
                else
                {
                    SuspendLastInspectWindow();
                }
            }

            // Down arrow -> Add to keep table
            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                if (currentIndex >= 0 && currentIndex < current.Count)
                {
                    Guid id = current[currentIndex].ID;
                    string debugName = current[currentIndex].DebugName;

                    // Remove from ignore table
                    if (ignoreList.ContainsKey(id))
                        ignoreList.Remove(id);

                    // Add to keep list
                    keepList[id] = debugName;
                    Game.Console.AddMessage("Keeping\n");

                    currentIndex++;
                    SuspendLastShowCurrent();
                }
                else
                {
                    SuspendLastInspectWindow();
                }
            }

            // Ctrl-K -> Switch to keep list
            if (Input.GetKeyDown(KeyCode.K) && Input.GetKey(KeyCode.LeftControl))
            {
                current = allProgressionUnlockables.Where(x => keepList.ContainsKey(x.ID)).ToList();
                Game.Console.AddMessage("Switched to keep list");
            }

            // Ctrl-K -> Switch to ignore list
            if (Input.GetKeyDown(KeyCode.I) && Input.GetKey(KeyCode.LeftControl))
            {
                current = allProgressionUnlockables.Where(x => ignoreList.ContainsKey(x.ID)).ToList();
                Game.Console.AddMessage("Switched to ignore list");
            }

            // Ctrl-R -> Switch to remaining list
            if (Input.GetKeyDown(KeyCode.R) && Input.GetKey(KeyCode.LeftControl))
            {
                current = allProgressionUnlockables.Where(x => !ignoreList.ContainsKey(x.ID) && !keepList.ContainsKey(x.ID)).ToList();
                Game.Console.AddMessage("Switched to remaining list");
            }
        }

        // Suspends any active inspect windows, then shows an inspect window for the current ability
        void SuspendLastShowCurrent()
        {
            if (current == null || currentIndex < 0 || currentIndex > current.Count)
                return;
            
            var gameData = current[currentIndex];
            Game.Console.AddMessage("#" + currentIndex + " - " + gameData.DebugName);

            SuspendLastInspectWindow();
            InspectAbilityData(gameData);

            // Debug FindGameDataReferencingAbility results
            FindGameDataReferencingAbility(gameData, true, out var _);
        }

        void InspectAbilityData(string abilityID)
        {
            var guid = new Guid(abilityID);
            Onyx.GameDataObject gameData = Game.ResourceManager.GetGameDataObject(guid);

            InspectAbilityData(gameData);
        }

        void InspectAbilityData(Onyx.GameDataObject gameDataObject)
        {
            IInspectable inspectable = gameDataObject as IInspectable;

            if (inspectable != null)
                InspectAbilityData(inspectable);
            else
                Game.Console.AddMessage("The object " + gameDataObject.DebugName + " cannot be inspected");
        }

        void InspectAbilityData(IInspectable inspectable)
        {
            if (inspectable != null)
                UIItemInspectManager.Examine(inspectable);
        }

        void SuspendLastInspectWindow()
        {
            UIItemInspectManager[] inspectWindows = FindObjectsOfType<UIItemInspectManager>();
            
            foreach (UIItemInspectManager inspectWindow in inspectWindows)
            {
                if (inspectWindow.name.EndsWith("(Clone)"))
                    ((UIHudWindow)inspectWindow).HideWindow(true);
            }
        }

        #endregion

        #region Extractor

        ExtractedAbilityData ExtractAbilityData(string abilityID)
        {
            // Get GameDataObject with this Guid
            var guid = new Guid(abilityID);

            return ExtractAbilityData(guid);
        }

        ExtractedAbilityData ExtractAbilityData(Guid guid)
        {
            Onyx.GameDataObject gameData = Game.ResourceManager.GetGameDataObject(guid);

            // ExtractedAbilityData represents information collected from this ability.
            var data = new ExtractedAbilityData();

            // Generic GameData fields
            data.guid = gameData.ID;
            data.internalname = gameData.DebugName;

            // Check if the GameDataObject implements certain interfaces
            IIcon gameDataIcon = gameData as IIcon;
            IInspectable gameDataInspectable = gameData as IInspectable;

            // IIcon fields
            if (gameDataIcon != null)
            {
                // SpriteKey seems to be a sprite atlas a SpriteName. We can use the SpriteName to get the cropping rect.
                var spriteKey = gameDataIcon.GetSpriteIcon();

                if (spriteKey.IsEmpty == false && spriteKey.IsValid)
                {
                    data.icon = spriteKey.SpriteName + "_icon.png";

                    // I was going to export each icon here, but the Texture2D isn't marked as read/write (meaning no access to raw bytes). Instead we'll just save the iconRect for cropping later on using a copy of the atlas already exported.
                    var sprite = spriteKey.Atlas.GetSprite(spriteKey.SpriteName);
                    data.iconRect = sprite.inner;
                }
            }
            else
            {
                Game.Console.AddMessage("GameData does not implement IIcon!");
            }

            // IInspectable fields
            if (gameDataInspectable != null)
            {
                // Here we get data directly from the inspectable data (what is shown in the in-game inspect window), instead of from GameData. This way the values are as close to in-game as possible

                // We use it to infer some data related to attacks, to make things a bit easier

                data.name = gameDataInspectable.GetName();
                data.description = StripHtmlFromString(gameDataInspectable.GetFlavorText()).Trim();
                data.statBlockRaw = StripHtmlFromString(gameDataInspectable.GetStatBlock(null));

                // Cast/recovery time
                float castTime = 0.0f;
                float recoveryTime = 0.0f;

                string castTimeString = GetValueFromStatBlock("Cast Time", data.statBlockRaw);
                string recoveryTimeString = GetValueFromStatBlock("Recovery Time", data.statBlockRaw);

                if (!string.IsNullOrEmpty(castTimeString))
                {
                    if (float.TryParse(castTimeString, out castTime))
                        data.castTime = castTime;
                    else if (castTimeString == "Instant")
                        data.castTime = 0.0f;
                }

                // Recovery time for modals is always 3.0 seconds (we check for modals below)
                if (!string.IsNullOrEmpty(recoveryTimeString))
                {
                    if (float.TryParse(recoveryTimeString, out recoveryTime))
                        data.recoveryTime = recoveryTime;
                    else if (recoveryTimeString == "Instant")
                        data.recoveryTime = 0.0f;
                }

                // Range
                data.range = GetValueFromStatBlock("Range", data.statBlockRaw);

                // Area of effect
                data.areaOfEffect = GetValueFromStatBlock("Area of Effect", data.statBlockRaw);

                // Duration
                string durationStr = GetValueFromStatBlock("Duration", data.statBlockRaw);

                if (string.IsNullOrEmpty(durationStr))
                    durationStr = GetValueFromStatBlock("AoE Duration", data.statBlockRaw);
                if (!string.IsNullOrEmpty(durationStr))
                {
                    durationStr = durationStr.Remove(durationStr.IndexOf(" sec"));
                    if (float.TryParse(durationStr, out float duration))
                        data.duration = duration;
                }

                // Linger
                string lingerStr = GetValueFromStatBlock("Linger", data.statBlockRaw);

                if (!string.IsNullOrEmpty(lingerStr))
                {
                    lingerStr = lingerStr.Remove(lingerStr.IndexOf(" sec"));
                    if (float.TryParse(lingerStr, out float linger))
                        data.linger = linger;
                }

                // Noise
                string noiseString = GetValueFromStatBlock("Noise", data.statBlockRaw);

                if (!string.IsNullOrEmpty(noiseString))
                {
                    if (noiseString.Contains("/"))
                    {
                        int slashIndex = noiseString.IndexOf('/');
                        data.noiseUse = noiseString.Substring(0, slashIndex - " (Use) ".Length);
                        data.noiseImpact = noiseString.Substring(slashIndex + 2, noiseString.IndexOf(" (Impact)") - slashIndex - 2);
                    }
                    else
                    {
                        data.noiseUse = data.noiseImpact = noiseString;
                    }
                }

                // Other stat block pairs
                data.statBlockPairsAll = GetStatBlockPairs(data.statBlockRaw);
                data.statBlockPairsUnused = new Dictionary<string, string>(data.statBlockPairsAll);

                // Remove pairs that we have already used
                data.statBlockPairsUnused.Remove("Keywords");
                data.statBlockPairsUnused.Remove("Counters");
                data.statBlockPairsUnused.Remove("Cost");
                data.statBlockPairsUnused.Remove("Cast Time");
                data.statBlockPairsUnused.Remove("Recovery Time");
                data.statBlockPairsUnused.Remove("Range");
                data.statBlockPairsUnused.Remove("Aura Range");
                data.statBlockPairsUnused.Remove("Area of Effect");
                data.statBlockPairsUnused.Remove("Duration");
                data.statBlockPairsUnused.Remove("AoE Duration");
                data.statBlockPairsUnused.Remove("Linger");
                data.statBlockPairsUnused.Remove("Noise");
                data.statBlockPairsUnused.Remove("Uses");

                // Effects

                StringEffects stringEffects = new StringEffects();
                gameDataInspectable.GetStatBlockEffects(stringEffects, null);

                data.effectBlocks = new List<EffectBlock>();

                // Loop through the StringEffects and create a new EffectBlock for each
                foreach (var kvp in stringEffects.Effects)
                {
                    var effectBlock = new EffectBlock();
                    effectBlock.target = kvp.Key;

                    // The StringEffect string representations currently don't have qualifiers like "If Successful", etc. Only the UIItemInspectStringEffect does this
                    string effectsString = Game.UI.UIItemInspectStringEffect.GetEffectsString(null, null, kvp.Value, null);
                    effectBlock.effectsUnformatted = effectsString;

                    // Substitute damage type icon with wiki template
                    var damageTypeRegex = new Regex(@"<link[:=]""neutralvalue://(.*?)""><sprite[:=]""Inline"" name[:=]""cs_.*?"" tint[:=]1></link>");

                    int matchIndex = 0;

                    foreach (Match match in damageTypeRegex.Matches(effectsString))
                    {
                        string damageType = match.Groups[1].Value.ToLower();

                        effectsString = effectsString.Replace(match.Value, (matchIndex == 0 ? " {{" : "{{") + damageType + "}}");

                        matchIndex++;
                    }

                    // Substitute pipe with pipe template
                    effectsString = effectsString.Replace("|", "{{!}}");

                    // Remove HTML
                    effectBlock.effects = StripHtmlFromString(effectsString);

                    data.effectBlocks.Add(effectBlock);
                }
            }
            else
            {
                Game.Console.AddMessage("GameData does not implement IInspectable!");
            }

            // ProgressionUnlockableGameData fields (+ derivatives)

            // There are only two classes that are directly derived from from ProgressionUnlockableGameData
            // PhraseGameData and GenericAbilityGameData
            // There is a third - GenericTalentGameData, but it is seemingly deprecated 
            // Unfortunately they declare their own unique components, so we have to get them separately.

            if (gameData is ProgressionUnlockableGameData == false)
            {
                Game.Console.AddMessage(data.internalname + " isn't ProgressionUnlockableGameData");
                return null;
            }

            PhraseGameData phraseGameData = gameData as PhraseGameData;
            GenericAbilityGameData abilityGameData = gameData as GenericAbilityGameData;

            if (phraseGameData == null && abilityGameData == null)
            {
                Game.Console.AddMessage("GameData isn't a GenericAbility or Phrase?");
                return null;
            }

            // Mixed fields
            else
            {
                data.abilityLevel = phraseGameData != null ? phraseGameData.Level : abilityGameData.AbilityLevel;

                // Find data referencing this ability to determine its origin
                FindGameDataReferencingAbility(gameData, false, out FindGameDataReferencingAbilityResults findResults);
                data.abilityOrigin = findResults.guessedAbilityOrigin;

                // Add the debug names of all objects "with" the ability

                data.gameDataReferencingThisAbility = findResults.allObjects.Select(x => x.DebugName).ToList();

                // Related items
                var relatedItemsTemp = new List<string>();

                if (findResults.equippables != null && findResults.equippables.Count > 0)
                    relatedItemsTemp.AddRange(findResults.equippables.Select(x => x.GetDisplayName()));
                if (findResults.consumables != null && findResults.consumables.Count > 0)
                    relatedItemsTemp.AddRange(findResults.equippables.Select(x => x.GetDisplayName()));

                if (relatedItemsTemp.Count > 0)
                    data.relatedItems = relatedItemsTemp.ToArray();

                // Get all ProgressionTableGameData referencing this ability
                var unlockableAbilities = new List<UnlockableAbility>();

                // Only include the character tables in the data to look at for the LearnType if this ability doesn't have a class ID, since the way that a class learns an ability should  take precedence over ways that other classes learn the same ability, at least for the wiki listing
                bool includeCharacterTables = abilityGameData != null && abilityGameData.AbilityClassID == Guid.Empty;
                FindUnlockableAbilitiesContainingAbility(gameData, includeCharacterTables, out unlockableAbilities);

                // Determine the LearnType from the UnlockStyle field
                if (unlockableAbilities.Count > 0)
                {
                    // Check if all UnlockableAbilities are the same
                    // Unfortunately linq won't work here, to we have to manually iterate
                    bool differentUnlockStyles = false;
                    ProgressionUnlockStyle unlockStyle = unlockableAbilities.First().UnlockStyle;

                    foreach (var ua in unlockableAbilities)
                    {
                        // If any are different from the first
                        if (ua.UnlockStyle != unlockStyle)
                        {
                            differentUnlockStyles = true;
                            break;
                        }
                    }

                    // Learn type
                    if (differentUnlockStyles)
                        data.learnType = LearnType.Mixed;
                    else if (unlockStyle == ProgressionUnlockStyle.AutoGrant)
                        data.learnType = LearnType.Automatic;
                    else if (unlockStyle == ProgressionUnlockStyle.Unlock)
                        data.learnType = LearnType.Optional;
                    else
                        data.learnType = LearnType.None;

                    // Unlock conditionals
                    foreach (var ua in unlockableAbilities)
                    {
                        // Ignore if all conditionals are upgrade conditions (i.e. ProgressionTableHasAbility)
                        // (include if any conditions are not)
                        // Yet again, we can't use linq here :(

                        bool includeUnlockPrerequisites = false;
                        bool includeVisibilityPrerequisites = false;

                        foreach (ExpressionComponent ec in ua.Prerequisites.Conditional.Components)
                        {
                            ConditionalCall cc = ec as ConditionalCall;

                            if (cc != null && cc.Data.FullName != "Boolean ProgressionTableHasAbility(Guid)")
                            {
                                includeUnlockPrerequisites = true;
                                break;
                            }
                        }

                        foreach (ExpressionComponent ec in ua.Prerequisites.VisibilityConditional.Components)
                        {
                            ConditionalCall cc = ec as ConditionalCall;

                            if (cc != null && cc.Data.FullName != "Boolean ProgressionTableHasAbility(Guid)")
                            {
                                includeVisibilityPrerequisites = true;
                                break;
                            }
                        }

                        if (includeVisibilityPrerequisites)
                            data.visibilityPrerequisites.Add(ua.Prerequisites.VisibilityConditional);
                        if (includeVisibilityPrerequisites)
                            data.unlockPrerequisites.Add(ua.Prerequisites.Conditional);
                    }
                }

                // Learn level (only if learn-by-level)
                if (data.learnType != LearnType.None)
                {
                    data.learnLevel = CharacterProgressionGameData.Instance.PowerLevelIncreasesAtWhatCharacterLevel((int)data.abilityLevel, false);
                    data.learnLevelMc = CharacterProgressionGameData.Instance.PowerLevelIncreasesAtWhatCharacterLevel((int)data.abilityLevel, true);
                }

                // Keywords + counters
                List<KeywordGameData> keywordData = phraseGameData != null ? phraseGameData.StatusEffectKeywords : abilityGameData.Keywords;
                List<KeywordGameData> counterData = AbilitySettingsGameData.Instance.GetOpposingKeywords(keywordData).ToList();

                if (keywordData != null && keywordData.Count > 0)
                {
                    var temp = new List<string>();

                    foreach (var keyword in keywordData)
                    {
                        if (keyword.HasName()) temp.Add(keyword.GetAdjective());
                    }

                    data.keywords = temp.ToArray();
                    data.keywordIds = keywordData.Select(x => x.DebugName.ToString()).ToArray();
                }

                if (counterData != null && counterData.Count > 0)
                {
                    var temp = new List<string>();

                    foreach (var counter in counterData)
                    {
                        if (counter.HasName()) temp.Add(counter.GetAdjective());
                    }

                    data.counters = temp.ToArray();
                    data.counterIds = counterData.Select(x => x.DebugName.ToString()).ToArray();
                }
            }

            // GenericAbilityGameData only
            if (abilityGameData != null)
            {
                if (abilityGameData.DescriptionTactical != -1)
                    data.turnBasedDescription = abilityGameData.GetDescriptionTactical();

                if (abilityGameData.AbilityClassID != Guid.Empty)
                    data.abilityClass = abilityGameData.AbilityClass.GetDisplayName();

                // Activation
                if (abilityGameData.IsPassive)
                    data.activation = ActivationType.Passive;
                else if (abilityGameData.IsModal)
                {
                    data.activation = ActivationType.Modal;

                    if (data.recoveryTime == null)
                        data.recoveryTime = GlobalGameSettingsGameData.Instance.ModalRecoveryTime;
                }
                else
                    data.activation = ActivationType.Active;

                // Prerequisites
                if (abilityGameData.ActivationPrerequisites.Conditional.Components.Count > 0)
                    data.activationPrerequisites = abilityGameData.ActivationPrerequisites.Conditional;
                if (abilityGameData.DeactivationPrerequisites.Conditional.Components.Count > 0)
                    data.deactivationPrerequisites = abilityGameData.DeactivationPrerequisites.Conditional;
                if (abilityGameData.ApplicationPrerequisites.Conditional.Components.Count > 0)
                    data.applicationPrerequisites = abilityGameData.ApplicationPrerequisites.Conditional;

                // Combat only
                data.combatOnly = abilityGameData.IsCombatOnly;

                // Infer abilityType firstly from AbilityClassId
                switch (abilityGameData.AbilityClass?.GetDebugName())
                {
                    case "Chanter":
                        data.abilityType = "Invocation";
                        break;
                    case "Cipher": case "Druid": case "Priest": case "Wizard":
                        data.abilityType = "Spell";
                        break;
                    case "Barbarian": case "Fighter": case "Monk": case "Paladin": case "Rogue":
                        data.abilityType = "Ability";
                        break;
                    default:
                    {
                        // Then from other sources

                        // Weapon Proficiency
                        if (abilityGameData.HasKeyword(weaponProficiencyKeyword))
                            data.abilityType = "Proficiency";

                        // Talent
                        else if (talentProgressionTable.HasAbility(abilityGameData))
                            data.abilityType = "Talent";

                        break;
                    }
                }

                // Modal groups
                if (abilityGameData.ModalGroupID != Guid.Empty)
                    data.modalGroup = abilityGameData.ModalGroup.GetDisplayName();

                // Collect all abilities this ability upgrades from and to
                var upgradesFrom = new List<ProgressionUnlockableGameData>();
                var upgradesTo = new List<ProgressionUnlockableGameData>();

                {
                    // UpgradesFrom - UpgradedFrom field of this GenericAbilityGameData
                    if (abilityGameData.UpgradedFromID != Guid.Empty)
                        upgradesFrom.Add(abilityGameData.UpgradedFrom);

                    // UpgradesTo - Abilities where the UpgradedFrom field is this ability
                    ResourceManager.GetGameDataObjects<ProgressionUnlockableGameData>(upgradesTo, x => x is GenericAbilityGameData && abilityGameData.ID == ((GenericAbilityGameData)x).UpgradedFromID);

                    // Sometimes abilities are missing the UpgradesFrom field, despite being upgraded from another ability. Because of this, we also double check the progression tables

                    // UpgradesFrom - ProgressionTables - Select all UnlockableAbilities which add this ability
                    var upgradesFromTemp = allProgressionTables.SelectMany(x => x.AbilityUnlocks.Where(y => y.AddAbilityID == abilityGameData.ID)).Select(x => x.RemoveAbility).Where(x => x.ID != Guid.Empty).Distinct();
                    if (upgradesFromTemp != null && upgradesFromTemp.Count() > 0) upgradesFrom.AddRange(upgradesFromTemp);

                    // UpgradesTo - ProgressionTables - Select all UnlockableAbilities which remove this ability
                    var upgradesToTemp = allProgressionTables.SelectMany(x => x.AbilityUnlocks.Where(y => y.RemoveAbilityID == abilityGameData.ID)).Select(x => x.AddAbility).Where(x => x.ID != Guid.Empty).Distinct();
                    if (upgradesToTemp != null && upgradesToTemp.Count() > 0) upgradesTo.AddRange(upgradesToTemp);
                }

                // Make both lists unique and remove empty GUIDs
                upgradesFrom = upgradesFrom.Where(x => x.ID != Guid.Empty).Distinct().ToList();
                upgradesTo = upgradesTo.Where(x => x.ID != Guid.Empty).Distinct().ToList();

                if (upgradesFrom.Count > 0)
                {
                    var upgradesFromStrings = new List<string>();

                    foreach (var temp in upgradesFrom)
                    {
                        var displayName = temp as IDisplayName;

                        if (displayName != null)
                            upgradesFromStrings.Add(displayName.GetName());
                        else
                            Game.Console.AddMessage(temp.DebugName + " - Cannot cast to IDisplayName");
                    }

                    // Make strings unique
                    upgradesFromStrings = upgradesFromStrings.Distinct().ToList();

                    if (upgradesFromStrings.Count > 1)
                    {
                        Game.Console.AddMessage(abilityGameData.DebugName + " - Upgrades from multiple different abilities!\n" + string.Join(",", upgradesFromStrings.ToArray()));
                    }
                    else if (upgradesFromStrings.Count == 1)
                        data.upgradesFrom = upgradesFromStrings[0];
                }

                if (upgradesTo.Count > 0)
                {
                    var upgradesToStrings = new List<string>();

                    foreach (var temp in upgradesTo)
                    {
                        var displayName = temp as IDisplayName;

                        if (displayName != null)
                            upgradesToStrings.Add(displayName.GetName());
                        else
                            Game.Console.AddMessage(temp.DebugName + " - Cannot cast to IDisplayName");
                    }

                    // Make strings unique
                    upgradesToStrings = upgradesToStrings.Distinct().ToList();

                    data.upgradesTo = upgradesToStrings.ToArray();
                }

                // Upgrades to/from (ItemMod)
                if (data.abilityOrigin == AbilityOrigin.Equipment)
                {
                    // Find the ItemMod adding this ability
                    var itemMod = allItemMods.First(x => x.AbilitiesOnEquip.Contains(abilityGameData));

                    if (allItemMods.Count(x => x.AbilitiesOnEquip.Contains(abilityGameData)) > 1)
                        Game.Console.AddMessage(abilityGameData.DebugName + " - Multiple ItemMods add this ability!");

                    // Find the RecipeData adding and removing the above ItemMod
                    if (itemMod != null)
                    {
                        // Recipes that add this ItemMod (and in turn ability)
                        var recipeDataAdding = allRecipeData.Where(x => x.ItemModsToAdd.Contains(itemMod));

                        // Recipes that remove this ItemMod (and in turn ability)
                        var recipeDataRemoving = allRecipeData.Where(x => x.ItemModsToRemove.Contains(itemMod));

                        // Upgrades from
                        // If the ItemModsToRemoveIDs contains a mod that adds an ability, it is the ability this ability "upgrades from"
                        if (recipeDataAdding != null && recipeDataAdding.Count() > 0)
                        {
                            var upgradesFromAbilities = recipeDataAdding

                                // Get list of all RecipeDatas (of those that add the ItemMod) that also remove ItemMods
                                .Where(x => x.ItemModsToRemove != null && x.ItemModsToRemove.Count > 0)

                                // Select the ItemModsToRemove
                                .SelectMany(x => x.ItemModsToRemove).Distinct()

                                // Trim the ItemMods to those that actually add abilities
                                .Where(x => x.AbilitiesOnEquip != null && x.AbilitiesOnEquip.Count > 0)

                                // Select the AbilitiesOnEquip
                                .SelectMany(x => x.AbilitiesOnEquip).Distinct();

                            if (upgradesFromAbilities != null && upgradesFromAbilities.Count() > 0)
                            {
                                if (upgradesFromAbilities.Count() == 1)
                                    data.upgradesFrom = upgradesFromAbilities.First().GetDisplayName();
                                else
                                    Game.Console.AddMessage(abilityGameData.DebugName + " - Upgraded from multiple abilities!");
                            }
                        }

                        // Upgrades to
                        // If the ItemModsToAddIDs contains a mod that remove this ability, it is the ability this ability "upgrades to"
                        if (recipeDataRemoving != null && recipeDataRemoving.Count() > 0)
                        {
                            var upgradesToAbilities = recipeDataRemoving

                                // Get list of all RecipeDatas (of those that remove the item mod) that also add ItemMods
                                .Where(x => x.ItemModsToAdd != null && x.ItemModsToAdd.Count > 0)

                                // Select the ItemModsToAdd
                                .SelectMany(x => x.ItemModsToAdd).Distinct()

                                // Trim the ItemMods to those that actually add abilities
                                .Where(x => x.AbilitiesOnEquip != null && x.AbilitiesOnEquip.Count > 0)

                                // Select the AbilitiesOnEquip
                                .SelectMany(x => x.AbilitiesOnEquip).Distinct();

                            if (upgradesToAbilities != null && upgradesToAbilities.Count() > 0)
                            {
                                data.upgradesTo = upgradesToAbilities.Select(x => x.GetDisplayName()).ToArray();
                            }
                        }
                    }
                }

                // Source/source cost and restoration/uses
                if (abilityGameData.UsageType == CooldownType.ClassAccruedResource)
                {
                    data.source = abilityGameData.AbilityClass.GetAccruedResourceName();
                    data.sourceCost = abilityGameData.UsageValue;
                }
                else if (abilityGameData.UsageType == CooldownType.ClassPowerPool)
                {
                    data.source = abilityGameData.AbilityClass.GetPowerPoolName();
                    data.sourceCost = abilityGameData.UsageValue;
                }
                else
                {
                    data.uses = abilityGameData.UsageValue;
                    data.restoration = abilityGameData.UsageType == CooldownType.PerEncounter ? RestorationType.Encounter : abilityGameData.UsageType == CooldownType.PerRest ? RestorationType.Rest : RestorationType.None;
                }
            }

            // PhraseGameData only
            if (phraseGameData != null)
            {
                data.abilityType = "Phrase";
                data.abilityClass = "Chanter";

                // Phrases are always passively activated 
                data.activation = ActivationType.Passive;
            }

            return data;
        }

        Texture2D iconAtlas;

        void CreateIconAndSave(SerializedRect rect, string fileName)
        {
            if (iconAtlas == null)
            {
                Game.Console.AddMessage("Atlas not loaded");
                return;
            }

            string filePath = Path.Combine(Path.Combine(workingDir, "icons"), fileName);

            if (File.Exists(filePath))
                return;

            try
            {
                int x = (int)rect.x;
                int y = iconAtlas.height - (int)rect.y - (int)rect.height;
                int width = (int)rect.width;
                int height = (int)rect.height;

                Color[] c = iconAtlas.GetPixels(x, y, width, height);
                var icon = new Texture2D(width, height);
                icon.SetPixels(c);
                icon.Apply();

                byte[] bytes = icon.EncodeToPNG();
                File.WriteAllBytes(filePath, bytes);
            }
            catch (Exception e)
            {
                Game.Console.AddMessage("Error while saving icon!\n" + e.ToString());
            }
        }

        #endregion
        #region Utility

        IEnumerator LoadTextureRuntime(string path)
        {
            using (var request = UnityEngine.Networking.UnityWebRequest.GetTexture(path))
            {
                yield return request.Send();

                if (request.isError)
                {
                    Game.Console.AddMessage(request.error);
                }
                else
                {
                    // Get downloaded asset bundle
                    iconAtlas = UnityEngine.Networking.DownloadHandlerTexture.GetContent(request);
                }
            }
        }

        // Finds all progression tables, bestiary entries, equippables and consumables referencing this ability, guessing the AbilityOrigin based on the results
        void FindGameDataReferencingAbility(GameDataObject gameData, bool debug, out FindGameDataReferencingAbilityResults results)
        {
            // Debug class progression tables with this ability
            FindClassProgressionTablesContainingAbility(gameData, out List<ClassProgressionTableGameData> classTables);

            if (classTables != null && classTables.Count > 0)
            {
                string[] tableNames = new string[classTables.Count];

                // Another instance where we can't use linq :(
                for (int i = 0; i < classTables.Count; i++)
                    tableNames[i] = classTables[i].DebugName;

                if (debug) Game.Console.AddMessage("\n\t" + string.Join("\n\t", tableNames));
            }
            else
            {
                //Game.Console.AddMessage("Not contained in progression tables");
            }

            // Debug progression tables with this ability
            FindCharacterProgressionTablesContainingAbility(gameData, out List<CharacterProgressionTableGameData> characterTables);

            if (characterTables != null && characterTables.Count > 0)
            {
                string[] tableNames = new string[characterTables.Count];

                // Another instance where we can't use linq :(
                for (int i = 0; i < characterTables.Count; i++)
                    tableNames[i] = characterTables[i].DebugName;

                if (debug) Game.Console.AddMessage("\n\t" + string.Join("\n\t", tableNames));
            }
            else
            {
                //Game.Console.AddMessage("Not contained in progression tables");
            }

            // Debug bestiary entries with this ability
            FindBestiaryEntiesContainingAbility(gameData, out List<BestiaryEntryGameData> bestiaryEntries);

            if (bestiaryEntries != null && bestiaryEntries.Count > 0)
            {
                string[] bestiaryEntryNames = new string[bestiaryEntries.Count];

                // Another instance where we can't use linq :(
                for (int i = 0; i < bestiaryEntries.Count; i++)
                    bestiaryEntryNames[i] = bestiaryEntries[i].DebugName;

                if (debug) Game.Console.AddMessage("\n\t" + string.Join("\n\t", bestiaryEntryNames));
            }
            else
            {
                //Game.Console.AddMessage("Not contained in bestiary entries");
            }

            // Debug equippables with this ability
            FindEquippablesContainingAbility(gameData, out List<EquippableGameData> equippables);

            if (equippables != null && equippables.Count > 0)
            {
                string[] equippablesNames = new string[equippables.Count];

                for (int i = 0; i < equippables.Count; i++)
                    equippablesNames[i] = equippables[i].DebugName;

                if (debug) Game.Console.AddMessage("\n\t" + string.Join("\n\t", equippablesNames));
            }
            else
            {
                //Game.Console.AddMessage("Not contained in equippables");
            }

            // Debug consumables with this ability
            FindConsumablesContainingAbility(gameData, out List<ConsumableGameData> consumables);

            if (consumables != null && consumables.Count > 0)
            {
                string[] consumableNames = new string[consumables.Count];

                // Another instance where we can't use linq :(
                for (int i = 0; i < consumables.Count; i++)
                    consumableNames[i] = consumables[i].DebugName;

                if (debug) Game.Console.AddMessage("\n\t" + string.Join("\n\t", consumableNames));
            }
            else
            {
                //Game.Console.AddMessage("Not contained in consumables");
            }
            
            // Construct results
            results = new FindGameDataReferencingAbilityResults();

            // Combine all results for allObjects
            results.allObjects = new List<GameDataObject>();
            results.allObjects.AddRange(classTables);
            results.allObjects.AddRange(characterTables);
            results.allObjects.AddRange(bestiaryEntries);
            results.allObjects.AddRange(equippables);
            results.allObjects.AddRange(consumables);

            results.classProgressionTables = classTables;
            results.characterProgressionTables = characterTables;
            results.bestiaryEntries = bestiaryEntries;
            results.equippables = equippables;
            results.consumables = consumables;

            // Return a guessed ability origin based on presence of ability

            // Class overrides all
            if (classTables.Count(x => x is ClassProgressionTableGameData) > 0)
                results.guessedAbilityOrigin = AbilityOrigin.Class;

            // Otherwise choose the origin with the highest number of hits
            else
            {
                Dictionary<AbilityOrigin, int> sources = new Dictionary<AbilityOrigin, int>();
                sources.Add(AbilityOrigin.Character, characterTables.Count);
                sources.Add(AbilityOrigin.Creature, bestiaryEntries.Count);
                sources.Add(AbilityOrigin.Equipment, equippables.Count);
                sources.Add(AbilityOrigin.Consumable, consumables.Count);

                results.guessedAbilityOrigin = sources.OrderByDescending(x => x.Value).First().Key;
            }
        }

        // Returns all UnlockableAbilities that unlock this ability in both ClassProgressionTables and CharacterProgressionTables
        void FindUnlockableAbilitiesContainingAbility(GameDataObject ability, bool includeCharacterTables, out List<UnlockableAbility> unlockableAbilities)
        {
            unlockableAbilities = new List<UnlockableAbility>();

            FindClassProgressionTablesContainingAbility(ability, out List<ClassProgressionTableGameData> classProgressionTables);
            unlockableAbilities.AddRange(classProgressionTables.Select(x => x.FindAbility(ability)));

            if (includeCharacterTables)
            {
                FindCharacterProgressionTablesContainingAbility(ability, out List<CharacterProgressionTableGameData> characterProgressionTables);
                unlockableAbilities.AddRange(characterProgressionTables.Select(x => x.FindAbility(ability)));
            }
        }

        void FindProgressionTablesContainingAbility<T>(GameDataObject gameData, out List<T> progressionTables) where T : BaseProgressionTableGameData
        {
            progressionTables = new List<T>();

            foreach (var table in allProgressionTables)
            {
                // Limit to type defined in T
                T tableAsT = table as T;

                if (tableAsT != null && table.HasAbility(gameData))
                {
                    progressionTables.Add(tableAsT);
                }
            }
        }

        void FindClassProgressionTablesContainingAbility(GameDataObject gameData, out List<ClassProgressionTableGameData> progressionTables)
        {
            FindProgressionTablesContainingAbility<ClassProgressionTableGameData>(gameData, out progressionTables);
        }

        void FindCharacterProgressionTablesContainingAbility(GameDataObject gameData, out List<CharacterProgressionTableGameData> characterProgressionTables)
        {
            FindProgressionTablesContainingAbility<CharacterProgressionTableGameData>(gameData, out characterProgressionTables);
        }

        void FindBestiaryEntiesContainingAbility(GameDataObject gameData, out List<BestiaryEntryGameData> bestiaryEntries)
        {
            bestiaryEntries = new List<BestiaryEntryGameData>();

            if (gameData is ProgressionUnlockableGameData)
            {
                var pugd = (ProgressionUnlockableGameData)gameData;

                // Loop through every bestiary entry
                foreach (BestiaryEntryGameData bestiaryEntry in this.allBestiaryEntries)
                {
                    // UIJournalBestiaryAbilities must only be Abilities, Immunities, Resistances, or Weaknesses.
                    if (bestiaryEntry.GetEntryAbilities(true, IndexableStat.Abilities).Contains(pugd))// ||
                                                                                                      //bestiaryEntry.GetEntryAbilities(true, IndexableStat.Immunities).Contains(pugd) ||
                                                                                                      //bestiaryEntry.GetEntryAbilities(true, IndexableStat.Resistances).Contains(pugd) ||
                                                                                                      //bestiaryEntry.GetEntryAbilities(true, IndexableStat.Weaknesses).Contains(pugd))
                    {
                        bestiaryEntries.Add(bestiaryEntry);
                    }
                }
            }
        }

        void FindItemModsContainingAbility(Onyx.GameDataObject gameData, out List<ItemModGameData> itemMods)
        {
            itemMods = new List<ItemModGameData>();

            // Only GenericAbilityGameData's can be present in ItemMods
            if (gameData is GenericAbilityGameData)
            {
                // Check every itemmod
                foreach (ItemModGameData mod in this.allItemMods)
                {
                    // ItemMods might add status effects that give abilities, but for now we just look at what the item directly grants
                    if (mod.AbilitiesOnEquip.Contains((GenericAbilityGameData)gameData))
                        itemMods.Add(mod);
                }
            }
        }

        void FindEquippablesContainingItemMod(ItemModGameData itemMod, out List<EquippableGameData> equippables)
        {
            equippables = new List<EquippableGameData>();

            foreach (EquippableGameData equippable in allEquippables)
            {
                // Already attached item mods
                if (equippable.ItemMods.Contains(itemMod))
                    equippables.Add(equippable);
                else
                {
                    // Item mods in future enchantments
                    foreach (RecipeData applicableRecipe in equippable.ApplicableRecipes)
                    {
                        if (applicableRecipe.ItemModsToAdd.Contains(itemMod))
                            equippables.Add(equippable);
                    }
                }
            }
        }

        // Find equippables that do or could contain ItemMods that contain the ability
        void FindEquippablesContainingAbility(GameDataObject gameData, out List<EquippableGameData> equippables)
        {
            FindItemModsContainingAbility(gameData, out List<ItemModGameData> itemMods);

            equippables = new List<EquippableGameData>();

            if (itemMods != null && itemMods.Count > 0)
            {
                foreach (ItemModGameData itemMod in itemMods)
                {
                    var temp = new List<EquippableGameData>();
                    FindEquippablesContainingItemMod(itemMod, out temp);

                    if (temp != null && temp.Count > 0)
                        equippables.AddRange(temp);
                }
            }
        }

        void FindConsumablesContainingAbility(GameDataObject gameData, out List<ConsumableGameData> consumables)
        {
            consumables = new List<ConsumableGameData>();

            if (gameData is GenericAbilityGameData && gameData.ID != Guid.Empty)
            {
                var ability = (GenericAbilityGameData)gameData;

                // Loop through every bestiary entry
                foreach (ConsumableGameData consumable in this.allConsumables)
                {
                    if (consumable.AbilityID == ability.ID || consumable.PickpocketAbilityID == ability.ID)
                        consumables.Add(consumable);
                }
            }
        }

        string StripHtmlFromString(string html)
        {
            //HtmlAgilityPack.HtmlDocument doc = new HtmlAgilityPack.HtmlDocument();
            //doc.LoadHtml(html);

            //return doc.DocumentNode.InnerText;
            return Regex.Replace(html, "<.*?>", String.Empty);
        }

        Dictionary<string, string> GetStatBlockPairs(string statBlock)
        {
            var statPairs = new Dictionary<string, string>();

            // Split the statBlock string into lines
            var lines = statBlock.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);

            string key = string.Empty;

            // Loop through every line, extracting the key (everything to the left of the ": ") and the value (all the lines that follow, up to the next line containing a key)
            foreach (string line in lines)
            {
                int colonSpaceIndex = line.IndexOf(": ");

                // This line is the start (or entirity) of the stat
                if (colonSpaceIndex != -1)
                {
                    key = line.Substring(0, colonSpaceIndex);
                    statPairs[key] = line.Substring(colonSpaceIndex + ": ".Length);
                }

                // Is continuation of last line (and we have a key
                else if (!string.IsNullOrEmpty(key))
                {
                    statPairs[key] += "\r\n" + line;
                }
            }

            return statPairs;
        }

        string GetValueFromStatBlock(string key, string text)
        {
            Match match = Regex.Match(text, key.ToString().ToLower() + @":\s([^\n\r]*)", RegexOptions.IgnoreCase);

            if (match.Success)
                return match.Groups[1].Value;//.Replace("\r", "").Replace("\n", "").Replace("\r\n", "");

            return null;
        }

        #endregion
    }

    public static class Consts
    {
        public static readonly Guid NoClass = new Guid("1fe85e8d-d541-44dd-9dbc-fb056810e10e");
        public static readonly Guid Barbarian = new Guid("825817d4-1fb0-4e5c-bf84-473743ad98de");
        public static readonly Guid Chanter = new Guid("b4a0f1d1-b8f7-47e7-b899-99e478004a37");
        public static readonly Guid Cipher = new Guid("ccdc9675-e2a7-46fa-83e9-7a5368b56265");
        public static readonly Guid Druid = new Guid("568f1c26-1398-4e67-8b81-0f6a60e6cdde");
        public static readonly Guid Fighter = new Guid("6e6750b6-61d7-4b61-9713-55957e0f0591");
        public static readonly Guid Monk = new Guid("f0036bfb-53d5-4d0c-b11a-b780d788a108");
        public static readonly Guid Paladin = new Guid("f64b5a21-2dd1-41ae-8562-60ce099b25aa");
        public static readonly Guid Priest = new Guid("f7cb46af-a719-41c0-9a53-107eefdbce2b");
        public static readonly Guid Ranger = new Guid("1718929c-1faf-4292-b82c-7e2a7c20b3ab");
        public static readonly Guid Rogue = new Guid("8efd7667-8bc9-4020-b7f6-5a91b9d04e48");
        public static readonly Guid Wizard = new Guid("acfd1303-4699-4939-91eb-6ac46d4af0bd");
    }
}
