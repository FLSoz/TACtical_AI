﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using UnityEngine;
using TAC_AI.AI;
using TAC_AI.AI.Enemy;

namespace TAC_AI.AI.Movement.AICores
{
    public class HelicopterAICore : IMovementAICore
    {
        private AIControllerAir pilot;
        private Tank tank;

        public void Initiate(Tank tank, IMovementAIController pilot)
        {
            this.tank = tank;
            this.pilot = (AIControllerAir) pilot;
            this.pilot.FlyStyle = AIControllerAir.FlightType.Helicopter;
            this.pilot.FlyingChillFactor = Vector3.one * 30;
        }
        public bool DriveMaintainer(TankControl thisControl, AIECore.TankAIHelper thisInst, Tank tank)
        {
            if (pilot.Grounded)
            {   //Become a ground vehicle for now
                Debug.Log("TACtical_AI: " + tank.name + " is GROUNDED!!!");
                if (!AIEPathing.AboveHeightFromGround(tank.boundsCentreWorldNoCheck, thisInst.lastTechExtents * 2))
                {
                    return false;
                }
                //WIP - Try fighting the controls to land safely

                return true;
            }

            if (tank.beam.IsActive)
            {   // BEAMING
                pilot.MainThrottle = 0;
                pilot.AdvisedThrottle = 0;
                pilot.CurrentThrottle = 0;
                HelicopterUtils.UpdateThrottleCopter(pilot, thisInst, thisControl);
                HelicopterUtils.AngleTowardsUp(thisControl, thisInst, tank, pilot, pilot.AirborneDest, true);
            }
            else if (tank.grounded || pilot.ForcePitchUp)
            {   // Try and takeoff
                //Debug.Log("TACtical_AI: " + tank.name + " is taking off");
                pilot.MainThrottle = HelicopterUtils.ModerateUpwardsThrust(tank, thisInst, pilot, AIEPathing.OffsetFromGroundA(tank.boundsCentreWorldNoCheck, thisInst, 45), true);
                HelicopterUtils.UpdateThrottleCopter(pilot, thisInst, thisControl);
                HelicopterUtils.AngleTowardsUp(thisControl, thisInst, tank, pilot, pilot.AirborneDest, true);
            }
            else
            {   // Normal flight
                pilot.MainThrottle = HelicopterUtils.ModerateUpwardsThrust(tank, thisInst, pilot, pilot.AirborneDest);
                HelicopterUtils.UpdateThrottleCopter(pilot, thisInst, thisControl);
                HelicopterUtils.AngleTowardsUp(thisControl, thisInst, tank, pilot, pilot.AirborneDest);
                /*
                if (thisInst.lastEnemy.IsNotNull())
                {
                    Debug.Log("TACtical_AI: " + tank.name + " is in combat at " + pilot.AirborneDest + " tank at " + thisInst.lastEnemy.tank.boundsCentreWorldNoCheck);
                }
                */
            }

            return true;
        }

        public bool DriveDirector()
        {
            bool Combat;
            if (pilot.Helper.IsMultiTech)
            {   //Override and disable most driving abilities
                MultiTechUtils.HandleMultiTech(pilot.Helper, tank);
                return true;
            }
            else if (pilot.Helper.ProceedToBase)
            {
                if (pilot.Helper.lastBasePos.IsNotNull())
                {
                    pilot.Helper.Steer = true;
                    pilot.Helper.DriveDir = EDriveType.Forwards;
                    pilot.Helper.lastDestination = pilot.Helper.AvoidAssistPrecise(pilot.Helper.lastBasePos.position);
                    pilot.Helper.MinimumRad = Mathf.Max(pilot.Helper.lastTechExtents - 2, 0.5f);
                }
                pilot.AirborneDest = this.pilot.Helper.lastDestination;
            }
            else if (pilot.Helper.ProceedToMine)
            {
                if (pilot.Helper.theResource.tank != null)
                {
                    if (pilot.Helper.PivotOnly)
                    {
                        pilot.Helper.Steer = true;
                        pilot.Helper.DriveDir = EDriveType.Forwards;
                        pilot.Helper.lastDestination = pilot.Helper.theResource.tank.boundsCentreWorldNoCheck;
                        pilot.Helper.MinimumRad = 0;
                    }
                    else
                    {
                        if (pilot.Helper.FullMelee)
                        {
                            pilot.Helper.Steer = true;
                            pilot.Helper.DriveDir = EDriveType.Forwards;
                            pilot.Helper.lastDestination = pilot.Helper.AvoidAssistPrecise(pilot.Helper.theResource.tank.boundsCentreWorldNoCheck);
                            pilot.Helper.MinimumRad = 0;
                        }
                        else
                        {
                            pilot.Helper.Steer = true;
                            pilot.Helper.DriveDir = EDriveType.Forwards;
                            pilot.Helper.lastDestination = pilot.Helper.AvoidAssistPrecise(pilot.Helper.theResource.tank.boundsCentreWorldNoCheck);
                            pilot.Helper.MinimumRad = pilot.Helper.lastTechExtents + 2;
                        }
                    }
                }
                else
                {
                    if (pilot.Helper.PivotOnly)
                    {
                        pilot.Helper.Steer = true;
                        pilot.Helper.DriveDir = EDriveType.Forwards;
                        pilot.Helper.lastDestination = pilot.Helper.theResource.trans.position;
                        pilot.Helper.MinimumRad = 0;
                    }
                    else
                    {
                        if (pilot.Helper.FullMelee)
                        {
                            pilot.Helper.Steer = true;
                            pilot.Helper.DriveDir = EDriveType.Forwards;
                            pilot.Helper.lastDestination = pilot.Helper.AvoidAssistPrecise(pilot.Helper.theResource.trans.position);
                            pilot.Helper.MinimumRad = 0;
                        }
                        else
                        {
                            pilot.Helper.Steer = true;
                            pilot.Helper.DriveDir = EDriveType.Forwards;
                            pilot.Helper.lastDestination = pilot.Helper.AvoidAssistPrecise(pilot.Helper.theResource.centrePosition);
                            pilot.Helper.MinimumRad = pilot.Helper.lastTechExtents + 2;
                        }
                    }
                }
                pilot.AirborneDest = this.pilot.Helper.lastDestination;
            }
            else if (pilot.Helper.DediAI == AIType.Aegis)
            {
                pilot.Helper.theResource = AIEPathing.ClosestUnanchoredAlly(tank.boundsCentreWorldNoCheck, out float bestval, tank).visible;
                Combat = this.TryAdjustForCombat();
                if (!Combat)
                {
                    if (pilot.Helper.MoveFromObjective && pilot.Helper.theResource.IsNotNull())
                    {
                        pilot.Helper.Steer = true;
                        pilot.Helper.DriveDir = EDriveType.Forwards;
                        pilot.Helper.lastDestination = pilot.Helper.theResource.transform.position;
                    }
                    else if (pilot.Helper.ProceedToObjective && pilot.Helper.theResource.IsNotNull())
                    {
                        pilot.Helper.Steer = true;
                        pilot.Helper.DriveDir = EDriveType.Forwards;
                        pilot.Helper.lastDestination = pilot.Helper.AvoidAssist(pilot.Helper.theResource.tank.transform.position);
                    }
                    else
                    {
                        //Debug.Log("TACtical_AI: AI IDLE");
                    }
                }
                pilot.AirborneDest = this.pilot.Helper.lastDestination;
            }
            else
            {
                Combat = this.TryAdjustForCombat();
                if (Combat)
                {
                    pilot.AirborneDest = this.pilot.Helper.lastDestination;
                }
                else
                {
                    if (this.pilot.Helper.ProceedToObjective)
                    {   // Fly to target
                        pilot.AirborneDest = this.pilot.Helper.lastDestination;
                    }
                    else if (this.pilot.Helper.MoveFromObjective)
                    {   // Fly away from target
                        //this.pilot.Helper.lastDestination = AIEPathing.OffsetFromGroundA(this.pilot.Helper.lastDestination, this.pilot.Helper, 44);
                        pilot.AirborneDest = ((this.pilot.Tank.trans.position - this.pilot.Helper.lastDestination).normalized * (pilot.DestSuccessRad * 2)) + this.pilot.Tank.boundsCentreWorldNoCheck;
                    }
                    else
                    {
                        this.pilot.Helper.lastPlayer = this.pilot.Helper.GetPlayerTech();
                        if (this.pilot.Helper.lastPlayer.IsNotNull())
                        {
                            pilot.AirborneDest.y = this.pilot.Helper.lastPlayer.tank.boundsCentreWorldNoCheck.y + (this.pilot.Helper.GroundOffsetHeight / 5);
                        }
                        else
                        {   //stay
                            pilot.AirborneDest = this.pilot.Helper.lastDestination;
                        }
                    }
                }
            }

            pilot.AirborneDest = AIEPathing.OffsetFromGroundA(pilot.AirborneDest, this.pilot.Helper, 32);
            pilot.AirborneDest = this.AvoidAssist(pilot.AirborneDest, this.pilot.Tank.boundsCentreWorldNoCheck + (this.pilot.Tank.rbody.velocity * pilot.AerofoilSluggishness));
            pilot.AirborneDest = AIEPathing.ModerateMaxAlt(pilot.AirborneDest, pilot.Helper);

            if (!AIEPathing.AboveHeightFromGround(this.pilot.Tank.boundsCentreWorldNoCheck + (this.pilot.Tank.rbody.velocity * Time.deltaTime), 26))
            {
                //Debug.Log("TACtical_AI: Tech " + this.pilot.tank.name + "  Avoiding Ground!");
                pilot.ForcePitchUp = true;
            }
            return true;
        }

        public bool DriveDirectorRTS()
        {
            bool combat = false;
            if (pilot.Helper.RTSDestination == Vector3.zero)
                combat = TryAdjustForCombat(); // When set to chase then chase
            if (combat)
            {
                pilot.AirborneDest = this.pilot.Helper.lastDestination;
            }
            else
                pilot.AirborneDest = this.pilot.Helper.RTSDestination;

            pilot.AirborneDest = AIEPathing.OffsetFromGroundA(pilot.AirborneDest, this.pilot.Helper, 32);
            pilot.AirborneDest = this.AvoidAssist(pilot.AirborneDest, this.pilot.Tank.boundsCentreWorldNoCheck + (this.pilot.Tank.rbody.velocity * pilot.AerofoilSluggishness));
            pilot.AirborneDest = AIEPathing.ModerateMaxAlt(pilot.AirborneDest, pilot.Helper);

            if (!AIEPathing.AboveHeightFromGround(this.pilot.Tank.boundsCentreWorldNoCheck + (this.pilot.Tank.rbody.velocity * Time.deltaTime), 26))
            {
                //Debug.Log("TACtical_AI: Tech " + this.pilot.tank.name + "  Avoiding Ground!");
                pilot.ForcePitchUp = true;
            }
            return true;
        }

        public bool DriveDirectorEnemy(EnemyMind mind)
        {
            pilot.ForcePitchUp = false;
            if (pilot.Grounded)
            {   //Become a ground vehicle for now
                if (!AIEPathing.AboveHeightFromGround(this.pilot.Tank.boundsCentreWorldNoCheck, pilot.Helper.lastTechExtents * 2))
                {
                    return false;
                }
                //Try fighting the controls to land safely

                return true;
            }
            bool combat = this.TryAdjustForCombatEnemy(mind);
            if (combat)
            {
                pilot.AirborneDest = this.pilot.Helper.lastDestination;
            }
            else
            {
                if (this.pilot.Helper.ProceedToObjective)
                {   // Fly to target
                    pilot.AirborneDest = this.pilot.Helper.lastDestination;
                }
                else if (this.pilot.Helper.MoveFromObjective)
                {   // Fly away from target
                    pilot.AirborneDest = this.pilot.Helper.lastDestination;
                    //pilot.AirborneDest = ((this.pilot.tank.trans.position - this.pilot.Helper.lastDestination).normalized * (pilot.DestSuccessRad * 2)) + this.pilot.tank.boundsCentreWorldNoCheck;
                }
                else
                {
                    this.pilot.Helper.lastPlayer = this.pilot.Helper.GetPlayerTech();
                    if (this.pilot.Helper.lastPlayer.IsNotNull())
                    {
                        pilot.AirborneDest.y = this.pilot.Helper.lastPlayer.tank.boundsCentreWorldNoCheck.y + (this.pilot.Helper.GroundOffsetHeight / 5);
                    }
                    else
                    {   //Fly off the screen
                        //Debug.Log("TACtical_AI: Tech " + this.pilot.Tank.name + "  Leaving scene!");
                        Vector3 fFlat = this.pilot.Tank.rootBlockTrans.forward;
                        fFlat.y = 0;
                        pilot.AirborneDest = (fFlat.normalized * 1000) + this.pilot.Tank.boundsCentreWorldNoCheck;
                    }
                }
            }

            pilot.AirborneDest = AIEPathing.OffsetFromGroundA(pilot.AirborneDest, this.pilot.Helper, 32);
            pilot.AirborneDest = AIEPathing.ModerateMaxAlt(pilot.AirborneDest, pilot.Helper);
            pilot.AirborneDest = RPathfinding.AvoidAssistEnemy(this.pilot.Tank, pilot.AirborneDest, this.pilot.Tank.boundsCentreWorldNoCheck + (this.pilot.Tank.rbody.velocity * pilot.AerofoilSluggishness), this.pilot.Helper, mind);

            if (!AIEPathing.AboveHeightFromGround(this.pilot.Tank.boundsCentreWorldNoCheck + (this.pilot.Tank.rbody.velocity * Time.deltaTime), 26))
            {
                //Debug.Log("TACtical_AI: Tech " + this.pilot.tank.name + "  Avoiding Ground!");
                pilot.ForcePitchUp = true;
            }
            return true;
        }

        public Vector3 AvoidAssist(Vector3 targetIn, Vector3 predictionOffset)
        {
            //The method to determine if we should avoid an ally nearby while navigating to the target
            AIECore.TankAIHelper thisInst = this.pilot.Helper;
            Tank tank = this.pilot.Tank;

            try
            {
                Tank lastCloseAlly;
                float lastAllyDist;
                if (thisInst.SecondAvoidence)// MORE processing power
                {
                    lastCloseAlly = AIEPathing.SecondClosestAllyPrecision(predictionOffset, out Tank lastCloseAlly2, out lastAllyDist, out float lastAuxVal, tank);
                    if (lastAllyDist < thisInst.lastTechExtents + lastCloseAlly.GetCheapBounds() + 12 + (predictionOffset - tank.boundsCentreWorldNoCheck).magnitude)
                    {
                        if (lastAuxVal < thisInst.lastTechExtents + lastCloseAlly2.GetCheapBounds() + 12 + (predictionOffset - tank.boundsCentreWorldNoCheck).magnitude)
                        {
                            IntVector3 ProccessedVal2 = thisInst.GetOtherDir(lastCloseAlly) + thisInst.GetOtherDir(lastCloseAlly2);
                            return (targetIn + ProccessedVal2) / 3;
                        }
                        IntVector3 ProccessedVal = thisInst.GetOtherDir(lastCloseAlly);
                        return (targetIn + ProccessedVal) / 2;
                    }

                }
                lastCloseAlly = AIEPathing.ClosestAllyPrecision(predictionOffset, out lastAllyDist, tank);
                if (lastCloseAlly == null)
                    Debug.Log("TACtical_AI: ALLY IS NULL");
                if (lastAllyDist < thisInst.lastTechExtents + lastCloseAlly.GetCheapBounds() + 12 + (predictionOffset - tank.boundsCentreWorldNoCheck).magnitude)
                {
                    IntVector3 ProccessedVal = thisInst.GetOtherDir(lastCloseAlly);
                    return (targetIn + ProccessedVal) / 2;
                }
            }
            catch (Exception e)
            {
                Debug.Log("TACtical_AI: Crash on AvoidAssistAir " + e);
                return targetIn;
            }
            if (targetIn.IsNaN())
            {
                Debug.Log("TACtical_AI: AvoidAssistAir IS NaN!!");
                //AIECore.TankAIManager.FetchAllAllies();
            }
            return targetIn;
        }

        public bool TryAdjustForCombat()
        {
            AIECore.TankAIHelper thisInst = this.pilot.Helper;
            bool output = false;
            if (thisInst.PursueThreat && !thisInst.Retreat && thisInst.lastEnemy.IsNotNull())
            {
                output = true;
                thisInst.Steer = true;
                thisInst.lastRangeCombat = (thisInst.lastEnemy.tank.boundsCentreWorldNoCheck - pilot.Tank.boundsCentreWorldNoCheck).magnitude;
                float driveDyna = Mathf.Clamp((thisInst.lastRangeCombat - thisInst.IdealRangeCombat) / 3f, -1, 1);
                if (thisInst.SideToThreat)
                {
                    if (thisInst.FullMelee)
                    {   //orbit WHILE at enemy!
                        thisInst.DriveDir = EDriveType.Perpendicular;
                        thisInst.lastDestination = thisInst.lastEnemy.transform.position;
                        thisInst.MinimumRad = 0;//WHAAAAAAAAAAAM
                    }
                    else if (driveDyna == 1)
                    {
                        thisInst.DriveDir = EDriveType.Perpendicular;
                        thisInst.lastDestination = thisInst.AvoidAssist(thisInst.lastEnemy.transform.position);
                        thisInst.MinimumRad = thisInst.lastTechExtents + thisInst.lastEnemy.GetCheapBounds() + 3;
                    }
                    else if (driveDyna < 0)
                    {
                        thisInst.DriveDir = EDriveType.Perpendicular;
                        thisInst.AdviseAway = true;
                        thisInst.lastDestination = thisInst.AvoidAssist(thisInst.lastEnemy.transform.position);
                        thisInst.MinimumRad = thisInst.lastTechExtents + thisInst.lastEnemy.GetCheapBounds() + 3;
                    }
                    else
                    {
                        thisInst.DriveDir = EDriveType.Perpendicular;
                        thisInst.lastDestination = thisInst.lastEnemy.transform.position;
                        thisInst.MinimumRad = thisInst.lastTechExtents + thisInst.lastEnemy.GetCheapBounds() + 3;
                    }
                }
                else
                {
                    if (thisInst.FullMelee)
                    {
                        thisInst.DriveDir = EDriveType.Forwards;
                        thisInst.lastDestination = thisInst.lastEnemy.transform.position;
                        thisInst.MinimumRad = 0;//WHAAAAAAAAAAAM
                                                //thisControl.m_Movement.DriveToPosition(tank, thisInst.AvoidAssist(thisInst.lastEnemy.transform.position), 1, TankControl.DriveRestriction.ForwardOnly, thisInst.lastEnemy, Mathf.Max(thisInst.lastTechExtents - 10, 0.2f));
                    }
                    else if (driveDyna == 1)
                    {
                        thisInst.DriveDir = EDriveType.Forwards;
                        thisInst.lastDestination = thisInst.AvoidAssist(thisInst.lastEnemy.transform.position);
                        thisInst.MinimumRad = thisInst.lastTechExtents + thisInst.lastEnemy.GetCheapBounds() + 5;
                        //thisControl.m_Movement.DriveToPosition(tank, thisInst.AvoidAssist(thisInst.lastEnemy.transform.position), 1, TankControl.DriveRestriction.None, thisInst.lastEnemy, thisInst.lastTechExtents + AIEnhancedCore.Extremes(thisInst.lastEnemy.tank.blockBounds.extents) + 5);
                    }
                    else if (driveDyna < 0)
                    {
                        thisInst.DriveDir = EDriveType.Forwards;
                        thisInst.AdviseAway = true;
                        thisInst.lastDestination = thisInst.AvoidAssist(thisInst.lastEnemy.transform.position);
                        thisInst.MinimumRad = 0.5f;
                        //thisControl.m_Movement.DriveToPosition(tank, thisInst.AvoidAssist(thisInst.lastEnemy.transform.position), 1, TankControl.DriveRestriction.None, thisInst.lastEnemy, thisInst.lastTechExtents + AIEnhancedCore.Extremes(thisInst.lastEnemy.tank.blockBounds.extents) + 5);
                    }
                    else
                    {
                        thisInst.lastDestination = thisInst.lastEnemy.transform.position;
                        thisInst.MinimumRad = 0;
                        //thisControl.m_Movement.FacePosition(tank, thisInst.lastEnemy.transform.position, driveDyna);//Face the music
                    }
                }
            }
            else
                thisInst.lastRangeCombat = float.MaxValue;
            return output;
        }

        public bool TryAdjustForCombatEnemy(EnemyMind mind)
        {
            AIECore.TankAIHelper thisInst = this.pilot.Helper;
            bool output = false;
            if (!thisInst.Retreat && thisInst.lastEnemy.IsNotNull() && mind.CommanderMind != Enemy.EnemyAttitude.OnRails)
            {
                output = true;
                thisInst.Steer = true;
                thisInst.lastRangeCombat = (thisInst.lastEnemy.tank.boundsCentreWorldNoCheck - pilot.Tank.boundsCentreWorldNoCheck).magnitude;
                float driveDyna = Mathf.Clamp((thisInst.lastRangeCombat - thisInst.IdealRangeCombat) / 3f, -1, 1);
                if (mind.CommanderAttack == Enemy.EnemyAttack.Circle)
                {
                    if (mind.CommanderMind == Enemy.EnemyAttitude.Miner)
                    {   //orbit WHILE at enemy!
                        thisInst.DriveDir = EDriveType.Perpendicular;
                        thisInst.lastDestination = Enemy.RCore.GetTargetCoordinates(tank, thisInst.lastEnemy, mind);
                        thisInst.MinimumRad = 0;//WHAAAAAAAAAAAM
                    }
                    else if (driveDyna == 1)
                    {
                        thisInst.DriveDir = EDriveType.Perpendicular;
                        thisInst.lastDestination = Enemy.RPathfinding.AvoidAssistEnemy(tank, Enemy.RCore.GetTargetCoordinates(tank, thisInst.lastEnemy, mind), thisInst, mind);
                        thisInst.MinimumRad = thisInst.lastTechExtents + thisInst.lastEnemy.GetCheapBounds() + 2;
                    }
                    else if (driveDyna < 0)
                    {
                        thisInst.DriveDir = EDriveType.Perpendicular;
                        thisInst.AdviseAway = true;
                        thisInst.lastDestination = Enemy.RPathfinding.AvoidAssistEnemy(tank, Enemy.RCore.GetTargetCoordinates(tank, thisInst.lastEnemy, mind), thisInst, mind);
                        thisInst.MinimumRad = thisInst.lastTechExtents + thisInst.lastEnemy.GetCheapBounds() + 2;
                    }
                    else
                    {
                        thisInst.DriveDir = EDriveType.Perpendicular;
                        thisInst.lastDestination = Enemy.RCore.GetTargetCoordinates(tank, thisInst.lastEnemy, mind);
                        thisInst.MinimumRad = thisInst.lastTechExtents + thisInst.lastEnemy.GetCheapBounds() + 2;
                    }
                }
                else
                {
                    if (mind.CommanderMind == Enemy.EnemyAttitude.Miner)
                    {
                        thisInst.DriveDir = EDriveType.Forwards;
                        thisInst.lastDestination = Enemy.RCore.GetTargetCoordinates(tank, thisInst.lastEnemy, mind);
                        thisInst.MinimumRad = 0;//WHAAAAAAAAAAAM
                    }
                    else if (driveDyna == 1)
                    {
                        thisInst.DriveDir = EDriveType.Forwards;
                        thisInst.lastDestination = Enemy.RPathfinding.AvoidAssistEnemy(tank, Enemy.RCore.GetTargetCoordinates(tank, thisInst.lastEnemy, mind), thisInst, mind);
                        thisInst.MinimumRad = thisInst.lastTechExtents + thisInst.lastEnemy.GetCheapBounds() + 5;
                    }
                    else if (driveDyna < 0)
                    {
                        thisInst.DriveDir = EDriveType.Forwards;
                        thisInst.AdviseAway = true;
                        thisInst.lastDestination = Enemy.RPathfinding.AvoidAssistEnemy(tank, Enemy.RCore.GetTargetCoordinates(tank, thisInst.lastEnemy, mind), thisInst, mind);
                        thisInst.MinimumRad = 0.5f;
                    }
                    else
                    {
                        thisInst.lastDestination = Enemy.RCore.GetTargetCoordinates(tank, thisInst.lastEnemy, mind);
                        thisInst.MinimumRad = 0;
                    }
                }
            }
            else
                thisInst.lastRangeCombat = float.MaxValue;
            return output;
        }
    }
}
