using System.Collections.Generic;
using static ReplayStudio.HelperFunctions;
using UnityEngine;
using Il2CppRUMBLE.Utilities;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Il2CppRUMBLE.Managers;
using Il2CppRUMBLE.Players;
using System;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ReplayStudio;

public static class ViewController
{
	public static bool IsViewCamEnabled = false;
	public static float ViewFOV = 75f;
	public static float ViewSize = 10f;
	public static ViewMode CurrentViewMode = ViewMode.Fly;
	public enum ViewMode
	{
		Orbit,
		Fly,
		Render,
		POV
	}

	static Player selectedPOVPlayer;

	public static float ViewSensitivity = 2f;

	public static Transform ViewCamTransform;


	public static Vector2 ViewRot = new Vector2(0f, 0f);

	public static Vector2 ViewRotVel;

	public static Vector3 ViewVel;

	public static float ViewPosSpeedMult = 1f;
	public static float ViewRotSpeedMult = 1f;
	public static Vector3 OrbitCamFocus = Vector3.zero;
	public static float OrbitCamDist = 5f;
	public static bool CinematicMode = false;
	public static bool IsOrthographic = false;

	public static bool DoDepthOfField = false;
	public static float DOFStrength = 1f;
	public static float DOFDistance = 5f;
	public static Volume DOFComponent;

	public static Vector3 storedViewCamPos;
	public static Vector2 storedViewRot;
	public static Vector3 storedOrbitCamFocus;
	public static float storedOrbitCamDist;
	public static float storedFOV;
	public static float storedSize;

	static bool isViewUnlocked = false;

	public static Camera LegacyCamRef => RecordingCamera.Instance?.LegacyCamera;
	public static AudioListener LegacyCamListener;

	public static void InitializeCamera(ViewMode? mode = null)
	{
		if (ViewCamTransform != null) RemoveViewCam(); // This is a singleton

		GameObject cameraGo = new GameObject("DesktopCam");
		ViewCamTransform = cameraGo.transform;
		ViewCamTransform.SetParent(Core.DDOL_GameObjects.transform, true);
		ViewCamTransform.position = Vector3.zero; // TODO
		ViewCamTransform.rotation = Quaternion.identity; // TODO

		if (mode != null)
			EnableViewCam(mode);
		else
			DisableViewCam();

		LegacyCamListener = LegacyCamRef?.gameObject?.GetComponent<AudioListener>();
		if (LegacyCamListener == null)
		{
			LegacyCamListener = LegacyCamRef?.gameObject?.AddComponent<AudioListener>();
		}

		LegacyCamRef.GetUniversalAdditionalCameraData().renderPostProcessing = true;
	}

	public static void RemoveViewCam()
	{
		if (ViewCamTransform == null) return;

		GameObject.Destroy(ViewCamTransform.gameObject);
		ViewCamTransform = null;
	}

	public static void HandleViewCam()
	{
		if (ViewCamTransform == null) return;
		if (IsViewCamEnabled == false) return;

		if (Mouse.current.rightButton.wasPressedThisFrame)
		{
			Cursor.visible = false;
			Cursor.lockState = CursorLockMode.Locked;
			isViewUnlocked = true;
		}
		if (Mouse.current.rightButton.wasReleasedThisFrame)
		{
			Cursor.visible = true;
			Cursor.lockState = CursorLockMode.None;
			isViewUnlocked = false;
		}
		else if (CurrentViewMode == ViewMode.Orbit) HandleOrbitCam();
		else if (CurrentViewMode == ViewMode.Fly) HandleFlyCam();
	}

	public static void HandleOrbitCam()
	{
		// TODO: I'm sure you can see what's wrong here
		List<KeyCode> PanKeys = new List<KeyCode> { KeyCode.LeftShift, KeyCode.RightShift };

		Vector3 desiredPos = OrbitCamFocus - ViewCamTransform.forward * OrbitCamDist;
		Quaternion desiredRot = ViewCamTransform.rotation;

		if (isViewUnlocked)
		{
			float mouseX = Input.GetAxis("Mouse X");
			float mouseY = Input.GetAxis("Mouse Y");

			if (IsPressingAny(PanKeys))
			{
				OrbitCamFocus += -ViewCamTransform.right * mouseX * OrbitCamDist * 0.025f;
				OrbitCamFocus += -ViewCamTransform.up * mouseY * OrbitCamDist * 0.025f;
			}
			else
			{
				ViewCamTransform.RotateAround(OrbitCamFocus, ViewCamTransform.up, mouseX * 7.5f);
				ViewCamTransform.RotateAround(OrbitCamFocus, -ViewCamTransform.right, mouseY * 7.5f);
				desiredRot = ViewCamTransform.rotation;
			}
		}

		float scrollDelta = OrbitCamDist - (Input.mouseScrollDelta.y * OrbitCamDist * 0.2f);
		OrbitCamDist = Mathf.Clamp(scrollDelta, 0.1f, 100f);
		desiredPos = OrbitCamFocus - ViewCamTransform.forward * OrbitCamDist;

		ViewCamTransform.position = desiredPos;
		ViewCamTransform.rotation = desiredRot;
		ViewCamTransform.localRotation = Quaternion.Euler(ViewCamTransform.localEulerAngles.x, ViewCamTransform.localEulerAngles.y, 0f);

		ViewRotVel = new Vector2(0, 0);
		ViewVel = new Vector3(0, 0, 0);
	}

	public static void HandleFlyCam()
	{
		// TODO: I'm sure you can see what's wrong here
		List<KeyCode> ForwardKeys = new List<KeyCode> { KeyCode.W, KeyCode.UpArrow };
		List<KeyCode> BackwardKeys = new List<KeyCode> { KeyCode.S, KeyCode.DownArrow };
		List<KeyCode> RightKeys = new List<KeyCode> { KeyCode.D, KeyCode.RightArrow };
		List<KeyCode> LeftKeys = new List<KeyCode> { KeyCode.A, KeyCode.LeftArrow };
		List<KeyCode> UpKeys = new List<KeyCode> { KeyCode.E, KeyCode.RightControl };
		List<KeyCode> DownKeys = new List<KeyCode> { KeyCode.Q, KeyCode.RightShift };
		List<KeyCode> SprintKeys = new List<KeyCode> { KeyCode.LeftShift, KeyCode.Return };

		float sprintMult = IsPressingAny(SprintKeys) ? 4f : 1f; // TODO: Make this configurable

		// Lateral movement is based in local space
		Vector3 moveDir = Vector3.zero;

		Vector3 forward = Quaternion.Euler(0f, ViewRot.y, 0f) * new Vector3(0, 0, 1);
		Vector3 right = Quaternion.Euler(0f, ViewRot.y, 0f) * new Vector3(1, 0, 0);

		// Vertical movement is based in world space
		float verticalMoveAmount = 0f;

		if (isViewUnlocked)
		{
			if (IsPressingAny(ForwardKeys))
				moveDir += forward;

			if (IsPressingAny(BackwardKeys) && !IsPressingAny(ShiftKeys))
				moveDir += -forward;

			if (IsPressingAny(RightKeys))
				moveDir += right;

			if (IsPressingAny(LeftKeys))
				moveDir += -right;
			if (IsPressingAny(UpKeys))
				verticalMoveAmount += 1f;

			if (IsPressingAny(DownKeys))
				verticalMoveAmount += -1f;
		}

		Vector3 lateralMoveDir = new Vector3(moveDir.x, 0f, moveDir.z).normalized;
		Vector3 moveAmount = lateralMoveDir + Vector3.up * verticalMoveAmount;

		
		if (Input.mouseScrollDelta.y != 0 && !IsPressingAny(AltKeys) )
		{
			ViewPosSpeedMult = Mathf.Pow(10, Math.Clamp(Mathf.Log(ViewPosSpeedMult, 10) + Input.mouseScrollDelta.y * 0.05f, -1, 0.75f));
			ViewRotSpeedMult = ViewPosSpeedMult * 1.2f;
		}

		float mouseX = isViewUnlocked ? Input.GetAxis("Mouse X") * ViewSensitivity * ViewRotSpeedMult : 0;
		float mouseY = isViewUnlocked ? Input.GetAxis("Mouse Y") * ViewSensitivity * ViewRotSpeedMult : 0;

		if (CinematicMode)
		{
			// rotation
			ViewRotVel.y += mouseX * 0.01f;
			ViewRotVel.x -= mouseY * 0.01f;

			ViewRot.y = (ViewRot.y + ViewRotVel.y * Time.deltaTime * 100f) % 360;
			ViewRot.x = (float)Math.IEEERemainder(ViewRot.x + ViewRotVel.x * Time.deltaTime * 100f, 360f);

			// auto decel
			float accelForce = 2f;

			ViewCamTransform.position += ViewVel * Time.deltaTime * ViewPosSpeedMult;

			if (!IsPressingAny(SprintKeys))
			{
				float decelBias = 1f - Math.Max(0, Vector3.Dot(moveAmount.normalized, ViewVel.normalized));
				ViewVel -= ViewVel.normalized * Math.Min(ViewVel.magnitude, Time.deltaTime * accelForce * decelBias * ViewPosSpeedMult);
			}

			ViewVel += moveAmount * (accelForce * sprintMult * ViewPosSpeedMult * Time.deltaTime);

			ViewRotVel /= 1 + 2 * (0.4f * ViewRotVel.magnitude + 0.8f) * Time.deltaTime * ViewRotSpeedMult;
		}
		else
		{
			ViewRotVel = new Vector2(0, 0);
			ViewVel = new Vector3(0, 0, 0);

			ViewRot.x = Math.Clamp(ViewRot.x - mouseY, -90f, 90f);
			ViewRot.y = (ViewRot.y + mouseX) % 360;

			ViewCamTransform.position += moveAmount * (sprintMult * ViewPosSpeedMult * 6f * Time.deltaTime);
		}

		ViewCamTransform.rotation = Quaternion.Euler(ViewRot.x, ViewRot.y, 0f);
	}

	public static void EnableViewCam(ViewMode? mode = null)
	{
		IsViewCamEnabled = true;

		ReplayMod.Core.Main.Playback.UpdateReplayCameraPOV(PlayerManager.Instance.LocalPlayer);

		if (mode != null)
			SetCameraMode((ViewMode)mode);
	}

	public static void DisableViewCam()
	{
		if (ViewCamTransform == null)
			throw new System.Exception("Desktop Camera is not initialized");

		IsViewCamEnabled = false;
		cameraDataStorage.ApplyData(LegacyCamRef, true);

		updateCameraModeUI();

		SetPlayer(true);

		if (ReplayMod.Replay.ReplayAPI.IsPlaying)
			ReplayMod.Core.Main.Playback?.UpdateReplayCameraPOV(PlayerManager.Instance.LocalPlayer);
	}

	public static void ReapplyViewCamTransform()
	{
		ViewCamTransform.position = storedViewCamPos;
		ViewRot = storedViewRot;
        OrbitCamFocus = storedOrbitCamFocus;
		OrbitCamDist = storedOrbitCamDist;
		ViewFOV = storedFOV;
		ViewSize = storedSize;
	}

    public static void StoreViewCamTransform()
    {
        storedViewCamPos = ViewCamTransform.position;
        storedViewRot = ViewRot;
        storedOrbitCamFocus = OrbitCamFocus;
        storedOrbitCamDist = OrbitCamDist;
		storedFOV = ViewFOV;
		storedSize = ViewSize;
    }

    public static void SetCameraMode(ViewMode mode)
	{
		cameraDataStorage.StoreData(LegacyCamRef, false);
		LegacyCamRef.useOcclusionCulling = false;

		CurrentViewMode = mode;
		IsViewCamEnabled = true;
		isViewUnlocked = false;
		updateCameraModeUI();

		SetPlayer(false);

		if (mode is not ViewMode.POV)
		{
			ReplayMod.Core.Main.Playback.UpdateReplayCameraPOV(PlayerManager.Instance.LocalPlayer);
		}
		else
		{
			if (selectedPOVPlayer == null || selectedPOVPlayer == PlayerManager.Instance.LocalPlayer) SelectPOVPlayer(false);
			ReplayMod.Core.Main.Playback.UpdateReplayCameraPOV(selectedPOVPlayer);
		}
	}

	public static void SelectPOVPlayer(bool previous)
	{
		List<Player> candidates = new();
		foreach (var player in PlayerManager.Instance.AllPlayers)
			if (player != PlayerManager.Instance.LocalPlayer) candidates.Add(player);

		if (candidates.Count == 0)
		{
			selectedPOVPlayer = PlayerManager.Instance.LocalPlayer;
			return;
		}

		int index = 0;
		if (selectedPOVPlayer != null)
		{
			index = candidates.IndexOf(selectedPOVPlayer);
			if (!previous)
				index = (index + 1) % candidates.Count;
			else
				index = (index - 1) % candidates.Count;
		}

		selectedPOVPlayer = candidates[index];

		ReplayMod.Core.Main.Playback.UpdateReplayCameraPOV(selectedPOVPlayer);
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
		Toggle povToggle = UIManager.TransformRefs["POVCamToggle"].GetComponent<Toggle>();

		offToggle.SetIsOnWithoutNotify(false);
		orbitToggle.SetIsOnWithoutNotify(false);
		flyToggle.SetIsOnWithoutNotify(false);
		povToggle.SetIsOnWithoutNotify(false);

		if (IsViewCamEnabled && CurrentViewMode is ViewMode.Orbit) orbitToggle.SetIsOnWithoutNotify(true);
		else if (IsViewCamEnabled && CurrentViewMode is ViewMode.Fly) flyToggle.SetIsOnWithoutNotify(true);
		else if (IsViewCamEnabled && CurrentViewMode is ViewMode.POV) povToggle.SetIsOnWithoutNotify(true);
		else offToggle.SetIsOnWithoutNotify(true);
	}

	public static void SnapLegacyCam()
	{
		LegacyCamRef.orthographic = IsOrthographic;
		LegacyCamRef.orthographicSize = ViewSize;

		if (ViewController.CurrentViewMode is ViewController.ViewMode.POV) return;

		LegacyCamRef.transform.position = ViewCamTransform.position;
		LegacyCamRef.transform.rotation = ViewCamTransform.rotation;

		LegacyCamRef.fieldOfView = ViewFOV;
	}

    public static void MoveCameraToMapStart(int map)
    {
        // TODO: Other maps
        if (map == 4) // Pit
        {
            ViewCamTransform.transform.position = new Vector3(8.12f, 8.89f, -10.13f);
            ViewCamTransform.transform.rotation = Quaternion.Euler(34f, 321f, 0f);
        }
        else
        {
            ViewCamTransform.transform.position = Vector3.zero;
            ViewCamTransform.transform.rotation = Quaternion.identity;
        }
    }

    internal static class cameraDataStorage
	{
		private static float fieldOfView = 90f;
		private static LayerMask cullingMask = ~0;
		private static bool useOcclusionCulling = true;
		private static bool isDataStored = false;
		private static bool orthographic = false;

		public static void StoreData(Camera camera, bool overrideData)
		{
			if (isDataStored && !overrideData) return;

			fieldOfView = camera.fieldOfView;
			cullingMask = camera.cullingMask;
			useOcclusionCulling = camera.useOcclusionCulling;
			orthographic = camera.orthographic;

			isDataStored = true;
		}

		public static void ApplyData(Camera camera, bool clearData)
		{
			if (!isDataStored) return;
			if (camera == null)
				throw new System.Exception("Camera is null");

			camera.fieldOfView = fieldOfView;
			camera.cullingMask = cullingMask;
			camera.useOcclusionCulling = useOcclusionCulling;
			camera.orthographic = orthographic;

			if (clearData) isDataStored = false;
		}

		public static void ClearData()
		{
			isDataStored = false;
		}
	}
}