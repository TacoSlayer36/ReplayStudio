using Il2CppExitGames.Client.Photon;
using Il2CppRUMBLE.Managers;
using Il2CppRUMBLE.Players;
using Il2CppRUMBLE.Utilities;
using Il2CppSteamworks;
using Il2CppTMPro;
using MelonLoader;
using ReplayMod;
using ReplayMod.Core;
using ReplayMod.Replay;
using ReplayMod.Replay.Files;
using ReplayMod.Replay.Serialization;
using ReplayMod.Replay.UI;
using ReplayStudio.Components;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.PlayerLoop;
using UnityEngine.UI;

namespace ReplayStudio;

internal static class UIManager
{
    public static Dictionary<string, Transform> TransformRefs = new();

    public static List<MouseDetector> MouseDetectors = new();

    public static MouseDetector PopoutHoverMD;
    public enum WindowMode
    {
        Maximized,
        Minimized,
        Hidden
    }
    private static WindowMode _currentWindowMode = WindowMode.Minimized;
    public static WindowMode CurrentWindowMode
    {
        get
        {
            return _currentWindowMode;
        }
        set
        {
            SetWindowType(value);
        }
    }

    public static bool IsHoveringAny => MouseDetectors.Any(m => m.IsHovering || m.HeldFromHovering);

    public static bool ViewingOtherPOV => ReplayPlayback.povPlayer != null && ReplayPlayback.povPlayer != PlayerManager.Instance?.LocalPlayer;

    public static void SetUpUI(Transform uiRoot)
    {
        FetchRefsInChildren(uiRoot);
        AddListeners();

        MouseDetectors.Clear();
        MouseDetectors.AddRange(uiRoot.GetComponentsInChildren<MouseDetector>());

        PopoutHoverMD = TransformRefs["PopoutHover"]?.GetComponent<MouseDetector>();
        TransformRefs["Editor"].gameObject.SetActive(false);
        TimelineController.Instance.ScrollTo(0f, 0.5f, 10f, false);
    }
    static void FetchRefsInChildren(Transform parent)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            TransformRefs[child.name] = child;
            FetchRefsInChildren(child);
        }
    }
    static void AddListeners()
    {
        TransformRefs["NextReplay"]?.GetComponent<Button>()?.onClick?.AddListener((System.Action)OnNextReplayClicked);
        TransformRefs["PreviousReplay"]?.GetComponent<Button>()?.onClick?.AddListener((System.Action)OnPreviousReplayClicked);
        ReplayMod.Replay.ReplayAPI.onReplaySelected += OnReplaySelected;
        TransformRefs["PlayReplayButton"]?.GetComponent<Button>()?.onClick?.AddListener((System.Action)OnPlayReplayButtonClicked);

        TransformRefs["RenameReplayButton"]?.GetComponent<Button>()?.onClick?.AddListener((System.Action)OnRenameButtonClicked);
        TransformRefs["CopyFilePathButton"]?.GetComponent<Button>()?.onClick?.AddListener((System.Action)OnCopyPathButtonClicked);
        TransformRefs["DeleteReplayButton"]?.GetComponent<Button>()?.onClick?.AddListener((System.Action)OnDeleteReplayButtonClicked);

        TransformRefs["OffCamToggle"]?.GetComponent<Toggle>()?.onValueChanged?.AddListener((System.Action<bool>)OnOffCamToggled);
        TransformRefs["OrbitCamToggle"]?.GetComponent<Toggle>()?.onValueChanged?.AddListener((System.Action<bool>)OnOrbitCamToggled);
        TransformRefs["FlyCamToggle"]?.GetComponent<Toggle>()?.onValueChanged?.AddListener((System.Action<bool>)OnFlyCamToggled);
        TransformRefs["POVCamToggle"]?.GetComponent<Toggle>()?.onValueChanged?.AddListener((System.Action<bool>)OnPOVCamToggled);

        TransformRefs["FOVInput"]?.GetComponent<TMP_InputField>().onEndEdit?.AddListener((System.Action<string>)OnFOVInputEdited);
        TransformRefs["CinematicToggle"]?.GetComponent<Toggle>()?.onValueChanged?.AddListener((System.Action<bool>)OnCinematicToggled);
        TransformRefs["OrthographicToggle"]?.GetComponent<Toggle>()?.onValueChanged?.AddListener((System.Action<bool>)OnOrthographicToggled);

        TransformRefs["PrevFrameButton"]?.GetComponent<Button>().onClick?.AddListener((System.Action)OnPrevFrameButtonClicked);
        TransformRefs["PlayButton"]?.GetComponent<Button>().onClick?.AddListener((System.Action)OnPlayButtonClicked);
        TransformRefs["PlayReverseButton"]?.GetComponent<Button>().onClick?.AddListener((System.Action)OnPlayReverseButtonClicked);
        TransformRefs["NextFrameButton"]?.GetComponent<Button>().onClick?.AddListener((System.Action)OnNextFrameButtonClicked);
        TransformRefs["PauseButton"]?.GetComponent<Button>().onClick?.AddListener((System.Action)OnPauseButtonClicked);

        TransformRefs["FineFastButton"]?.GetComponent<Button>().onClick?.AddListener((System.Action)OnFineFastClicked);
        TransformRefs["CoarseFastButton"]?.GetComponent<Button>().onClick?.AddListener((System.Action)OnCoarseFastClicked);
        TransformRefs["SpeedInput"]?.GetComponent<TMP_InputField>().onEndEdit?.AddListener((System.Action<string>)OnSpeedInputEdited);
        TransformRefs["FineSlowButton"]?.GetComponent<Button>().onClick?.AddListener((System.Action)OnFineSlowClicked);
        TransformRefs["CoarseSlowButton"]?.GetComponent<Button>().onClick?.AddListener((System.Action)OnCoarseSlowClicked);

        TransformRefs["DurationInput"]?.GetComponent<TMP_InputField>().onEndEdit?.AddListener((System.Action<string>)OnDurationInputEdited);
        TransformRefs["FrameInput"]?.GetComponent<TMP_InputField>().onEndEdit?.AddListener((System.Action<string>)OnFrameInputEdited);

        TransformRefs["StopReplayButton"]?.GetComponent<Button>().onClick?.AddListener((System.Action)OnStopReplayButtonClicked);
        TransformRefs["ExitMapButton"]?.GetComponent<Button>().onClick?.AddListener((System.Action)OnExitMapButtonClicked);

        TransformRefs["MinimizeButton"]?.GetComponent<Button>().onClick?.AddListener((System.Action)OnMinimizeButtonClicked);
        TransformRefs["MaximizeButton"]?.GetComponent<Button>().onClick?.AddListener((System.Action)OnMaximizeButtonClicked);
        TransformRefs["HideButton"]?.GetComponent<Button>().onClick?.AddListener((System.Action)OnHideButtonClicked);

        TransformRefs["PrevPOVButton"]?.GetComponent<Button>().onClick?.AddListener((System.Action)OnPrevPOVButtonClicked);
        TransformRefs["NextPOVButton"]?.GetComponent<Button>().onClick?.AddListener((System.Action)OnNextPOVButtonClicked);
    }
    static void AddComponents()
    {
        TransformRefs["Viewport"].gameObject.AddComponent<MouseDetector>();
        TransformRefs["Timeline"].gameObject.AddComponent<TimelineController>();
    }

    static void OnNextReplayClicked()
    {
        ReplayFiles.NextReplay();
    }

    static void OnPreviousReplayClicked()
    {
        ReplayFiles.PreviousReplay();
    }

    static void OnPlayReplayButtonClicked()
    {
        Core.ReplayModMain.LoadSelectedReplay();
    }

    static void OnRenameButtonClicked()
    {
        
    }

    static void OnCopyPathButtonClicked()
    {
        //GUIUtility.systemCopyBuffer = ReplayFiles.explorer.CurrentReplayPath;

        var path = ReplayFiles.explorer.CurrentReplayPath;
        if (!string.IsNullOrEmpty(path))
        {
            System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{path}\"");
        }
    }

    static void OnOffCamToggled(bool toggleState)
    {
        CameraController.DisableCamera();
    }
    static void OnOrbitCamToggled(bool toggleState)
    {
        CameraController.SetCameraMode(CameraController.CameraMode.Orbit);
    }
    static void OnFlyCamToggled(bool toggleState)
    {
        CameraController.SetCameraMode(CameraController.CameraMode.Fly);
    }

    static void OnPOVCamToggled(bool toggleState)
    {
        CameraController.SetCameraMode(CameraController.CameraMode.POV);
    }

    static void OnFOVInputEdited(string input)
    {
        if (float.TryParse(input, out float fov))
        {
            if (!CameraController.IsOrthographic)
            {
                if (!ViewingOtherPOV)
                    CameraController.CameraFOV = fov;
                else
                    RecordingCamera.instance.fovSlider.value = fov;
            }
            else
            {
                CameraController.CameraSize = fov;
            }
        }
    }

    static void OnCinematicToggled(bool toggleState)
    {
        CameraController.CinematicMode = toggleState;
    }

    static void OnOrthographicToggled(bool toggleState)
    {
        CameraController.IsOrthographic = toggleState;
    }

    static void OnDeleteReplayButtonClicked()
    {
        if (ReplayFiles.explorer.currentIndex != -1)
        {
            if (Core.ReplayModMain.crystalBreakCoroutine == null)
            {
                ReplayMod.Replay.UI.ReplayCrystals.Crystal crystal = ReplayMod.Replay.UI.ReplayCrystals.Crystals.FirstOrDefault(c => c.ReplayPath == ReplayFiles.explorer.CurrentReplayPath);
                Core.ReplayModMain.crystalBreakCoroutine = MelonCoroutines.Start(ReplayMod.Replay.UI.ReplayCrystals.CrystalBreakAnimation(ReplayFiles.explorer.CurrentReplayPath, crystal));
            }
        }
        else
        {
            ReplayMod.Core.Main.ReplayError();
        }
    }

    static void OnReplaySelected(ReplayExplorer.Entry entry, string _)
    {
        var count = ReplayFiles.explorer.currentReplayEntries.Count(e => !e.IsFolder);
        var index = ReplayFiles.explorer.currentIndex;
        var shownIndex = index < 0
             ? 0
             : ReplayFiles.explorer.currentReplayEntries.Take(index + 1).Count(e => !e.IsFolder);

        if (entry == null || index < 0)
        {
            TransformRefs["ReplayMetadataText"].GetComponent<TextMeshProUGUI>().text = "";
            TransformRefs["ReplayName"].GetComponent<TextMeshProUGUI>().text = "No Replay Selected";
            TransformRefs["ReplayIndex"].GetComponent<TextMeshProUGUI>().text = "";
            TransformRefs["TimelinePreview"]?.GetComponent<TimelinePreview>()?.ClearMarkers();
        }
        else
        {
            TransformRefs["ReplayMetadataText"].GetComponent<TextMeshProUGUI>().text = ReplayAPI.FormatReplayTemplate(ReplayFiles.GetMetadataFormat(entry.header.Scene), entry.header);
            TransformRefs["ReplayName"].GetComponent<TextMeshProUGUI>().text = entry.header.Title;
            TransformRefs["ReplayIndex"].GetComponent<TextMeshProUGUI>().text = $"{shownIndex} / {count}";
        }

        TimelinePreview timelinePreview = TransformRefs["TimelinePreview"].GetComponent<TimelinePreview>();
        if (timelinePreview == null)
            timelinePreview = TransformRefs["TimelinePreview"].gameObject.AddComponent<TimelinePreview>();
        timelinePreview.InitializeMarkers(entry.header);
    }

    public static void OnPrevFrameButtonClicked()
    {
        ReplayAPI.Seek(ReplayAPI.CurrentTime - 1 / Core.FPS);
    }

    public static void OnPlayButtonClicked()
    {
        ReplayMod.Core.Main.Playback.playbackSpeed = Mathf.Abs(ReplayMod.Core.Main.Playback.playbackSpeed);
        ReplayMod.Core.Main.Playback.TogglePlayback(true);
    }

    public static void OnPlayReverseButtonClicked()
    {
        ReplayMod.Core.Main.Playback.playbackSpeed = Mathf.Abs(ReplayMod.Core.Main.Playback.playbackSpeed) * -1f;
        ReplayMod.Core.Main.Playback.TogglePlayback(true);
    }

    public static void OnPauseButtonClicked()
    {
        ReplayMod.Core.Main.Playback.TogglePlayback(false);
    }

    public static void OnNextFrameButtonClicked()
    {
        ReplayAPI.Seek(ReplayAPI.CurrentTime + 1 / Core.FPS);
    }

    static void OnFineFastClicked()
    {
        ReplayMod.Core.Main.Playback.playbackSpeed += 0.1f;
    }
    static void OnCoarseFastClicked()
    {
        ReplayMod.Core.Main.Playback.playbackSpeed += 1f;
    }

    static void OnSpeedInputEdited(string input)
    {
        if (float.TryParse(input, out float speed))
        {
            ReplayMod.Core.Main.Playback.playbackSpeed = speed;
        }
    }

    static void OnFineSlowClicked()
    {
        ReplayMod.Core.Main.Playback.playbackSpeed -= 0.1f;
    }

    static void OnCoarseSlowClicked()
    {
        ReplayMod.Core.Main.Playback.playbackSpeed -= 1f;
    }

    static void OnDurationInputEdited(string input)
    {
        string[] parts = input.Split(':');
        TimeSpan timeSpan;

        if (parts.Length == 1 && int.TryParse(parts[0], out int seconds)) // ss
        {
            timeSpan = TimeSpan.FromSeconds(seconds);
        }
        else if (parts.Length == 2 &&
                 int.TryParse(parts[0], out int minutes) &&
                 int.TryParse(parts[1], out seconds)) // mm:ss
        {
            timeSpan = new TimeSpan(0, 0, minutes, seconds);
        }
        else if (parts.Length == 3 &&
                 int.TryParse(parts[0], out int hours) &&
                 int.TryParse(parts[1], out minutes) &&
                 int.TryParse(parts[2], out seconds)) // mm:ss
        {
            timeSpan = new TimeSpan(0, 0, minutes, seconds);
        }
        else
        {
            return;
        }

            float duration = (float)timeSpan.TotalSeconds;
        ReplayAPI.Seek(duration);
    }

    static void OnFrameInputEdited(string input)
    {
        if (float.TryParse(input, out float frame))
        {
            ReplayAPI.Seek(frame / Core.FPS);
        }
    }

    static void OnStopReplayButtonClicked()
    {
        ReplayMod.Replay.ReplayAPI.Stop();
    }

    static void OnExitMapButtonClicked()
    {
        MelonCoroutines.Start(Utilities.LoadMap(1));
        CameraController.DisableCamera();
    }

    static void OnMinimizeButtonClicked()
    {
        SetWindowType(WindowMode.Minimized);
    }

    static void OnMaximizeButtonClicked()
    {
        SetWindowType(WindowMode.Maximized);
    }

    static void OnHideButtonClicked()
    {
        SetWindowType(WindowMode.Hidden);
    }

    static void OnPrevPOVButtonClicked()
    {
        CameraController.SelectPOVPlayer(true);
    }

    static void OnNextPOVButtonClicked()
    {
        CameraController.SelectPOVPlayer(false);
    }

    public static void UpdateSpeedInput()
    {
        if (ReplayMod.Core.Main.Playback?.playbackSpeed != null && TransformRefs.ContainsKey("SpeedInput"))
        {
            TMP_InputField field = TransformRefs["SpeedInput"]?.GetComponent<TMP_InputField>();
            if (field == null || field.isFocused) return;

            float roundedSpeed = Mathf.Round(ReplayMod.Core.Main.Playback.playbackSpeed * 100) / 100;
            field.SetTextWithoutNotify(roundedSpeed.ToString("G"));
        }
    }

    public static void UpdatePlayPause()
    {
        if (ReplayMod.Core.Main.Playback?.isPaused != null && TransformRefs.ContainsKey("PauseButton"))
        {
            TransformRefs["PauseButton"]?.gameObject?.SetActive(!ReplayMod.Core.Main.Playback.isPaused);
        }
    }

    public static void UpdateFOVInput()
    {
        if (TransformRefs.ContainsKey("FOVInput"))
        {
            TMP_InputField field = TransformRefs["FOVInput"]?.GetComponent<TMP_InputField>();
            if (field == null || field.isFocused) return;

            if (!CameraController.IsOrthographic)
            {
                if (ViewingOtherPOV)
                {
                    float legacyFOV = RecordingCamera.instance.fovSlider.value;
                    field.SetTextWithoutNotify(legacyFOV.ToString("0.##"));
                }
                else
                {
                    field.SetTextWithoutNotify(CameraController.CameraFOV.ToString("0.##"));
                }
            }
            else
            {
                field.SetTextWithoutNotify(CameraController.CameraSize.ToString("0.##"));
            }

            if (CameraController.IsOrthographic)
                TransformRefs["FOVText"].GetComponent<TextMeshProUGUI>().text = "Size";
            else
                TransformRefs["FOVText"].GetComponent<TextMeshProUGUI>().text = "FOV";
        }
    }

    public static void UpdatePOVSelector()
    {
        RectTransform povPanel = TransformRefs["POVSelector"].GetComponent<RectTransform>();

        if (CameraController.CurrentCameraMode is CameraController.CameraMode.POV && CameraController.IsCameraEnabled)
        {
            Vector2 pos = povPanel.anchoredPosition;
            pos.y = Mathf.Lerp(pos.y, -157f, 0.5f);
            povPanel.anchoredPosition = pos;

            TransformRefs["POVPlayerText"].GetComponent<TextMeshProUGUI>().text = ReplayPlayback.povPlayer.Data.GeneralData.PublicUsername;
        }
        else
        {
            Vector2 pos = povPanel.anchoredPosition;
            pos.y = Mathf.Lerp(pos.y, -95f, 0.5f);
            povPanel.anchoredPosition = pos;
        }
    }

    public static void HandleWindow()
    {
        if (PopoutHoverMD == null) return;

        RectTransform rct = TransformRefs["UIOptions"]?.GetComponent<RectTransform>();
        if (rct != null)
        {
            if (CurrentWindowMode is WindowMode.Hidden)
            {
                Vector2 pos = rct.anchoredPosition;
                if (PopoutHoverMD.IsHovering)
                    pos.x = Mathf.Lerp(pos.x, -50f, 0.3f);
                else
                    pos.x = Mathf.Lerp(pos.x, 5f, 0.3f);
                rct.anchoredPosition = pos;
            }
            else if (CurrentWindowMode is WindowMode.Maximized or WindowMode.Minimized)
            {
                Vector2 pos = rct.anchoredPosition;
                    pos.x = Mathf.Lerp(pos.x, -100f, 0.3f);
                rct.anchoredPosition = pos;
            }
        }
    }

    //public static void HandleSelectPOV()
    //{
    //    if (!SelectingPOV) return;

    //    CameraController.SnapLegacyCam();
    //    Ray ray = CameraController.LegacyCamRef.ScreenPointToRay(Input.mousePosition);
    //    if (Physics.Raycast(ray, out RaycastHit hit, 100f, LayerMask.GetMask("PlayerHitbox")))
    //    {
    //        PlayerController player = hit.collider.GetComponentInParent<PlayerController>();
    //        if (player != null)
    //        {
    //            if (selectPOVHit != player)
    //            {
    //                Tooltip.ShowOrSetText(player.assignedPlayer.Data.GeneralData.PublicUsername);
    //            }
    //            selectPOVHit = player;
    //        }
    //    }
    //    else
    //    {
    //        if (selectPOVHit != null)
    //        {
    //            Tooltip.Hide();
    //        }
    //        selectPOVHit = null;
    //    }

    //    if (Input.GetMouseButtonDown(0))
    //    {
    //        SelectingPOV = false;
    //        Tooltip.Hide();
    //        UIManager.SetWindowType(WindowMode.Maximized);

    //        if (selectPOVHit != null)
    //        {
    //            ReplayMod.Core.Main.Playback.UpdateReplayCameraPOV(selectPOVHit.assignedPlayer);
    //            CameraController.DisableCamera();
    //            CameraController.SetPlayer(false);
    //        }
    //    }
    //}

    public static void SetWindowType(WindowMode windowMode)
    {
        if (windowMode is not WindowMode.Maximized) MelonCoroutines.Start(fadeOut());
        if (windowMode is WindowMode.Maximized) MelonCoroutines.Start(fadeIn());
        TransformRefs["MaximizeButton"].gameObject.SetActive(windowMode is not WindowMode.Maximized);

        if (windowMode is not WindowMode.Hidden)
            RecordingCamera.Instance?.OnLegacyRecordingCameraEnabledChanged(true);

        Core.DesktopMode = windowMode is not WindowMode.Hidden;

        _currentWindowMode = windowMode;

        IEnumerator fadeOut()
        {
            //CanvasGroup cg = TransformRefs["Editor"].GetComponent<CanvasGroup>();
            //for (int i = 0; i < 3; i ++)
            //{
            //    cg.alpha = 1 - i / 3f;
            //    yield return null;
            //}
            TransformRefs["Editor"].gameObject.SetActive(false);
            yield break;
        }

        IEnumerator fadeIn()
        {
            TransformRefs["Editor"].gameObject.SetActive(true);
            //CanvasGroup cg = TransformRefs["Editor"].GetComponent<CanvasGroup>();
            //for (int i = 0; i <= 3; i++)
            //{
            //    cg.alpha = i / 3f;
            //    yield return null;
            //}
            yield break;
        }
    }
}
