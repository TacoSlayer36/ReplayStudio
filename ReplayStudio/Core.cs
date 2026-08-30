using Il2CppRUMBLE.Managers;
using Il2CppRUMBLE.Players;
using Il2CppTMPro;
using MelonLoader;
using Newtonsoft.Json;
using ReplayMod.Core;
using ReplayMod.Replay;
using ReplayMod.Replay.Serialization;
using ReplayStudio.Components;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UIFramework;
using static ReplayStudio.StudioData;
using static Il2CppRUMBLE.Players.Subsystems.PlayerAnimator;

/* TODO:
 * Crystals
 */

namespace ReplayStudio;

public class Core : MelonMod
{
    public static HeadAnimation AA_Expression = HeadAnimation.Idle;

    public static Core Instance;
    public static ReplayMod.Core.Main ReplayModMain;
    public static string CurrentReplayPath; // Set via a harmony patch :(
    const string saveFileName = "replay_studio.json";

    public static StudioData StudioData;

    internal bool GlobalInit = false;
    internal int ActiveScene = 0;
    internal bool IsSceneReady = false;

    public static bool DesktopMode = false;
    public static bool AssumedInVR = false;
    public static float FPS = 30f;

    public static GameObject DDOL_GameObjects;
    public static Material DottedLineMat;

    public struct Settings
    {
        internal const string USER_DATA = "UserData/ReplayStudio/Settings/";
        internal const string CONFIG_FILE = "config.cfg";

        public static MelonPreferences_Entry<bool> RenderBezierWidgets;
        public static MelonPreferences_Entry<int> SplineResolution;
    }

    public static JsonSerializerSettings settings = new JsonSerializerSettings()
    {
        ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
        TypeNameHandling = TypeNameHandling.Auto,
        ObjectCreationHandling = ObjectCreationHandling.Replace,
        Converters = { new Vector3Converter() }
    };

    internal static PlayerController LocalPlayerRef => PlayerManager.Instance.localPlayer.Controller;

    public override void OnInitializeMelon()
    {
        if (!Directory.Exists(Settings.USER_DATA))
            Directory.CreateDirectory(Settings.USER_DATA);

        string configPath = Path.Combine(Settings.USER_DATA, Settings.CONFIG_FILE);
        var tmpCategory = MelonPreferences.CreateCategory("tmp");
        tmpCategory.SetFilePath(configPath);
        Settings.RenderBezierWidgets = tmpCategory.CreateEntry("ReplayStudio-RenderBezierWidgets", true, "Render Bezier Widgets", "Render the handle widgets for Bezier keyframes");
        Settings.SplineResolution = tmpCategory.CreateEntry("ReplayStudio-SplineResolution", 10, "Spline Resolution", "The amount of steps used to render each spline");

        UI.RegisterMelon(this, tmpCategory);
    }


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
        if (Rendering.RENDERING)
        {
            if (Input.GetKey(KeyCode.Escape))
            {
                Rendering.StopRender();
            }
            return;
        }

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

        if (!UIManager.IsHoveringAny)
            CameraController.HandleCamera();

        if (ReplayAPI.IsPlaying && !UIManager.IsInputBlocked)
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                ReplayMod.Core.Main.Playback.TogglePlayback(ReplayMod.Core.Main.Playback.isPaused);
            }

            if (TimelineController.Instance.IsHovering)
            {
                if (Input.GetKeyDown(KeyCode.Delete))
                {
                    TimelineController.Instance.DeletePrevKeyframeMarker();
                    SaveStudioData();
                }


                if (Input.GetKeyDown(KeyCode.I))
                {
                    CameraController.KeyframeComponent.Capture<TrackingBezierKeyframe, KeyframedObject.FovKeyFrame>();
                    SaveStudioData();
                }
            }

            if (Input.GetKeyDown(KeyCode.Insert) || Input.GetKeyDown(KeyCode.Keypad0))
            {
                    if (CameraController.Enabled)
                    {
                        CameraController.ExitCamera();
                    }
                    else
                    {
                    CameraController.EnterCamera();
                    }
                }

            if (Input.GetKeyDown(KeyCode.KeypadPeriod))
            {
                CameraController.MapCameraToView();
            }

            if (Input.GetKeyDown(KeyCode.R)) // TODO: This is temporary
            {
                TimelineController.OnKeyframesModified();
            }
        }
    }

    public override void OnLateUpdate()
    {
        if (Rendering.RENDERING)
            Rendering.HandleRendering();
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
            ViewController.SetPlayer(false);
    }

    internal void RunGlobalInit()
    {
        GlobalInit = true;
        DDOL_GameObjects = GameObject.Instantiate(GameObjectManager.LoadAssetFromStream<GameObject>(this, "ReplayStudio.assets.replaystudio", "ReplayStudio"));
        GameObject.DontDestroyOnLoad(DDOL_GameObjects);

        DottedLineMat = new Material(GameObjectManager.LoadAssetFromStream<Material>(this, "ReplayStudio.assets.replaystudio", "DottedLine"));
        DottedLineMat.hideFlags = HideFlags.HideAndDontSave | HideFlags.DontUnloadUnusedAsset;

        Transform uiRoot = DDOL_GameObjects.transform.Find("Canvas");
        UIManager.SetUpUI(uiRoot);

        ViewController.InitializeCamera();
    }

    internal void OnReplayStarted(ReplayInfo _)
    {
        GameObject timeline = UIManager.TransformRefs["Timeline"].gameObject;
        timeline.GetComponent<TimelineController>()?.Reset();
        if (!ViewController.IsViewCamEnabled && !AssumedInVR) ViewController.SetCameraMode(ViewController.ViewMode.Fly);

        CameraController.InitializeCamera();

        foreach (var player in ReplayMod.Core.Main.Playback.PlaybackPlayers)
        {
            if (player.Controller.gameObject.GetComponent<KeyframedObject>() == null)
                player.Controller.gameObject.AddComponent<KeyframedObject>();
        }

        LoadStudioData();
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
        MelonLogger.Msg("LoadStudioData");
        if (File.Exists(CurrentReplayPath))
        {
            string fileContent = string.Empty;
            using (ZipArchive archive = ZipFile.OpenRead(CurrentReplayPath))
            {
                ZipArchiveEntry entry = archive.GetEntry(saveFileName);
                if (entry != null)
                {
                    using (Stream stream = entry.Open())
                    using (StreamReader reader = new(stream))
                    {
                        fileContent = reader.ReadToEnd();
                    }
                } else {
                    MelonLogger.Msg($"LoadStudioData::({saveFileName}) no such file");
                }
            }
            if (!string.IsNullOrEmpty(fileContent))
            {
                MelonLogger.Msg("Deserializing");
                MelonLogger.Msg(fileContent);
                StudioData = JsonConvert.DeserializeObject<StudioData>(fileContent, settings);
            } else {
                MelonLogger.Msg($"LoadStudioData::file content is null");
            }
        }
        else
        {
            MelonLogger.Msg($"LoadStudioData::({CurrentReplayPath}) no such file");
            StudioData = new();
        }

        MelonLogger.Msg("LoadStudioData;");

        CameraController.KeyframeComponent.InitializeAll();
        foreach (KeyframedObject playerKeyframe in StudioData.playerComponents.Values)
        {
            playerKeyframe?.InitializeAll();
        }
    }

    internal void SaveStudioData()
    {
        string serializedJson = JsonConvert.SerializeObject(StudioData, Formatting.Indented, settings);
        MelonLogger.Msg("serializing");
        MelonLogger.Msg(serializedJson);

        using (FileStream archiveToOpen = new FileStream(CurrentReplayPath, FileMode.OpenOrCreate))
        using (ZipArchive archive = new(archiveToOpen, ZipArchiveMode.Update))
        {
            ZipArchiveEntry existingEntry = archive.GetEntry(saveFileName);
            if (existingEntry != null)
            {
                existingEntry.Delete();
            }

            ZipArchiveEntry newEntry = archive.CreateEntry(saveFileName);
            using (StreamWriter writer = new StreamWriter(newEntry.Open()))
            {
                writer.Write(serializedJson);
            }
        }
    }

    [HarmonyLib.HarmonyPatch(typeof(ReplayPlayback), nameof(ReplayPlayback.LoadReplay), new Type[] { typeof(string), typeof(bool) })]
    public static class Patch_ReplayPlayback_LoadReplay
    {
        private static void Postfix(string path, bool allowDifferentSceneLoad)
        {
            CurrentReplayPath = path;
        }
    }
}