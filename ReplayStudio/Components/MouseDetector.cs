using MelonLoader;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ReplayStudio.Components;

[RegisterTypeInIl2Cpp]
public class MouseDetector : MonoBehaviour
{
    RectTransform rt;

    public bool IsHovering;
    public bool HeldFromHovering;

    void Awake() => rt = GetComponent<RectTransform>();

    void Update()
    {
        Vector2 screenPoint = Input.mousePosition;
        Canvas canvas = GetComponentInParent<Canvas>();

        if (RectTransformUtility.RectangleContainsScreenPoint(rt, screenPoint, null))
        {
            IsHovering = true;

            if (Input.GetMouseButtonDown(0))
            {
                HeldFromHovering = true;
            }
        }
        else
        {
            IsHovering = false;
        }

        if (!Input.GetMouseButton(0))
        {
            HeldFromHovering = false;
        }
    }

    public Vector2 GetHoverPos()
    {
        Vector2 screenPoint = Input.mousePosition;
        Canvas canvas = GetComponentInParent<Canvas>();
        RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, screenPoint, null, out Vector2 localPoint);
        return localPoint;
    }

    public Vector2 GetNormalizedHoverPos()
    {
        Vector2 localPoint = GetHoverPos();
        Vector2 normalizedPoint;
        normalizedPoint.x = (localPoint.x - rt.rect.xMin) / rt.rect.width;
        normalizedPoint.y = (localPoint.y - rt.rect.yMin) / rt.rect.height;
        return normalizedPoint;
    }
}