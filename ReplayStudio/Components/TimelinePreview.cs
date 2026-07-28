using Il2CppSystem.Security.Cryptography;
using MelonLoader;
using ReplayMod.Replay.Files;
using ReplayMod.Replay.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace ReplayStudio.Components;

[RegisterTypeInIl2Cpp]
public class TimelinePreview : MonoBehaviour
{
    public List<GameObject> Markers = new();

    public void ClearMarkers()
    {
        foreach (GameObject marker in Markers)
        {
            GameObject.Destroy(marker);
        }
        Markers.Clear();
    }

    public void InitializeMarkers(ReplaySerializer.ReplayHeader header)
    {
        ClearMarkers();

        if (header?.Markers == null) return;

        GameObject template = transform.GetChild(0).gameObject;

        foreach (Marker replayMarker in header?.Markers)
        {
            GameObject newMarker = GameObject.Instantiate(template);
            newMarker.transform.SetParent(transform, false);
            newMarker.SetActive(true);
            Markers.Add(newMarker);

            newMarker.GetComponent<UnityEngine.UI.Image>().color = new Color(replayMarker.r, replayMarker.g, replayMarker.b);

            RectTransform rectTransform = newMarker.GetComponent<RectTransform>();
            Vector2 pos = rectTransform.anchoredPosition;

            float t = (replayMarker.time / header.Duration);
            float myWidth = GetComponent<RectTransform>().rect.m_Width;

            pos.x = t * myWidth;
            rectTransform.anchoredPosition = pos;
        }
    }
}
