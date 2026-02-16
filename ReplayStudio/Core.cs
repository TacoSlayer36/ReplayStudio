using Il2CppPlayFab.ClientModels;
using Il2CppRUMBLE.Managers;
using Il2CppRUMBLE.Players;
using Il2CppTMPro;
using MelonLoader;
using UnityEngine;
using static ReplayStudio.CameraController;
using ReplayMod;

namespace ReplayStudio
{
    /// <summary>
    /// Contains anything that runs universally as well as overrides for game events
    /// </summary>
    public class Core : MelonMod
    {
        /// <summary> Melon singleton for this mod </summary>
        public static Core Instance;
        /// <summary> Melon singleton for ReplayMod </summary>
        public static ReplayMod.Main ReplayModMain;

        internal bool GlobalInit = false;
        internal int ActiveScene = 0;
        internal bool IsSceneReady = false;

        /// <summary> The mod's parent Game Object in DontDestroyOnLoad </summary>
        public GameObject DDOL_GameObjects;

        internal static PlayerController LocalPlayerRef => PlayerManager.Instance.localPlayer.Controller;

        /// <summary></summary>
        public override void OnLateInitializeMelon()
        {
            Instance = this;
            ReplayModMain = Melon<ReplayMod.Main>.Instance;
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

            if (Input.GetKeyDown(KeyCode.T))
            {
                if (!CameraController.IsCameraEnabled) CameraController.EnableCamera(CameraMode.Fly);
                else if (CameraController.CurrentCameraMode == CameraMode.Fly) CameraController.EnableCamera(CameraMode.Orbit);
                else CameraController.DisableCamera();
            }

            CameraController.HandleCamera();
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
        }

        internal void RunGlobalInit()
        {
            GlobalInit = true;
            DDOL_GameObjects = GameObject.Instantiate(GameObjectManager.LoadAssetFromStream<GameObject>(this, "ReplayStudio.assets.replaystudio", "ReplayStudio"));
            GameObject.DontDestroyOnLoad(DDOL_GameObjects);

            Transform uiRoot = DDOL_GameObjects.transform.Find("Canvas");
            UIManager.SetUpUI(uiRoot);

            InitializeCamera();
        }
    }
}
