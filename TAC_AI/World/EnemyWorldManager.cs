﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using TAC_AI.Templates;
using TAC_AI.AI.Enemy;
using TAC_AI.AI;

namespace TAC_AI.World
{
    // Manages Enemy bases that are off-screen
    // Za wardo
    //  Enemy bases only attack if:
    //    PLAYER BASES (Only when player base is ON SCENE):
    //      An enemy scout has found the player's BASE position
    //      An enemy scout follows the player home to their base and shoots at it
    //      the player attacks the enemy and the enemy base is ON SCENE
    //    ENEMY BASES
    //      An enemy scout has found another enemy base
    //
    public class EnemyWorldManager : MonoBehaviour
    {
        public static EnemyWorldManager inst;
        public static bool enabledThis = false;
        public static bool subToTiles = false;

        // There are roughly around 6 chunks per node
        internal static float SurfaceHarvestingMulti = 5.5f;
        internal static int HealthRepairCost = 60;
        internal static int HealthRepairRate = 15; 
        internal static int UpdateDelay = 400;
        internal static int UpdateMoveDelay = 160;
        internal static int ExpectedDPSDelitime = 60;
        internal const int UnitSightRadius = 2;
        internal const int BaseSightRadius = 3;
        internal const float EnemyBaseCullingRangeSq = 562500;// 750
        private static float TerrainTraverseMulti = 0.75f;
        private static Dictionary<FactionTypesExt, float> corpSpeeds = new Dictionary<FactionTypesExt, float>() {
            {
                FactionTypesExt.GSO , 60
            },
            {
                FactionTypesExt.GC , 40
            },
            {
                FactionTypesExt.VEN , 100
            },
            {
                FactionTypesExt.HE , 50
            },
            {
                FactionTypesExt.BF , 75
            },
            { FactionTypesExt.EXP, 45 },
            { FactionTypesExt.GT, 65 },
            { FactionTypesExt.TAC, 70 },
            { FactionTypesExt.OS, 45 },
        };

        private static FieldInfo ProdDelay = typeof(ModuleItemProducer).GetField("m_SecPerItemProduced", BindingFlags.NonPublic | BindingFlags.Instance);

        private static int UpdateTimer = 0;
        private static int UpdateMoveTimer = 0;
        private static Dictionary<int, EnemyPresence> EnemyTeams = new Dictionary<int, EnemyPresence>();
        private static List<KeyValuePair<float, TileMoveCommand>> QueuedUnitMoves = new List<KeyValuePair<float, TileMoveCommand>>();

        public static void Initiate()
        {
            if (!KickStart.AllowStrategicAI)
                return;
            inst = new GameObject("EnemyWorldManager").AddComponent<EnemyWorldManager>();
            Debug.Log("TACtical_AI: Created EnemyWorldManager.");

        }
        public static void LateInitiate()
        {
            if (!KickStart.AllowStrategicAI)
                return;
            Singleton.Manager<ManTechs>.inst.TankDestroyedEvent.Subscribe(OnTechDestroyed);
            Singleton.Manager<ManGameMode>.inst.ModeStartEvent.Subscribe(OnWorldLoad);
            Singleton.Manager<ManGameMode>.inst.ModeSwitchEvent.Subscribe(OnWorldReset);
            World.PlayerRTSControl.Initiate();
        }
        public static void OnWorldLoad(Mode mode)
        {
            EnemyTeams.Clear();
            QueuedUnitMoves.Clear();
            if (mode is ModeAttract)
            {
                enabledThis = false;
                return;
            }
            if (!subToTiles)
            {
                Singleton.Manager<ManWorld>.inst.TileManager.TilePopulatedEvent.Subscribe(OnTileTechsRespawned);
                Singleton.Manager<ManWorld>.inst.TileManager.TileDepopulatedEvent.Subscribe(OnTileTechsDespawned);
                subToTiles = true;
            }
            enabledThis = true;
            UpdateTimer = 0;
            int count = 0;
            try
            {
                List<IntVector2> tiles = Singleton.Manager<ManSaveGame>.inst.CurrentState.m_StoredTilesJSON.Keys.ToList();
                //List<ManSaveGame.StoredTile> tiles = Singleton.Manager<ManSaveGame>.inst.CurrentState.m_StoredTiles.Values.ToList();
                foreach (IntVector2 tile in tiles)
                {
                    ManSaveGame.StoredTile tileInst = Singleton.Manager<ManSaveGame>.inst.GetStoredTile(tile, false);
                    if (tileInst == null)
                        continue;
                    if (tileInst.m_StoredVisibles.TryGetValue(1, out List<ManSaveGame.StoredVisible> techs))
                    {
                        foreach (ManSaveGame.StoredVisible Vis in techs)
                        {
                            if (Vis is ManSaveGame.StoredTech tech)
                            {
                                HandleTechUnloaded(tech, tileInst.coord);
                                count++;
                            }
                        }
                    }
                }
                Debug.Log("TACtical_AI: OnWorldLoad Handled " + count + " Techs");
                //Singleton.Manager<ManSaveGame>.inst.CurrentState.m_StoredTiles.Count();
            }
            catch { }
        }
        public static void OnWorldReset()
        {
            /*
            EnemyTeams.Clear();
            QueuedUnitMoves.Clear();
            */
        }

        public static void OnTileTechsRespawned(WorldTile WT)
        {
            if (!enabledThis)
                return;
            foreach (EnemyTechUnit ETU in GetTechsInTile(WT.Coord))
            {
                RemoveTechFromTeam(ETU); // Cannot manage loaded techs
            }
        }
        public static void OnTileTechsDespawned(WorldTile WT)
        {
            if (!enabledThis)
                return;
            ManSaveGame.StoredTile tileInst = Singleton.Manager<ManSaveGame>.inst.GetStoredTile(WT.Coord, false);
            if (tileInst == null)
                return;
            if (tileInst.m_StoredVisibles.TryGetValue(1, out List<ManSaveGame.StoredVisible> techs))
            {
                foreach (ManSaveGame.StoredVisible Vis in techs)
                {
                    if (Vis is ManSaveGame.StoredTech tech)
                    {
                        HandleTechUnloaded(tech, WT.Coord);
                    }
                }
            }
        }
        public static void OnTechDestroyed(Tank tech, ManDamage.DamageInfo poof)
        {
            if (!enabledThis)
                return;
            EnemyBaseWorld.RemoteRemove(GetETUFromTank(tech));
        }
        
        public static IntVector2 TryRefindTech(IntVector2 prev, EnemyTechUnit tech)
        {
            ManSaveGame.StoredTech techFind = tech.tech;
            try
            {
                List<IntVector2> tiles = Singleton.Manager<ManSaveGame>.inst.CurrentState.m_StoredTilesJSON.Keys.ToList();
                //List<ManSaveGame.StoredTile> tiles = Singleton.Manager<ManSaveGame>.inst.CurrentState.m_StoredTiles.Values.ToList();
                foreach (IntVector2 tile in tiles)
                {
                    ManSaveGame.StoredTile tileInst = Singleton.Manager<ManSaveGame>.inst.GetStoredTile(tile, false);
                    if (tileInst == null)
                        continue;
                    if (tileInst.m_StoredVisibles.TryGetValue(1, out List<ManSaveGame.StoredVisible> techs))
                    {
                        if (techs.Contains(techFind))
                            return tile;
                    }
                }
            }
            catch { }
            return prev;
        }


        // WORLD Loading
        public static void HandleTechUnloaded(ManSaveGame.StoredTech tech, IntVector2 tilePos)
        {
            int level = 0;
            if (ManSpawn.IsEnemyTeam(tech.m_TeamID) && !tech.m_IsPopulation)
            {   // Enemy Team
                List<TankPreset.BlockSpec> specs = tech.m_TechData.m_BlockSpecs;
                long healthAll = 0;
                if (tech.m_TechData.Name.Contains(" ¥¥"))
                {
                    try
                    {
                        EnemyBaseUnloaded EBU = new EnemyBaseUnloaded(tilePos, tech, PrepTeam(tech.m_TeamID));
                        level++;
                        foreach (TankPreset.BlockSpec spec in specs)
                        {
                            TankBlock TB = ManSpawn.inst.GetBlockPrefab(spec.GetBlockType());
                            if ((bool)TB)
                            {
                                var Weap = TB.GetComponent<ModuleWeapon>();
                                if ((bool)Weap)
                                {
                                    EBU.isArmed = true;
                                    EBU.AttackPower += TB.filledCells.Length;
                                }
                                var MIP = TB.GetComponent<ModuleItemProducer>();
                                if ((bool)MIP)
                                {
                                    EBU.revenue += (int)((GetBiomeGains(tech.m_WorldPosition.GameWorldPosition) * UpdateDelay) / (float)ProdDelay.GetValue(MIP));
                                }
                                healthAll += Mathf.Max((int)TB.GetComponent<Damageable>().Health / 10, 1);
                            }
                        }
                        level++;
                        EBU.Faction = tech.m_TechData.GetMainCorpExt();
                        EBU.Health = healthAll;
                        EBU.MaxHealth = healthAll;
                        EBU.MoveSpeed = 0; //(STATIONARY)
                        level++;
                        EBU.Funds = RBases.GetBuildBucksFromNameExt(tech.m_TechData.Name);
                        SpawnBaseTypes SBT = RawTechLoader.GetEnemyBaseTypeFromName(RBases.EnemyBaseFunder.GetActualName(tech.m_TechData.Name));
                        List<BasePurpose> BP = RawTechLoader.GetBaseTemplate(SBT).purposes;

                        level++;
                        if (BP.Contains(BasePurpose.TechProduction))
                            EBU.isTechBuilder = true;
                        if (BP.Contains(BasePurpose.HasReceivers))
                        {
                            EBU.isHarvestBase = true;
                            EBU.revenue += GetBiomeSurfGains(tech.m_WorldPosition.GameWorldPosition) * UpdateDelay;
                        }
                        if (BP.Contains(BasePurpose.Headquarters))
                            EBU.isSiegeBase = true;

                        level++;
                        AddToTeam(EBU);
                    }
                    catch
                    {
                        Debug.Log("TACtical_AI: HandleTechUnloaded(EBU) Failiure on init at level " + level + "!");
                    }
                }
                else
                {
                    try
                    {
                        EnemyTechUnit ETU = new EnemyTechUnit(tilePos, tech);
                        level++;
                        foreach (TankPreset.BlockSpec spec in specs)
                        {
                            TankBlock TB = ManSpawn.inst.GetBlockPrefab(spec.GetBlockType());
                            if ((bool)TB)
                            {
                                var Weap = TB.GetComponent<ModuleWeapon>();
                                if ((bool)Weap)
                                {
                                    ETU.isArmed = true;
                                    ETU.AttackPower += TB.filledCells.Length;
                                }
                                if (TB.GetComponent<ModuleItemHolderBeam>())
                                    ETU.canHarvest = true;
                                healthAll += Mathf.Max((int)TB.GetComponent<Damageable>().Health / 10, 1);
                            }
                        }
                        level++;
                        ETU.Health = healthAll;
                        ETU.MaxHealth = healthAll;
                        ETU.Faction = tech.m_TechData.GetMainCorpExt();
                        ETU.MoveSpeed = 0;
                        level++;
                        if (!tech.m_TechData.CheckIsAnchored() && !tech.m_TechData.Name.Contains(" â"))
                        {
                            ETU.MoveSpeed = 25;
                            if (corpSpeeds.TryGetValue(ETU.Faction, out float sped))
                                ETU.MoveSpeed = sped;
                        }
                        level++;
                        AddToTeam(ETU);
                    }
                    catch
                    {
                        Debug.Log("TACtical_AI: HandleTechUnloaded(ETU) Failiure on init at level " + level + "!");
                    }
                }
            }
        }
        public static ManSaveGame.StoredTile GetTile(EnemyTechUnit tech)
        {
            ManSaveGame.StoredTile Tile = Singleton.Manager<ManSaveGame>.inst.GetStoredTile(tech.tilePos);
            if (Tile != null)
            {
                if (Tile.m_StoredVisibles.TryGetValue(1, out List<ManSaveGame.StoredVisible> techs))
                {
                    if (techs.Contains(tech.tech))
                        return Tile;
                }
            }
            return null;
        }
        public static bool MoveTechIntoTile(EnemyTechUnit tech, ManSaveGame.StoredTile tile)
        {
            if (tile != null)
            {
                if (ManWorld.inst.TileManager.IsTileAtPositionLoaded(WorldPosition.FromGameWorldPosition(tech.tech.GetBackwardsCompatiblePosition()).ScenePosition))
                {
                    if (!FindFreeSpaceOnTile(tile.coord - tech.tilePos, tile, out Vector2 newPosOff))
                        return false;
                    ManSpawn.TankSpawnParams tankSpawn = new ManSpawn.TankSpawnParams();
                    tankSpawn.techData = tech.tech.m_TechData;
                    tankSpawn.blockIDs = null;
                    tankSpawn.teamID = tech.tech.m_TeamID;
                    tankSpawn.position = newPosOff;
                    tankSpawn.rotation = Quaternion.LookRotation(Singleton.cameraTrans.position - tech.tech.GetBackwardsCompatiblePosition(), Vector3.up);
                    tankSpawn.ignoreSceneryOnSpawnProjection = false;
                    tankSpawn.forceSpawn = false;
                    tankSpawn.isPopulation = false;
                    tankSpawn.grounded = tech.tech.m_Grounded;
                    Tank newTech = Singleton.Manager<ManSpawn>.inst.SpawnTank(tankSpawn, true);
                    if (newTech != null)
                    {
                        return true;
                    }
                }
                else
                {
                    List<int> BTs = new List<int>();
                    ManSaveGame.StoredTech ST = tech.tech;
                    foreach (TankPreset.BlockSpec mem in ST.m_TechData.m_BlockSpecs)
                    {
                        if (!BTs.Contains((int)mem.m_BlockType))
                        {
                            BTs.Add((int)mem.m_BlockType);
                        }
                    }
                    if (!FindFreeSpaceOnTile(tile.coord - tech.tilePos, tile, out Vector2 newPosOff))
                        return false;
                    Vector3 newPos = newPosOff.ToVector3XZ() + ManWorld.inst.TileManager.CalcTileCentre(tile.coord);
                    Quaternion fromDirect = Quaternion.LookRotation(newPos - ST.m_Position);
                    tile.AddSavedTech(ST.m_TechData, BTs.ToArray(), ST.m_TeamID, newPos, fromDirect, true, false, true, ST.m_ID, false, 1, true);
                    if (tile.m_StoredVisibles.TryGetValue(1, out List<ManSaveGame.StoredVisible> SV))
                    {
                        HandleTechUnloaded((ManSaveGame.StoredTech)SV.Last(), tile.coord);
                    }
                }
                return true;
            }
            return false;
        }
        public static bool FindFreeSpaceOnTile(Vector2 headingDirection, ManSaveGame.StoredTile tile, out Vector2 finalPos)
        {
            finalPos = Vector3.zero;
            //List<EnemyTechUnit> ETUs = GetTechsInTile(tile.coord);
            int partitions = (int)ManWorld.inst.TileSize / 64;
            float partitionScale = ManWorld.inst.TileSize / partitions;
            float halfDist = (ManWorld.inst.TileSize - partitionScale) / 2;
            List<Vector2> possibleSpots = new List<Vector2>();

            for (int stepX = (int)-halfDist; stepX < halfDist; stepX += (int)partitionScale)
            {
                for (int stepY = (int)-halfDist; stepY < halfDist; stepY += (int)partitionScale)
                {
                    Vector2 New = new Vector2(stepX, stepY);
                    if (GetTechsInTile(tile.coord, New, partitionScale / 2).Count() == 0)
                        possibleSpots.Add(New);
                }
            }
            if (possibleSpots.Count == 0)
                return false;

            if (possibleSpots.Count == 1)
            {
                finalPos = possibleSpots.First();
                return true;
            }

            Vector2 Directed = -((headingDirection.normalized) * ManWorld.inst.TileSize);
            possibleSpots = possibleSpots.OrderBy(x => (x - Directed).sqrMagnitude).ToList();
            finalPos = possibleSpots.First();
            return true;
        }
        public static void RemoveTechFromTeam(EnemyTechUnit tech)
        {
            try
            {
                EnemyPresence EP = GetTeam(tech.tech.m_TeamID);
                if (tech is EnemyBaseUnloaded EBU)
                {
                    EP.EBUs.Remove(EBU);
                }
                else
                {
                    EP.ETUs.Remove(tech);
                }
            }
            catch { }
        }
        public static bool RemoveTechFromTile(EnemyTechUnit tech)
        {
            var tile = GetTile(tech);
            if (tile != null)
            {
                ManVisible.inst.StopTrackingVisible(tech.tech.m_ID);
                tile.RemoveSavedVisible(ObjectTypes.Vehicle, tech.tech.m_ID);
                return true;
            }
            return false;
        }

        public static EnemyPresence PrepTeam(int Team)
        {
            if (!EnemyTeams.TryGetValue(Team, out EnemyPresence EP))
            {
                Debug.Log("TACtical_AI: EnemyWorldManager - New team " + Team + " added");
                EP = new EnemyPresence(Team);
                EnemyTeams.Add(Team, EP);
            }
            return EP;
        }
        public static EnemyPresence GetTeam(int Team)
        {
            if (!EnemyTeams.TryGetValue(Team, out EnemyPresence EP))
            {
                EP = new EnemyPresence(Team);
                EnemyTeams.Add(Team, EP);
            }
            return EP;
        }
        public static void AddToTeam(EnemyTechUnit tech)
        {
            if (!EnemyTeams.TryGetValue(tech.tech.m_TeamID, out EnemyPresence EP))
            {
                EP = new EnemyPresence(tech.tech.m_TeamID);
                EnemyTeams.Add(tech.tech.m_TeamID, EP);
            }
            if (tech is EnemyBaseUnloaded EBU)
            {
                if (!EP.EBUs.Contains(EBU))
                {
                    //Debug.Log("TACtical_AI: HandleTechUnloaded(EBU) New tech " + tech.tech.m_TechData.Name + " of type " + EBU.Faction + ", health " + EBU.MaxHealth + ", weapons " + EBU.AttackPower + ", funds " + EBU.Funds);
                    EP.EBUs.Add(EBU);
                }
                else
                {
                    Debug.Log("TACtical_AI: HandleTechUnloaded(EBU) DUPLICATE TECH ADD REQUEST!");
                }
            }
            else
            {
                if (!EP.EBUs.Contains(tech))
                {
                    //Debug.Log("TACtical_AI: HandleTechUnloaded(ETU) New tech " + tech.tech.m_TechData.Name + " of type " + tech.Faction + ", health " + tech.MaxHealth + ", weapons " + tech.AttackPower);
                    EP.ETUs.Add(tech);
                }
                else
                {
                    Debug.Log("TACtical_AI: HandleTechUnloaded(ETU) DUPLICATE TECH ADD REQUEST!");
                }
            }
        }


        // MOVEMENT
        public static bool CanSeePositionTile(EnemyBaseUnloaded EBU, Vector3 pos)
        {
            Vector2 vec = EBU.tilePos - WorldPosition.FromGameWorldPosition(pos).TileCoord;
            return vec.sqrMagnitude < BaseSightRadius * BaseSightRadius;
        }
        /// <summary>
        /// Moves the provided ETU in roughly 1 tile per movement token
        /// </summary>
        /// <param name="ETU"></param>
        /// <param name="target"></param>
        /// <returns>True if it can perform</returns>
        public static bool StrategicMoveQueue(EnemyTechUnit ETU, IntVector2 target)
        {
            if (ETU.tilePos == target)
                return true;
            ManSaveGame.StoredTech ST = ETU.tech;
            bool worked = false;
            ETU.isMoving = false;
            //Debug.Log("TACtical_AI: Enemy Tech " + ST.m_TechData.Name + " wants to move to " + target);
            ManSaveGame.StoredTile Tile1 = Singleton.Manager<ManSaveGame>.inst.GetStoredTile(ETU.tilePos);
            if (Tile1 != null)
            {
                if (Tile1.m_StoredVisibles.TryGetValue(1, out List<ManSaveGame.StoredVisible> techs))
                {
                    if (techs.Contains(ST))
                    {
                        worked = true;
                    }
                }
            }
            if (!worked)
            {
                return false;
            }
            float moveRate = (ETU.MoveSpeed / Globals.inst.MilesPerGameUnit) / (ManWorld.inst.TileSize * TerrainTraverseMulti);
            Vector2 moveDist = target - ETU.tilePos;
            Vector2 moveTileDist = moveDist.Clamp(-Vector2.one, Vector2.one);
            float dist = moveTileDist.magnitude;
            float ETA = dist / moveRate; // how long will it take?

            IntVector2 newWorldPos = ETU.tilePos + new IntVector2(moveTileDist);

            ManSaveGame.StoredTile Tile2 = Singleton.Manager<ManSaveGame>.inst.GetStoredTile(newWorldPos);
            if (Tile2 != null)
            {
                TileMoveCommand TMC = new TileMoveCommand();
                TMC.ETU = ETU;
                TMC.TargetTileCoord = newWorldPos;
                ETU.isMoving = true;
                QueuedUnitMoves.Add(new KeyValuePair<float, TileMoveCommand>(ETA, TMC));
                Debug.Log("TACtical_AI: StrategicMoveQueue - Enemy Tech " + ETU.tech.m_TechData.Name + " Requested move to " + newWorldPos);
                Debug.Log("   ETA is " + ETA);
                return true;
            }
            return false;
        }
        public static bool StrategicMoveConcluded(TileMoveCommand TMC)
        {
            //Debug.Log("TACtical_AI: StrategicMoveConcluded - EXECUTING");
            ManSaveGame.StoredTech ST = TMC.ETU.tech;
            bool worked = false;
            TMC.ETU.isMoving = false;
            ManSaveGame.StoredTile Tile1 = Singleton.Manager<ManSaveGame>.inst.GetStoredTile(TMC.ETU.tilePos);
            if (Tile1 != null)
            {
                if (Tile1.m_StoredVisibles.TryGetValue(1, out List<ManSaveGame.StoredVisible> techs))
                {
                    if (techs.Contains(ST))
                    {
                        worked = true;
                    }
                }
            }
            if (!worked)
            {
                IntVector2 IV2 = TMC.ETU.tilePos;
                TMC.ETU.tilePos = TryRefindTech(TMC.ETU.tilePos, TMC.ETU);
                if (IV2 != TMC.ETU.tilePos)
                    Debug.Log("TACtical_AI: StrategicMoveConcluded - Enemy Tech " + TMC.ETU.tech.m_TechData.Name + " Position was borked!  Refound positions!");
                else
                    Debug.Log("TACtical_AI: StrategicMoveConcluded - Enemy Tech " + TMC.ETU.tech.m_TechData.Name + " was destroyed before finishing move!");
                return false;
            }
            ManSaveGame.StoredTile Tile2 = Singleton.Manager<ManSaveGame>.inst.GetStoredTile(TMC.TargetTileCoord);
            if (Tile2 != null)
            {
                //ST.m_WorldPosition = new WorldPosition(Tile2.coord, Vector3.one);
                if (MoveTechIntoTile(TMC.ETU, Tile2))
                {
                    RemoveTechFromTile(TMC.ETU);
                    RemoveTechFromTeam(TMC.ETU);
                    //lastPos.Remove(ST);
                    Debug.Log("TACtical_AI: StrategicMoveConcluded - Enemy Tech " + TMC.ETU.tech.m_TechData.Name + " Moved to " + Tile2.coord);
                    return true;
                }
            }
            Debug.Log("TACtical_AI: StrategicMoveConcluded - Enemy Tech " + TMC.ETU.tech.m_TechData.Name + " - Battlefield overloaded! staying back!");
            //Debug.Log("TACtical_AI: StrategicMoveConcluded - Enemy Tech " + TMC.ETU.tech.m_TechData.Name + " - CRITICAL MISSION FAILIURE");
            return false;
        }


        // TECH BUILDING
        public static void ConstructNewTech(EnemyBaseUnloaded BuilderTech, EnemyPresence EP, SpawnBaseTypes SBT)
        {
            ManSaveGame.StoredTile ST = Singleton.Manager<ManSaveGame>.inst.GetStoredTile(BuilderTech.tilePos, true);
            if (ST != null)
            {
                if (!FindFreeSpaceOnTile(Vector2.up, ST, out Vector2 newPosOff))
                    return;
                if (!KickStart.EnemiesHaveCreativeInventory)
                {
                    var funder = EnemyBaseWorld.GetTeamFunder(EP);
                    funder.Funds -= RawTechLoader.GetBaseBBCost(RawTechLoader.GetBlueprint(SBT));
                }

                int ID = Singleton.Manager<ManSaveGame>.inst.CurrentState.GetNextVisibleID(ObjectTypes.Vehicle);
                Quaternion quat = BuilderTech.tech.m_Rotation;
                Vector3 pos = ManWorld.inst.TileManager.CalcTileCentre(ST.coord) + newPosOff.ToVector3XZ();
                TechData TD = RawTechLoader.SpawnUnloadedTech(SBT, out int[] bIDs);
                if (TD != null)
                {
                    ST.AddSavedTech(TD, bIDs, EP.Team, pos, Quaternion.LookRotation(quat * Vector3.right, Vector3.up), true, false, true, ID, false, 99, false);

                    TrackedVisible TVa = new TrackedVisible(ID, null, ObjectTypes.Vehicle, RadarTypes.Vehicle);
                    TVa.SetPos(pos);
                    Singleton.Manager<ManVisible>.inst.TrackVisible(TVa);
                    if (ST.m_StoredVisibles.TryGetValue(1, out List<ManSaveGame.StoredVisible> SV))
                    {
                        HandleTechUnloaded((ManSaveGame.StoredTech)SV.Last(), ST.coord);
                    }
                }
            }
        }
        public static void ConstructNewExpansion(Vector3 position, EnemyBaseUnloaded BuilderTech, EnemyPresence EP, SpawnBaseTypes SBT)
        {
            ManSaveGame.StoredTile ST = Singleton.Manager<ManSaveGame>.inst.GetStoredTile(BuilderTech.tilePos, true);
            if (ST != null)
            {
                if (!KickStart.EnemiesHaveCreativeInventory)
                {
                    var funder = EnemyBaseWorld.GetTeamFunder(EP);
                    funder.Funds -= RawTechLoader.GetBaseBBCost(RawTechLoader.GetBlueprint(SBT));
                }

                int ID = Singleton.Manager<ManSaveGame>.inst.CurrentState.GetNextVisibleID(ObjectTypes.Vehicle);
                Quaternion quat = BuilderTech.tech.m_Rotation;
                TechData TD = RawTechLoader.GetBaseExpansionUnloaded(position, EP, SBT, out int[] bIDs);
                if (TD != null)
                {
                    ST.AddSavedTech(TD, bIDs, EP.Team, position, Quaternion.LookRotation(quat * Vector3.right, Vector3.up), true, false, true, ID, false, 99, false);

                    TrackedVisible TVa = new TrackedVisible(ID, null, ObjectTypes.Vehicle, RadarTypes.Base);
                    TVa.SetPos(position);
                    Singleton.Manager<ManVisible>.inst.TrackVisible(TVa);
                    if (ST.m_StoredVisibles.TryGetValue(1, out List<ManSaveGame.StoredVisible> SV))
                    {
                        HandleTechUnloaded((ManSaveGame.StoredTech)SV.Last(), ST.coord);
                    }
                }
            }
        }


        // UPDATE
        public void Update()
        {
            if (!ManPauseGame.inst.IsPaused && (ManNetwork.IsHost || !ManNetwork.IsNetworked))
            {
                UpdateMoveTimer++;
                if (UpdateMoveTimer >= UpdateMoveDelay)
                {
                    UpdateMoveTimer = 0;
                    //Debug.Log("TACtical_AI: EnemyWorldManager - Updating unit move commands");
                    int count = QueuedUnitMoves.Count;
                    for (int step = 0; step < count;)
                    {
                        try
                        {
                            KeyValuePair<float, TileMoveCommand> move = QueuedUnitMoves.ElementAt(step);
                            if (move.Key <= 1)
                            {
                                StrategicMoveConcluded(move.Value);
                                QueuedUnitMoves.RemoveAt(step);
                                count--;
                            }
                            else
                            {
                                QueuedUnitMoves.Add(new KeyValuePair<float, TileMoveCommand>(move.Key - 1, move.Value));
                                QueuedUnitMoves.RemoveAt(step);
                            }
                            count--;
                        }
                        catch
                        {
                            Debug.Log("TACtical_AI: EnemyWorldManager(Update) - ERROR");
                            QueuedUnitMoves.RemoveAt(step);
                            count--;
                        }
                    }
                }
                UpdateTimer++;
                if (UpdateTimer >= UpdateDelay)
                {
                    UpdateTimer = 0;
                    //Debug.Log("TACtical_AI: EnemyWorldManager - Updating All EnemyPresence");
                    
                    List<EnemyPresence> EPScrambled = EnemyTeams.Values.ToList();
                    EPScrambled.Shuffle();
                    int Count = EPScrambled.Count;
                    for (int step = 0; step < Count;)
                    {
                        EnemyPresence EP = EPScrambled.ElementAt(step);
                        if (EP.UpdateGrandCommand())
                        {
                            step++;
                            continue;
                        }
                        EPScrambled.RemoveAt(step);
                        Count--;
                    }
                }
            }
        }

        // ETC
        private static EnemyTechUnit GetETUFromTank(Tank sTech)
        {
            EnemyTechUnit ETUo = null;
            if (EnemyTeams.TryGetValue(sTech.Team, out EnemyPresence EP))
            {
                ETUo = EP.EBUs.Find(delegate (EnemyBaseUnloaded cand) { return cand.tech.m_ID == sTech.visible.ID; });
                if (ETUo != null)
                    return ETUo;
                ETUo = EP.ETUs.Find(delegate (EnemyTechUnit cand) { return cand.tech.m_ID == sTech.visible.ID; });
            }
            return ETUo;
        }
        private static EnemyTechUnit GetETUFromInst(ManSaveGame.StoredTech sTech)
        {
            EnemyTechUnit ETUo = null;
            if (EnemyTeams.TryGetValue(sTech.m_TeamID, out EnemyPresence EP))
            {
                ETUo = EP.EBUs.Find(delegate (EnemyBaseUnloaded cand) { return cand.tech == sTech; });
                if (ETUo != null)
                    return ETUo;
                ETUo = EP.ETUs.Find(delegate (EnemyTechUnit cand) { return cand.tech == sTech; });
            }
            return ETUo;
        }
        internal static List<EnemyTechUnit> GetTechsInTile(IntVector2 tilePos, Vector3 InTilePos, float radius)
        {
            //List<EnemyPresence> EPs = EnemyTeams.Values.ToList();
            List<EnemyTechUnit> ETUsInRange = new List<EnemyTechUnit>();
            float radS = radius * radius; 
            ManSaveGame.StoredTile Tile = Singleton.Manager<ManSaveGame>.inst.GetStoredTile(tilePos);
            Vector3 tilePosWorld = ManWorld.inst.TileManager.CalcTileCentre(tilePos);
            if (Tile != null)
            { 
                //Singleton.Manager<ManVisible>.inst.ID
                if (Tile.m_StoredVisibles.TryGetValue(1, out List<ManSaveGame.StoredVisible> viss))
                {
                    foreach (ManSaveGame.StoredVisible STV in viss)
                    {
                        var tech = (ManSaveGame.StoredTech)STV;
                        if ((tech.GetBackwardsCompatiblePosition() - (InTilePos + tilePosWorld)).sqrMagnitude <= radS)
                            ETUsInRange.Add(GetETUFromInst(tech));
                    }
                }
            }
            return ETUsInRange;
        }
        internal static List<EnemyTechUnit> GetTechsInTile(IntVector2 tilePos, Vector2 InTilePos, float radius)
        {
            //List<EnemyPresence> EPs = EnemyTeams.Values.ToList();
            List<EnemyTechUnit> ETUsInRange = new List<EnemyTechUnit>();
            float radS = radius * radius;
            ManSaveGame.StoredTile Tile = Singleton.Manager<ManSaveGame>.inst.GetStoredTile(tilePos);
            Vector2 tilePosWorld = ManWorld.inst.TileManager.CalcTileCentre(tilePos).ToVector2XZ();
            if (Tile != null)
            {
                //Singleton.Manager<ManVisible>.inst.ID
                if (Tile.m_StoredVisibles.TryGetValue(1, out List<ManSaveGame.StoredVisible> viss))
                {
                    foreach (ManSaveGame.StoredVisible STV in viss)
                    {
                        var tech = (ManSaveGame.StoredTech)STV;
                        if ((tech.GetBackwardsCompatiblePosition().ToVector2XZ() - (InTilePos + tilePosWorld)).sqrMagnitude <= radS)
                            ETUsInRange.Add(GetETUFromInst(tech));
                    }
                }
            }
            return ETUsInRange;
        }
        internal static List<EnemyTechUnit> GetTechsInTile(IntVector2 tilePos)
        {
            //List<EnemyPresence> EPs = EnemyTeams.Values.ToList();
            List<EnemyTechUnit> ETUsInRange = new List<EnemyTechUnit>();
            //Singleton.Manager<ManSaveGame>.inst.CurrentState.m_StoredTiles.TryGetValue(tilePos, out ManSaveGame.StoredTile Tile)

            ManSaveGame.StoredTile Tile = Singleton.Manager<ManSaveGame>.inst.GetStoredTile(tilePos, false);
            if (Tile == null)
                return ETUsInRange;

            //Singleton.Manager<ManVisible>.inst.ID
            if (Tile.m_StoredVisibles.TryGetValue(1, out List<ManSaveGame.StoredVisible> viss))
            {
                foreach (ManSaveGame.StoredVisible STV in viss)
                {
                    var tech = (ManSaveGame.StoredTech)STV;
                    ETUsInRange.Add(GetETUFromInst(tech));
                }
            }
            return ETUsInRange;
        }

        public static int GetBiomeGains(Vector3 pos)
        {
            ChunkTypes[] res = RBases.TryGetBiomeResource(pos);
            int resCount = res.Count();
            int Gains = 0;
            for (int step = 0; resCount > step; step++)
            {
                Gains += ResourceManager.inst.GetResourceDef(EnemyBaseWorld.TransChunker(res[step])).saleValue;
            }
            Gains /= resCount;
            return Gains;
        }
        public static int GetBiomeSurfGains(Vector3 pos)
        {
            ChunkTypes[] res = EnemyBaseWorld.GetBiomeResourcesSurface(pos);
            int resCount = res.Count();
            int Gains = 0;
            for (int step = 0; resCount > step; step++)
            {
                Gains += ResourceManager.inst.GetResourceDef(EnemyBaseWorld.TransChunker(res[step])).saleValue;
            }
            Gains /= resCount;
            return Mathf.RoundToInt(Gains * SurfaceHarvestingMulti);
        }
    }

}