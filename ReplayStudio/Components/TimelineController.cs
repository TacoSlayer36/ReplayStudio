using Il2CppMichsky.UI.ModernUIPack;
using Il2CppOculus.Platform;
using Il2CppTMPro;
using MelonLoader;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

/*
 * This system is almost entirely
 * implemented by Syborg
 * (@syborg64 on Discord)
 * 
 * It is very cool
 */

[RegisterTypeInIl2Cpp]
public class TimelineController : MonoBehaviour
{
    public static TimelineController Instance;

    /// Length in seconds of the recorded clip;
    public float ClipLength = 100f;
    public int FrameCount => (int)Mathf.Floor(ClipLength * ReplayStudio.Core.FPS);
    public int CurrentFrame => (int)Mathf.Floor(ReplayMod.Replay.ReplayAPI.CurrentTime * ReplayStudio.Core.FPS);

    /// zoom multiplier (after tweening)
    public float TargetZoom = 1f;

    /// tweening center [0-1] relative position
    public float FocusPos = 0f;
    /// timepoint at tweening center
    public float TimeAtFocus = 0f;
    /// amount of scrolling to tween, in seconds
    public float ScrollChange = 0f;


    /// linearized zoom factor using a logarithm: log(zoom)
    private float ZoomLog = 1f;

    private int maxVisibleSegments = 20;
    /// Maximum number of segments to display
    public int MinVisibleSegments
    {
        get
        {
            return maxVisibleSegments / Subdivisions;
        }
        set
        {
            maxVisibleSegments = value * Subdivisions;
            InitializeSegments();
        }
    }

    /// Number of segments to recursively subdivide each segment into
    public int Subdivisions = 10;

    /// Zooming out limit
    [System.NonSerialized]
    public float MinZoom = 0.0001f;
    /// Zooming in limit
    [System.NonSerialized]
    public float MaxZoom = 117.5f;
    /// Scrolling speed multiplier. Affect scaling and sliding
    public float ScrollMult = 0.2f;

    /// current scroll offset, measured from the left side, in seconds
    public float Scroll = 0f;
    /// current zoom multiplier
    private float zoom
    {
        get
        {
            return unlogify(ZoomLog);
        }
        set
        {
            ZoomLog = logify(value);
        }
    }

    /// relative scaling of a segment
    private float localZoom => Mathf.Pow(Subdivisions, ZoomLog - octave);
    /// level of zoom, quantized on the logarithmic scale
    private int octave => (int)Mathf.Floor(ZoomLog);

    /// total pixel width of the timeline, including the trailing segment
    private float totalWidth => Screen.width * localZoom * (1 + (1f / maxVisibleSegments));
    /// pixel width of a segment
    private float segmentWidth => (Screen.width * localZoom) / maxVisibleSegments;
    /// duration in seconds of a segment
    private float segmentDuration => 1 / Mathf.Pow(Subdivisions, octave);

    /// level of scrolling, quantized in segments. equivalent to the number of segments between 0 and the leftmost
    private int unitScroll => (int)Mathf.Floor(Scroll / segmentDuration);
    /// relative scrolling in seconds of the first segment compared to the left side
    private float localScroll => Scroll - unitScroll * segmentDuration;

    /// true if the mouse is hovering on the timeline
    private bool hovering => (viewportMouseDetector?.IsHovering ?? false) || (viewportMouseDetector?.HeldFromHovering ?? false);

    /// RectTransform Component
    private RectTransform rectTransform;
    /// MouseDetector Component
    private MouseDetector viewportMouseDetector;

    private GameObject segmentTemplate;
    /// all children segments
    private List<GameObject> segments = new();
    /// all timestamps of child segments
    private List<TextMeshProUGUI> timeCodes = new();

    public static Transform MarkerParent;
    private List<ReplayMarker> markers = new();

    private GameObject scrubber;
    private float scrubberDuration;
    private bool scrubbing = false;
    private bool pausedBeforeScrubbing = false;

    private RectTransform leftMargin;
    private RectTransform rightMargin;

    void OnEnable() => Instance = this;

    void Start()
    {
        InitializeReplay();
        rectTransform = GetComponent<RectTransform>();
        viewportMouseDetector = transform.parent.GetComponent<MouseDetector>();
        segmentTemplate = transform.Find("TimelineSegment").gameObject;
        scrubber = transform.parent.Find("Scrubber").gameObject;
        MarkerParent = transform.parent.Find("Markers");
        leftMargin = transform.parent.Find("LeftMargin").GetComponent<RectTransform>();
        rightMargin = transform.parent.Find("RightMargin").GetComponent<RectTransform>();
        InitializeSegments();
        InitializeMarkers();

        ScrollTo(ClipLength / 2f, 0.5f, maxVisibleSegments / ClipLength * 0.9f, false);
    }

    void Update()
    {
        if (hovering)
        {
            float newFocus = viewportMouseDetector.GetNormalizedHoverPos().x;
            float offset = (newFocus - FocusPos) * maxVisibleSegments / TargetZoom;
            TimeAtFocus += offset;
            FocusPos = newFocus;
            float scrollDelta = Input.mouseScrollDelta.y;
            if (scrollDelta != 0)
            {
                // TODO: Controls
                if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                scrollDelta *= 2f;

            }
            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) || Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand))
            {
                TargetZoom = Mathf.Clamp(unlogify(logify(TargetZoom) + scrollDelta * 0.2f * ScrollMult), MinZoom, MaxZoom);
            }
            else
            {
                ScrollChange += scrollDelta * 0.7f * ScrollMult / zoom;
            }

            if (Input.GetKey(KeyCode.F))
            {
                ScrollTo(scrubberDuration, 0.5f, 1f);
            }

            if (Input.GetKey(KeyCode.Home))
            {
                ScrollTo(ClipLength / 2f, 0.5f, maxVisibleSegments / ClipLength * 0.9f);
            }
        }

        {
            float lerp = 0.3f;
            zoom = Mathf.Lerp(zoom, TargetZoom, lerp);

            float scrollPart = Mathf.Lerp(0, ScrollChange, lerp);
            ScrollChange = Mathf.Lerp(ScrollChange, 0, lerp);
            TimeAtFocus += scrollPart;

            scrollCorrection(TimeAtFocus, FocusPos);
        }

        Vector2 size = rectTransform.sizeDelta;
        size.x = totalWidth;
        rectTransform.sizeDelta = size;

        Vector2 timelineOffset = rectTransform.anchoredPosition;
        timelineOffset.x = (-localScroll / segmentDuration) * segmentWidth;
        rectTransform.anchoredPosition = timelineOffset;

        RectTransform scrubberRCT = scrubber?.GetComponent<RectTransform>();
        if (scrubberRCT != null)
        {
            Vector2 pos2 = scrubberRCT.anchoredPosition;
            pos2.x = DurationToTimelinePos(scrubberDuration) * Screen.width;
            scrubberRCT.anchoredPosition = pos2;
        }

        // Scrubbing
        RectTransform rct = scrubber?.GetComponent<RectTransform>();
        if (rct != null && ReplayMod.Core.Main.Playback.isPlaying)
        {
            Vector2 scrubberPos = rct.anchoredPosition;
            bool scrubbingNow = viewportMouseDetector.HeldFromHovering;

            if (scrubbingNow && !scrubbing)
            {
                pausedBeforeScrubbing = ReplayMod.Core.Main.Playback.isPaused;
            }

            if (scrubbingNow)
            {
                float hoverPos = viewportMouseDetector.GetHoverPos().x;
                ReplayMod.Core.Main.Playback.TogglePlayback(false);

                float duration = TimelinePosToDuration(hoverPos / Screen.width);
                duration = Mathf.Clamp(duration, 0.001f, ClipLength - 0.001f);
                scrubberDuration = duration;

                scrubberPos.x = DurationToTimelinePos(scrubberDuration);
                rct.anchoredPosition = scrubberPos * Screen.width;

                ReplayMod.Replay.ReplayAPI.Seek(duration);

                if (viewportMouseDetector.IsHovering && hoverPos / Screen.width < 0.03f)
                    ScrollChange -= 0.7f * ScrollMult / zoom;

                if (viewportMouseDetector.IsHovering && hoverPos / Screen.width > 0.97f)
                    ScrollChange += 0.7f * ScrollMult / zoom;
            }
            else
            {
                float duration = DurationToTimelinePos(ReplayMod.Replay.ReplayAPI.CurrentTime);
                duration = Mathf.Clamp(duration, 0.001f, ClipLength - 0.001f);
                scrubberPos.x = duration * Screen.width;
                rct.anchoredPosition = scrubberPos;
            }

            if (!scrubbingNow && scrubbing)
            {
                ReplayMod.Core.Main.Playback.TogglePlayback(!pausedBeforeScrubbing);
            }

            scrubbing = scrubbingNow;
        }

        for (int i = 0; i < timeCodes.Count; i++)
        {
            TextMeshProUGUI segment = timeCodes[i];
            Canvas canvas = segment.GetComponentInParent<Canvas>();
            float timeCodeSeconds = (unitScroll + i) * segmentDuration;
            segment.text = formatTimecode(timeCodeSeconds);
        }

        Vector2 leftSize = leftMargin.sizeDelta;
        if (ReplayMod.Replay.ReplayAPI.IsPlaying)
            leftSize.x = Mathf.Clamp(DurationToTimelinePos(0f) * Screen.width, 0f, Screen.width);
        else
            leftSize.x = Screen.width;
        leftMargin.sizeDelta = leftSize;

        Vector2 rightSize = leftMargin.sizeDelta;
        if (ReplayMod.Replay.ReplayAPI.IsPlaying)
            rightSize.x = Mathf.Clamp(Screen.width - DurationToTimelinePos(ClipLength) * Screen.width, 0f, Screen.width);
        else
            rightSize.x = Screen.width;
        rightMargin.sizeDelta = rightSize;

        foreach (var marker in markers)
            marker.UpdateLocation();
    }

    /// clear and create children segments
    public void InitializeSegments()
    {
        clearSegments();
        for (int i = 0; i < maxVisibleSegments + 1; i++)
        {
            GameObject newSegment = GameObject.Instantiate(segmentTemplate);
            newSegment.SetActive(true);
            newSegment.transform.SetParent(transform, false);
            segments.Add(newSegment);
            timeCodes.Add(newSegment.GetComponentsInChildren<TextMeshProUGUI>().First(t => t?.name == "TimeCode"));
        }
    }

    /// clear children segments
    private void clearSegments()
    {
        foreach (GameObject segment in segments)
        {
            GameObject.Destroy(segment);
        }
        segments.Clear();
        timeCodes.Clear();
    }

    /// Convert a relative [0-1] position to a timepoint
    /// <param name="assumeScroll">calculate as if `assumeScroll` is the current `scroll` value</param>
    /// <param name="assumeZoom">calculate as if `assumeZoom` is the current `zoom` value</param>
    public float TimelinePosToDuration(float t, float? assumeScroll = null, float? assumeZoom = null)
    {
        return (t * maxVisibleSegments) / (assumeZoom ?? zoom) + (assumeScroll ?? Scroll);
    }

    /// Convert a timepoint to a relative [0-1] position
    /// <param name="assumeScroll">calculate as if `assumeScroll` is the current `scroll` value</param>
    /// <param name="assumeZoom">calculate as if `assumeZoom` is the current `zoom` value</param>
    public float DurationToTimelinePos(float duration, float? assumeScroll = null, float? assumeZoom = null)
    {
        return (duration - (assumeScroll ?? Scroll)) * (assumeZoom ?? zoom) / maxVisibleSegments;
    }

    /// Scroll the timeline to a given duration, using interpolated tweening
    /// <param name"duration">timepoint in seconds to scroll to</param>
    /// <param name"t">relative [0-1] position that will be set to the timepoint. Defaults to 0f = left side</param>
    /// <param name"setZoom">zoom factor to set</param>
    /// <param name"interpolate">true to use tweening, false for instantaneous</param>
    public void ScrollTo(float duration, float t = 0f, float? setZoom = null, bool interpolate = true)
    {
        TimeAtFocus = TimelinePosToDuration(t);
        ScrollChange = duration - TimeAtFocus;
        FocusPos = t;
        if (setZoom != null)
        {
            TargetZoom = Mathf.Clamp((float)setZoom, MinZoom, MaxZoom);
        }
        if (!interpolate)
        {
            zoom = TargetZoom;
            float scrollPart = ScrollChange;
            ScrollChange = 0;
            TimeAtFocus += scrollPart;
            scrollCorrection(TimeAtFocus, FocusPos);
        }
    }

    /// Set up values based on the active replay
    public void InitializeReplay()
    {
        ClipLength = ReplayMod.Replay.ReplayAPI.Duration;
    }

    public void UpdateClipInfos()
    {
        string lengthString = formatTimecode(ClipLength);
        ReplayStudio.UIManager.TransformRefs["TotalDuration"].GetComponent<TextMeshProUGUI>().text = "/ " + lengthString;

        ReplayStudio.UIManager.TransformRefs["TotalFrames"].GetComponent<TextMeshProUGUI>().text = "/ " + FrameCount.ToString();

        TMP_InputField durationField = ReplayStudio.UIManager.TransformRefs["DurationInput"]?.GetComponent<TMP_InputField>();
        if (durationField != null && !durationField.isFocused)
            durationField.SetTextWithoutNotify(formatTimecode(ReplayMod.Replay.ReplayAPI.CurrentTime));

        TMP_InputField frameField = ReplayStudio.UIManager.TransformRefs["FrameInput"]?.GetComponent<TMP_InputField>();
        if (frameField != null && !frameField.isFocused)
            frameField.SetTextWithoutNotify(CurrentFrame.ToString());
    }

    public void InitializeMarkers()
    {
        clearMarkers();
        if (ReplayMod.Replay.ReplayAPI.CurrentReplay?.Header?.Markers == null) return;
        if (!ReplayMod.Replay.ReplayAPI.IsPlaying) return;

        foreach (ReplayMod.Replay.Serialization.Marker replayMarker in ReplayMod.Replay.ReplayAPI.CurrentReplay.Header.Markers)
        {
            markers.Add(new ReplayMarker(this, replayMarker));
        }
    }

    private void clearMarkers()
    {
        foreach (ReplayMarker marker in markers)
            marker.RemoveRenderer();
        markers.Clear();
    }

    public void Reset()
    {
        ClipLength = 100f;
        TargetZoom = 1f;
        FocusPos = 0f;
        TimeAtFocus = 0f;
        ScrollChange = 0f;
        ZoomLog = 1f;
        maxVisibleSegments = 20;
        Subdivisions = 10;
        Scroll = 0f;
        Start();
    }

    /// correct the current `scroll` value based on the focus
    private void scrollCorrection(float duration, float t)
    {
        float currDuration = TimelinePosToDuration(t);
        float delta = duration - currDuration;
        Scroll += delta;
    }

    /// print a duration to a string
    string formatTimecode(float secs)
    {
        if (!float.IsFinite(secs))
        {
            return "Nan";
        }
        TimeSpan t = TimeSpan.FromSeconds(Math.Abs(secs));

        string output = t.Minutes.ToString("D2") + ":" + t.Seconds.ToString("D2");
        if (Math.Abs(secs) >= 3600) output = t.Hours.ToString("F0") + ":" + output;

        int sigFigs = (int)Math.Clamp(ZoomLog, 0, 3);
        if (sigFigs > 0)
            output += "." + (t.Milliseconds).ToString("D03").Substring(0, sigFigs);

        if (secs < 0)
            output = "-" + output;

        if (t.Days != 0)
        {
            output = output + "+" + t.Days;
        }

        return output;
    }

    /// convert a factor to a linearly-growing form
    float logify(float input)
    {
        return Mathf.Log(input, Subdivisions);
    }

    /// convert a log to a multiplicative form
    float unlogify(float input)
    {
        return Mathf.Pow(Subdivisions, input);
    }

    public class ReplayMarker
    {
        private TimelineController timelineController;

        private GameObject markerIcon;
        private RectTransform rectTransform;

        private ReplayMod.Replay.Serialization.Marker parentMarker;

        public ReplayMarker(TimelineController timelineController, ReplayMod.Replay.Serialization.Marker parentMarker)
        {
            this.timelineController = timelineController;
            this.parentMarker = parentMarker;

            InitializeRenderer();
        }

        public void UpdateLocation()
        {
            Vector2 pos = rectTransform.anchoredPosition;
            pos.x = timelineController.DurationToTimelinePos(parentMarker.time) * Screen.width;
            rectTransform.anchoredPosition = pos;
        }

        public void InitializeRenderer()
        {
            markerIcon = GameObject.Instantiate(MarkerParent.GetChild(0).gameObject);
            markerIcon.SetActive(true);
            markerIcon.transform.SetParent(MarkerParent.transform, false);
            markerIcon.GetComponent<UnityEngine.UI.Image>().color = new Color(parentMarker.r, parentMarker.g, parentMarker.b);
            rectTransform = markerIcon.GetComponent<RectTransform>();
            UpdateLocation();
        }

        public void RemoveRenderer()
        {
            GameObject.Destroy(markerIcon);
        }
    }
}