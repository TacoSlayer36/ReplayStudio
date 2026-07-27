using MelonLoader;
using UnityEngine;
using UnityEngine.EventSystems;

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
            if (!IsHovering)
            {
                IsHovering = true;
            }

            if (Input.GetMouseButtonDown(0))
            {
                HeldFromHovering = true;
            }

            if (Input.GetMouseButtonUp(0))
            {
                HeldFromHovering = false;
            }
        }
        else
        {
            IsHovering = false;
        }

        if (Input.GetMouseButtonUp(0))
        {
            HeldFromHovering = false;
        }
    }

    public Vector2 GetHoverPos()
    {
        if (!IsHovering) return Vector2.zero;

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