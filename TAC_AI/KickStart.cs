﻿using System;
using System.Reflection;
//using Harmony;
using HarmonyLib;
using UnityEngine;
using ModHelper.Config;
using Nuterra.NativeOptions;
using TAC_AI.AI;
using TAC_AI.Templates;


namespace TAC_AI
{
    // Previously an extension to RandomAdditions, TACtical AI is the AI branch of the mod.
    //
    public class KickStart
    {
        const string ModName = "TACtical AIs";

        // Control the aircrafts and AI
        public const float AirMaxHeightOffset = 250;
        public const float AirMaxHeight = 150;
        public const float AirPromoteHeight = 200;
        public const int DefaultEnemyRange = 150;
        public const int EnemyExtendActionRange = 450;


        internal static bool testEnemyAI = true;
        internal static int MaxEnemySplitLimit = 20;// How many techs that can exist for each team before giving up on splitting?
        internal static int MaxEnemyBaseLimit = 3;  // How many bases are allowed to exist in one instance
        internal static int MaxEnemyHQLimit = 1;    // How many HQs are allowed to exist in one instance
        public static int AIClockPeriod = 5;        // How frequently we update

        public static bool EnableBetterAI = true;
        public static int AIDodgeCheapness = 30;
        public static int AIPopMaxLimit = 6;
        public static bool MuteNonPlayerRacket = true;
        public static bool AllowOverleveledBlockDrops { get { return EnemyBlockDropChance == 100; } } // Obsolete - true when 
        public static bool enablePainMode = true;
        public static bool EnemiesHaveCreativeInventory = false;
        public static bool AllowEnemiesToStartBases = true;
        public static bool AllowAirEnemiesToSpawn = true;
        public static bool AllowSeaEnemiesToSpawn = true;
        public static bool TryForceOnlyPlayerSpawns = false;
        public static bool DesignsToLog = false;

        //public static bool DestroyTreesInWater = false;


        internal static bool isWaterModPresent = false;
        internal static bool isTougherEnemiesPresent = false;
        internal static bool isWeaponAimModPresent = false;
        internal static bool isBlockInjectorPresent = false;
        internal static bool isPopInjectorPresent = false;
        internal static bool isAnimeAIPresent = false;

        public static int Difficulty = 50;
        // 50 means the full AI range is used
        // -50 means only the simpleton AI spawns

        public static int EnemyBlockDropChance = 40;

        //Calculated
        public static int LastRawTechCount = 0;
        public static int LowerDifficulty { get { return Mathf.Clamp(Difficulty - 50, 0, 99); } }
        public static int UpperDifficulty { get { return Mathf.Clamp(Difficulty + 50, 1, 100); } }

        // NativeOptions Parameters
        public static OptionToggle betterAI;
        public static OptionRange dodgePeriod;
        public static OptionToggle muteNonPlayerBuildRacket;
        public static OptionToggle allowOverLevelBlocksDrop;
        public static OptionToggle painfulEnemies;
        public static OptionRange diff;
        public static OptionRange blockRecoveryChance;
        public static OptionToggle infEnemySupplies;
        public static OptionToggle enemyBaseSpawn;
        public static OptionToggle enemyAirSpawn;
        public static OptionToggle enemySeaSpawn;
        public static OptionToggle playerMadeTechsOnly;
        public static OptionRange enemyBaseCount;
        public static OptionRange enemyMaxCount;


        internal static bool firedAfterBlockInjector = false;
        public static bool SpecialAttract = false;
        internal static AttractType SpecialAttractNum = 0;
        public static int retryForBote = 0;
        public static Vector3 SpecialAttractPos;

        public static float WaterHeight 
        { 
            get 
            {
                float outValue = -25;
                    try { outValue = WaterMod.QPatch.WaterHeight; } catch { }
                return outValue;
            }
        }

        public static void Main()
        {
            //Where the fun begins

            //Initiate the madness
            //HarmonyInstance harmonyInstance = HarmonyInstance.Create("legionite.tactical_ai");
            Harmony harmonyInstance = new Harmony("legionite.tactical_ai");
            try
            {
                harmonyInstance.PatchAll(Assembly.GetExecutingAssembly());
            }
            catch (Exception e)
            {
                Debug.Log("TACtical_AI: Error on patch");
                Debug.Log(e);
            }

            AIECore.TankAIManager.Initiate();
            GUIAIManager.Initiate();
            RawTechExporter.Initiate();

            if (LookForMod("WaterMod"))
            {
                Debug.Log("TACtical_AI: Found Water Mod!  Enabling water-related features!");
                isWaterModPresent = true;
            }

            if (LookForMod("WeaponAimMod"))
            {
                Debug.Log("TACtical_AI: Found WeaponAimMod!  Halting aim-related changes and letting WeaponAimMod take over!");
                isWeaponAimModPresent = true;
            }

            if (LookForMod("TougherEnemies"))
            {
                Debug.Log("TACtical_AI: Found Tougher Enemies!  MAKING THE PAIN REAL!");
                isTougherEnemiesPresent = true;
            }

            if (LookForMod("BlockInjector"))
            {
                Debug.Log("TACtical_AI: Found Block Injector!  Setting up modded base support!");
                isBlockInjectorPresent = true;
            }
            if (LookForMod("PopulationInjector"))
            {
                Debug.Log("TACtical_AI: Found Population Injector!  Holding off on using built-in spawning system!");
                isPopInjectorPresent = true;
            }
            if (LookForMod("AnimeAI"))
            {
                Debug.Log("TACtical_AI: Found Anime AI!  Hooking into commentary system and actions!");
                isAnimeAIPresent = true;
            }

            ModConfig thisModConfig = new ModConfig();
            thisModConfig.BindConfig<KickStart>(null, "EnableBetterAI");
            thisModConfig.BindConfig<KickStart>(null, "AIDodgeCheapness");
            thisModConfig.BindConfig<KickStart>(null, "MuteNonPlayerRacket");
            thisModConfig.BindConfig<KickStart>(null, "enablePainMode");
            thisModConfig.BindConfig<KickStart>(null, "Difficulty");
            thisModConfig.BindConfig<KickStart>(null, "EnemyBlockDropChance");
            thisModConfig.BindConfig<KickStart>(null, "EnemiesHaveCreativeInventory");
            thisModConfig.BindConfig<KickStart>(null, "AllowEnemiesToStartBases");
            thisModConfig.BindConfig<KickStart>(null, "AllowAirEnemiesToSpawn");
            //thisModConfig.BindConfig<KickStart>(null, "AllowOverleveledBlockDrops");
            thisModConfig.BindConfig<KickStart>(null, "DesignsToLog");
            thisModConfig.BindConfig<KickStart>(null, "MaxEnemyBaseLimit");
            thisModConfig.BindConfig<KickStart>(null, "AIPopMaxLimit");
            if (!isPopInjectorPresent)
                OverrideEnemyMax();
            thisModConfig.BindConfig<KickStart>(null, "TryForceOnlyPlayerSpawns"); 


            var TACAI = ModName + " - General";
            betterAI = new OptionToggle("<b>Rebuilt AI</b> \n(Toggle this OFF to uninstall and Save your Techs & Worlds to keep!)", TACAI, EnableBetterAI);
            betterAI.onValueSaved.AddListener(() => { EnableBetterAI = betterAI.SavedValue; thisModConfig.WriteConfigJsonFile(); });
            dodgePeriod = new OptionRange("AI Dodge Processing Shoddiness", TACAI, AIDodgeCheapness, 1, 61, 5);
            dodgePeriod.onValueSaved.AddListener(() => { AIDodgeCheapness = (int)dodgePeriod.SavedValue; thisModConfig.WriteConfigJsonFile(); });
            muteNonPlayerBuildRacket = new OptionToggle("Mute Non-Player Build Racket", TACAI, MuteNonPlayerRacket);
            muteNonPlayerBuildRacket.onValueSaved.AddListener(() => { MuteNonPlayerRacket = muteNonPlayerBuildRacket.SavedValue; thisModConfig.WriteConfigJsonFile(); });
            //allowOverLevelBlocksDrop = new OptionToggle("Overleveled Enemy Block Grade Drops", TACAI, AllowOverleveledBlockDrops);
            //allowOverLevelBlocksDrop.onValueSaved.AddListener(() => { AllowOverleveledBlockDrops = allowOverLevelBlocksDrop.SavedValue; thisModConfig.WriteConfigJsonFile(); });
            playerMadeTechsOnly = new OptionToggle("Try Spawning From Raw Enemy Folder Only", TACAI, TryForceOnlyPlayerSpawns);
            playerMadeTechsOnly.onValueSaved.AddListener(() => { TryForceOnlyPlayerSpawns = playerMadeTechsOnly.SavedValue; thisModConfig.WriteConfigJsonFile(); });

            var TACAIEnemies = ModName + " - Enemies";
            painfulEnemies = new OptionToggle("<b>Rebuilt Enemies</b>", TACAIEnemies, enablePainMode);
            painfulEnemies.onValueSaved.AddListener(() => { enablePainMode = painfulEnemies.SavedValue; thisModConfig.WriteConfigJsonFile(); });
            diff = new OptionRange("Enemy Difficulty", TACAI, Difficulty, -50, 150, 25);
            diff.onValueSaved.AddListener(() => { Difficulty = (int)diff.SavedValue; thisModConfig.WriteConfigJsonFile(); });
            blockRecoveryChance = new OptionRange("Enemy Block Drop Chance", TACAI, EnemyBlockDropChance, 0, 100, 10);
            blockRecoveryChance.onValueSaved.AddListener(() => { EnemyBlockDropChance = (int)blockRecoveryChance.SavedValue; thisModConfig.WriteConfigJsonFile(); });
            enemyBaseSpawn = new OptionToggle("Enemies Can Start Bases", TACAIEnemies, AllowEnemiesToStartBases);
            enemyBaseSpawn.onValueSaved.AddListener(() => { AllowEnemiesToStartBases = enemyBaseSpawn.SavedValue; thisModConfig.WriteConfigJsonFile(); });
            enemyBaseCount = new OptionRange("Max Enemy Base Count", TACAIEnemies, MaxEnemyBaseLimit, 1, 16, 1);
            enemyBaseCount.onValueSaved.AddListener(() => { MaxEnemyBaseLimit = (int)enemyBaseCount.SavedValue; thisModConfig.WriteConfigJsonFile(); });
            infEnemySupplies = new OptionToggle("Enemies Have Unlimited Parts", TACAIEnemies, EnemiesHaveCreativeInventory);
            infEnemySupplies.onValueSaved.AddListener(() => { EnemiesHaveCreativeInventory = infEnemySupplies.SavedValue; thisModConfig.WriteConfigJsonFile(); });

            if (!isPopInjectorPresent)
            {
                enemyMaxCount = new OptionRange("Max Random Enemies Permitted", TACAIEnemies, AIPopMaxLimit, 6, 16, 1);
                enemyMaxCount.onValueSaved.AddListener(() => { 
                    AIPopMaxLimit = (int)enemyMaxCount.SavedValue; 
                    thisModConfig.WriteConfigJsonFile();
                    OverrideEnemyMax();
                });
                enemyAirSpawn = new OptionToggle("Enemy Aircraft Spawning", TACAIEnemies, AllowAirEnemiesToSpawn);
                enemyAirSpawn.onValueSaved.AddListener(() => { AllowAirEnemiesToSpawn = enemyAirSpawn.SavedValue; thisModConfig.WriteConfigJsonFile(); });
                enemySeaSpawn = new OptionToggle("Enemy Ship Spawning", TACAIEnemies, AllowSeaEnemiesToSpawn);
                enemySeaSpawn.onValueSaved.AddListener(() => { AllowSeaEnemiesToSpawn = enemySeaSpawn.SavedValue; thisModConfig.WriteConfigJsonFile(); });
            }


            // Now setup bases
            //if (!isBlockInjectorPresent)
            //    InstantBaseLoader();
        }
        public static void DelayedBaseLoader()
        {
            Debug.Log("TACtical_AI: LAUNCHED MODDED BLOCKS BASE VALIDATOR");
            Templates.TempManager.ValidateAllStringTechs();
            firedAfterBlockInjector = true;
        }
        public static void InstantBaseLoader()
        {
            Debug.Log("TACtical_AI: LAUNCHED BASE VALIDATOR");
            Templates.TempManager.ValidateAllStringTechs();
        }

        internal static FieldInfo limitBreak = typeof(ManPop).GetField("m_PopulationLimit", BindingFlags.NonPublic | BindingFlags.Instance);
        public static void OverrideEnemyMax()
        {
            try
            {
                limitBreak.SetValue(ManPop.inst, AIPopMaxLimit);
            }
            catch { }
        }

        public static bool LookForMod(string name)
        {
            foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.FullName.StartsWith(name))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
