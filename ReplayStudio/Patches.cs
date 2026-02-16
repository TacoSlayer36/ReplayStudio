using Il2CppRUMBLE.Players.Subsystems;
using Il2CppRUMBLE.Players;
using MelonLoader;
using System;
using UnityEngine;
using HarmonyLib;
using Il2CppRUMBLE.Utilities;

namespace ReplayStudio
{
    /// <summary>
    /// Use a part of the local player's initialization to determine if scenes are fully loaded
    /// </summary>
    [HarmonyPatch(typeof(PlayerVisuals), nameof(PlayerVisuals.ApplyPlayerVisuals), new Type[] { typeof(Il2CppRUMBLE.MeshGeneration.PlayerCharacterBaker.GeneratedPlayerVisuals) })]
    public static class PlayerVisuals_ApplyPlayerVisuals_Patch
    {
        private static void Postfix()
        {
            Core.Instance.SceneReady();
        }
    }

    /// <summary>
    /// Runs the function to move the Legacy Camera after it moves itself but before it renders
    /// </summary>
    [HarmonyPatch(typeof(RecordingCamera), nameof(RecordingCamera.BeginFrameRendering))]
    public static class RecordingCamera_BeginFrameRendering_Patch
    {
        private static void Postfix()
        {
            if (CameraController.IsCameraEnabled)
                CameraController.SnapLegacyCam();
        }
    }
}
