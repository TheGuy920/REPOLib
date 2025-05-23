﻿using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace REPOLib.Patches;

[HarmonyPatch(typeof(EnemyGnomeDirector))]
internal static class EnemyGnomeDirectorPatch
{
    [HarmonyTranspiler]
    [HarmonyPatch(nameof(EnemyGnomeDirector.Awake))]
    private static IEnumerable<CodeInstruction> AwakeTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        MethodInfo? originalMethod = AccessTools.Method(typeof(EnemyGnomeDirector), nameof(EnemyGnomeDirector.Setup));
        MethodInfo? replacementMethod = AccessTools.Method(typeof(EnemyGnomeDirectorPatch), nameof(PreSetup));

        if (originalMethod is null || replacementMethod is null)
        {
            Logger.LogError("EnemyGnomeDirectorPatch: failed to find required methods for AwakeTranspiler.");
            return instructions;
        }

        List<CodeInstruction> modifiedInstructions = [];
        foreach (CodeInstruction? instruction in instructions)
        {
            bool isMethodCall = instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt;
            if (isMethodCall && instruction.operand is MethodInfo methodInfo && methodInfo == originalMethod)
            {
                // Replace original method call with replacement method call
                modifiedInstructions.Add(new CodeInstruction(OpCodes.Call, replacementMethod));
                Logger.LogDebug(
                    $"EnemyGnomeDirectorPatch: AwakeTranspiler replaced {originalMethod.Name} call with {replacementMethod.Name}.");
                continue;
            }

            modifiedInstructions.Add(instruction); // Add unmodified instruction
        }

        return modifiedInstructions.AsEnumerable();
    }

    private static IEnumerator PreSetup(EnemyGnomeDirector instance)
    {
        if (LevelGenerator.Instance.Generated)
            yield return new WaitForSeconds(0.1f);

        yield return instance.Setup();
    }
}