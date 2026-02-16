using Il2CppTMPro;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ReplayMod;
using MelonLoader;
using System.Linq;
using Il2CppPlayFab.ClientModels;

namespace ReplayStudio
{
    internal static class UIManager
    {
        public static Dictionary<string, Transform> TransformRefs = new();

        public static void SetUpUI(Transform uiRoot)
        {
            FetchRefsInChildren(uiRoot);
            AddListeners();
            foreach (var tmpugui in uiRoot.GetComponentsInChildren<TextMeshProUGUI>(true))
                tmpugui.font = Resources.Load<TMP_FontAsset>("Fonts & Materials/Arial SDF");
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
            TransformRefs["NextReplay"].GetComponent<Button>()?.onClick.AddListener((System.Action)OnNextReplayClicked);
            TransformRefs["PreviousReplay"].GetComponent<Button>()?.onClick.AddListener((System.Action)OnPreviousReplayClicked);
            ReplayAPI.ReplaySelected += OnReplaySelected;
            TransformRefs["PlayButton"].GetComponent<Button>()?.onClick.AddListener((System.Action)OnPlayButtonClicked);
            
            TransformRefs["CopyPathButton"].GetComponent<Button>()?.onClick.AddListener((System.Action)OnCopyPathButtonClicked);
            TransformRefs["DeleteReplayButton"].GetComponent<Button>()?.onClick.AddListener((System.Action)OnDeleteReplayButtonClicked);
        }

        static void OnNextReplayClicked()
        {
            ReplayFiles.NextReplay();
        }

        static void OnPreviousReplayClicked()
        {
            ReplayFiles.PreviousReplay();
        }

        static void OnPlayButtonClicked()
        {
            Core.ReplayModMain.LoadSelectedReplay();
        }

        static void OnCopyPathButtonClicked()
        {
            GUIUtility.systemCopyBuffer = ReplayFiles.explorer.CurrentReplayPath;
        }

        static void OnDeleteReplayButtonClicked()
        {
            if (ReplayFiles.explorer.currentIndex != -1)
            {
                if (Core.ReplayModMain.crystalBreakCoroutine == null)
                {
                    ReplayCrystals.Crystal crystal = ReplayCrystals.Crystals.FirstOrDefault(c => c.ReplayPath == ReplayFiles.explorer.CurrentReplayPath);
                    Core.ReplayModMain.crystalBreakCoroutine = MelonCoroutines.Start(ReplayCrystals.CrystalBreakAnimation(ReplayFiles.explorer.CurrentReplayPath, crystal));
                }
            }
            else
            {
                Core.ReplayModMain.ReplayError();
            }
        }

        static void OnReplaySelected(ReplaySerializer.ReplayHeader replayHeader)
        {
            if (replayHeader == null)
            {
                TransformRefs["ReplayMetadataText"].GetComponent<TextMeshProUGUI>().text = "";
                TransformRefs["ReplayName"].GetComponent<TextMeshProUGUI>().text = "No Replay Selected";
            }
            else
            {
                TransformRefs["ReplayMetadataText"].GetComponent<TextMeshProUGUI>().text = ReplaySerializer.FormatReplayString(ReplayFiles.GetMetadataFormat(replayHeader.Scene), replayHeader);
                TransformRefs["ReplayName"].GetComponent<TextMeshProUGUI>().text = replayHeader.Title;
            }

            int shownIndex = ReplayFiles.explorer.currentIndex < 0 ? 0 : ReplayFiles.explorer.currentIndex + 1;
            string indexString = $"({shownIndex} / {ReplayFiles.explorer.currentReplayPaths.Count})";
            TransformRefs["ReplayIndex"].GetComponent<TextMeshProUGUI>().text = indexString;
        }
    }
}
