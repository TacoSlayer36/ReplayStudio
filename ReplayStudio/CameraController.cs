using System.Collections.Generic;
using static ReplayStudio.HelperFunctions;
using UnityEngine;
using Il2CppRUMBLE.Utilities;
using UnityEngine.InputSystem;

namespace ReplayStudio
{
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
        public static CameraMode CurrentCameraMode = CameraMode.Fly;
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
        public static Vector3 orbitCamFocus = Vector3.zero;
        /// <summary> The camera's distance from its rotation origin for Orbit mode </summary>
        public static float orbitCamDist = 5f;
        /// <summary> The camera's move speed for Fly mode, taking framerate into account </summary>
        static float flyCamSpeed => Time.deltaTime * CameraSpeedMult;
        /// <summary> Whether the camera in Fly mode is panning </summary>
        static bool isDraggingCam = false;

        static Camera LegacyCamRef => RecordingCamera.Instance?.LegacyCamera;


        /// <summary> Create the single disembodied camera to view the replay through </summary>
        /// <param name="mode">Sets the camera to this mode and enables it</param>
        public static void InitializeCamera(CameraMode? mode = null)
        {
            if (CameraTransform != null) RemoveCamera(); // This is a singleton

            GameObject cameraGo = new GameObject("DesktopCam");
            CameraTransform = cameraGo.transform;
            CameraTransform.SetParent(Core.Instance.DDOL_GameObjects.transform, true);
            CameraTransform.position = Vector3.zero; // TODO
            CameraTransform.rotation = Quaternion.identity; // TODO

            if (mode != null)
                EnableCamera(mode);
            else
                DisableCamera();
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

            if (isDraggingCam)
            {
                float mouseX = Input.GetAxis("Mouse X");
                float mouseY = Input.GetAxis("Mouse Y");

                if (IsPressing(PanKeys))
                {
                    orbitCamFocus += -CameraTransform.right * mouseX * orbitCamDist * 0.025f;
                    orbitCamFocus += -CameraTransform.up * mouseY * orbitCamDist * 0.025f;
                }
                else
                {
                    CameraTransform.RotateAround(orbitCamFocus, CameraTransform.up, mouseX * 7.5f); // TODO
                    CameraTransform.RotateAround(orbitCamFocus, -CameraTransform.right, mouseY * 7.5f); // TODO
                }
            }

            //float xRot = CameraTransform.eulerAngles.x;
            //if (xRot >= 90) xRot = 90;
            //if (xRot <= -90) xRot = -90;
            //CameraTransform.rotation = Quaternion.Euler(xRot, CameraTransform.eulerAngles.y, 0f);

            float scrollDelta = orbitCamDist - (Input.mouseScrollDelta.y * orbitCamDist * 0.2f);
            orbitCamDist = Mathf.Clamp(scrollDelta, 0.1f, 100f);
            CameraTransform.position = orbitCamFocus - CameraTransform.forward * orbitCamDist;
        }

        static void HandleFlyCam()
        {
            // TODO: I'm sure you can see what's wrong here
            List<KeyCode> ForwardKeys = new List<KeyCode>{ KeyCode.W, KeyCode.UpArrow };
            List<KeyCode> BackwardKeys = new List<KeyCode>{ KeyCode.S, KeyCode.DownArrow };
            List<KeyCode> RightKeys = new List<KeyCode>{ KeyCode.D, KeyCode.RightArrow };
            List<KeyCode> LeftKeys = new List<KeyCode>{ KeyCode.A, KeyCode.LeftArrow };
            List<KeyCode> UpKeys = new List<KeyCode>{ KeyCode.Space, KeyCode.RightControl };
            List<KeyCode> DownKeys = new List<KeyCode>{ KeyCode.LeftShift, KeyCode.RightShift };
            List<KeyCode> SprintKeys = new List<KeyCode>{ KeyCode.LeftControl, KeyCode.Return };

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

            CameraTransform.transform.position += lateralMoveDir * flyCamSpeed * sprintMult
                                         + Vector3.up * verticalMoveAmount * flyCamSpeed * sprintMult;

            if (isDraggingCam)
            {
                float mouseX = Input.GetAxis("Mouse X") * 10f; // TODO: Make this configurable
                float mouseY = Input.GetAxis("Mouse Y") * 10f; // TODO: Make this configurable

                CameraTransform.Rotate(Vector3.up * mouseX);
                CameraTransform.Rotate(Vector3.left * mouseY);
            }
        }

        /// <summary> Enable the disembodied camera </summary>
        /// <param name="mode">Optional camera mode specification</param>
        public static void EnableCamera(CameraMode? mode = null)
        {
            if (CameraTransform == null)
            {
                Debug.Log("Failed to enable Desktop Camera. It is not initialized", false, 2);
                return;
            }

            IsCameraEnabled = true;

            if (mode != null)
                SetCameraMode((CameraMode)mode);
        }

        /// <summary> Disable the disembodied camera </summary>
        public static void DisableCamera()
        {
            if (CameraTransform == null)
            {
                Debug.Log("Failed to enable Desktop Camera. It is not initialized", false, 2);
                return;
            }

            IsCameraEnabled = false;
        }

        /// <summary> Set the camera's active control system </summary>
        /// <param name="mode">Camera mode to set</param>
        public static void SetCameraMode(CameraMode mode)
        {
            CurrentCameraMode = mode;
            isDraggingCam = false;
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
    }
}
