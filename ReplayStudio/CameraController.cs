using ReplayMod.Replay;
using ReplayStudio.Components;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering.Universal;

namespace ReplayStudio
{
    public static class CameraController
    {
        public static Camera Camera;   
        public static AudioListener AudioListener;
        public static KeyframedObject KeyframeComponent;

        public static GameObject CameraModel;


        public static bool Enabled = false;
        public static bool DoMapping = true; // TODO: Should this ever be false?

        public static void HandleCamera()
        {
            if (DoMapping)
            {
                CameraController.MapCamera();
            }
        }

        public static void InitializeCamera()
        {
            if (Camera != null) RemoveCamera(); // This is a singleton

            GameObject cameraGo = new GameObject("RenderCam");

            Camera = cameraGo.AddComponent<Camera>();
            AudioListener = cameraGo.AddComponent<AudioListener>();
            KeyframeComponent = cameraGo.AddComponent<KeyframedObject>();
            Camera.GetUniversalAdditionalCameraData().allowXRRendering = false;

            Camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            Camera.transform.SetParent(Core.DDOL_GameObjects.transform, true);

            MoveCameraToMapStart(Core.Instance.ActiveScene);

            Camera.nearClipPlane = 0.001f;

            Camera.enabled = Enabled;
            AudioListener.enabled = Enabled;


            CameraModel = GameObject.Instantiate(Core.DDOL_GameObjects.transform.GetChild(4).gameObject);

            CameraModel.SetActive(true);
            CameraModel.transform.SetParent(Camera.transform);
            CameraModel.transform.localPosition = Vector3.zero;
            CameraModel.transform.localRotation = Quaternion.Euler(270f, 180f, 0f);
        }

        public static void RemoveCamera()
        {
            if (CameraModel != null) GameObject.Destroy(CameraModel.gameObject);
            if (Camera != null) GameObject.Destroy(Camera.gameObject);
        }

        public static void EnterCamera(bool snapView)
        {
            ViewController.StoreViewCamTransform();

            Enabled = false;
            if (Camera == null) return;

            Camera.enabled = true;
            AudioListener.enabled = true;
            Enabled = true;

            ViewController.ViewCamTransform.position = Camera.transform.position;
            ViewController.ViewCamTransform.rotation = Camera.transform.rotation;
        }

        public static void ExitCamera()
        {
            ViewController.ReapplyViewCamTransform();

            Enabled = false;
            if (Camera == null) return;

            Camera.enabled = false;
            AudioListener.enabled = false;
        }

        public static void MapCamera()
        {
            if (Camera == null) return;

            CameraController.Camera.transform.position = ViewController.LegacyCamRef.transform.position;
            CameraController.Camera.transform.rotation = ViewController.LegacyCamRef.transform.rotation;
            CameraController.Camera.fieldOfView = ViewController.ViewFOV;
        }

        public static void MoveCameraToMapStart(int map)
        {
            // TODO: Other maps
            if (map == 4) // Pit
            {
                Camera.transform.position = new Vector3(6.64f, 9.92f, 9.80f);
                Camera.transform.rotation = Quaternion.Euler(44f, 210f, 0f);
            }
            else
            {
                Camera.transform.position = Vector3.zero;
                Camera.transform.rotation = Quaternion.identity;
            }
        }
    }
}
