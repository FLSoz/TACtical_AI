﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace TAC_AI.AI.Enemy
{
    public static class RCore
    {
        /*
            RELIES ON EVERYTHING IN THE "AI" FOLDER TO FUNCTION PROPERLY!!!  
                [excluding the Designators in said folder]
        */ 
        
        // Main host of operations
        public static void BeEvil(AIECore.TankAIHelper thisInst, Tank tank)
        {
            //Debug.Log("TACtical_AI: enemy AI active!");
            RunEvilOperations(thisInst, tank);
            ScarePlayer(tank);
        }
        public static void ScarePlayer(Tank tank)
        {
            //Debug.Log("TACtical_AI: enemy AI active!");
            var tonk = tank.Vision.GetFirstVisibleTechIsEnemy(tank.Team);
            if (tonk.IsNotNull())
            {
                bool player = tonk.tank.IsPlayer;
                if (player)
                {
                    Singleton.Manager<ManMusic>.inst.SetDanger(ManMusic.DangerContext.Circumstance.Enemy, tank, tonk.tank);
                }
            }
        }

        // Begin the AI tree
        public static void RunEvilOperations(AIECore.TankAIHelper thisInst, Tank tank)
        {
            EnemyMind Mind = thisInst.MovementController.EnemyMind;
            if (Mind.IsNull())
            {
                RandomizeBrain(thisInst, tank);
                return;
            }
            if (Mind.remove)
            {
                return;
            }
            /*
            var bookworm = tank.GetComponent<Templates.BookmarkName>();
            if (bookworm.IsNotNull())
            {
                tank.SetName(bookworm.savedName);
                Mind.EvilCommander = EnemyHandling.Stationary;
                Mind.CommanderMind = EnemyAttitude.Default;
                Mind.CommanderSmarts = EnemySmarts.IntAIligent;
                Mind.CommanderAttack = EnemyAttack.Grudge;
                Mind.CommanderBolts = EnemyBolts.AtFull;
                Mind.AllowInvBlocks = true;
                Mind.AllowRepairsOnFly = true;

                Mind.TechMemor = tank.gameObject.GetComponent<AIERepair.DesignMemory>();
                if (Mind.TechMemor.IsNull())
                {
                    Mind.TechMemor = tank.gameObject.AddComponent<AIERepair.DesignMemory>();
                    Mind.TechMemor.Initiate();
                }
                Mind.TechMemor.SetupForNewTechConstruction(thisInst, bookworm.blueprint, !bookworm.infBlocks);
                Mind.StartedAnchored = true;

                UnityEngine.Object.Destroy(bookworm);
            }
            */

            RBolts.ManageBolts(thisInst, tank, Mind);
            if (Mind.AllowRepairsOnFly)
            {
                bool venPower = false;
                if (Mind.MainFaction == FactionSubTypes.VEN) venPower = true;
                RRepair.EnemyRepairStepper(thisInst, tank, Mind, 50, venPower);// longer while fighting
            }
            if (Mind.EvilCommander != EnemyHandling.Stationary && Mind.EvilCommander != EnemyHandling.Airplane)
            {
                switch (Mind.CommanderAttack)
                {
                    case EnemyAttack.Coward:
                        RGeneral.SelfDefense(thisInst, tank, Mind);
                        break;
                    case EnemyAttack.Spyper:
                        RGeneral.AimAttack(thisInst, tank, Mind);
                        break;
                    case EnemyAttack.Grudge:
                        RGeneral.HoldGrudge(thisInst, tank, Mind);
                        break;
                    default:
                        RGeneral.AidAttack(thisInst, tank, Mind);
                        break;
                }
            }

            //CommanderMind is handled in each seperate class
            Mind.EnemyOpsController.Execute();
        }
        public static Vector3 GetTargetCoordinates(Tank tank, Visible target, EnemyMind mind)
        {
            if (mind.CommanderSmarts >= EnemySmarts.Smrt)   // Rough Target leading
            {
                if (target.tank.rbody.IsNull())
                    return target.tank.boundsCentreWorldNoCheck;
                else
                    return target.tank.rbody.velocity + target.tank.boundsCentreWorldNoCheck;
            }
            else
                return target.tank.boundsCentreWorldNoCheck;
        }


        // AI SETUP
        public static void RandomizeBrain(AIECore.TankAIHelper thisInst, Tank tank)
        {
            if (!tank.gameObject.GetComponent<EnemyMind>())
                tank.gameObject.AddComponent<EnemyMind>();
            thisInst.lastPlayer = null;

            thisInst.ResetToDefaultAIController();

            var toSet = tank.gameObject.GetComponent<EnemyMind>();
            toSet.HoldPos = tank.boundsCentreWorldNoCheck;
            toSet.Initiate();
            toSet.Range = 250;

            bool isMissionTech = RMission.SetupMissionAI(thisInst, tank, toSet);
            if (isMissionTech)
            {
                if (toSet.CommanderSmarts >= EnemySmarts.Smrt)
                {
                    toSet.TechMemor = tank.gameObject.GetComponent<AIERepair.DesignMemory>();
                    if (toSet.TechMemor.IsNull())
                    {
                        toSet.TechMemor = tank.gameObject.AddComponent<AIERepair.DesignMemory>();
                        toSet.TechMemor.Initiate();
                    }
                }
                if (toSet.CommanderAttack == EnemyAttack.Grudge)
                    toSet.FindEnemy();
                return;
            }

            if (tank.Anchors.NumAnchored > 0)
                toSet.StartedAnchored = true;

            //add Smartness
            AutoSetIntelligence(toSet, tank);

            bool setEnemy = SetSmartAIStats(thisInst, tank, toSet);
            if (!setEnemy)
            {
                RandomSetMindAttack(toSet, tank);
            }
            if (toSet.CommanderSmarts == EnemySmarts.Default && toSet.EvilCommander == EnemyHandling.Wheeled)
            {
                thisInst.Hibernate = true;// enable the default AI
                Debug.Log("TACtical_AI: Tech " + tank.name + " is ready to roll!  Default enemy with Default everything");
                return;
            }

            if (!Singleton.Manager<ManGameMode>.inst.GetIsInPlayableMode())
            {   // make sure they fight
                if (toSet.EvilCommander != EnemyHandling.Wheeled)
                    toSet.CommanderAttack = EnemyAttack.Grudge;
                if (toSet.CommanderAttack == EnemyAttack.Coward)
                    toSet.CommanderAttack = EnemyAttack.Bully;
                if (toSet.CommanderAttack == EnemyAttack.Spyper)
                    toSet.CommanderAttack = EnemyAttack.Grudge;
                toSet.CommanderMind = EnemyAttitude.Homing;
            }

            if (toSet.CommanderAttack == EnemyAttack.Grudge)
                toSet.FindEnemy();
            if (toSet.CommanderMind == EnemyAttitude.Miner)
            {
                thisInst.lastTechExtents = AIECore.Extremes(tank.blockBounds.extents);
                Templates.RawTechLoader.TrySpawnBase(tank, thisInst);
                Debug.Log("TACtical_AI: Tech " + tank.name + " is a base hosting tech!!  " + toSet.EvilCommander.ToString() + " based enemy with attitude " + toSet.CommanderAttack.ToString() + " | Mind " + toSet.CommanderMind.ToString() + " | Smarts " + toSet.CommanderSmarts.ToString() + " inbound!");
            }
            else if (toSet.CommanderMind == EnemyAttitude.Boss)
            {
                thisInst.lastTechExtents = AIECore.Extremes(tank.blockBounds.extents);
                Templates.RawTechLoader.TrySpawnBase(tank, thisInst, Templates.BasePurpose.Headquarters);
                Debug.Log("TACtical_AI: Tech " + tank.name + " is a base boss with dangerous potential!  " + toSet.EvilCommander.ToString() + " based enemy with attitude " + toSet.CommanderAttack.ToString() + " | Mind " + toSet.CommanderMind.ToString() + " | Smarts " + toSet.CommanderSmarts.ToString() + " inbound!");
            }
            else if (toSet.CommanderMind == EnemyAttitude.Invader)
            {
                thisInst.lastTechExtents = AIECore.Extremes(tank.blockBounds.extents);
                Templates.RawTechLoader.TrySpawnBase(tank, thisInst, Templates.BasePurpose.AnyNonHQ);
                Debug.Log("TACtical_AI: Tech " + tank.name + " is a base hosting tech!!  " + toSet.EvilCommander.ToString() + " based enemy with attitude " + toSet.CommanderAttack.ToString() + " | Mind " + toSet.CommanderMind.ToString() + " | Smarts " + toSet.CommanderSmarts.ToString() + " inbound!");
            }
            else
                Debug.Log("TACtical_AI: Tech " + tank.name + " is ready to roll!  " + toSet.EvilCommander.ToString() + " based enemy with attitude " + toSet.CommanderAttack.ToString() + " | Mind " + toSet.CommanderMind.ToString() + " | Smarts " + toSet.CommanderSmarts.ToString() + " inbound!");
        }


        // Setup functions
        public static void AutoSetIntelligence(EnemyMind toSet, Tank tank)
        {
            //add Smartness
            int randomNum = UnityEngine.Random.Range(KickStart.LowerDifficulty, KickStart.UpperDifficulty);
            if (randomNum < 35)
                toSet.CommanderSmarts = EnemySmarts.Default;
            else if (randomNum < 60)
                toSet.CommanderSmarts = EnemySmarts.Mild;
            else if (randomNum < 80)
                toSet.CommanderSmarts = EnemySmarts.Meh;
            else if (randomNum < 92)
                toSet.CommanderSmarts = EnemySmarts.Smrt;
            else
                toSet.CommanderSmarts = EnemySmarts.IntAIligent;
            if (randomNum > 98)
            {
                toSet.AllowRepairsOnFly = true;//top 2
                toSet.InvertBullyPriority = true;
            }
            if (toSet.CommanderSmarts > EnemySmarts.Meh)
            {
                toSet.TechMemor = tank.gameObject.GetComponent<AIERepair.DesignMemory>();
                if (toSet.TechMemor.IsNull())
                    toSet.TechMemor = tank.gameObject.AddComponent<AIERepair.DesignMemory>();
                toSet.TechMemor.Initiate();
                toSet.CommanderBolts = EnemyBolts.AtFullOnAggro;// allow base function
            }
        }
        public static bool SetSmartAIStats(AIECore.TankAIHelper thisInst, Tank tank, EnemyMind toSet)
        {
            bool fired = false;
            var BM = tank.blockman;
            //Determine driving method
            BlockSetEnemyHandling(tank, toSet);

            thisInst.TestForFlyingAIRequirement();

            //Determine Attitude
            if (BM.blockCount > 250 && KickStart.MaxEnemyHQLimit > RBases.GetEnemyHQCount())
            {   // Boss
                toSet.InvertBullyPriority = true;
                toSet.CommanderMind = EnemyAttitude.Boss;
                toSet.CommanderAttack = EnemyAttack.Bully;
            }
            else if (BM.GetBlockWithID((uint)BlockTypes.HE_CannonBattleship_216).IsNotNull() || BM.GetBlockWithID((uint)BlockTypes.GSOBigBertha_845).IsNotNull())
            {   // Artillery
                toSet.CommanderMind = EnemyAttitude.Homing;
                toSet.CommanderAttack = EnemyAttack.Spyper;
                fired = true;
            }
            else if (BM.IterateBlockComponents<ModuleItemHolder>().Count() > 0 || toSet.MainFaction == FactionSubTypes.GC)
            {   // Miner
                switch (UnityEngine.Random.Range(0, 3))
                {
                    case 0:
                        if (BM.IterateBlockComponents<ModuleItemHolder>().Count() > 0)
                            toSet.CommanderMind = EnemyAttitude.Miner;
                        else
                            toSet.CommanderMind = EnemyAttitude.Default;
                        toSet.CommanderAttack = EnemyAttack.Coward;
                        toSet.Range = 64;
                        break;
                    default:
                        if (BM.IterateBlockComponents<ModuleItemHolder>().Count() > 0)
                            toSet.CommanderMind = EnemyAttitude.Miner;
                        else
                            toSet.CommanderMind = EnemyAttitude.Default;
                        toSet.CommanderAttack = EnemyAttack.Bully;
                        toSet.Range = 64;
                        toSet.InvertBullyPriority = true;
                        break;
                }
                fired = true;
            }
            else if (BM.IterateBlockComponents<ModuleWeapon>().Count() > 50)
            {   // Over-armed
                toSet.CommanderMind = EnemyAttitude.Homing;
                toSet.CommanderAttack = EnemyAttack.Bully;
                fired = true;
            }
            else if (toSet.MainFaction == FactionSubTypes.VEN)
            {   // Ven
                toSet.CommanderMind = EnemyAttitude.Default;
                toSet.CommanderAttack = EnemyAttack.Circle;
                fired = true;
            }
            else if (toSet.MainFaction == FactionSubTypes.HE)
            {   // Assault
                toSet.CommanderMind = EnemyAttitude.Homing;
                toSet.CommanderAttack = EnemyAttack.Grudge;
                fired = true;
            }
            return fired;
        }
        public static void BlockSetEnemyHandling(Tank tank, EnemyMind toSet)
        {
            var BM = tank.blockman;

            bool isFlying = false;
            bool isFlyingDirectionForwards = true;
            List<ModuleBooster> Engines = BM.IterateBlockComponents<ModuleBooster>().ToList();
            Vector3 biasDirection = Vector3.zero;
            Vector3 boostBiasDirection = Vector3.zero;

            foreach (ModuleBooster module in Engines)
            {
                //Get the slowest spooling one
                List<FanJet> jets = module.transform.GetComponentsInChildren<FanJet>().ToList();
                foreach (FanJet jet in jets)
                {
                    if (jet.spinDelta <= 10)
                    {
                        biasDirection -= tank.rootBlockTrans.InverseTransformDirection(jet.EffectorForwards) * jet.force;
                    }
                }
                List<BoosterJet> boosts = module.transform.GetComponentsInChildren<BoosterJet>().ToList();
                foreach (BoosterJet boost in boosts)
                {
                    //We have to get the total thrust in here accounted for as well because the only way we CAN boost is ALL boosters firing!
                    boostBiasDirection -= tank.rootBlockTrans.InverseTransformDirection(boost.transform.TransformDirection(boost.LocalBoostDirection));
                }
            }
            boostBiasDirection.Normalize();
            biasDirection.Normalize();

            if (biasDirection == Vector3.zero && boostBiasDirection != Vector3.zero)
            {
                isFlying = true;
                if (boostBiasDirection.y > 0.6)
                    isFlyingDirectionForwards = false;
            }
            else if (biasDirection != Vector3.zero)
            {
                isFlying = true;
                if (biasDirection.y > 0.6)
                    isFlyingDirectionForwards = false;
            }
            Debug.Log("TACtical_AI: Tech " + tank.name + " Has bias of" + biasDirection + " and a boost bias of" + boostBiasDirection);

            int FoilCount = 0;
            foreach (ModuleWing module in BM.IterateBlockComponents<ModuleWing>())
            {
                //Get teh slowest spooling one
                List<ModuleWing.Aerofoil> foils = module.m_Aerofoils.ToList();
                FoilCount += foils.Count();
            }

            int modBoostCount = BM.IterateBlockComponents<ModuleBooster>().Count();
            int modHoverCount = BM.IterateBlockComponents<ModuleHover>().Count();
            int modGyroCount = BM.IterateBlockComponents<ModuleGyro>().Count();

            if (tank.IsAnchored)
            {
                toSet.StartedAnchored = true;
                toSet.EvilCommander = EnemyHandling.Stationary;
                toSet.CommanderBolts = EnemyBolts.AtFull;
            }
            else if (modBoostCount > 3 && (modHoverCount > 2 || BM.IterateBlockComponents<ModuleAntiGravityEngine>().Count() > 0))
            {
                toSet.EvilCommander = EnemyHandling.Starship;
            }
            else if (FoilCount > 4 && isFlying && isFlyingDirectionForwards)
            {
                toSet.EvilCommander = EnemyHandling.Airplane;
            }
            else if (modGyroCount > 0 && isFlying && !isFlyingDirectionForwards)
            {
                toSet.EvilCommander = EnemyHandling.Chopper;
            }
            else if (KickStart.isWaterModPresent && modGyroCount > 0 && modBoostCount > 0 && (BM.IterateBlockComponents<ModuleWheels>().Count() < 4 || modHoverCount > 1))
            {
                toSet.EvilCommander = EnemyHandling.Naval;
            }
            else if (BM.IterateBlockComponents<ModuleWeaponGun>().Count() < 2 && BM.IterateBlockComponents<ModuleDrill>().Count() < 2 && modBoostCount > 0)
            {
                toSet.EvilCommander = EnemyHandling.SuicideMissile;
            }
            else
                toSet.EvilCommander = EnemyHandling.Wheeled;

        }
        public static void RandomSetMindAttack(EnemyMind toSet, Tank tank)
        {
            //add Attitude
            int randomNum2 = UnityEngine.Random.Range(1, 4);
            switch (randomNum2)
            {
                case 1:
                    toSet.CommanderMind = EnemyAttitude.Default;
                    break;
                case 2:
                    toSet.CommanderMind = EnemyAttitude.Homing;
                    break;
                case 3:
                    //toSet.CommanderMind = EnemyAttitude.Junker;
                    if (tank.blockman.IterateBlockComponents<ModuleItemHolder>().Count() > 0)
                        toSet.CommanderMind = EnemyAttitude.Miner;
                    else
                        toSet.CommanderMind = EnemyAttitude.Default;
                    break;
                case 4:
                    if (tank.blockman.IterateBlockComponents<ModuleItemHolder>().Count() > 0)
                        toSet.CommanderMind = EnemyAttitude.Miner;
                    else
                        toSet.CommanderMind = EnemyAttitude.Default;
                    break;
            }
            //add Attack
            int randomNum3 = UnityEngine.Random.Range(1, 6);
            switch (randomNum3)
            {
                case 1:
                    toSet.CommanderAttack = EnemyAttack.Circle;
                    break;
                case 2:
                    toSet.CommanderAttack = EnemyAttack.Grudge;
                    break;
                case 3:
                    toSet.CommanderAttack = EnemyAttack.Coward;
                    break;
                case 4:
                    toSet.CommanderAttack = EnemyAttack.Bully;
                    break;
                case 5:
                    toSet.CommanderAttack = EnemyAttack.Pesterer;
                    break;
                case 6:
                    toSet.CommanderAttack = EnemyAttack.Spyper;
                    break;
            }
        }
        public static void SetFromScheme(EnemyMind toSet, Tank tank)
        {
            try
            {
                ControlSchemeCategory Schemer = tank.control.ActiveScheme.Category;
                switch (Schemer)
                {
                    case ControlSchemeCategory.Car:
                        toSet.EvilCommander = EnemyHandling.Wheeled;
                        break;
                    case ControlSchemeCategory.Aeroplane:
                        toSet.EvilCommander = EnemyHandling.Airplane;
                        break;
                    case ControlSchemeCategory.Helicopter:
                        toSet.EvilCommander = EnemyHandling.Chopper;
                        break;
                    case ControlSchemeCategory.AntiGrav:
                        toSet.EvilCommander = EnemyHandling.Starship;
                        break;
                    case ControlSchemeCategory.Rocket:
                        toSet.EvilCommander = EnemyHandling.Starship;
                        break;
                    case ControlSchemeCategory.Hovercraft:
                        toSet.EvilCommander = EnemyHandling.Starship;
                        break;
                    default:
                        string name = tank.control.ActiveScheme.CustomName;
                        if (name == "Ship" || name == "ship" || name == "Naval" || name == "naval" || name == "Boat" || name == "boat")
                        {
                            toSet.EvilCommander = EnemyHandling.Naval;
                        }
                        //Else we just default to Wheeled
                        break;
                }
            }
            catch { }//some population techs are devoid of schemes
        }
    }
}