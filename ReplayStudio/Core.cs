using Il2CppRUMBLE.Managers;
using Il2CppRUMBLE.Players;
using MelonLoader;
using UnityEngine;
using static ReplayStudio.ViewController;
using ReplayMod.Replay.Serialization;
using ReplayMod.Replay;
using System.IO;
using Newtonsoft.Json;
using static ReplayStudio.HelperFunctions;
using static ReplayStudio.Components.KeyframedObject;
using UnityEngine.EventSystems;
using Il2CppTMPro;

/* TODO:
 * Crystals
 */

namespace ReplayStudio;

public class Core : MelonMod
{
    public static Core Instance;
    public static ReplayMod.Core.Main ReplayModMain;

    public static StudioData StudioData;

    internal bool GlobalInit = false;
    internal int ActiveScene = 0;
    internal bool IsSceneReady = false;

    public static bool DesktopMode = false;
    public static bool AssumedInVR = false;
    public static float FPS = 30f;

    public static GameObject DDOL_GameObjects;
    public static GameObject LineTemplate;

    internal static PlayerController LocalPlayerRef => PlayerManager.Instance.localPlayer.Controller;

    public override void OnLateInitializeMelon()
    {
        Instance = this;
        ReplayModMain = Melon<ReplayMod.Core.Main>.Instance;
        ReplayAPI.onReplayStarted += OnReplayStarted;
        ReplayAPI.onReplayEnded += OnReplayEnded;
    }

    public override void OnSceneWasLoaded(int buildIndex, string sceneName)
    {
        ActiveScene = buildIndex;
    }

    public override void OnSceneWasUnloaded(int buildIndex, string sceneName)
    {
        IsSceneReady = false;
    }

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
            ViewController.HandleViewCam();

        if (ReplayAPI.IsPlaying)
        {
            if (EventSystem.current?.currentSelectedGameObject?.GetComponent<TMP_InputField>() != null) return;

            if (Input.GetKeyDown(KeyCode.Space))
            {
                ReplayMod.Core.Main.Playback.TogglePlayback(ReplayMod.Core.Main.Playback.isPaused);
            }

            if (Input.GetKeyDown(KeyCode.I))
            {
                CameraController.KeyframeComponent.Capture<PositionKeyFrame, RotationKeyFrame, FovKeyFrame>();
                SaveStudioData();
            }

            if (Input.GetKeyDown(KeyCode.Keypad0))
            {
                if (CameraController.Enabled)
                {
                    CameraController.ExitCamera();
                    CameraController.DoMapping = false;
                }
                else
                {
                    CameraController.EnterCamera(true);
                    CameraController.DoMapping = true;
                }
            }

            if (IsPressingAny(ControlKeys) && IsPressingAny(AltKeys) && Input.GetKeyDown(KeyCode.C))
            {
                CameraController.Camera.transform.position = ViewController.LegacyCamRef.transform.position;
                CameraController.Camera.transform.rotation = ViewController.LegacyCamRef.transform.rotation;
                CameraController.Camera.fieldOfView = ViewController.ViewFOV;
            }

            if (ReplayAPI.IsPaused && CameraController.DoMapping)
            {
                CameraController.MapCamera();
            }
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

    internal void SceneReady()
    {
        if (ActiveScene == 0) return; // Skip the Loader

        IsSceneReady = true;

        if (ActiveScene == 1 && !GlobalInit)
            RunGlobalInit();

        if (!DesktopMode) return;

        if (ViewController.IsViewCamEnabled)
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
        LoadStudioData();

        GameObject timeline = UIManager.TransformRefs["Timeline"].gameObject;
        timeline.GetComponent<TimelineController>()?.Reset();
        if (!ViewController.IsViewCamEnabled && !AssumedInVR) ViewController.SetCameraMode(ViewMode.Fly);

        CameraController.InitializeCamera();
    }

    internal void OnReplayEnded(ReplayInfo _)
    {
        TimelineController.Instance.Reset();
        TimelineController.Instance.ScrollTo(0f, 0.5f, 10f, false);
        ViewController.DisableViewCam();

        CameraController.RemoveCamera();
    }

    internal void LoadStudioData()
    {
        string directory = ReplayAPI.CurrentFolder;
        string replayName = Utilities.CleanName(ReplayAPI.CurrentReplay.Header.Title);
        string studioDataPath = Path.Combine(directory, replayName + ".json");
        if (File.Exists(studioDataPath))
        {
            StudioData = JsonConvert.DeserializeObject<StudioData>(File.ReadAllText(studioDataPath));
        }
        else
        {
            StudioData = new();
        }
    }

    internal void SaveStudioData()
    {
        string directory = ReplayAPI.CurrentFolder;
        string replayName = Utilities.CleanName(ReplayAPI.CurrentReplay.Header.Title);
        string studioDataPath = Path.Combine(directory, replayName + ".json");
        
        string serializedJson = JsonConvert.SerializeObject(CameraController.KeyframeComponent.Channels, Formatting.Indented);
        MelonLogger.Msg(serializedJson);

        File.WriteAllText(studioDataPath, serializedJson);
    }
}