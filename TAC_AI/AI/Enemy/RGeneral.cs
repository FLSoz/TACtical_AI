﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace TAC_AI.AI.Enemy
{
    public static class RGeneral
    {
        static float RANDRange = 50;

        public static void LollyGag(AIECore.TankAIHelper thisInst, Tank tank, RCore.EnemyMind mind)
        {
            if (mind.Hurt && thisInst.lastDestination.Approximately(tank.boundsCentreWorldNoCheck, 10))
            {
                var energy = tank.EnergyRegulator.Energy(EnergyRegulator.EnergyType.Electric);
                if (mind.CommanderSmarts >= EnemySmarts.Meh)
                {
                    if (energy.storageTotal > 500)
                    {
                        if (mind.SolarsAvail && tank.Anchors.NumPossibleAnchors > 0 && !tank.IsAnchored)
                        {
                            if (thisInst.anchorAttempts < 6)
                            {
                                tank.TryToggleTechAnchor();
                                thisInst.anchorAttempts++;
                            }
                            else
                            {   //Try to find new spot
                                DefaultIdle(thisInst, tank, mind);
                            }
                        }
                        if (energy.storageTotal - 100 < energy.currentAmount)
                        {
                            mind.Hurt = false;
                        }
                    }
                    else
                    {
                        //Cannot repair block damage or recharge shields!
                        mind.Hurt = false;
                    }
                }
                if (mind.CommanderSmarts == EnemySmarts.Smrt)
                {
                    if (mind.PendingSystemsCheck && mind.AttemptedRepairs < 3)
                    {
                        mind.PendingSystemsCheck = !RRepair.RepairLerp(tank, mind);
                        mind.AttemptedRepairs++;
                        return;
                    }
                }
                if (mind.CommanderSmarts >= EnemySmarts.IntAIligent)
                {
                    if (mind.PendingSystemsCheck && mind.AttemptedRepairs < 4)
                    {
                        if (energy.currentAmount / energy.storageTotal > 0.5)
                        {
                            //flex yee building speeds on them players
                            mind.PendingSystemsCheck = !RRepair.InstaRepair(tank, mind);
                            mind.AttemptedRepairs++;
                        }
                        else
                        {
                            mind.PendingSystemsCheck = !RRepair.RepairLerp(tank, mind);
                            mind.AttemptedRepairs++;
                        }
                        return;
                    }
                }
            }
            else
            {
                thisInst.anchorAttempts = 0;
            }

            switch (mind.CommanderMind)
            {
                case EnemyAttitude.Default: // do dumb stuff
                    DefaultIdle(thisInst, tank, mind);
                    break;
                case EnemyAttitude.Homing:  // Get nearest tech regardless of max combat range and attack them
                    HomingIdle(thisInst, tank, mind);
                    break;
                //The cases below I still have to think of a reason for them to do the things
                case EnemyAttitude.Miner:   // mine resources
                    DefaultIdle(thisInst, tank, mind);
                    break;
                case EnemyAttitude.Junker:  // Huddle up by blocks on the ground
                    DefaultIdle(thisInst, tank, mind);
                    break;
            }
            if (mind.EvilCommander == EnemyHandling.Naval)
                thisInst.lastDestination = AIEPathing.OffsetToSea(thisInst.lastDestination, thisInst);
            else if (mind.EvilCommander == EnemyHandling.Starship)
                thisInst.lastDestination = AIEPathing.OffsetFromGround(thisInst.lastDestination, thisInst);
            else //Snap to ground
                thisInst.lastDestination = AIEPathing.OffsetFromGround(thisInst.lastDestination, thisInst, tank.blockBounds.size.y);
        }

        public static void Engadge(AIECore.TankAIHelper thisInst, Tank tank, RCore.EnemyMind mind)
        {
            if (!mind.StartedAnchored && tank.IsAnchored)
            {
                tank.TryToggleTechAnchor();
                thisInst.anchorAttempts = 0;
            }
        }

        // Handle being bored AIs
        public static void DefaultIdle(AIECore.TankAIHelper thisInst, Tank tank, RCore.EnemyMind mind)
        {
            if (thisInst.ActionPause == 1)
            {
                thisInst.lastDestination = GetRANDPos(tank);
                thisInst.ActionPause = 0;
            }
            else if (thisInst.ActionPause == 0)
                thisInst.ActionPause = 30;
            else
                thisInst.ActionPause--;
        }
        public static void HomingIdle(AIECore.TankAIHelper thisInst, Tank tank, RCore.EnemyMind mind)
        {
            //Try find next target to assault
            try
            {
                thisInst.lastEnemy = mind.FindEnemy(inRange: 500).visible;
            }
            catch { }//No tanks available
        }

        public static Vector3 GetRANDPos(Tank tank)
        {
            Vector3 final = tank.boundsCentreWorldNoCheck;

            final.x += UnityEngine.Random.Range(-RANDRange, RANDRange);
            final.y += UnityEngine.Random.Range(-RANDRange, RANDRange);
            final.z += UnityEngine.Random.Range(-RANDRange, RANDRange);

            return final;
        }
    }
}
