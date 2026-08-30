using Il2CppRUMBLE.Players.Subsystems;
using MelonLoader;
using Newtonsoft.Json;
using ReplayMod.Replay;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using UnityEngine;
using static Il2CppRUMBLE.Players.Subsystems.PlayerAnimator;

namespace ReplayStudio.Components;

#nullable enable
[RegisterTypeInIl2Cpp]
public class KeyframedObject : MonoBehaviour
{
    public abstract class Keyframe
    {
        public class SnapTypeConverter : TypeConverter
        {
            public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
                => sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

            public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
            {
                if (value is string s && float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var f))
                    return new Snap(f);
                return base.ConvertFrom(context, culture, value);
            }

            public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType)
                => destinationType == typeof(string) || base.CanConvertTo(context, destinationType);

            public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
            {
                if (destinationType == typeof(string) && value is Snap snap)
                    return snap.Time().ToString(CultureInfo.InvariantCulture);
                return base.ConvertTo(context, culture, value, destinationType);
            }
        }

        [TypeConverter(typeof(SnapTypeConverter))]
        public record Snap : IComparable<Snap>, IEquatable<Snap>
        {
            private const int DIV = 360;
            [JsonProperty]
            int time;

            public Snap(float time)
            {
                this.time = (int)System.Math.Round(time * DIV);
            }

            public static bool operator <=(Snap lhs, Snap rhs)
            {
                return lhs.time <= rhs.time;
            }
            public static bool operator >=(Snap lhs, Snap rhs)
            {
                return lhs.time >= rhs.time;
            }
            public static bool operator <(Snap lhs, Snap rhs)
            {
                return lhs.time < rhs.time;
            }
            public static bool operator >(Snap lhs, Snap rhs)
            {
                return lhs.time > rhs.time;
            }
            public int CompareTo(Snap? other)
            {
                return time - other?.time ?? 0;
            }

            public static Snap operator+(Snap lhs, int rhs)
            {
                return new Snap(0f) {
                    time = lhs.time + rhs,
                };
            }

            public static Snap operator-(Snap lhs, int rhs)
            {
                return new Snap(0f) {
                    time = lhs.time - rhs,
                };
            }

            public float Time()
            {
                return (float)time / DIV;
            }
        }

        [JsonProperty]
        [TypeConverter(typeof(SnapTypeConverter))]
        public Snap snap;

        public Keyframe()
        {
            snap = new Snap(0f);
        }

        public virtual void Remove() {}
        public virtual void Render(KeyframedObject keys) {}

        public virtual void Move(GameObject obj, float time)
        {
            var keys = obj.GetComponent<KeyframedObject>();
            keys.Remove(this);
            this.snap = new Snap(time);
            keys.Add(this);
        }

        /// <summary>
        /// Must return an instance of this
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public abstract Keyframe Capture(GameObject obj);

        public abstract void Apply(GameObject obj, float time);
        
        /// <summary>
        /// Safety: T must be the most derived type
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="keys"></param>
        /// <param name="time"></param>
        /// <returns></returns>
        public static T? Previous<T>(KeyframedObject keys, Snap snap) where T : Keyframe {
            return (T?)Previous(typeof(T), keys, snap);
        }
        
        /// <summary>
        /// Safety: T must be the most derived type
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="keys"></param>
        /// <param name="time"></param>
        /// <param name="def"></param>
        /// <returns></returns>
        public static T? Next<T>(KeyframedObject keys, Snap snap, T? def) where T : Keyframe
        {
            return (T?)Next(typeof(T), keys, snap) ?? def;
        }

        public static Keyframe? Previous(Type type, KeyframedObject keys, Snap snap)
        {
            var frames = keys.Channels.GetValueOrDefault(type);
            return frames?.Values.LastOrDefault((k) => k != null && k.snap <= snap);
        }

        public static Keyframe? Next(Type type, KeyframedObject keys, Snap snap)
        {
            var frames = keys.Channels.GetValueOrDefault(type);
            return frames?.Values.FirstOrDefault((k) => k != null && k.snap > snap);
        }

        public float tValue(Keyframe next, float time)
        {
            return System.Math.Clamp(snap == next.snap ? 0 : (time - snap.Time()) / (next.snap.Time() - snap.Time()), 0f, 1f);
        }
    }

    public class PositionKeyFrame : Keyframe
    {
        [JsonProperty]
        Vector3 data;

        public override Keyframe Capture(GameObject obj)
        {
            return new PositionKeyFrame
            {
                data = obj.transform.position,
                snap = new Snap(ReplayAPI.CurrentTime),
            };
        }

        public override void Apply(GameObject obj, float time)
        {
            var tm = obj.transform;
            var next = Next(obj.GetComponent<KeyframedObject>(), snap, this)!;

            tm.position = Vector3.Lerp(data, next.data, tValue(next, time));
        }
    }

    public class DollyKeyFrame : Keyframe
    {
        [JsonProperty]
        Vector3 focus;
        [JsonProperty]
        Quaternion rotation;
        [JsonProperty]
        float distance;

        public override Keyframe Capture(GameObject obj)
        {
            var d = obj.GetComponent<CameraRig>()?.Distance ?? 5;
            return new DollyKeyFrame
            {
                distance = d,
                focus = inverse(obj.transform, d),
                rotation = obj.transform.rotation,
                snap = new Snap(ReplayAPI.CurrentTime),
            };
        }

        public override void Apply(GameObject obj, float time)
        {
            var next = Next(obj.GetComponent<KeyframedObject>(), snap, this)!;
            var f = Vector3.Lerp(focus, next.focus, tValue(next, time));
            var d = Mathf.Lerp(distance, next.distance, tValue(next, time));
            var r = Quaternion.Slerp(rotation, next.rotation, tValue(next, time));

            obj.transform.rotation = r;
            obj.transform.position = f - (r * Vector3.forward * d);
        }

        static Vector3 inverse(Transform transform, float distance)
        {
            return transform.position + (transform.rotation * Vector3.forward * distance);
        }
    }


    public class TrackingKeyframe : Keyframe
    {
        static GameObject KeyframesRoot {
            get 
            {
                // var ReplayRoot = ReplayAPI.ReplayRoot;
                var ReplayRoot = ReplayMod.Core.Main.Playback.ReplayRoot;
                var found = ReplayRoot.transform.FindChild("Keyframes")?.gameObject;

                if (found == null) {
                    found = new GameObject("Keyframes");
                    found.transform.SetParent(ReplayRoot.transform);
                }
                return found;
            }
        }
        static GameObject KeyframesForObj(String obj)
        {
            var found = KeyframesRoot.transform.FindChild(obj)?.gameObject;
                
            if (found == null) {
                found = new GameObject(obj);
                found.transform.SetParent(KeyframesRoot.transform);
            }
            return found;
        }

        internal static GameObject ParentForKeyframe(Snap snap, String obj)
        {
            var found = KeyframesForObj(obj).transform.FindChild(snap.Time().ToString())?.gameObject;

            if (found == null)
            {
                found = new GameObject(snap.Time().ToString());
                found.transform.SetParent(KeyframesForObj(obj).transform);
            }
            return found;
        }

        protected static GameObject? FocusObject(GameObject obj) {
            return obj.GetComponent<CameraRig>()?.FocusObject;
        }

        public Vector3 Focus { get => focus.position; set => focus.position = value; }
        protected Transform focus;
        public Vector3 Position { get => position.position; set => position.position = value; }
        protected Transform position;
        public String Obj;

        internal GameObject? renderer;
        internal GameObject? rendererFocus;
        internal LineRenderer? lineNext;
        internal LineRenderer? lineFocus;

        public TrackingKeyframe() {
            renderer = null;
            rendererFocus = null;
            position = new();
            focus = new();
            Obj = "empty";
        }


        /// <summary>
        /// Renderer constructor
        /// </summary>
        private TrackingKeyframe(string obj, Snap snap)
        {
            this.Obj = obj;
            this.snap = snap;

            var parent = ParentForKeyframe(snap, obj);

            var r = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            {
                r.name = "keyframe";
                r.transform.SetParent(parent.transform);
                r.transform.localScale *= 0.1f;
                r.GetComponent<Collider>().excludeLayers = ~0;
                r.GetComponent<Collider>().includeLayers = 0;
                r.SetActive(Core.Settings.RenderKeyframeWidgets.Value);

                Material mat = new(Shader.Find("Universal Render Pipeline/Unlit"))
                {
                    color = Color.blue
                };
                r.GetComponent<Renderer>().material = mat;

                renderer = r;

                position = renderer.transform;

                Core.Settings.RenderKeyframeWidgets.OnEntryValueChanged.Subscribe(UpdateRenderer);
            }
            
            {
                var f = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                f.name = "focus";
                f.transform.SetParent(parent.transform);
                f.transform.localScale *= 0.02f;
                f.GetComponent<Collider>().excludeLayers = ~0;
                f.GetComponent<Collider>().includeLayers = 0;
                f.SetActive(Core.Settings.RenderKeyframeWidgets.Value);

                Material mat = new(Shader.Find("Universal Render Pipeline/Unlit"))
                {
                    color = Color.softYellow
                };
                f.GetComponent<Renderer>().material = mat;

                rendererFocus = f;

                focus = rendererFocus.transform;

                Core.Settings.RenderKeyframeWidgets.OnEntryValueChanged.Subscribe(UpdateRenderer);
            }

            {
                var lineNextObj = new GameObject("lineNext");
                lineNextObj.transform.SetParent(parent.transform);
                lineNext = lineNextObj.AddComponent<LineRenderer>();
                lineNext.material = Core.DottedLineMat;
                lineNext.widthMultiplier = 0.01f;
                lineNext.gameObject.SetActive(Core.Settings.RenderKeyframeWidgets.Value);
            }

            {
                var lineFocusObj = new GameObject("lineFocus");
                lineFocusObj.transform.SetParent(parent.transform);
                lineFocus = lineFocusObj.AddComponent<LineRenderer>();
                lineFocus.material = Core.DottedLineMat;
                lineFocus.widthMultiplier = 0.002f;
                lineFocus.gameObject.SetActive(Core.Settings.RenderKeyframeWidgets.Value);
            }
        }

        /// <summary>
        /// Capturing constructor
        /// </summary>
        public TrackingKeyframe(GameObject obj) : this(obj.name, new Snap(ReplayAPI.CurrentTime))
        {
            Position = obj.transform.position;
            Focus = FocusObject(obj)?.transform.position ?? (obj.transform.position + obj.transform.forward);
        }

        public override Keyframe Capture(GameObject obj)
        {
            return new TrackingKeyframe(obj);
        }

        public override void Apply(GameObject obj, float time)
        {
            var next = Next(obj.GetComponent<KeyframedObject>(), snap, this)!;
            var f = Vector3.Lerp(Focus, next.Focus, tValue(next, time));
            var p = Vector3.Lerp(Position, next.Position, tValue(next, time));

            obj.transform.position = p;
            obj.transform.LookAt(f, Vector3.up);
            
            var focusObj = FocusObject(obj);
            if (focusObj != null)
            {
                focusObj.transform.position = f;
            }
        }

        void UpdateRenderer(bool _oldvalue, bool enabled) {
            renderer?.SetActive(enabled);
            lineNext?.gameObject.SetActive(enabled);
            lineFocus?.gameObject.SetActive(enabled);
        }

        void RenderInternal(TrackingKeyframe? next)
        {
            if (next != null)
            {
                lineNext?.SetPositions(new Vector3[2] { Position, next.Position});
                lineNext?.gameObject.SetActive(Core.Settings.RenderKeyframeWidgets.Value);
            }
            lineFocus?.SetPositions(new Vector3[2] { Position, Focus });
            lineFocus?.gameObject.SetActive(Core.Settings.RenderKeyframeWidgets.Value);
        }

        public override void Render(KeyframedObject keys)
        {
            TrackingKeyframe? next = (TrackingKeyframe?)Next(typeof(TrackingKeyframe), keys, snap);
            RenderInternal(next);
        }

        public override void Remove()
        {
            if (renderer != null) {
                UnityEngine.Object.DestroyObject(renderer);
                UnityEngine.Object.DestroyObject(rendererFocus);
                UnityEngine.Object.DestroyObject(lineFocus);
                UnityEngine.Object.DestroyObject(lineNext);
                Core.Settings.RenderKeyframeWidgets.OnEntryValueChanged.Unsubscribe(UpdateRenderer);
            }
        }

         public override void Move(GameObject obj, float time)
        {
            /// keep the renderer alive while moving
            var renderer = this.renderer;
            /// mark it null so nothing else gets deleted
            this.renderer = null;
            var keys = obj.GetComponent<KeyframedObject>();
            keys.Remove(this);
            snap = new Snap(time);
            keys.Add(this);
            this.renderer = renderer;
            if (this.renderer != null) {
                this.renderer.name = snap.Time().ToString();
            }
        }

        [Newtonsoft.Json.JsonConstructor]
        public TrackingKeyframe(String obj, Snap snap, Vector3 Position, Vector3 Focus) : this(obj, snap)
        {
            this.Position = Position;
            this.Focus = Focus;
            this.Obj = obj;
        }
    }

    public class RotationKeyFrame : Keyframe
    {
        [JsonProperty]
        Quaternion data;

        public override Keyframe Capture(GameObject obj)
        {
            return new RotationKeyFrame
            {
                snap = new Snap(ReplayAPI.CurrentTime),
                data = obj.transform.rotation
            };
        }

        public override void Apply(GameObject obj, float time)
        {
            var tm = obj.transform;
            var next = Next(obj.GetComponent<KeyframedObject>(), snap, this)!;

            tm.rotation = Quaternion.Slerp(data, next.data, tValue(next, time));
        }
    }

    public class FovKeyFrame : Keyframe
    {
        [JsonProperty]
        float data;

        public override Keyframe Capture(GameObject obj)
        {
            return new FovKeyFrame
            {
                snap = new Snap(ReplayAPI.CurrentTime),
                data = obj.GetComponent<Camera>().fieldOfView
            };
        }

        public override void Apply(GameObject obj, float time)
        {
            var tm = obj.GetComponent<Camera>();
            var next = Next(obj.GetComponent<KeyframedObject>(), snap, this)!;

            tm.fieldOfView = Mathf.Lerp(data, next.data, tValue(next, time));
        }
    }

    public class ExpressionKeyFrame : Keyframe
    {
        [JsonProperty]
        int data;

        public override Keyframe Capture(GameObject obj)
        {
            return new ExpressionKeyFrame
            {
                snap = new Snap(ReplayAPI.CurrentTime),
                data = (int)Core.AA_Expression // TODO
            };
        }

        public override void Apply(GameObject obj, float time)
        {
            PlayerAnimator anim = obj.GetComponentInChildren<PlayerAnimator>();
            anim?.PlayHeadAnimation((HeadAnimation)data, float.MaxValue);
        }

        public override void Remove()
        {
            
        }
    }

    [JsonProperty]
    public Dictionary<Type, SortedList<Keyframe.Snap, Keyframe>> Channels = new();

    Dictionary<Type, Keyframe> constructors = new();

    void Start()
    {
        ReplayAPI.onReplayTimeChanged += Apply;
    }

    void OnDestroy()
    {
        ReplayAPI.onReplayTimeChanged -= Apply;
    }

    public void Redraw(Type t)
    {
        if (!Channels.ContainsKey(t))
            foreach (Keyframe keyframe in Channels[t].Values)
                keyframe.Render(this);
    }
    public void Redraw<K>() where K : Keyframe
    {
        Redraw(typeof(K));
    }

    void Apply(float time)
    {
        if (!enabled) return;

        foreach (var (type, _) in Channels)
        {
            var frame = Keyframe.Previous(type, this, new Keyframe.Snap(time)) ?? Keyframe.Next(type, this, new Keyframe.Snap(time));
            frame?.Apply(gameObject, time);
        }
    }

    protected void EnsureErased(Type t)
    {
        if (t == typeof(Keyframe))
            throw new Exception("Lost Rich type K of Keyframe!");
        if (!Channels.ContainsKey(t))
            Channels[t] = new();
    }
    protected void Ensure<K>() where K : Keyframe
    {
        if (typeof(K) == typeof(Keyframe))
        {
            throw new Exception("Lost Rich type K of Keyframe!");
        }
        if (!Channels.ContainsKey(typeof(K)))
        {
            Channels[typeof(K)] = new();
        }
    }
    protected void Register<K>() where K : Keyframe, new()
    {
        Ensure<K>();
        if (!constructors.ContainsKey(typeof(K)))
        {
            constructors[typeof(K)] = new K();
        }
    }

    public void Add(Keyframe keyframe)
    {
        Type t = keyframe.GetType();
        EnsureErased(t);
        RemoveAt(t, keyframe.snap);
        Channels[t].Add(keyframe.snap, keyframe);
    }

    /// <summary>
    /// Capture a new Keyframe and save it to the timeline
    /// This is one of 2 ways to create a keyframe
    /// </summary>
    /// <typeparam name="K">Type of keyframe to save</typeparam>
    /// <returns></returns>
    public TimelineController.KeyframeMarker Capture<K>() where K : Keyframe, new()
    {
        Register<K>();
        var keyframe = constructors[typeof(K)].Capture(gameObject);
        Add(keyframe);
        var newKeyframeMarker = new TimelineController.KeyframeMarker(gameObject, keyframe, typeof(K));
        TimelineController.OnKeyframesModified(newKeyframeMarker);
        return newKeyframeMarker;
    }

    public void Capture<K1, K2>()
        where K1 : Keyframe, new()
        where K2 : Keyframe, new()
    {
        Capture<K1>();
        Capture<K2>();        
    }
    public void Capture<K1, K2, K3>()
        where K1 : Keyframe, new()
        where K2 : Keyframe, new()
        where K3 : Keyframe, new()
    {
        Capture<K1>();
        Capture<K2>();
        Capture<K3>();
    }

    public void InitializeAll()
    {
        foreach (var (type, sortedList) in Channels)
        {
            foreach (var (snap, keyframe) in sortedList)
            {
                new TimelineController.KeyframeMarker(gameObject, keyframe, type);
                keyframe.Render(this);
            }
        }
    }

    public K? Next<K>(float time) where K : Keyframe
    {
        return (K?)Keyframe.Next(typeof(K), this, new Keyframe.Snap(time));
    }

    public K? Previous<K>(float time) where K : Keyframe
    {
        return (K?)Keyframe.Previous(typeof(K), this, new Keyframe.Snap(time));
    }

    protected void RemoveAt(Type t, Keyframe.Snap snap)
    {
        EnsureErased(t);
        var k = Channels[t].GetValueOrDefault(snap);
        if (k != null)
        {
            Channels[t][snap]?.Remove();
            Channels[t].Remove(snap);
        }
    }

    public void Remove(Keyframe keyframe)
    {
        RemoveAt(keyframe.GetType(), keyframe.snap);
    }
}