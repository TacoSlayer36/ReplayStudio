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