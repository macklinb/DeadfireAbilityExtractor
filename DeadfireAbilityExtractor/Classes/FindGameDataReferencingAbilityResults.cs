using System.Collections.Generic;
using Game.GameData;
using Onyx;

namespace DeadfireAbilityExtractor
{
    public class FindGameDataReferencingAbilityResults
    {
        public AbilityOrigin guessedAbilityOrigin;

        public List<GameDataObject> allObjects;

        public List<ClassProgressionTableGameData> classProgressionTables;
        public List<CharacterProgressionTableGameData> characterProgressionTables;
        public List<BestiaryEntryGameData> bestiaryEntries;
        public List<EquippableGameData> equippables;
        public List<ConsumableGameData> consumables;
    }
}
