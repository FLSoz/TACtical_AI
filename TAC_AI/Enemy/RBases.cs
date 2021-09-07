﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using TAC_AI.Templates;

namespace TAC_AI.AI.Enemy
{
    public static class RBases
    {
        const int MinimumBBRequired = 10000; // Before expanding
        private static int MaxSingleBaseType { get { return KickStart.MaxBasesPerTeam / 3; } }
        private static int MaxDefenses { get { return (int)(KickStart.MaxBasesPerTeam * (float)(2f / 3f)); } }
        private static int MaxAutominers { get { return KickStart.MaxBasesPerTeam / 2; } }


        public static List<EnemyBaseFunder> EnemyBases = new List<EnemyBaseFunder>();

        /// <summary>
        /// Does NOT count Defenses!!!
        /// </summary>
        /// <param name="Team"></param>
        /// <returns></returns>
        public static int GetTeamBaseCount(int Team)
        {
            return EnemyBases.FindAll(delegate (EnemyBaseFunder cand) { return cand.Team == Team; }).Count;
        }
        public static EnemyBaseFunder GetTeamFunder(int Team)
        {
            List<EnemyBaseFunder> baseFunders = EnemyBases.FindAll(delegate (EnemyBaseFunder cand) { return cand.Team == Team; });
            if (baseFunders.Count == 0)
            {
                //Debug.Log("TACtical_AI: " + Team + " CALLED GetTeamFunds WITH NO BASE!!!");
                return null;
            }
            if (baseFunders.Count > 1)
            {
                //Debug.Log("TACtical_AI: " + Team + " has " + baseFunders.Count + " bases on scene. The richest will be selected.");
                EnemyBaseFunder funder = null;
                int highestFunds = 0;
                foreach (EnemyBaseFunder funds in baseFunders)
                {
                    if (highestFunds < funds.BuildBucks)
                    {
                        highestFunds = funds.BuildBucks;
                        funder = funds;
                    }
                }
                return funder;
            }
            return baseFunders.First();
        }
        public static int GetTeamFunds(int Team)
        {
            EnemyBaseFunder funder = GetTeamFunder(Team);
            if (funder.IsNull())
            {
                return 0;
            }
            return funder.BuildBucks;
        }
        public static bool PurchasePossible(int BBCost, int Team)
        {
            if (BBCost <= GetTeamFunds(Team))
                return true;
            return false;
        }
        public static bool PurchasePossible(BlockTypes bloc, int Team)
        {
            if (Singleton.Manager<RecipeManager>.inst.GetBlockBuyPrice(bloc, true) <= GetTeamFunds(Team))
                return true;
            return false;
        }
        public static bool PurchasePossible(Tank tank, int Team)
        {
            if (RawTechExporter.GetBBCost(tank) <= GetTeamFunds(Team))
                return true;
            return false;
        }
        public static bool TryMakePurchase(BlockTypes bloc, int Team)
        {
            if (PurchasePossible(bloc, Team))
            {
                var funds = GetTeamFunder(Team);
                funds.SetBuildBucks(funds.BuildBucks - Singleton.Manager<RecipeManager>.inst.GetBlockBuyPrice(bloc, true));
                return true;
            }
            return false;
        }
        public static bool TryMakePurchase(int Pay, int Team)
        {
            if (Pay <= GetTeamFunds(Team))
            {
                var funds = GetTeamFunder(Team);
                funds.SetBuildBucks(funds.BuildBucks - Pay);
                return true;
            }
            return false;
        }
        public static bool TryBribeTech(Tank tank, int Team)
        {
            if (tank.Team != Team && PurchasePossible(tank, Team) && RBolts.AllyCostCount(tank) < KickStart.EnemyTeamTechLimit)
            {
                var funds = GetTeamFunder(Team);
                funds.SetBuildBucks(funds.BuildBucks - RawTechExporter.GetBBCost(tank));
                try
                {   // Prevent bribing of dead Techs
                    if (tank.rootBlockTrans.GetComponent<TankBlock>())
                    {
                        return !tank.rootBlockTrans.GetComponent<TankBlock>().damage.AboutToDie;
                    }
                    else
                        return false;   // Root block does not exist
                }
                catch { return false; }
            }
            return false;
        }
        public static void TryAddMoney(int amount, int Team)
        {
            EnemyBaseFunder funds = GetTeamFunder(Team);
            if (funds.IsNotNull())
            {
                funds.AddBuildBucks(amount);
            }
        }
        public static int GetEnemyHQCount()
        {
            return EnemyBases.FindAll(delegate (EnemyBaseFunder funds) { return funds.isHQ; }).Count;
        }
        public static string GetActualNameDef(string name)
        {
            StringBuilder nameActual = new StringBuilder();
            foreach (char ch in name)
            {
                if (ch == 'â')
                {
                    nameActual.Remove(nameActual.Length - 1, 1);
                    break;
                }
                else
                    nameActual.Append(ch);
            }
            return nameActual.ToString();
        }
        public static void AllTeamTechsBuildRequest(int Team)
        {
            if (!BaseFunderManager.TeamsBuildRequested.Contains(Team))
                BaseFunderManager.TeamsBuildRequested.Add(Team);
        }
        public static void PoolTeamMoney(int Team)
        {
            EnemyBaseFunder funder = GetTeamFunder(Team);
            if (funder.IsNull())
            {
                return;
            }

            List<EnemyBaseFunder> baseFunders = EnemyBases.FindAll(delegate (EnemyBaseFunder cand) { return cand.Team == Team; });
            int moneyPool = 0;
            foreach (EnemyBaseFunder funds in baseFunders)
            {
                if (funder != funds)
                {
                    moneyPool += funds.BuildBucks;
                    funds.SetBuildBucks(0);
                }
            }
            Debug.Log("TACtical_AI: PoolTeamMoney - Team " + Team + " Pooled a total of " + moneyPool + " Build Bucks this time.");
            funder.AddBuildBucks(moneyPool);
        }

        public static void EmergencyMoveMoney(Tank tank)
        {
            int Team = tank.Team;
            EnemyBaseFunder funder = GetTeamFunder(Team);
            var funds = tank.GetComponent<EnemyBaseFunder>();
            if (funder.IsNull())
                return;
            if (!(bool)funds)
                return;

            if (funder == funds)
            {   // Get the next in line
                int baseSize = 0;
                EnemyBaseFunder funderChange = funds;
                List<EnemyBaseFunder> baseFunders = EnemyBases.FindAll(delegate (EnemyBaseFunder cand) { return cand.Team == Team; });
                foreach (EnemyBaseFunder fundC in baseFunders)
                {
                    int blockC = fundC.Tank.blockman.IterateBlocks().Count();
                    if (baseSize < blockC)
                    {
                        baseSize = blockC;
                        funderChange = fundC;
                    }
                }
                if (funderChange == funds)
                    return;

                // Transfer the BB
                funderChange.AddBuildBucks(funds.GetBuildBucksFromName());
                funds.SetBuildBucks(0);

                // Change positioning
                EnemyBases.Remove(funderChange);
                EnemyBases.Insert(0, funderChange);
            }
        }
        public static void RequestFocusFire(Tank tank, Visible Target)
        {
            if (Target.IsNull())
                return;
            if (Target.tank.IsNull())
                return;
            int Team = tank.Team;
            AIECore.AIMessage(tank, tank.name + " is under attack!  Concentrate all fire on " + Target.tank.name + "!");
            foreach (Tank tech in Singleton.Manager<ManTechs>.inst.CurrentTechs)
            {
                if (tech.Team == Team)
                {
                    var helper = tech.GetComponent<AIECore.TankAIHelper>();
                    if ((bool)helper)
                    {
                        var baseFunds = tech.GetComponent<EnemyBaseFunder>();
                        if (!tech.IsAnchored)
                        {
                            var mind = tech.GetComponent<EnemyMind>();
                            if ((bool)mind)
                            {
                                mind.Provoked = true;
                                mind.TempAggro(Target);
                                helper.lastDestination = Target.tank.boundsCentreWorldNoCheck;
                            }
                        }
                        else if ((bool)baseFunds)
                        {
                            var mind = tech.GetComponent<EnemyMind>();
                            if ((bool)mind && baseFunds.Purposes.Contains(BasePurpose.TechProduction))
                            {
                                if (helper.lastEnemy.IsNotNull() && RBolts.AllyCostCount(tech) < KickStart.EnemyTeamTechLimit && !AIERepair.SystemsCheckBolts(tech, mind.TechMemor))
                                    RBolts.BlowBolts(tech, mind);
                            }
                        }
                    }
                }
            }
        }
        public static bool HasTooMuchOfType(int Team, BasePurpose purpose)
        {
            List<EnemyBaseFunder> baseFunders = EnemyBases.FindAll(delegate (EnemyBaseFunder cand) { return cand.Team == Team; });

            int Count = 0;
            foreach (EnemyBaseFunder funds in baseFunders)
            {
                if (funds.Purposes.Contains(purpose))
                {
                    Count++;
                }
            }
            if (purpose == BasePurpose.Defense)
            {
                foreach (Tank tech in Singleton.Manager<ManTechs>.inst.CurrentTechs)
                {
                    if (tech.Team == Team)
                    {
                        if (tech.IsAnchored && !tech.GetComponent<EnemyBaseFunder>())
                        {
                            Count++;
                        }
                    }
                }
            }

            bool thisIsTrue = false;
            if (purpose == BasePurpose.Defense)
            {
                thisIsTrue = Count >= MaxDefenses;
                if (thisIsTrue)
                    Debug.Log("TACtical_AI: HasTooMuchOfType - Team " + Team + " already has too many defenses and cannot make more");
            }
            else if (purpose == BasePurpose.Autominer)
            {
                thisIsTrue = Count >= MaxAutominers;
                if (thisIsTrue)
                    Debug.Log("TACtical_AI: HasTooMuchOfType - Team " + Team + " already has too many autominers and cannot make more");
            }
            else
            {
                thisIsTrue = Count >= MaxSingleBaseType;
                if (thisIsTrue)
                    Debug.Log("TACtical_AI: HasTooMuchOfType - Team " + Team + " already has too many of type " + purpose.ToString() + " and cannot make more");
            }

            return thisIsTrue;
        }

        public class BaseFunderManager : MonoBehaviour
        {
            public static BaseFunderManager inst;

            public static List<int> TeamsBuildRequested = new List<int>();
            private int timeStep = 0;

            public static void Initiate()
            {
                inst = new GameObject("BaseFunderManagerMain").AddComponent<BaseFunderManager>();
                Debug.Log("TACtical_AI: Initiated BaseFunderManager");
            }
            public void Update()
            {
                if (timeStep <= 0)
                {
                    DelayedUpdate();
                    timeStep = 120;
                }
                RunBuildRequests();
                timeStep--;
            }
            public void DelayedUpdate()
            {
                PeriodicBuildRequest();
            }
            public void RunBuildRequests()
            {
                if (TeamsBuildRequested.Count < 1)
                    return;

                foreach (Tank tech in Singleton.Manager<ManTechs>.inst.CurrentTechs)
                {
                    if (TeamsBuildRequested.Contains(tech.Team))
                    {
                        var helper = tech.GetComponent<AIECore.TankAIHelper>();
                        if (helper)
                        {
                            helper.PendingSystemsCheck = true;
                        }
                        else if (tech.IsAnchored)
                        {
                            if (tech.GetComponent<EnemyBaseFunder>())
                                Debug.Log("TACtical_AI: Tech " + tech.name + " is a funder base but contains no DesignMemory?!?");
                        }
                        Debug.Log("TACtical_AI: Team " + tech.Team + " has been issued a team-wide build request!");
                    }
                }
                TeamsBuildRequested.Clear();
            }
            public void PeriodicBuildRequest()
            {
                List<int> teamsFunded = new List<int>();
                if (EnemyBases.Count < 1)
                    return;

                foreach (EnemyBaseFunder funds in EnemyBases)
                {
                    if (!teamsFunded.Contains(funds.Team))
                    {
                        teamsFunded.Add(funds.Team);
                    }
                }
                foreach (Tank tech in Singleton.Manager<ManTechs>.inst.CurrentTechs)
                {
                    if (teamsFunded.Contains(tech.Team))
                    {
                        var helper = tech.GetComponent<AIECore.TankAIHelper>();
                        if (helper)
                        {
                            helper.PendingSystemsCheck = true;
                        }
                        else if (tech.IsAnchored)
                        {
                            if (tech.GetComponent<EnemyBaseFunder>())
                                Debug.Log("TACtical_AI: Tech " + tech.name + " is a funder base but contains no DesignMemory?!?");
                        }
                        //Debug.Log("TACtical_AI: Team " + Team + " has been issued a team-wide build request!");
                    }
                }
                Debug.Log("TACtical_AI: BaseFunderManager - Sent worldwide build request");
            }
        }
        public class EnemyBaseFunder : MonoBehaviour
        {
            public Tank Tank;
            public List<BasePurpose> Purposes = new List<BasePurpose>();
            public int Team { get { return Tank.Team; } }
            public int BuildBucks { get { return buildBucks; } }
            private int buildBucks = 5000;
            public bool isHQ = false;

            public void Initiate(Tank tank)
            {
                Tank = tank;
                tank.TankRecycledEvent.Subscribe(OnRecycle);
                Purposes = RawTechLoader.GetBaseTemplate(RawTechLoader.GetEnemyBaseTypeFromName(GetActualName(tank.name))).purposes;
                if (buildBucks == 5000)
                    buildBucks = GetBuildBucksFromName();
                EnemyBases.Add(this);
                PoolTeamMoney(tank.Team);
                Debug.Log("TACtical_AI: Tech " + tank.name + " Initiated EnemyBaseFunder");
            }
            public void OnRecycle(Tank tank)
            {
                Debug.Log("TACtical_AI: Tech " + tank.name + " Recycled EnemyBaseFunder");
                tank.TankRecycledEvent.Unsubscribe(OnRecycle);
                EnemyBases.Remove(this);
                Destroy(this);
            }
            public void AddBuildBucks(int toAdd)
            {
                SetBuildBucks(buildBucks + toAdd);
            }
            public void SetBuildBucks(int newVal, bool noNameChange = false)
            {
                if (!noNameChange)
                {
                    StringBuilder nameActual = new StringBuilder();
                    char lastIn = 'n';
                    bool doingBB = false;
                    foreach (char ch in name)
                    {
                        if (!doingBB)
                        {
                            if (ch == '¥' && lastIn == '¥')
                            {
                                nameActual.Remove(nameActual.Length - 2, 2);
                                doingBB = true;
                            }
                            else
                                nameActual.Append(ch);
                            lastIn = ch;
                        }
                    }
                    if (newVal == -1)
                    {
                        nameActual.Append(" ¥¥ Inf");
                    }
                    else
                        nameActual.Append(" ¥¥" + newVal);
                    Tank.SetName(nameActual.ToString());
                }
                buildBucks = newVal;
            }
            public int GetBuildBucksFromName(string name = "")
            {
                if (name == "")
                    name = Tank.name;
                StringBuilder funds = new StringBuilder();
                char lastIn = 'n';
                bool doingBB = false;
                foreach (char ch in name)
                {
                    if (!doingBB)
                    {
                        if (ch == '¥' && lastIn == '¥')
                        {
                            doingBB = true;
                        }
                        lastIn = ch;
                    }
                    else    // Get the base's "saved" funds
                    {
                        funds.Append(ch);
                    }
                }
                if (!doingBB)
                    return 0;
                string Funds = funds.ToString();
                if (Funds == " Inf")
                {
                    return -1;
                }
                bool worked = int.TryParse(Funds, out int Output);
                if (!worked)
                {
                    Debug.Log("TACtical_AI: BuildBucks corrupted for tech " + name + ", returning 0");
                    return 0;
                }
                return Output;
            }
            public string GetActualName(string name)
            {
                StringBuilder nameActual = new StringBuilder();
                char lastIn = 'n';
                foreach (char ch in name)
                {
                    if (ch == '¥' && lastIn == '¥')
                    {
                        nameActual.Remove(nameActual.Length - 2, 2);
                        break;
                    }
                    else
                        nameActual.Append(ch);
                    lastIn = ch;
                }
                return nameActual.ToString();
            }
        }

        public static bool SetupBaseAI(AIECore.TankAIHelper thisInst, Tank tank, EnemyMind mind)
        {   // iterate through EVERY BASE dammit
            string name = tank.name;
            bool DidFire = false;

            // Enemy base tech purchese spawn


            if (name == "TEST_BASE")
            {   //It's a base spawned by this mod
                tank.Anchors.TryAnchorAll(true);
                MakeMinersMineUnlimited(tank);
                DidFire = true;
            }
            if (tank.GetComponent<BookmarkBuilder>())
            {
                mind.AllowInvBlocks = true;
                mind.AllowRepairsOnFly = true;
                mind.CommanderAttack = EnemyAttack.Grudge;
                mind.CommanderSmarts = EnemySmarts.IntAIligent;
                mind.CommanderBolts = EnemyBolts.MissionTrigger;
                var builder = tank.GetComponent<BookmarkBuilder>();
                mind.TechMemor = tank.gameObject.GetComponent<AIERepair.DesignMemory>();
                if (mind.TechMemor.IsNull())
                {
                    mind.TechMemor = tank.gameObject.AddComponent<AIERepair.DesignMemory>();
                    mind.TechMemor.Initiate();
                    Debug.Log("TACtical_AI: Tech " + tank.name + " Setup for DesignMemory (SetupBaseAI - BookmarkBuilder)");
                }
                mind.TechMemor.SetupForNewTechConstruction(thisInst, builder.blueprint);
                tank.MainCorps = new List<FactionSubTypes> { builder.faction };
                if (builder.faction != FactionSubTypes.NULL)
                {
                    tank.MainCorps = new List<FactionSubTypes> { builder.faction };
                    mind.MainFaction = builder.faction;
                    //Debug.Log("TACtical_AI: Tech " + tank.name + " set faction " + tank.GetMainCorp().ToString());
                }
                if (builder.instant)
                {
                    AIERepair.Turboconstruct(tank, mind.TechMemor, true);
                    RCore.BlockSetEnemyHandling(tank, mind, true);
                    RCore.RandomSetMindAttack(mind, tank);
                }

                if (builder.unprovoked)
                {
                    mind.CommanderMind = EnemyAttitude.SubNeutral;
                }

                UnityEngine.Object.DestroyImmediate(builder);
                DidFire = true;
                //Debug.Log("TACtical_AI: Tech " + tank.name + " is ready to roll!  " + mind.EvilCommander.ToString() + " based enemy with attitude " + mind.CommanderAttack.ToString() + " | Mind " + mind.CommanderMind.ToString() + " | Smarts " + mind.CommanderSmarts.ToString() + " inbound!");
            }

            if (name.Contains(" ¥¥"))
            {   // Main base
                if (name.Contains("#"))
                {
                    //It's not a base
                    if (tank.IsAnchored)
                    {   // It's a fragment of the base - prevent unwanted mess from getting in the way
                        if (!ManNetwork.IsNetworked || ManNetwork.IsHost)
                            tank.blockman.Disintegrate();
                        return true;
                    }

                    StringBuilder nameNew = new StringBuilder();
                    char lastIn = 'n';
                    foreach (char ch in name)
                    {
                        if (ch == '¥' && lastIn == '¥')
                        {
                            nameNew.Remove(nameNew.Length - 2, 2);
                            break;
                        }
                        else
                            nameNew.Append(ch);
                        lastIn = ch;
                    }
                    nameNew.Append(" Minion");
                    tank.SetName(nameNew.ToString());
                    // it's a minion of the base

                    // Charge the new Tech and send it on it's way!
                    RawTechLoader.ChargeAndClean(tank);
                }
                else
                {
                    mind.TechMemor = tank.gameObject.GetComponent<AIERepair.DesignMemory>();
                    if (mind.TechMemor.IsNull())
                    {
                        mind.TechMemor = tank.gameObject.AddComponent<AIERepair.DesignMemory>();
                        mind.TechMemor.Initiate(false);
                        Debug.Log("TACtical_AI: Tech " + tank.name + " Setup for DesignMemory (SetupBaseAI - Base)");
                    }

                    var funds = tank.gameObject.GetComponent<EnemyBaseFunder>(); 
                    if (funds.IsNull())
                    {
                        funds = tank.gameObject.AddComponent<EnemyBaseFunder>();
                        funds.Initiate(tank);
                    }
                    funds.SetBuildBucks(funds.GetBuildBucksFromName(name), true);

                    try
                    {
                        SpawnBaseTypes type = RawTechLoader.GetEnemyBaseTypeFromName(funds.GetActualName(name));
                        SetupBaseType(type, mind);
                        Debug.Log("TACtical_AI: Registered base " + funds.GetActualName(name) + " |type " + type.ToString());
                        mind.TechMemor.SetupForNewTechConstruction(thisInst, RawTechLoader.GetBlueprint(type));
                        tank.MainCorps.Add(RawTechLoader.GetMainCorp(type));
                    }
                    catch { }
                    if (!tank.IsAnchored)
                        tank.Anchors.TryAnchorAll(true);
                    //if (!tank.IsAnchored)
                        //tank.TryToggleTechAnchor();
                    if (!tank.IsAnchored)
                    {
                        tank.Anchors.RetryAnchorOnBeam = true;
                        tank.Anchors.TryAnchorAll(true);
                        //tank.TryToggleTechAnchor();
                    }
                    MakeMinersMineUnlimited(tank);
                    AllTeamTechsBuildRequest(tank.Team);
                    DidFire = true;
                }
            }
            else if (name.Contains(" â"))
            {   // Defense
                if (name.Contains("#"))
                {
                    //It's not a base
                    StringBuilder nameNew = new StringBuilder();
                    nameNew.Append(GetActualNameDef(name));
                    nameNew.Append(" Minion");
                    tank.SetName(nameNew.ToString());
                    // it's a minion of the base
                }
                else
                {
                    mind.TechMemor = tank.gameObject.GetComponent<AIERepair.DesignMemory>();
                    if (mind.TechMemor.IsNull())
                    {
                        mind.TechMemor = tank.gameObject.AddComponent<AIERepair.DesignMemory>();
                        mind.TechMemor.Initiate();
                        Debug.Log("TACtical_AI: Tech " + tank.name + " Setup for DesignMemory (SetupBaseAI - Defense)");
                    }
                    try
                    {
                        SpawnBaseTypes type = RawTechLoader.GetEnemyBaseTypeFromName(GetActualNameDef(name));
                        SetupBaseType(type, mind);
                        mind.TechMemor.SetupForNewTechConstruction(thisInst, RawTechLoader.GetBlueprint(type));
                        tank.MainCorps.Add(RawTechLoader.GetMainCorp(type));
                    }
                    catch { }
                    if (!tank.IsAnchored)
                        tank.Anchors.TryAnchorAll(true);
                    if (!tank.IsAnchored)
                        tank.TryToggleTechAnchor();
                    if (!tank.IsAnchored)
                    {
                        tank.Anchors.RetryAnchorOnBeam = true;
                        tank.TryToggleTechAnchor();
                    }
                    DidFire = true;
                }
            }

            return DidFire;
        }

        public static void SetupBaseType(SpawnBaseTypes type, EnemyMind mind)
        {   // iterate through EVERY BASE dammit
            if (RawTechLoader.IsHQ(type))
            {
                mind.StartedAnchored = true;
                mind.AllowInvBlocks = true;
                mind.AllowRepairsOnFly = true;
                mind.InvertBullyPriority = true;
                mind.EvilCommander = EnemyHandling.Stationary;
                mind.CommanderAttack = EnemyAttack.Bully;
                mind.CommanderMind = EnemyAttitude.Homing;
                mind.CommanderSmarts = EnemySmarts.IntAIligent;
                mind.CommanderBolts = EnemyBolts.AtFull;
            }
            else if (RawTechLoader.ContainsPurpose(type, BasePurpose.Harvesting))
            {
                mind.StartedAnchored = true;
                mind.AllowInvBlocks = true;
                mind.AllowRepairsOnFly = true;
                mind.EvilCommander = EnemyHandling.Stationary;
                mind.CommanderMind = EnemyAttitude.Default;
                mind.CommanderSmarts = EnemySmarts.IntAIligent;
                mind.CommanderAttack = EnemyAttack.Grudge;
                mind.CommanderBolts = EnemyBolts.AtFull;
            }
            else if (RawTechLoader.ContainsPurpose(type, BasePurpose.TechProduction))
            {
                mind.StartedAnchored = true;
                mind.AllowInvBlocks = true;
                mind.AllowRepairsOnFly = true;
                mind.EvilCommander = EnemyHandling.Stationary;
                mind.CommanderMind = EnemyAttitude.Default;
                mind.CommanderSmarts = EnemySmarts.IntAIligent;
                mind.CommanderAttack = EnemyAttack.Grudge;
                mind.CommanderBolts = EnemyBolts.AtFullOnAggro;
            }
            else
            {
                mind.StartedAnchored = true;
                mind.AllowInvBlocks = true;
                mind.AllowRepairsOnFly = true;
                mind.EvilCommander = EnemyHandling.Stationary;
                mind.CommanderMind = EnemyAttitude.Default;
                mind.CommanderSmarts = EnemySmarts.IntAIligent;
                mind.CommanderAttack = EnemyAttack.Grudge;
                mind.CommanderBolts = EnemyBolts.AtFull;
            }
        }


        // Base Operations
        public static void ImTakingThatExpansion(EnemyMind mind, EnemyBaseFunder funds)
        {   // Expand the base!
            if (!KickStart.AllowEnemyBaseExpand)
                return;
            try
            {
                if (SpecialAISpawner.IsAttract)
                    return; // no branching

                if (funds.BuildBucks < MinimumBBRequired)
                    return; // Reduce expansion lag

                Tank tech = mind.AIControl.tank;


                if (GetTeamBaseCount(tech.Team) >= KickStart.MaxBasesPerTeam)
                    return;

                int grade = 99;
                try
                {
                    if (!SpecialAISpawner.CreativeMode)
                        grade = Singleton.Manager<ManLicenses>.inst.GetCurrentLevel(mind.MainFaction);
                }
                catch { }


                if (TryFindExpansionLocation(tech, tech.boundsCentreWorldNoCheck, out Vector3 pos))
                {   // Try spawning defense
                    SpawnBaseTypes type = RawTechLoader.GetEnemyBaseType(mind.MainFaction, PickBuildBasedOnPriorities(mind, funds), RawTechLoader.GetTerrain(pos), maxGrade: grade, maxPrice: GetTeamFunds(tech.Team));
                    if (RawTechLoader.IsFallback(type))
                        return;
                    if (RawTechLoader.SpawnBaseExpansion(tech, pos, tech.Team, type))
                    {
                        Debug.Log("TACtical_AI: ImTakingThatExpansion - Team " + tech.Team + ": That expansion is mine!");
                    }
                    else
                        Debug.Log("TACtical_AI: SpawnBaseExpansion - Team " + tech.Team + ": Failiure on expansion");
                }
                else if (TryFindExpansionLocation2(tech, tech.boundsCentreWorldNoCheck, out Vector3 pos2))
                {   // Try spawning base extensions
                    SpawnBaseTypes type = RawTechLoader.GetEnemyBaseType(mind.MainFaction, PickBuildNonDefense(mind), RawTechLoader.GetTerrain(pos2), maxGrade: grade, maxPrice: GetTeamFunds(tech.Team));
                    if (RawTechLoader.IsFallback(type))
                        return;
                    if (RawTechLoader.SpawnBaseExpansion(tech, pos2, tech.Team, type))
                    {
                        Debug.Log("TACtical_AI: ImTakingThatExpansion - Team " + tech.Team + ": That expansion is mine!");
                    }
                    else
                        Debug.Log("TACtical_AI: SpawnBaseExpansion - Team " + tech.Team + ": Failiure on expansion");
                }
            }
            catch
            {
                Debug.Log("TACtical_AI: ImTakingThatExpansion - game is being stubborn");
            }
        }

        public static BasePurpose PickBuildBasedOnPriorities(EnemyMind mind, EnemyBaseFunder funds)
        {   // Expand the base!
            int team = mind.Tank.Team;
            if (GetTeamFunds(team) <= CheapestAutominerPrice(mind) && !HasTooMuchOfType(team, BasePurpose.Autominer))
            {   // YOU MUST CONSTRUCT ADDITIONAL PYLONS
                return BasePurpose.Autominer;
            }
            else if (mind.AIControl.lastEnemy)
            {
                switch (UnityEngine.Random.Range(1, 7))
                {
                    case 1:
                        if (HasTooMuchOfType(team, BasePurpose.Defense))
                            return BasePurpose.TechProduction;
                        return BasePurpose.Defense;
                    case 2:
                        if (HasTooMuchOfType(team, BasePurpose.Harvesting))
                            return BasePurpose.TechProduction;
                        return BasePurpose.Harvesting;
                    case 3:
                        if (HasTooMuchOfType(team, BasePurpose.HasReceivers))
                            return BasePurpose.TechProduction;
                        return BasePurpose.HasReceivers;
                    case 4:
                        return BasePurpose.TechProduction;
                    case 5:
                        if (HasTooMuchOfType(team, BasePurpose.Autominer))
                            return BasePurpose.TechProduction;
                        return BasePurpose.Autominer;
                    default:
                        if (HasTooMuchOfType(team, BasePurpose.Defense))
                            return BasePurpose.TechProduction;
                        return BasePurpose.Defense;
                }
            }
            else
            {
                switch (UnityEngine.Random.Range(0, 5))
                {
                    case 1:
                        if (HasTooMuchOfType(team, BasePurpose.Defense))
                            return BasePurpose.TechProduction;
                        return BasePurpose.Defense;
                    case 2:
                        if (HasTooMuchOfType(team, BasePurpose.Harvesting))
                            return BasePurpose.TechProduction;
                        return BasePurpose.Harvesting;
                    case 3:
                        if (HasTooMuchOfType(team, BasePurpose.HasReceivers))
                            return BasePurpose.TechProduction;
                        return BasePurpose.HasReceivers;
                    case 4:
                        return BasePurpose.TechProduction;
                    case 5:
                        if (HasTooMuchOfType(team, BasePurpose.Autominer))
                            return BasePurpose.TechProduction;
                        return BasePurpose.Autominer;
                    default:
                        if (HasTooMuchOfType(team, BasePurpose.Harvesting))
                            return BasePurpose.TechProduction;
                        return BasePurpose.AnyNonHQ;
                }
            }
        }
        public static BasePurpose PickBuildNonDefense(EnemyMind mind)
        {   // Expand the base!
            int team = mind.Tank.Team;
            switch (UnityEngine.Random.Range(0, 5))
            {
                case 2:
                    if (HasTooMuchOfType(team, BasePurpose.Defense))
                        return BasePurpose.TechProduction;
                    return BasePurpose.Harvesting;
                case 3:
                    if (HasTooMuchOfType(team, BasePurpose.HasReceivers))
                        return BasePurpose.TechProduction;
                    return BasePurpose.HasReceivers;
                case 4:
                case 5:
                    return BasePurpose.TechProduction;
                default:
                    if (HasTooMuchOfType(team, BasePurpose.Autominer))
                        return BasePurpose.TechProduction;
                    return BasePurpose.Autominer;
            }
        }


        // inf money for enemy autominer bases - make sure that no mess
        public static void MakeMinersMineUnlimited(Tank tank)
        {   // make autominers mine deep based on biome
            try
            {
                //Debug.Log("TACtical_AI: " + tank.name + " is trying to mine unlimited");
                foreach (ModuleItemProducer module in tank.blockman.IterateBlockComponents<ModuleItemProducer>())
                {
                    module.gameObject.GetOrAddComponent<ReverseCache>().SaveComponents();
                }
            }
            catch
            {
                Debug.Log("TACtical_AI: MakeMinersMineUnlimited - game is being stubborn");
            }
        }
        public static ChunkTypes[] TryGetBiomeResource(Vector3 pos)
        {   // make autominers mine deep based on biome
            switch (ManWorld.inst.GetBiomeWeightsAtScenePosition(pos).Biome(0).BiomeType)
            {
                case BiomeTypes.Grassland:
                    return new ChunkTypes[1] { ChunkTypes.EruditeShard };
                case BiomeTypes.Desert:
                    return new ChunkTypes[4] { ChunkTypes.OleiteJelly, ChunkTypes.OleiteJelly, ChunkTypes.OleiteJelly, ChunkTypes.IgniteShard };
                case BiomeTypes.Mountains:
                    return new ChunkTypes[1] { ChunkTypes.RoditeOre, };
                case BiomeTypes.SaltFlats:
                case BiomeTypes.Ice:
                    return new ChunkTypes[4] { ChunkTypes.CarbiteOre, ChunkTypes.CarbiteOre, ChunkTypes.CarbiteOre, ChunkTypes.CelestiteShard };

                case BiomeTypes.Pillars:
                    return new ChunkTypes[1] { ChunkTypes.RubberJelly };

                default:
                    return new ChunkTypes[2] { ChunkTypes.PlumbiteOre, ChunkTypes.TitaniteOre };
            }
        }


        // Utilities
        internal static bool IsLocationGridEmpty(Vector3 expansionCenter, bool ignoreNeutrals = true)
        {
            bool chained = false;
            if (!IsLocationValid(expansionCenter + (Vector3.forward * 64), ref chained, false, ignoreNeutrals))
                return false;
            if (!IsLocationValid(expansionCenter - (Vector3.forward * 64), ref chained, false, ignoreNeutrals))
                return false;
            if (!IsLocationValid(expansionCenter - (Vector3.right * 64), ref chained, false, ignoreNeutrals))
                return false;
            if (!IsLocationValid(expansionCenter + (Vector3.right * 64), ref chained, false, ignoreNeutrals))
                return false;
            if (!IsLocationValid(expansionCenter + ((Vector3.right + Vector3.forward) * 64), ref chained, false, ignoreNeutrals))
                return false;
            if (!IsLocationValid(expansionCenter - ((Vector3.right + Vector3.forward) * 64), ref chained, false, ignoreNeutrals))
                return false;
            if (!IsLocationValid(expansionCenter + ((Vector3.right - Vector3.forward) * 64), ref chained, false, ignoreNeutrals))
                return false;
            if (!IsLocationValid(expansionCenter - ((Vector3.right - Vector3.forward) * 64), ref chained, false, ignoreNeutrals))
                return false;
            return true;
        }

        internal static bool TryFindExpansionLocationGrid(Vector3 expansionCenter, out Vector3 pos)
        {
            bool chained = false;
            int MaxPossibleLocations = 7;
            List<int> location = new List<int>();
            for (int step = 0; step < MaxPossibleLocations; step++)
            {
                location.Add(step);
            }

            int locationsCount = MaxPossibleLocations;
            while (locationsCount > 0)
            {
                int choice = location.GetRandomEntry();
                location.Remove(choice);
                switch (choice)
                {
                    case 0:
                        if (IsLocationValid(expansionCenter + (Vector3.forward * 64), ref chained))
                        {
                            pos = expansionCenter + (Vector3.forward * 64);
                            return true;
                        }
                        break;
                    case 1:
                        if (IsLocationValid(expansionCenter - (Vector3.forward * 64), ref chained))
                        {
                            pos = expansionCenter - (Vector3.forward * 64);
                            return true;
                        }
                        break;
                    case 2:
                        if (IsLocationValid(expansionCenter - (Vector3.right * 64), ref chained))
                        {
                            pos = expansionCenter - (Vector3.right * 64);
                            return true;
                        }
                        break;
                    case 3:
                        if (IsLocationValid(expansionCenter + (Vector3.right * 64), ref chained))
                        {
                            pos = expansionCenter + (Vector3.right * 64);
                            return true;
                        }
                        break;
                    case 4:
                        if (IsLocationValid(expansionCenter + ((Vector3.right + Vector3.forward) * 64), ref chained))
                        {
                            pos = expansionCenter + ((Vector3.right + Vector3.forward) * 64);
                            return true;
                        }
                        break;
                    case 5:
                        if (IsLocationValid(expansionCenter - ((Vector3.right + Vector3.forward) * 64), ref chained))
                        {
                            pos = expansionCenter - ((Vector3.right + Vector3.forward) * 64);
                            return true;
                        }
                        break;
                    case 6:
                        if (IsLocationValid(expansionCenter + ((Vector3.right - Vector3.forward) * 64), ref chained))
                        {
                            pos = expansionCenter + ((Vector3.right - Vector3.forward) * 64);
                            return true;
                        }
                        break;
                    case 7:
                        if (IsLocationValid(expansionCenter - ((Vector3.right - Vector3.forward) * 64), ref chained))
                        {
                            pos = expansionCenter - ((Vector3.right - Vector3.forward) * 64);
                            return true;
                        }
                        break;
                }
                locationsCount--;
            }
            pos = expansionCenter;
            return false;
        }
        private static bool TryFindExpansionLocation(Tank tank, Vector3 expansionCenter, out Vector3 pos)
        {
            bool chained = false;
            if (IsLocationValid(expansionCenter + (tank.rootBlockTrans.forward * 64), ref chained))
            {
                pos = expansionCenter + (tank.rootBlockTrans.forward * 64);
                return true;
            }
            else if (IsLocationValid(expansionCenter - (tank.rootBlockTrans.forward * 64), ref chained))
            {
                pos = expansionCenter - (tank.rootBlockTrans.forward * 64);
                return true;
            }
            else if (IsLocationValid(expansionCenter - (tank.rootBlockTrans.right * 64), ref chained))
            {
                pos = expansionCenter - (tank.rootBlockTrans.right * 64);
                return true;
            }
            else if (IsLocationValid(expansionCenter + (tank.rootBlockTrans.right * 64), ref chained))
            {
                pos = expansionCenter + (tank.rootBlockTrans.right * 64);
                return true;
            }
            else
            {
                pos = expansionCenter;
                return false;
            }
        }
        private static bool TryFindExpansionLocation2(Tank tank, Vector3 expansionCenter, out Vector3 pos)
        {
            bool chained = false;
            if (IsLocationValid(expansionCenter + ((tank.rootBlockTrans.right + tank.rootBlockTrans.forward) * 64), ref chained))
            {
                pos = expansionCenter + ((tank.rootBlockTrans.right + tank.rootBlockTrans.forward) * 64);
                return true;
            }
            else if (IsLocationValid(expansionCenter - ((tank.rootBlockTrans.right + tank.rootBlockTrans.forward) * 64), ref chained))
            {
                pos = expansionCenter - ((tank.rootBlockTrans.right + tank.rootBlockTrans.forward) * 64);
                return true;
            }
            else if (IsLocationValid(expansionCenter + ((tank.rootBlockTrans.right - tank.rootBlockTrans.forward) * 64), ref chained))
            {
                pos = expansionCenter + ((tank.rootBlockTrans.right - tank.rootBlockTrans.forward) * 64);
                return true;
            }
            else if (IsLocationValid(expansionCenter - ((tank.rootBlockTrans.right - tank.rootBlockTrans.forward) * 64), ref chained))
            {
                pos = expansionCenter - ((tank.rootBlockTrans.right - tank.rootBlockTrans.forward) * 64);
                return true;
            }
            else
            {
                pos = expansionCenter;
                return false;
            }
        }
        private static bool IsLocationValid(Vector3 pos, ref bool ChainCancel, bool resourcesToo = true, bool IgnoreNeutral = false)
        {
            if (ChainCancel)
                return false;
            bool validLocation = true;
            if (!Singleton.Manager<ManWorld>.inst.GetTerrainHeight(pos, out _))
            {
                return false;
            }

            foreach (Visible vis in Singleton.Manager<ManVisible>.inst.VisiblesTouchingRadius(pos, 32, new Bitfield<ObjectTypes>()))
            {
                if (resourcesToo && vis.resdisp.IsNotNull())
                {
                    if (vis.isActive)
                        validLocation = false;
                }
                if (vis.tank.IsNotNull())
                {
                    if (IgnoreNeutral && vis.tank.Team == -2)
                        continue;
                    var helper = vis.tank.GetComponent<AIECore.TankAIHelper>();
                    if (helper.TechMemor)
                    {
                        if (helper.PendingSystemsCheck)
                            ChainCancel = true; // A tech is still being built here - we cannot build more until done!
                    }
                    validLocation = false;
                }
            }
            return validLocation;
        }
        private static int CheapestAutominerPrice(EnemyMind mind)
        {
            List<SpawnBaseTypes> types = RawTechLoader.GetEnemyBaseTypes(mind.MainFaction, BasePurpose.Autominer, BaseTerrain.Land);
            int lowest = 150000;
            foreach (SpawnBaseTypes type in types)
            {
                int tryThis = RawTechLoader.GetBaseStartingFunds(type);
                if (tryThis < lowest)
                {
                    lowest = tryThis;
                }
            }
            return lowest;
        }
    }
}
