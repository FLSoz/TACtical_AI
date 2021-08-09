﻿using System;
using TAC_AI.AI;
using UnityEngine;

namespace TAC_AI
{
    public class ModuleChargerTracker : Module
    {
        TankBlock TankBlock;
        // Returns the position of itself in the world as a point the AI can pathfind to
        public Tank tank;
        public Transform trans;
        public ModuleItemHolder holder;
        internal float minEnergyAmount = 200;
        private bool DockingRequested = false;

        public static implicit operator Transform(ModuleChargerTracker yes)
        {
            return yes.trans;
        }

        public void OnPool()
        {
            TankBlock = gameObject.GetComponent<TankBlock>();
            TankBlock.AttachEvent.Subscribe(new Action(OnAttach));
            TankBlock.DetachEvent.Subscribe(new Action(OnDetach));
            trans = transform;
            holder = gameObject.GetComponent<ModuleItemHolder>();
        }
        public void OnAttach()
        {
            AIECore.Chargers.Add(this);
            tank = transform.root.GetComponent<Tank>();
            DockingRequested = false;
        }
        public void OnDetach()
        {
            tank = null;
            AIECore.Chargers.Remove(this);
            DockingRequested = false;
        }
        public bool CanTransferCharge(Tank toChargeTank)
        {
            if (tank == null)
                return false;
            EnergyRegulator.EnergyState energyThis = tank.EnergyRegulator.Energy(EnergyRegulator.EnergyType.Electric);

            EnergyRegulator.EnergyState energyThat = toChargeTank.EnergyRegulator.Energy(EnergyRegulator.EnergyType.Electric);
            float chargeFraction = energyThat.currentAmount / energyThat.storageTotal;

            return energyThis.currentAmount > minEnergyAmount && energyThis.currentAmount / energyThis.storageTotal > chargeFraction;
        }
        public void RequestDocking()
        {
            if (!DockingRequested)
            {
                if (tank == null)
                {
                    Debug.Log("TACtical_AI: Tried to request docking to a charger that was not attached to anything");
                    return;
                }
                DockingRequested = true;
                Invoke("StopDocking", 2);
                tank.GetComponent<AIECore.TankAIHelper>().AllowApproach();
            }
        }
        private void StopDocking()
        {
            if (DockingRequested)
            {
                DockingRequested = false;
            }
        }
    }
}
