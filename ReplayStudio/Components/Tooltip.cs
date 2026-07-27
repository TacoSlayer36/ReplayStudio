using Il2CppSystem.Security.Cryptography;
using Il2CppTMPro;
using MelonLoader;
using ReplayMod.Replay.Files;
using ReplayMod.Replay.Serialization;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace ReplayStudio.Components;

[RegisterTypeInIl2Cpp]
public class Tooltip : MonoBehaviour
{
    public static Tooltip Instance;

    public static bool Visible = false;

    RectTransform textTransform;
    RectTransform imageTransform;
    RectTransform mainTransform;

    TextMeshProUGUI tmp;

    CanvasGroup canvasGroup;

    void Start()
    {
        textTransform = transform.GetChild(1).gameObject.GetComponent<RectTransform>();
        imageTransform = transform.GetChild(0).gameObject.GetComponent<RectTransform>();
        mainTransform = GetComponent<RectTransform>();

        tmp = GetComponentInChildren<TextMeshProUGUI>();

        canvasGroup = GetComponent<CanvasGroup>();

        Instance = this;
    }

    void Update()
    {
        Vector2 size = imageTransform.sizeDelta;
        size.x = textTransform.sizeDelta.x;
        imageTransform.sizeDelta = size;

        mainTransform.anchoredPosition = Input.mousePosition;
        mainTransform.position = Input.mousePosition;
    }

    public static void Show(string text = null)
    {
        MelonCoroutines.Start(Instance.fade(true, 0f));

        Visible = true;

        if (text != null)
            Instance.tmp.text = text;
    }

    public static void ShowOrSetText(string text = null)
    {
        if (!Visible) Show(text);
        else Instance.tmp.text = text;
    }

    public static void Hide()
    {
        Visible = false;
        MelonCoroutines.Start(Instance.fade(false, 0f));
    }

    IEnumerator fade(bool active, float fadeTime)
    {
        if (active) gameObject.SetActive(true);
        for (int i = 0; i < fadeTime; i++)
        {
            canvasGroup.alpha = active ? i / fadeTime : 1 - i / fadeTime;
            yield return null;
        }
        if (!active) gameObject.SetActive(false);
    }
}
