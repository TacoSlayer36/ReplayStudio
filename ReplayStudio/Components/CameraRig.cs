using MelonLoader;
using UnityEngine;
namespace ReplayStudio.Components;

#nullable enable

[RegisterTypeInIl2Cpp]
public class CameraRig : MonoBehaviour
{
    public Vector3 Focus;
    public Quaternion Rotation;
    public float Distance = 5;

    public GameObject? FocusObject;
    public Vector3 UpVector = Vector3.up;

    void UpdateRenderer(bool _oldvalue, bool enabled) {
        FocusObject?.SetActive(enabled);
    }

    public void AddFocusObject() {
        FocusObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        FocusObject.GetComponent<Renderer>().material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        FocusObject.GetComponent<Renderer>().material.SetInteger("Render Face", 2);
        FocusObject.AddComponent<KeyframedObject>();
        FocusObject.transform.SetParent(transform);
        FocusObject.transform.localPosition = new Vector3(0, 0, 5);
        // FocusObject.transform.SetParent(transform.parent);
        FocusObject.transform.localScale = Vector3.one * -0.5f;
        FocusObject.GetComponent<Collider>().excludeLayers = ~0;
        FocusObject.GetComponent<Collider>().includeLayers = 0;
        FocusObject.SetActive(Core.Settings.RenderBezierWidgets.Value);
        Core.Settings.RenderBezierWidgets.OnEntryValueChanged.Subscribe(UpdateRenderer);
    }

    public void RemoveFocusObject() {
        GameObject.Destroy(FocusObject);
        FocusObject = null;
    }

    void Start() {
        AddFocusObject();
    }

    void OnDestroy() {
        Core.Settings.RenderBezierWidgets.OnEntryValueChanged.Unsubscribe(UpdateRenderer);
        RemoveFocusObject();
    }

    void Update() {
        if (!UIManager.IsHoveringAny && CameraController.DoMapping && FocusObject != null && Input.mouseScrollDelta.y != 0) {
            FocusObject.transform.Translate(Vector3.back * Input.mouseScrollDelta.y);
        }
    }

    // void LateUpdate() {
    //     if (FocusObject != null) {
    //         transform.LookAt(FocusObject.transform, UpVector);
    //     }
    // }
}