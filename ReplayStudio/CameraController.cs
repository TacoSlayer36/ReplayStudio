using System.Collections.Generic;
using static ReplayStudio.HelperFunctions;
using UnityEngine;
using Il2CppRUMBLE.Utilities;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Il2CppRUMBLE.Managers;

namespace ReplayStudio;

/// <summary>
/// Control a disembodied camera from your desktop. The actual rendering is done by the Legacy Camera
/// </summary>
public static class CameraController
{
    /// <summary> Whether to snap the legacy camera to the disembodied location or not </summary>
    public static bool IsCameraEnabled = false;
    /// <summary> FOV to force the Legacy Camera into while it's being snapped to CameraTransform </summary>
    public static float CameraFOV = 75f; // TODO
    /// <summary> The active way the camera is controlled </summary>
    public static CameraMode CurrentCameraMode = CameraMode.Orbit;
    /// <summary> Two camera control types </summary>
    public enum CameraMode
    {
        /// <summary> The camera will be moved with an orbiting style, similar to Blender </summary>
        Orbit,
        /// <summary> The camera will be moved with a flight style, similar to Minecraft creative mode </summary>
        Fly
    }

    /// <summary> The Transform on the Game Object representing the transform of the disembodied camera </summary>
    public static Transform CameraTransform;
    /// <summary> The camera's speed multiplier; to be applied after frame calculations </summary>
    public static float CameraSpeedMult = 10f; // TODO: Make this configurable
    /// <summary> The location of the camera's origin for Orbit mode </summary>
    public static Vector3 OrbitCamFocus = Vector3.zero;
    /// <summary> The camera's distance from its rotation origin for Orbit mode </summary>
    public static float OrbitCamDist = 5f;
    /// <summary> The camera's move speed for Fly mode, taking framerate into account </summary>
    static float flyCamSpeed => Time.deltaTime * CameraSpeedMult;

    public static bool CinematicMode = false;

    /// <summary> Whether the camera in Fly mode is panning </summary>
    static bool isDraggingCam = false;

    public static Camera LegacyCamRef => RecordingCamera.Instance?.LegacyCamera;
    public static AudioListener LegacyCamListener;

    static Vector3 PlayerPos;
    static Quaternion PlayerRot;

    /// <summary> Create the single disembodied camera to view the replay through </summary>
    /// <param name="mode">Sets the camera to this mode and enables it</param>
    public static void InitializeCamera(CameraMode? mode = null)
    {
        if (CameraTransform != null) RemoveCamera(); // This is a singleton

        GameObject cameraGo = new GameObject("DesktopCam");
        CameraTransform = cameraGo.transform;
        CameraTransform.SetParent(Core.DDOL_GameObjects.transform, true);
        CameraTransform.position = Vector3.zero; // TODO
        CameraTransform.rotation = Quaternion.identity; // TODO

        if (mode != null)
            EnableCamera(mode);
        else
            DisableCamera();

        LegacyCamListener = LegacyCamRef?.gameObject?.GetComponent<AudioListener>();
        if (LegacyCamListener == null)
        {
            LegacyCamListener = LegacyCamRef?.gameObject?.AddComponent<AudioListener>();
        }
    }

    /// <summary> Remove the single disembodied camera </summary>
    public static void RemoveCamera()
    {
        if (CameraTransform == null) return;

        GameObject.Destroy(CameraTransform.gameObject);
        CameraTransform = null;
    }

    /// <summary> Run by Core.OnUpdate; branches to different control types or does nothing if the camera is off </summary>
    public static void HandleCamera()
    {
        if (CameraTransform == null) return;
        if (IsCameraEnabled == false) return;

        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
            isDraggingCam = true;
        }
        if (Mouse.current.rightButton.wasReleasedThisFrame)
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            isDraggingCam = false;
        }

        else if (CurrentCameraMode == CameraMode.Orbit) HandleOrbitCam();
        else if (CurrentCameraMode == CameraMode.Fly) HandleFlyCam();

        CameraTransform.localRotation = Quaternion.Euler(CameraTransform.localEulerAngles.x, CameraTransform.localEulerAngles.y, 0f);
    }

    static void HandleOrbitCam()
    {
        // TODO: I'm sure you can see what's wrong here
        List<KeyCode> PanKeys = new List<KeyCode> { KeyCode.LeftShift, KeyCode.RightShift };

        Vector3 desiredPos = OrbitCamFocus - CameraTransform.forward * OrbitCamDist;
        Quaternion desiredRot = CameraTransform.rotation;

        if (isDraggingCam)
        {
            float mouseX = Input.GetAxis("Mouse X");
            float mouseY = Input.GetAxis("Mouse Y");

            if (IsPressing(PanKeys))
            {
                OrbitCamFocus += -CameraTransform.right * mouseX * OrbitCamDist * 0.025f;
                OrbitCamFocus += -CameraTransform.up * mouseY * OrbitCamDist * 0.025f;
            }
            else
            {
                CameraTransform.RotateAround(OrbitCamFocus, CameraTransform.up, mouseX * 7.5f);
                CameraTransform.RotateAround(OrbitCamFocus, -CameraTransform.right, mouseY * 7.5f);
                desiredRot = CameraTransform.rotation;
            }
        }

        float scrollDelta = OrbitCamDist - (Input.mouseScrollDelta.y * OrbitCamDist * 0.2f);
        OrbitCamDist = Mathf.Clamp(scrollDelta, 0.1f, 100f);
        desiredPos = OrbitCamFocus - CameraTransform.forward * OrbitCamDist;

        CameraTransform.position = desiredPos;
        CameraTransform.rotation = desiredRot;
    }

    static void HandleFlyCam()
    {
        // TODO: I'm sure you can see what's wrong here
        List<KeyCode> ForwardKeys = new List<KeyCode> { KeyCode.W, KeyCode.UpArrow };
        List<KeyCode> BackwardKeys = new List<KeyCode> { KeyCode.S, KeyCode.DownArrow };
        List<KeyCode> RightKeys = new List<KeyCode> { KeyCode.D, KeyCode.RightArrow };
        List<KeyCode> LeftKeys = new List<KeyCode> { KeyCode.A, KeyCode.LeftArrow };
        List<KeyCode> UpKeys = new List<KeyCode> { KeyCode.E, KeyCode.RightControl };
        List<KeyCode> DownKeys = new List<KeyCode> { KeyCode.Q, KeyCode.RightShift };
        List<KeyCode> SprintKeys = new List<KeyCode> { KeyCode.LeftControl, KeyCode.Return };

        float sprintMult = IsPressing(SprintKeys) ? 2.25f : 1f; // TODO: Make this configurable

        Vector3 moveDir = Vector3.zero;

        // Lateral movement is based in local space
        if (IsPressing(ForwardKeys))
            moveDir += CameraTransform.transform.forward;

        if (IsPressing(BackwardKeys))
            moveDir += -CameraTransform.transform.forward;

        if (IsPressing(RightKeys))
            moveDir += CameraTransform.transform.right;

        if (IsPressing(LeftKeys))
            moveDir += -CameraTransform.transform.right;

        Vector3 lateralMoveDir = new Vector3(moveDir.x, 0f, moveDir.z).normalized;

        // Vertical movement is based in world space
        float verticalMoveAmount = 0f;

        if (IsPressing(UpKeys))
            verticalMoveAmount += 1f;

        if (IsPressing(DownKeys))
            verticalMoveAmount += -1f;

        Vector3 combinedPosDelta = lateralMoveDir * flyCamSpeed * sprintMult
                                  + Vector3.up * verticalMoveAmount * flyCamSpeed * sprintMult;

        Quaternion combinedRotDelta = Quaternion.identity;
        if (isDraggingCam)
        {
            float mouseX = Input.GetAxis("Mouse X") * 10f; // TODO: Make this configurable
            float mouseY = Input.GetAxis("Mouse Y") * 10f; // TODO: Make this configurable

            combinedRotDelta *= Quaternion.Euler(Vector3.left * mouseY);
            combinedRotDelta *= Quaternion.Euler(Vector3.up * mouseX);
        }

        if (CinematicMode)
        {

        }
        else
        {
            CameraTransform.position += combinedPosDelta;
            CameraTransform.rotation *= combinedRotDelta;
        }
    }

    /// <summary> Enable the disembodied camera </summary>
    /// <param name="mode">Optional camera mode specification</param>
    public static void EnableCamera(CameraMode? mode = null)
    {
        IsCameraEnabled = true;

        if (mode != null)
            SetCameraMode((CameraMode)mode);
    }

    /// <summary> Disable the disembodied camera </summary>
    public static void DisableCamera()
    {
        if (CameraTransform == null)
            throw new System.Exception("Desktop Camera is not initialized");

        IsCameraEnabled = false;
        cameraDataStorage.ApplyData(LegacyCamRef, true);

        updateCameraModeUI();

        SetPlayer(true);
    }

    /// <summary> Set the camera's active control system </summary>
    /// <param name="mode">Camera mode to set</param>
    public static void SetCameraMode(CameraMode mode)
    {
        cameraDataStorage.StoreData(LegacyCamRef, false);
        LegacyCamRef.useOcclusionCulling = false;

        CurrentCameraMode = mode;
        IsCameraEnabled = true;
        isDraggingCam = false;
        updateCameraModeUI();

        SetPlayer(false);
    }

    public static void SetPlayer(bool enabled)
    {
        if (PlayerManager.Instance?.LocalPlayer?.Controller?.gameObject == null || LegacyCamListener == null) return;

        PlayerManager.Instance.LocalPlayer.Controller.gameObject.SetActive(enabled);
        LegacyCamListener.enabled = !enabled;
    }

    private static void updateCameraModeUI()
    {
        if (!Core.Instance.GlobalInit) return;

        Toggle offToggle = UIManager.TransformRefs["OffCamToggle"].GetComponent<Toggle>();
        Toggle orbitToggle = UIManager.TransformRefs["OrbitCamToggle"].GetComponent<Toggle>();
        Toggle flyToggle = UIManager.TransformRefs["FlyCamToggle"].GetComponent<Toggle>();

        offToggle.SetIsOnWithoutNotify(false);
        orbitToggle.SetIsOnWithoutNotify(false);
        flyToggle.SetIsOnWithoutNotify(false);

        if (IsCameraEnabled && CurrentCameraMode is CameraMode.Orbit) orbitToggle.SetIsOnWithoutNotify(true);
        else if (IsCameraEnabled && CurrentCameraMode is CameraMode.Fly) flyToggle.SetIsOnWithoutNotify(true);
        else offToggle.SetIsOnWithoutNotify(true);
    }

    /// <summary>
    /// Copy the pos/rot from the CameraTransform to the Legacy Camera
    /// To be run via a harmony patch before the Legacy Camera renders
    /// </summary>
    public static void SnapLegacyCam()
    {
        LegacyCamRef.transform.position = CameraTransform.position;
        LegacyCamRef.transform.rotation = CameraTransform.rotation;
        LegacyCamRef.fieldOfView = CameraFOV;
    }

    /// <summary>
    /// Stores the data of the Legacy Camera before modifying it
    /// </summary>
    internal static class cameraDataStorage
    {
        private static float fieldOfView = 90f;
        private static LayerMask cullingMask = ~0;
        private static bool useOcclusionCulling = true;
        private static bool isDataStored = false;

        /// <summary>
        /// Store the input camera's data
        /// </summary>
        /// <param name="camera">The camera to store the data from</param>
        /// <param name="overrideData">Determines if existing stored data is overriden</param>
        public static void StoreData(Camera camera, bool overrideData)
        {
            if (isDataStored && !overrideData) return;

            fieldOfView = camera.fieldOfView;
            cullingMask = camera.cullingMask;
            useOcclusionCulling = camera.useOcclusionCulling;

            isDataStored = true;
        }

        /// <summary>
        /// Apply the stored data onto the input camera
        /// </summary>
        /// <param name="camera">The camera to apply the data to</param>
        /// <param name="clearData">Determines if the stored data is cleared</param>
        public static void ApplyData(Camera camera, bool clearData)
        {
            if (!isDataStored) return;
            if (camera == null)
                throw new System.Exception("Camera is null");

            camera.fieldOfView = fieldOfView;
            camera.cullingMask = cullingMask;
            camera.useOcclusionCulling = useOcclusionCulling;

            if (clearData) isDataStored = false;
        }

        /// <summary>
        /// Sets the flag that determines if data is already stored to false
        /// </summary>
        public static void ClearData()
        {
            isDataStored = false;
        }
    }
}