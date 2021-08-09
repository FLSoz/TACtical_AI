﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TAC_AI.AI.AlliedOperations
{
    public static class BAssassin
    {
        public static void MotivateKill(AIECore.TankAIHelper thisInst, Tank tank)
        {
            //The Handler that tells the Tank (Assassin) what to do movement-wise
            float dist = (tank.boundsCentreWorldNoCheck - thisInst.lastDestination).magnitude;
            bool hasMessaged = false;
            thisInst.lastRange = dist;

            BGeneral.ResetValues(thisInst);


            EnergyRegulator.EnergyState state = tank.EnergyRegulator.Energy(EnergyRegulator.EnergyType.Electric);
            if (thisInst.areWeFull)
            {
                thisInst.areWeFull = false;
                if (state.currentAmount / state.storageTotal > 0.95f)
                    thisInst.areWeFull = true;

                thisInst.ActionPause = 20;
            }
            else
            {
                thisInst.areWeFull = true;
                if (state.currentAmount / state.storageTotal < 0.4f)
                    thisInst.areWeFull = false;
            }

            if (thisInst.areWeFull || thisInst.ActionPause > 10)
            {
                thisInst.foundBase = AIECore.FetchChargedChargers(tank, tank.Radar.Range + 150, out thisInst.lastBasePos, out thisInst.theBase, tank.Team);
                if (!thisInst.foundBase)
                {
                    hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Searching for nearest charger!");
                    thisInst.EstTopSped = 1;//slow down the clock to reduce lagg
                    if (thisInst.theBase == null)
                        return; // There's no base!
                    thisInst.lastBaseExtremes = AIECore.Extremes(thisInst.theBase.blockBounds.extents);
                }
                thisInst.forceDrive = true;
                thisInst.DriveVar = 1;

                if (dist < thisInst.lastBaseExtremes + thisInst.lastTechExtents + 3)
                {
                    thisInst.theBase.GetComponent<AIECore.TankAIHelper>().AllowApproach();
                    if (thisInst.recentSpeed == 1)
                    {
                        hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Trying to unjam...");
                        thisInst.AvoidStuff = false;
                        thisInst.DriveVar = -1;
                        //thisInst.TryHandleObstruction(hasMessaged, dist, false, false);
                    }
                    else
                    {
                        hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Arrived at nearest charger and recharging!");
                        thisInst.AvoidStuff = false;
                        thisInst.ActionPause -= KickStart.AIClockPeriod / 5;
                        thisInst.Yield = true;
                        thisInst.SettleDown();
                    }
                }
                else if (dist < thisInst.lastBaseExtremes + thisInst.lastTechExtents + 8)
                {
                    thisInst.theBase.GetComponent<AIECore.TankAIHelper>().AllowApproach();
                    if (thisInst.recentSpeed < 3)
                    {
                        hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Trying to unjam...");
                        thisInst.AvoidStuff = false;
                        thisInst.TryHandleObstruction(hasMessaged, dist, false, false);
                    }
                    else if (thisInst.recentSpeed < 8)
                    {
                        hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Rattling off resources...");
                        thisInst.AvoidStuff = false;
                        thisInst.Yield = true;
                    }
                    else
                    {
                        hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Yielding base approach...");
                        thisInst.AvoidStuff = false;
                        thisInst.ActionPause -= KickStart.AIClockPeriod / 5;
                        thisInst.Yield = true;
                        thisInst.SettleDown();
                    }
                }
                else if (dist < thisInst.lastBaseExtremes + thisInst.lastTechExtents + 12)
                {
                    thisInst.theBase.GetComponent<AIECore.TankAIHelper>().AllowApproach();
                    if (thisInst.recentSpeed < 3)
                    {
                        hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  unjamming from base...");
                        thisInst.TryHandleObstruction(hasMessaged, dist, false, true);
                    }
                    else
                    {
                        hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Arrived at base!");
                        thisInst.ActionPause -= KickStart.AIClockPeriod / 5;
                        //thisInst.Yield = true;
                        thisInst.SettleDown();
                    }
                }
                else if (thisInst.recentSpeed < 3)
                {
                    hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Removing obstruction on way to base...");
                    thisInst.TryHandleObstruction(hasMessaged, dist, false, true);
                }
                AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Heading back to base!");
                thisInst.ProceedToBase = true;
                thisInst.foundGoal = false;
            }
            else if (thisInst.ActionPause > 0)
            {
                AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Reversing from base...");
                thisInst.forceDrive = true;
                thisInst.DriveVar = -1;
            }
            else
            {
                if (!thisInst.foundGoal)
                {
                    thisInst.EstTopSped = 1;//slow down the clock to reduce lagg
                    thisInst.foundGoal = AIECore.FindTarget(tank, thisInst, thisInst.theResource, out thisInst.theResource);
                    AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Scanning for enemies...");
                    if (!thisInst.foundGoal)
                    {
                        thisInst.foundBase = AIECore.FetchChargedChargers(tank, tank.Radar.Range + 150, out thisInst.lastBasePos, out thisInst.theBase, tank.Team);
                        if (thisInst.theBase == null)
                            return; // There's no base!
                        thisInst.lastBaseExtremes = AIECore.Extremes(thisInst.theBase.blockBounds.extents);
                    }
                    thisInst.ProceedToBase = true;
                    return; // There's no resources left!
                }
                thisInst.forceDrive = true;
                thisInst.DriveVar = 1;

                if (dist < thisInst.lastTechExtents + 3 && thisInst.recentSpeed < 3)
                {
                    hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Engadging the enemy at " + thisInst.theResource.centrePosition);
                    thisInst.Yield = true;
                    if (!thisInst.FullMelee)
                        thisInst.PivotOnly = true;
                    thisInst.SettleDown();
                }
                else if (thisInst.recentSpeed < 3)
                {
                    hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Removing obstruction at " + tank.transform.position);
                    thisInst.TryHandleObstruction(hasMessaged, dist, false, true);
                }
                else if (dist < thisInst.lastTechExtents + 12)
                {
                    hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  In combat at " + thisInst.theResource.centrePosition);
                    thisInst.SettleDown();
                }
                AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Moving out to fight at " + thisInst.theResource.centrePosition + " |Tech is at " + tank.boundsCentreWorldNoCheck);
                thisInst.ProceedToMine = true;
                thisInst.foundBase = false;
            }
        }
    }
}
