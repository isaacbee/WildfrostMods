using Deadpan.Enums.Engine.Components.Modding;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System;
using Random = UnityEngine.Random;
using UnityEngine.Events;
using HarmonyLib;

namespace RandomCharms
{
    public class RandomCharmsMod : WildfrostMod
    {
        // Our mod's constructor
        public RandomCharmsMod(string modDirectory) : base(modDirectory) {}
        public override string GUID => "isaacbee.wildfrost.randomcharms"; //[creator name].[game name].[mod name] is standard convention. LOWERCASE!
        public override string[] Depends => new string[] { }; //The GUIDs of other mods that must load before yours. 
        public override string Title => "Random Charms";
        public override string Description => "Adds random charms to every unit, item, enemy, etc. Configurable via the Config Manager mod or the config.cfg file in the mod folder.";
        internal static RandomCharmsMod instance;

        [ConfigItem(1, "Number of random charms to attempt to add to Friendly units (i.e. Leader, Pets, Clunkers). Does not apply to Summons. Cannot exceed the charm limit normally. (default: 1)", "Charms on Friendly")]
        public int AddNumberOfFriendlyUnitsRandomCharms;

        [ConfigItem(1, "Number of random charms to attempt to add to items rewards. Does not apply to Junk added to hand via Trash. Cannot exceed the charm limit normally. (default: 1)", "Charms on Items")]
        public int AddNumberOfItemsRandomCharms;

        [ConfigItem(1, "Number of random charms to attempt to add to Summoned units. Cannot exceed the charm limit normally. (default: 1)", "Charms on Summoned")]
        public int AddNumberOfSummonedRandomCharms;

        [ConfigItem(1, "Number of random charms to attempt to add to Junk added via Trash. Cannot exceed the charm limit normally. (default: 1)", "Charms on Trash Junk")]
        public int AddNumberOfTrashRandomCharms;

        [ConfigItem(1, "Number of random charms to attempt to add to Enemy units, bosses, clunkers, etc. Cannot exceed the charm limit normally. (default: 1)", "Charms on Enemies")]
        public int AddNumberOfEnemyRandomCharms;

        [ConfigItem(false, "Randomize number of charms. Uses the above values as a maximum. (default: false)", "Randomize Charm Count")]
        public bool RandomizeCharmCount;

        [ConfigItem(false, "Allow cursed charms (i.e. Fish Charm, Weakness Charm) in the random pool. Typically makes the game easier. (default: false)", "Allow Cursed Charms")]
        public bool AllowNegativeCharms;

        [ConfigItem(false, "Allow locked charms in the random pool. Locked charms are still not obtainable normally. (default: false)", "Allow Locked Charms")]
        public bool AllowLockedCharms;

        [ConfigItem(false, "Allow Spark Charm to be added to enemies. Enable at your own risk. (default: false)", "Allow Spark Charm on Enemies")]
        public bool AllowEnemySparkCharms;

        [ConfigItem(3, "Change the charm limit. Increase at your own risk. (default: 3)", "Charm Limit")]
        public int ModCharmLimit;

        public override void Load()
        {
            instance = this;
            base.Load();
            
            Events.OnEntitySummoned += AddRandomCharms;
            Events.OnCardDataCreated += AddRandomCharms;
        }

        public override void Unload()
        {
            base.Unload();

            Events.OnCardDataCreated -= AddRandomCharms;
            Events.OnEntitySummoned -= AddRandomCharms;
        }

        private void AddRandomCharms(Entity entity, Entity _)
        {
            foreach (var charm in GetRandomCharms(entity.data, isSummoned: true))
            {
                ActionQueue.RunParallel(new ActionSequence(charm.Assign(entity)));
            }
        } 

        private void AddRandomCharms(CardData cardData)
        {
            foreach (var charm in GetRandomCharms(cardData, isSummoned: false))
            {
                charm.Assign(cardData);
            }
        }

        private IEnumerable<CardUpgradeData> GetRandomCharms(CardData cardData, bool isSummoned = false)
        {
            // change default charm limit
            cardData.charmSlots = ModCharmLimit;
            Debug.Log($"[RandomCharms] New CardData created with {cardData.charmSlots} charm slots: " + cardData.name);

            bool doAddCharm = false;
            bool isFriendly = false;
            int charmsToAdd = 0;

            // check if "friendly" unit
            if (AddNumberOfFriendlyUnitsRandomCharms > 0 && (cardData.cardType.name == "Leader" || cardData.cardType.name == "Friendly" || (cardData.cardType.name == "Clunker" && cardData.isEnemyClunker == false)))
            {
                doAddCharm = true;
                isFriendly = true;
                charmsToAdd = AddNumberOfFriendlyUnitsRandomCharms;
                Debug.Log($"[RandomCharms] {cardData.name} is a friendly unit and is set to receive random charm(s)");
            }
            // check if item
            else if (AddNumberOfItemsRandomCharms > 0 && cardData.cardType.name == "Item")
            {
                doAddCharm = true;
                isFriendly = true;
                charmsToAdd = AddNumberOfItemsRandomCharms;
                Debug.Log($"[RandomCharms] {cardData.name} is an Item and is set to receive random charm(s)");
            }
            // check if summoned
            else if (isSummoned && AddNumberOfSummonedRandomCharms > 0 && cardData.cardType.name == "Summoned")
            {
                doAddCharm = true;
                isFriendly = true;
                charmsToAdd = AddNumberOfSummonedRandomCharms;
                Debug.Log($"[RandomCharms] {cardData.name} is Summoned and is set to receive random charm(s)");
            }
            // check if trash
            else if (isSummoned && AddNumberOfTrashRandomCharms > 0 && cardData.cardType.name == "Item")
            {
                doAddCharm = true;
                isFriendly = true;
                charmsToAdd = AddNumberOfTrashRandomCharms;
                Debug.Log($"[RandomCharms] {cardData.name} is Trash and is set to receive random charm(s)");
            }
            // check if "enemy" card
            else if (AddNumberOfEnemyRandomCharms > 0 && (cardData.cardType.name == "Boss" || (cardData.cardType.name == "BossSmall" && cardData.title != "Truffle") || cardData.cardType.name == "Enemy" || cardData.cardType.name == "Miniboss" || cardData.cardType.name == "Summoned" || (cardData.cardType.name == "Clunker" && cardData.isEnemyClunker == true)))
            {
                doAddCharm = true;
                charmsToAdd = AddNumberOfEnemyRandomCharms;
                Debug.Log($"[RandomCharms] {cardData.name} is an enemy and is set to receive random charm(s)");
            }

            if (doAddCharm)
            {
                // check locked charms
                List<string> lockedCharms = MetaprogressionSystem.GetLockedCharms(MetaprogressionSystem.GetRemainingUnlocks());
                Debug.Log($"[RandomCharms] The charms {string.Join(", ", lockedCharms)} are locked");

                // check available upgrades
                List<CardUpgradeData> upgradeData = AddressableLoader.GetGroup<CardUpgradeData>("CardUpgradeData");
                Debug.Log($"[RandomCharms] {upgradeData.Count} upgrades found in CardUpgradeData");

                // only allow unlocked charms; remove outlier charms that either crash the game or make it less fun
                var allowedCharms = upgradeData.Where(x => 
                    x.type == CardUpgradeData.Type.Charm // is charm
                    && (AllowLockedCharms || !lockedCharms.Contains(x.name)) // is unlocked
                    && (isFriendly || AllowEnemySparkCharms || x.name != "CardUpgradeSpark") // is not a spark charm for the enemy
                    && x.name != "CardUpgradeMime" // is not mime charm
                    && x.name != "CardUpgradeWeakness" // is not unused charm
                    && (AllowNegativeCharms || x.tier >= 0) // is not a cursed charm, if set
                );
                Debug.Log($"[RandomCharms] {allowedCharms.Count()} equipable charms found in CardUpgradeData for {cardData.name}");

                if (RandomizeCharmCount)
                {
                    charmsToAdd = (int)Math.Round((double)Random.Range(0, charmsToAdd));
                    Debug.Log($"[RandomCharms] Randomized to instead add {charmsToAdd} charms to {cardData.name}");
                }

                for (int i = 0; i < charmsToAdd; i++)
                {
                    // only allow charms that can be equipped
                    var upgradesCharms = allowedCharms.Where(x => x.CanAssign(cardData)).ToList();

                    if (upgradesCharms.Count > 0)
                    {
                        // selects a random charm from that list
                        var randomCharm = upgradesCharms.RandomItem();
                        Debug.Log($"[RandomCharms] {randomCharm.name} selected from CardUpgradeData charms to be put on {cardData.name}");
                        yield return randomCharm;

                        // check if too much snow
                        if (cardData.startWithEffects.Any(x => x.data is StatusEffectImmuneToX y && y.immunityType == "snow") && cardData.startWithEffects.Any(x => x.data is StatusEffectSnow y && x.count > 1))
                        {
                            Debug.Log($"[RandomCharms] {cardData.name} has too much snow. Removing extra");
                            cardData.startWithEffects.First(x => x.data is StatusEffectSnow).count = 1;
                        }
                    }
                    else
                    {
                        Debug.Log($"[RandomCharms] There are no equipable CardUpgradeData charms to be put on {cardData.name}");
                        break;
                    }
                }
            }
        }
    }
}