using Il2CppRUMBLE.Players.Subsystems;
using Il2CppRUMBLE.Players;
using MelonLoader;
using System;
using UnityEngine;
using HarmonyLib;
using Il2CppRUMBLE.Utilities;
using Il2CppRUMBLE.Managers;

namespace ReplayStudio
{
    [HarmonyPatch(typeof(PlayerVisuals), nameof(PlayerVisuals.ApplyPlayerVisuals), new Type[] { typeof(Il2CppRUMBLE.MeshGeneration.PlayerCharacterBaker.GeneratedPlayerVisuals) })]
    public static class PlayerVisuals_ApplyPlayerVisuals_Patch
    {
        private static void Postfix()
        {
            if (!Core.Instance.IsSceneReady)
                Core.Instance.SceneReady();
        }
    }

    [HarmonyPatch(typeof(RecordingCamera), nameof(RecordingCamera.BeginFrameRendering))]
    public static class RecordingCamera_BeginFrameRendering_Patch
    {
        private static void Postfix()
        {
            if (ViewController.IsViewCamEnabled)
                ViewController.SnapLegacyCam();
        }
    }

    [HarmonyPatch(typeof(PlayerMovement), nameof(PlayerMovement.Move), new Type[] { typeof(Vector2) })]
    public static class PlayerMovement_Move_Patch
    {
        private static void Postfix(ref Vector2 input)
        {
            if (input.magnitude <= 0.9f) return;
            if (PlayerManager.Instance.LocalPlayer.Controller.PlayerSessionStateSystem.CurrentVRState is not PlayerSessionStateSystem.VRState.Present) return;

            if (UIManager.CurrentWindowMode is not UIManager.WindowMode.Hidden)
                UIManager.SetWindowType(UIManager.WindowMode.Hidden);

            Core.AssumedInVR = true;
        }
    }

    [HarmonyPatch(typeof(PlayerPoseSystem), nameof(PlayerPoseSystem.OnPoseCompleted))]
    public static class PlayerPoseSystem_OnPoseCompleted_Patch
    {
        private static void Postfix()
        {
            if (PlayerManager.Instance.LocalPlayer.Controller.PlayerSessionStateSystem.CurrentVRState is not PlayerSessionStateSystem.VRState.Present) return;

            if (UIManager.CurrentWindowMode is not UIManager.WindowMode.Hidden)
                UIManager.SetWindowType(UIManager.WindowMode.Hidden);

            Core.AssumedInVR = true;
        }
    }
}
