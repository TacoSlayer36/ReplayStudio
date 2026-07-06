using Il2CppPlayFab.ClientModels;
using Il2CppRUMBLE.Managers;
using Il2CppRUMBLE.Players;
using Il2CppTMPro;
using MelonLoader;
using UnityEngine;
using static ReplayStudio.CameraController;
using ReplayMod;
using ReplayMod.Replay.Serialization;
using System.Collections;
using Microsoft.Diagnostics.Runtime;
using ReplayStudio.Components;

/* TODO:
 * Crystals
 */

namespace ReplayStudio;

/// <summary>
/// Contains anything that runs universally as well as overrides for game events
/// </summary>
public class Core : MelonMod
{
    /// <summary> Melon singleton for this mod </summary>
    public static Core Instance;
    /// <summary> Melon singleton for ReplayMod </summary>
    public static ReplayMod.Core.Main ReplayModMain;

    internal bool GlobalInit = false;
    internal int ActiveScene = 0;
    internal bool IsSceneReady = false;

    /// <summary>Whether the user is viewing replays on the desktop or in VR</summary>
    public static bool DesktopMode = false;
    public static bool AssumedInVR = false;
    public static float FPS = 30f;

    /// <summary> The mod's parent Game Object in DontDestroyOnLoad </summary>
    public static GameObject DDOL_GameObjects;
    public static GameObject LineTemplate;

    internal static PlayerController LocalPlayerRef => PlayerManager.Instance.localPlayer.Controller;

    /// <summary></summary>
    public override void OnLateInitializeMelon()
    {
        Instance = this;
        ReplayModMain = Melon<ReplayMod.Core.Main>.Instance;
        ReplayMod.Replay.ReplayAPI.onReplayStarted += OnReplayStarted;
        ReplayMod.Replay.ReplayAPI.onReplayEnded += OnReplayEnded;
    }

    /// <summary></summary>
    public override void OnSceneWasLoaded(int buildIndex, string sceneName)
    {
        ActiveScene = buildIndex;
    }

    /// <summary></summary>
    public override void OnSceneWasUnloaded(int buildIndex, string sceneName)
    {
        IsSceneReady = false;
    }

    /// <summary></summary>
    public override void OnUpdate()
    {
        if (!GlobalInit || !IsSceneReady) return;

        if (Input.GetKeyDown(KeyCode.F1))
        {
            if (UIManager.CurrentWindowMode is not UIManager.WindowMode.Maximized)
                UIManager.SetWindowType(UIManager.WindowMode.Maximized);
            else
                UIManager.SetWindowType(UIManager.WindowMode.Hidden);
        }

        if (!UIManager.IsHoveringAny)
            CameraController.HandleCamera();

        if (Input.GetKeyDown(KeyCode.Space))
        {
            ReplayMod.Core.Main.Playback.TogglePlayback(ReplayMod.Core.Main.Playback.isPaused);
        }
    }

    public override void OnFixedUpdate()
    {
        UIManager.HandleWindow();

        if (!DesktopMode) return;

        UIManager.UpdateSpeedInput();
        UIManager.UpdatePlayPause();
        UIManager.UpdateFOVInput();
        UIManager.UpdatePOVSelector();
        UIManager.UpdateDOFSettings();
        UIManager.UpdateRename();
        TimelineController.Instance.UpdateClipInfos();
    }

    /// <summary>
    /// Runs when player visuals are generated
    /// </summary>
    internal void SceneReady()
    {
        if (ActiveScene == 0) return; // Skip the Loader
        
        IsSceneReady = true;

        if (ActiveScene == 1 && !GlobalInit)
            RunGlobalInit();

        if (!DesktopMode) return;

        if (CameraController.IsCameraEnabled)
            SetPlayer(false);
    }

    internal void RunGlobalInit()
    {
        GlobalInit = true;
        DDOL_GameObjects = GameObject.Instantiate(GameObjectManager.LoadAssetFromStream<GameObject>(this, "ReplayStudio.assets.replaystudio", "ReplayStudio"));
        GameObject.DontDestroyOnLoad(DDOL_GameObjects);

        Transform uiRoot = DDOL_GameObjects.transform.Find("Canvas");
        UIManager.SetUpUI(uiRoot);

        LineTemplate = DDOL_GameObjects.transform.Find("LineTemplate").gameObject;

        InitializeCamera();
    }

    internal void OnReplayStarted(ReplayInfo _)
    {
        GameObject timeline = UIManager.TransformRefs["Timeline"].gameObject;
        timeline.GetComponent<TimelineController>()?.Reset();
        if (!CameraController.IsCameraEnabled && !AssumedInVR) CameraController.SetCameraMode(CameraMode.Orbit);
    }

    internal void OnReplayEnded(ReplayInfo _)
    {
        TimelineController.Instance.Reset();
        TimelineController.Instance.ScrollTo(0f, 0.5f, 10f, false);
        CameraController.DisableCamera();
    }
}