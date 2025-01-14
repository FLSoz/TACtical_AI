﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using UnityEngine;
using TAC_AI.AI.Movement.AICores;


namespace TAC_AI.AI {
    
    public interface IMovementAIController
    {
        IMovementAICore AICore
        {
            get;
        }

        Tank Tank
        {
            get;
        }

        AIECore.TankAIHelper Helper
        {
            get;
        }

        Enemy.EnemyMind EnemyMind
        {
            get;
        }

        void Initiate(Tank tank, AIECore.TankAIHelper helper, Enemy.EnemyMind mind = null);
        void UpdateEnemyMind(Enemy.EnemyMind mind);

        void DriveDirector();

        void DriveDirectorRTS();

        void DriveMaintainer(TankControl tankControl);

        void OnMoveWorldOrigin(IntVector3 move);
        Vector3 GetDestination();

        void Recycle();
    }
}
