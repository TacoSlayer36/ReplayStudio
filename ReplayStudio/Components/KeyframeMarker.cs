using Il2CppMichsky.UI.ModernUIPack;
using Il2CppOculus.Platform;
using Il2CppTMPro;
using MelonLoader;
using ReplayStudio.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace ReplayStudio.Components;

public partial class TimelineController : MonoBehaviour {

    /// <summary>
    /// This class manages all storage of keyframe related data. It is the beginning and the end of a keyframe
    /// </summary>
    public class KeyframeMarker
    {
        public GameObject Target;

        private GameObject markerIcon;
        private RectTransform rectTransform;

        public KeyframedObject.Keyframe keyframe {get; }

        public Type KeyframeType;

        public KeyframeMarker(GameObject gameObject, KeyframedObject.Keyframe keyframe, Type keyframeType)
        {
            this.Target = gameObject;
            this.keyframe = keyframe;
            this.KeyframeType = keyframeType;
            TimelineController.Instance.AddKeyframeMarker(this);
            InitializeRenderer();
        }

        public static KeyframeMarker Capture<K>(GameObject gameObject) where K: KeyframedObject.Keyframe, new()
        {
            return gameObject.GetComponent<KeyframedObject>().Capture<K>();
        }

        // public KeyframeMarker(KeyframedObject.Keyframe keyframe)
        // {
        //     this.keyframe = keyframe;
        //     InitializeRenderer();
        // }

        public void UpdateLocation()
        {
            Vector2 pos = rectTransform.anchoredPosition;
            pos.x = TimelineController.Instance.DurationToTimelinePos(keyframe.snap.Time()) * Screen.width;
            rectTransform.anchoredPosition = pos;
        }

        protected void InitializeRenderer()
        {
            markerIcon = GameObject.Instantiate(KeyframeParent.GetChild(0).gameObject);
            markerIcon.SetActive(true);
            markerIcon.transform.SetParent(KeyframeParent.transform, false);
            markerIcon.GetComponent<UnityEngine.UI.Image>().color = new Color(255f / 255f, 252f / 255f, 115f / 255f);
            rectTransform = markerIcon.GetComponent<RectTransform>();
            UpdateLocation();
        }

        protected void RemoveRenderer()
        {
            GameObject.Destroy(markerIcon);
        }

        public void Render()
        {
            keyframe.Render(Target.GetComponent<KeyframedObject>());
        }

        public void Remove()
        {
            Target.GetComponent<KeyframedObject>().Remove(keyframe);
            TimelineController.Instance.RemoveKeyframeMarker(this);
            RemoveRenderer();
        }

        public void Move(float time)
        {
            TimelineController.Instance.RemoveKeyframeMarker(this);
            this.keyframe.Move(this.Target, time);
            UpdateLocation();
            TimelineController.Instance.AddKeyframeMarker(this);
        }
    }
}