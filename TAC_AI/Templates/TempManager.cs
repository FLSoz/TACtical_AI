﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace TAC_AI.Templates
{
    public static class TempManager
    {
        public static void ValidateAllStringTechs()
        {
            techBases = new Dictionary<SpawnBaseTypes, BaseTemplate>();
            foreach (KeyValuePair<SpawnBaseTypes, BaseTemplate> pair in TempStorage.techBasesAll)
            {
                if (ValidateBlocksInTech(pair.Value.savedTech))
                {
                    techBases.Add(pair.Key, pair.Value);
                }
                else 
                {
                    Debug.Log("TACtical AIs: Could not load " + pair.Value.techName + " as it contained missing blocks");
                }
            }
            TempStorage.techBasesAll.Clear(); // GC, do your duty
        }

        public static bool ValidateBlocksInTech(string toLoad)
        {
            StringBuilder RAW = new StringBuilder();
            foreach (char ch in toLoad)
            {
                if (ch != '\\')
                {
                    RAW.Append(ch);
                }
            }
            List<AI.BlockMemory> mem = new List<AI.BlockMemory>();
            StringBuilder blockCase = new StringBuilder();
            string RAWout = RAW.ToString();
            foreach (char ch in RAWout)
            {
                if (ch == '|')//new block
                {
                    mem.Add(JsonUtility.FromJson<AI.BlockMemory>(blockCase.ToString()));
                    blockCase.Clear();
                }
                else
                    blockCase.Append(ch);
            }
            bool valid = true;
            foreach (AI.BlockMemory bloc in mem)
            {
                if (!Singleton.Manager<ManSpawn>.inst.IsValidBlockToSpawn(bloc.blockType))
                    valid = false;
            }
            return valid;
        }

        public static Dictionary<SpawnBaseTypes, BaseTemplate> techBases;
    }
}