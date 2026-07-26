using ReplayStudio.Components;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ReplayStudio
{
    public static class CameraController
    {
        public static Camera Camera;   
        public static AudioListener AudioListener;
        public static KeyframedObject KeyframeComponent;


        public static bool Enabled = false;

        public static void InitializeCamera()
        {
            if (Camera != null) RemoveCamera(); // This is a singleton

            GameObject cameraGo = new GameObject("RenderCam");

            Camera = cameraGo.AddComponent<Camera>();
            AudioListener = cameraGo.AddComponent<AudioListener>();
            KeyframeComponent = cameraGo.AddComponent<KeyframedObject>();
            Core.StudioData.CameraKeyframeComponent = KeyframeComponent;
            Camera.GetUniversalAdditionalCameraData().allowXRRendering = false;

            Camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            Camera.transform.SetParent(Core.DDOL_GameObjects.transform, true);
            Camera.transform.position = Vector3.zero; // TODO
            Camera.transform.rotation = Quaternion.identity; // TODO

            Camera.enabled = Enabled;
            AudioListener.enabled = Enabled;
        }

        public static void RemoveCamera()
        {
            if (Camera == null) return;

            GameObject.Destroy(Camera.gameObject);
            Camera = null;
        }

        public static void ToggleCamera(bool enabled)
        {
            if (Camera == null) return;

            Camera.enabled = enabled;
            AudioListener.enabled = enabled;
            Enabled = enabled;
        }
    }
}
