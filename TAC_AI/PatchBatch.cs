﻿using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using Harmony;
using UnityEngine;
using UnityEngine.UI;
using TAC_AI.AI;

namespace TAC_AI
{
    class PatchBatch
    {
    }

    internal static class Patches
    {
        // Where it all happens
        [HarmonyPatch(typeof(ModuleTechController))]
        [HarmonyPatch("ExecuteControl")]//On Control
        private static class PatchControlSystem
        {
            private static bool Prefix(ModuleTechController __instance)
            {
                if (KickStart.EnableBetterAI)
                {
                    //Debug.Log("TACtical_AI: AIEnhanced enabled");
                    try
                    {
                        var aI = __instance.transform.root.GetComponent<Tank>().AI;
                        var tank = __instance.transform.root.GetComponent<Tank>();
                        if (!tank.PlayerFocused && !Singleton.Manager<ManGameMode>.inst.IsCurrentModeMultiplayer())
                        {
                            if (aI.CheckAIAvailable() && tank.IsFriendly())
                            {
                                //Debug.Log("TACtical_AI: AI Valid!");
                                //Debug.Log("TACtical_AI: (TankAIHelper) is " + tank.gameObject.GetComponent<AIEnhancedCore.TankAIHelper>().wasEscort);
                                var tankAIHelp = tank.gameObject.GetComponent<AI.AIECore.TankAIHelper>();
                                //tankAIHelp.AIState && 
                                if (tankAIHelp.lastAIType == AITreeType.AITypes.Escort)
                                {
                                    //Debug.Log("TACtical_AI: Running BetterAI");
                                    //Debug.Log("TACtical_AI: Patched Tank ExecuteControl(TankAIHelper)");
                                    tankAIHelp.BetterAI(__instance.block.tank.control);
                                    return false;
                                }
                            }
                            else if ((KickStart.testEnemyAI || KickStart.isTougherEnemiesPresent) && KickStart.enablePainMode && tank.IsEnemy())
                            {
                                var tankAIHelp = tank.gameObject.GetComponent<AI.AIECore.TankAIHelper>();
                                if (!tankAIHelp.Hibernate)
                                {
                                    tankAIHelp.BetterAI(__instance.block.tank.control);
                                    return false;
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.Log("TACtical_AI: Failure on handling AI addition!");
                        Debug.Log(e);
                    }
                }
                return true;
            }
        }

        // this is a VERY big mod
        [HarmonyPatch(typeof(Mode))]
        [HarmonyPatch("EnterPreMode")]//On very late update
        private static class Startup
        {
            private static void Prefix(Mode __instance)
            {
                if (KickStart.isBlockInjectorPresent && !KickStart.firedAfterBlockInjector)
                    KickStart.DelayedBaseLoader();
            }
        }
        [HarmonyPatch(typeof(ModeAttract))]
        [HarmonyPatch("UpdateModeImpl")]
        private static class RestartAttract
        {
            private static void Prefix(ModeAttract __instance)
            {
                FieldInfo state = typeof(ModeAttract).GetField("m_State", BindingFlags.NonPublic | BindingFlags.Instance);
                int mode = (int)state.GetValue(__instance);
                if (mode == 2)
                {
                    bool restart = true;
                    List<Tank> active = Singleton.Manager<ManTechs>.inst.CurrentTechs.ToList();
                    foreach (Tank tonk in active)
                    {
                        if (tonk.Weapons.GetFirstWeapon().IsNotNull())
                        {
                            foreach (Tank tonk2 in active)
                            {
                                if (tonk.IsEnemy(tonk2.Team))
                                    restart = false;
                            }
                        }
                    }
                    if (restart == true)
                    {
                        UILoadingScreenHints.SuppressNextHint = true;
                        Singleton.Manager<ManUI>.inst.FadeToBlack();
                        state.SetValue(__instance, 3);
                    }

                }

            }
        }

        [HarmonyPatch(typeof(ModeAttract))]
        [HarmonyPatch("SetupTechs")]//lol
        private static class ThrowCoolAIInAttract
        {
            private static void Postfix(ModeAttract __instance)
            {
                try
                {
                    if (UnityEngine.Random.Range(1, 100) > 5)
                    {
                        Debug.Log("TACtical_AI: Ooop - the special threshold has been met");
                        Tank tankPos = Singleton.Manager<ManTechs>.inst.CurrentTechs.First();
                        Vector3 spawn = tankPos.boundsCentreWorld + (tankPos.transform.forward * 20);
                        Singleton.Manager<ManWorld>.inst.GetTerrainHeight(spawn, out float height);
                        spawn.y = height;
                        bool caseOverride = true;

                        int TechCount = Singleton.Manager<ManTechs>.inst.CurrentTechs.Count();
                        List<Tank> tanksToConsider = Singleton.Manager<ManTechs>.inst.CurrentTechs.ToList();
                        for (int step = 0; TechCount > step; step++)
                        {
                            Tank tech = tanksToConsider.ElementAt(step);
                            Vector3 position = tech.boundsCentreWorld - (tech.transform.forward * 32);
                            position.y += 64;

                            if (Templates.RawTechLoader.SpawnAttractTech(position, (int)(UnityEngine.Random.Range(1, 999) + 0.5f), -tech.transform.forward, Templates.BaseTerrain.Air, silentFail: false))
                                tech.visible.RemoveFromGame();
                        }

                        if (!caseOverride)
                        {
                            int randNum = (int)(UnityEngine.Random.Range(1, 100) + 0.5f);
                            if (randNum < 10)
                            {   // space invader
                                //Debug.Log("TACtical_AI: Throwing in TAC ref lol");
                                Templates.RawTechLoader.SpawnAttractTech(spawn, 749, Vector3.forward, Templates.BaseTerrain.Space);
                            }
                            else if (randNum < 18)
                            {   // Aircraft fight
                                for (int step = 0; TechCount > step; step++)
                                {
                                    Tank tech = tanksToConsider.ElementAt(step);
                                    Vector3 position = tech.boundsCentreWorld - (tech.transform.forward * 32);
                                    position.y += 64;

                                    if (Templates.RawTechLoader.SpawnAttractTech(position, (int)(UnityEngine.Random.Range(1, 999) + 0.5f), -tech.transform.forward, Templates.BaseTerrain.Air, silentFail: false))
                                        tech.visible.RemoveFromGame();
                                }
                            }
                            else if (randNum < 24)
                            {   // Airship assault
                                for (int step = 0; TechCount > step; step++)
                                {
                                    Tank tech = tanksToConsider.ElementAt(step);
                                    Vector3 position = tech.boundsCentreWorld - (tech.transform.forward * 48);
                                    position.y += 64;

                                    if (Templates.RawTechLoader.SpawnAttractTech(position, (int)(UnityEngine.Random.Range(1, 999) + 0.5f), tech.transform.forward, Templates.BaseTerrain.Space, silentFail: false))
                                        tech.visible.RemoveFromGame();
                                }
                            }
                            else if (randNum < 32)
                            {   // Naval Brawl
                                //if (KickStart.isWaterModPresent)
                                //{
                                //    Templates.RawTechLoader.SpawnAttractTech(spawn, 749, Templates.BaseTerrain.Sea);
                                //}
                                //else
                                Templates.RawTechLoader.SpawnAttractTech(spawn, 749, Vector3.forward, Templates.BaseTerrain.Land);
                            }
                            else if (randNum < 45)
                            {   // HQ Siege
                                foreach (Tank tech in Singleton.Manager<ManTechs>.inst.CurrentTechs)
                                {
                                    tech.SetTeam(4114);
                                }
                                tankPos.SetTeam(916);
                                Templates.RawTechLoader.SpawnAttractTech(spawn, tankPos.Team, Vector3.forward, Templates.BaseTerrain.Land, tankPos.GetMainCorp(), Templates.BasePurpose.Headquarters);
                            }
                            else if (randNum < 60)
                            {   // pending
                                for (int step = 0; TechCount > step; step++)
                                {
                                    Tank tech = tanksToConsider.ElementAt(step);
                                    Vector3 position = tech.boundsCentreWorld - (tech.transform.forward * 10);
                                    position.y += 10;

                                    if (Templates.RawTechLoader.SpawnAttractTech(position, (int)(UnityEngine.Random.Range(1, 999) + 0.5f), tech.transform.forward, Templates.BaseTerrain.Land))
                                        tech.visible.RemoveFromGame();
                                }
                                Templates.RawTechLoader.SpawnAttractTech(spawn, 749, Vector3.forward, Templates.BaseTerrain.Space);
                            }
                            else
                            {   // Land battle invoker
                                Templates.RawTechLoader.SpawnAttractTech(spawn, 749, Vector3.forward, Templates.BaseTerrain.Land);
                            }
                        }
                    }
                }
                catch { }
            }
        }

        [HarmonyPatch(typeof(Tank))]
        [HarmonyPatch("OnPool")]//On Creation
        private static class PatchTankToHelpAIAndClocks
        {
            private static void Postfix(Tank __instance)
            {
                //Debug.Log("TACtical_AI: Patched Tank OnPool(TankAIHelper & TimeTank)");
                var ModuleCheck = __instance.gameObject.GetComponent<AI.AIECore.TankAIHelper>();
                if (ModuleCheck.IsNull())
                {
                    __instance.gameObject.AddComponent<AI.AIECore.TankAIHelper>().Subscribe(__instance);
                }
            }
        }

        /*
        [HarmonyPatch(typeof(TankBeam))]
        [HarmonyPatch("Update")]//Give the AI some untangle help
        private class PatchTankBeamToHelpAI
        {
            private static void Postfix(TankBeam __instance)
            {
                //Debug.Log("TACtical_AI: Patched TankBeam Update(TankAIHelper)");
                var ModuleCheck = __instance.gameObject.GetComponent<AIEnhancedCore.TankAIHelper>();
                if (ModuleCheck != null)
                {
                }
            }
        }
        */


        [HarmonyPatch(typeof(ModuleAIBot))]
        [HarmonyPatch("OnPool")]//On Creation
        private static class ImproveAI
        {
            private static void Postfix(ModuleAIBot __instance)
            {
                var valid = __instance.GetComponent<ModuleAIExtension>();
                if (valid)
                {
                    valid.OnPool();
                }
                else
                {
                    var ModuleAdd = __instance.gameObject.AddComponent<ModuleAIExtension>();
                    ModuleAdd.OnPool();
                    // Now retrofit AIs
                    try
                    {
                        var name = __instance.gameObject.name;
                        if (name == "GSO_AI_Module_Guard_111")
                        {
                            ModuleAdd.Aegis = true;
                            ModuleAdd.AidAI = true;
                            ModuleAdd.Prospector = true;//Temp until main intended function arrives
                        }
                        if (name == "GSO_AIAnchor_121")
                        {
                            ModuleAdd.Aegis = true;
                            ModuleAdd.AidAI = true;
                            ModuleAdd.Prospector = true;//Temp until main intended function arrives
                            ModuleAdd.MaxCombatRange = 150;
                        }
                        else if (name == "GC_AI_Module_Guard_222")
                        {
                            ModuleAdd.Prospector = true;
                            ModuleAdd.MTForAll = true;
                            ModuleAdd.MeleePreferred = true;
                        }
                        else if (name == "VEN_AI_Module_Guard_111")
                        {
                            ModuleAdd.Aviator = true;
                            ModuleAdd.Prospector = true;//Temp until main intended function arrives
                            ModuleAdd.SidePreferred = true;
                            ModuleAdd.MaxCombatRange = 300;
                        }
                        else if (name == "HE_AI_Module_Guard_112")
                        {
                            ModuleAdd.Assault = true;
                            ModuleAdd.Aviator = true;
                            ModuleAdd.Prospector = true;//Temp until main intended function arrives
                            ModuleAdd.AdvancedAI = true;
                            ModuleAdd.MinCombatRange = 50;
                            ModuleAdd.MaxCombatRange = 200;
                        }
                        else if (name == "HE_AI_Turret_111")
                        {
                            ModuleAdd.Assault = true;
                            ModuleAdd.Prospector = true;//Temp until main intended function arrives
                            ModuleAdd.AdvancedAI = true;
                            ModuleAdd.MinCombatRange = 50;
                            ModuleAdd.MaxCombatRange = 150;
                        }
                        else if (name == "BF_AI_Module_Guard_212")
                        {
                            ModuleAdd.Astrotech = true;
                            //ModuleAdd.Prospector = true;//Temp until main intended function arrives
                            ModuleAdd.AdvAvoidence = true;
                            ModuleAdd.MinCombatRange = 60;
                            ModuleAdd.MaxCombatRange = 180;
                        }
                        /*
                        else if (name == "RR_AI_Module_Guard_212")
                        {
                            ModuleAdd.Energizer = true;
                            ModuleAdd.AdvAvoidence = true;
                            ModuleAdd.MinCombatRange = 160;
                            ModuleAdd.MaxCombatRange = 220;
                        }
                        else if (name == "SJ_AI_Module_Guard_122")
                        {
                            ModuleAdd.Scrapper = true;
                            ModuleAdd.MTForAll = true;
                            ModuleAdd.MinCombatRange = 60;
                            ModuleAdd.MaxCombatRange = 120;
                        }
                        else if (name == "TSN_AI_Module_Guard_312")
                        {
                            ModuleAdd.Buccaneer = true;
                            ModuleAdd.AdvAvoidence = true;
                            ModuleAdd.MinCombatRange = 150;
                            ModuleAdd.MaxCombatRange = 250;
                        }
                        else if (name == "LEG_AI_Module_Guard_112")
                        {   //Incase Legion happens and the AI needs help lol
                            ModuleAdd.Assault = true;
                            ModuleAdd.Aegis = true;
                            ModuleAdd.Prospector = true;
                            ModuleAdd.Scrapper = true;
                            ModuleAdd.Energizer = true;
                            ModuleAdd.Assault = true;
                            ModuleAdd.Aviator = true;
                            ModuleAdd.Buccaneer = true;
                            ModuleAdd.Astrotech = true;
                            ModuleAdd.AidAI = true;
                            ModuleAdd.AdvancedAI = true;
                            ModuleAdd.AdvAvoidence = true;
                            ModuleAdd.SidePreferred = true;
                            ModuleAdd.MeleePreferred = true;
                            ModuleAdd.MaxCombatRange = 200;
                        }
                        else if (name == "TAC_AI_Module_Plex_323")
                        {
                            ModuleAdd.Aviator = true;
                            ModuleAdd.Buccaneer = true;
                            ModuleAdd.Astrotech = true;
                            ModuleAdd.AidAI = true;
                            ModuleAdd.AnimeAI = true;
                            ModuleAdd.AdvancedAI = true;
                            ModuleAdd.AdvAvoidence = true;
                            ModuleAdd.MinCombatRange = 100;
                            ModuleAdd.MaxCombatRange = 400;
                        }
                        */
                    }
                    catch (Exception e)
                    {
                        Debug.Log("TACtical_AI: CRASH ON HANDLING EXISTING AIS");
                        Debug.Log(e);
                    }
                }
            }
        }

        [HarmonyPatch(typeof(TargetAimer))]// cannot override
        [HarmonyPatch("UpdateTarget")]//On targeting
        private static class PatchAimingToHelpAI
        {
            private static void Postfix(TargetAimer __instance)
            {
                    var AICommand = __instance.transform.root.GetComponent<AI.AIECore.TankAIHelper>();
                if (AICommand.IsNotNull() && !KickStart.isWeaponAimModPresent)
                {
                    if (AICommand.OverrideAim == 1)
                    {
                        var tank = __instance.transform.root.GetComponent<Tank>();
                        if (tank.IsNotNull() && !tank.IsPlayer)
                        {
                            if (AICommand.lastEnemy.IsNotNull())
                            {
                                //Debug.Log("TACtical_AI: Overriding targeting to aim at " + AICommand.lastEnemy.name + "  pos " + AICommand.lastEnemy.tank.boundsCentreWorldNoCheck);
                                //FieldInfo targ = typeof(TargetAimer).GetField("Target", BindingFlags.NonPublic | BindingFlags.Instance);
                                //targ.SetValue(__instance, AICommand.lastEnemy);

                                FieldInfo targPos = typeof(TargetAimer).GetField("m_TargetPosition", BindingFlags.NonPublic | BindingFlags.Instance);
                                //targPos.SetValue(__instance, tank.control.TargetPositionWorld);
                                targPos.SetValue(__instance, AICommand.lastEnemy.tank.boundsCentreWorldNoCheck);
                                //Debug.Log("TACtical_AI: final aim is " + targPos.GetValue(__instance));

                            }
                        }
                    }
                    else if (AICommand.OverrideAim == 2)
                    {
                        var tank = __instance.transform.root.GetComponent<Tank>();
                        if (tank.IsNotNull() && !tank.IsPlayer)
                        {
                            if (AICommand.Obst.IsNotNull())
                            {
                                //Debug.Log("TACtical_AI: Overriding targeting to aim at obstruction");

                                FieldInfo targPos = typeof(TargetAimer).GetField("m_TargetPosition", BindingFlags.NonPublic | BindingFlags.Instance);
                                targPos.SetValue(__instance, AICommand.Obst.position);

                            }
                        }
                    }
                    else if (AICommand.OverrideAim == 3)
                    {
                        var tank = __instance.transform.root.GetComponent<Tank>();
                        if (tank.IsNotNull() && !tank.IsPlayer)
                        {
                            if (AICommand.LastCloseAlly.IsNotNull())
                            {
                                //Debug.Log("TACtical_AI: Overriding targeting to aim at player's target");

                                FieldInfo targPos = typeof(TargetAimer).GetField("m_TargetPosition", BindingFlags.NonPublic | BindingFlags.Instance);
                                targPos.SetValue(__instance, AICommand.LastCloseAlly.control.TargetPositionWorld);

                            }
                        }
                    }
                }
            }
        }

        [HarmonyPatch(typeof(ModuleWeapon))]
        [HarmonyPatch("UpdateAutoAimBehaviour")]//On targeting
        private static class PatchAimingSystemsToHelpAI
        {
            private static void Postfix(TargetAimer __instance)
            {
                if (!KickStart.isWeaponAimModPresent)
                {
                    FieldInfo aimers = typeof(ModuleWeapon).GetField("m_TargetAimer", BindingFlags.NonPublic | BindingFlags.Instance);
                    TargetAimer thisAimer = (TargetAimer)aimers.GetValue(__instance);

                    if (thisAimer.HasTarget)
                    {
                        FieldInfo aimerTargPos = typeof(TargetAimer).GetField("m_TargetPosition", BindingFlags.NonPublic | BindingFlags.Instance);
                        FieldInfo WeaponTargPos = typeof(ModuleWeapon).GetField("m_TargetPosition", BindingFlags.NonPublic | BindingFlags.Instance);
                        WeaponTargPos.SetValue(__instance, (Vector3)aimerTargPos.GetValue(thisAimer));
                    }
                }
            }
        }


        [HarmonyPatch(typeof(ResourceDispenser))]
        [HarmonyPatch("OnSpawn")]//On World Spawn
        private static class PatchResourcesToHelpAI
        {
            private static void Prefix(ResourceDispenser __instance)
            {
                //Debug.Log("TACtical_AI: Added resource to list (OnSpawn)");
                if (!AI.AIECore.Minables.Contains(__instance.visible))
                    AI.AIECore.Minables.Add(__instance.visible);
                else
                    Debug.Log("TACtical_AI: RESOURCE WAS ALREADY ADDED! (OnSpawn)");
            }
        }

        [HarmonyPatch(typeof(ResourceDispenser))]
        [HarmonyPatch("Regrow")]//On World Spawn
        private static class PatchResourceRegrowToHelpAI
        {
            private static void Prefix(ResourceDispenser __instance)
            {
                //Debug.Log("TACtical_AI: Added resource to list (OnSpawn)");
                if (!AI.AIECore.Minables.Contains(__instance.visible))
                    AI.AIECore.Minables.Add(__instance.visible);
                //else
                //    Debug.Log("TACtical_AI: RESOURCE WAS ALREADY ADDED! (OnSpawn)");
            }
        }

        [HarmonyPatch(typeof(ResourceDispenser))]
        [HarmonyPatch("Die")]//On resource destruction
        private static class PatchResourceDeathToHelpAI
        {
            private static void Prefix(ResourceDispenser __instance)
            {
                //Debug.Log("TACtical_AI: Removed resource from list (Die)");
                if (AI.AIECore.Minables.Contains(__instance.visible))
                {
                    AI.AIECore.Minables.Remove(__instance.visible);
                }
                else
                    Debug.Log("TACtical_AI: RESOURCE WAS ALREADY REMOVED! (Die)");
            }
        }

        [HarmonyPatch(typeof(ResourceDispenser))]
        [HarmonyPatch("OnRecycle")]//On World Destruction
        private static class PatchResourceRecycleToHelpAI
        {
            private static void Prefix(ResourceDispenser __instance)
            {
                //Debug.Log("TACtical_AI: Removed resource from list (OnRecycle)");
                if (AI.AIECore.Minables.Contains(__instance.visible))
                {
                    AI.AIECore.Minables.Remove(__instance.visible);
                }
                //else
                //    Debug.Log("TACtical_AI: RESOURCE WAS ALREADY REMOVED! (OnRecycle)");

            }
        }

        [HarmonyPatch(typeof(ModuleItemPickup))]
        [HarmonyPatch("OnPool")]//On Creation
        private static class MarkReceiver
        {
            private static void Postfix(ModuleItemPickup __instance)
            {
                var valid = __instance.GetComponent<ModuleItemHolder>();
                if (valid)
                {
                    if (valid.IsFlag(ModuleItemHolder.Flags.Receiver))
                    {
                        var ModuleAdd = __instance.gameObject.AddComponent<ModuleHarvestReciever>();
                        ModuleAdd.OnPool();
                    }
                }
            }
        }

        /*
        [HarmonyPatch(typeof(TechAI))]
        [HarmonyPatch("SetCurrentTree")]//On SettingTechAI
        private class DetectAIChangePatch
        {
            private static void Prefix(TechAI __instance, ref AITreeType aiTreeType)
            {
                if (aiTreeType != null)
                {
                    FieldInfo currentTreeActual = typeof(TechAI).GetField("m_CurrentAITreeType", BindingFlags.NonPublic | BindingFlags.Instance);
                    if ((AITreeType)currentTreeActual.GetValue(__instance) != aiTreeType)
                    {
                        //
                    }
                }
            }
        }
        */
        [HarmonyPatch(typeof(TechAI))]
        [HarmonyPatch("UpdateAICategory")]//On Auto Setting Tech AI
        private class ForceAIToComplyAnchorCorrectly
        {
            private static void Postfix(TechAI __instance)
            {
                var tAI = __instance.gameObject.GetComponent<AI.AIECore.TankAIHelper>();
                if (tAI.IsNotNull())
                {
                    if (tAI.JustUnanchored && tAI.AIState == 1)
                    {   //Set the AI back to escort to continue operations if autoanchor is true
                        FieldInfo currentTreeActual = typeof(TechAI).GetField("m_CurrentAITreeType", BindingFlags.NonPublic | BindingFlags.Instance);
                        AITreeType AISetting = (AITreeType)currentTreeActual.GetValue(__instance);

                        AISetting.m_TypeName = AITreeType.AITypes.Escort.ToString();

                        currentTreeActual.SetValue(__instance, AISetting);
                        tAI.JustUnanchored = false;
                    }
                }
            }
        }

        [HarmonyPatch(typeof(UIRadialTechControlMenu))]
        [HarmonyPatch("Show")]//On popup
        private static class DetectAIRadialAction
        {
            private static void Prefix(UIRadialTechControlMenu __instance, ref object context)
            {
                OpenMenuEventData nabData = (OpenMenuEventData)context;
                TankBlock thisBlock = nabData.m_TargetTankBlock;
                if (thisBlock.tank.IsNotNull())
                {
                    Debug.Log("TACtical_AI: grabbed tank data = " + thisBlock.tank.name.ToString());
                    GUIAIManager.GetTank(thisBlock.tank);
                }
                else
                {
                    Debug.Log("TACtical_AI: TANK IS NULL!");
                }
            }
        }


        [HarmonyPatch(typeof(UIRadialTechControlMenu))]//UIRadialMenuOptionWithWarning
        [HarmonyPatch("OnAIOptionSelected")]//On AI option
        private static class DetectAIRadialMenuAction
        {
            private static void Prefix(UIRadialTechControlMenu __instance, ref UIRadialTechControlMenu.PlayerCommands command)
            {
                //Debug.Log("TACtical_AI: click menu FIRED!!!  input = " + command.ToString() + " | num = " + (int)command);
                if ((int)command == 3)
                {
                    if (GUIAIManager.IsTankNull())
                    {
                        FieldInfo currentTreeActual = typeof(UIRadialTechControlMenu).GetField("m_TargetTank", BindingFlags.NonPublic | BindingFlags.Instance);
                        Tank tonk = (Tank)currentTreeActual.GetValue(__instance);
                        GUIAIManager.GetTank(tonk);
                        if (GUIAIManager.IsTankNull())
                        {
                            Debug.Log("TACtical_AI: TANK IS NULL AFTER SEVERAL ATTEMPTS!!!");
                        }
                    }
                    GUIAIManager.LaunchSubMenuClickable();
                }

                //Debug.Log("TACtical_AI: click menu " + __instance.gameObject.name);
                //Debug.Log("TACtical_AI: click menu host gameobject " + Nuterra.NativeOptions.UIUtilities.GetComponentTree(__instance.gameObject, __instance.gameObject.name));
            }
        }

        [HarmonyPatch(typeof(TankControl))]
        [HarmonyPatch("CopySchemesFrom")]//On Split
        private static class SetMTAIAuto
        {
            private static void Prefix(TankControl __instance, ref TankControl other)
            {
                if (__instance.Tech.blockman.IterateBlockComponents<ModuleWheels>().Count() > 0 || __instance.Tech.blockman.IterateBlockComponents<ModuleHover>().Count() > 0)
                    __instance.gameObject.GetComponent<AI.AIECore.TankAIHelper>().DediAI = AIType.Escort;
                else
                {
                    if (__instance.Tech.blockman.IterateBlockComponents<ModuleWeapon>().Count() > 0)
                        __instance.gameObject.GetComponent<AI.AIECore.TankAIHelper>().DediAI = AIType.MTTurret;
                    else
                        __instance.gameObject.GetComponent<AI.AIECore.TankAIHelper>().DediAI = AIType.MTSlave;
                }
            }
        }
    }
}
