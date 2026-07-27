using MelonLoader;
using Newtonsoft.Json;
using ReplayMod.Replay;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ReplayStudio.Components;

#nullable enable
[RegisterTypeInIl2Cpp]
public class KeyframedObject : MonoBehaviour
{
    [Serializable]
    public abstract class Keyframe
    {
        public record Snap : IComparable<Snap>, IEquatable<Snap>
        {
            private const int DIV = 360;
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

            public float Time()
            {
                return (float)time / DIV;
            }
        }

        [JsonProperty]
        public Snap snap;

        public Keyframe()
        {
            snap = new Snap(0f);
        }

        public void Move(GameObject obj, float time)
        {
            var keys = obj.GetComponent<KeyframedObject>();
            keys.Remove(this);
            this.snap = new Snap(ReplayAPI.CurrentTime);
            keys.Add(this);
        }

        public abstract Keyframe Capture(GameObject obj);
        public abstract void Apply(GameObject obj, float t);
        
        /// <summary>
        /// Safety: This must be of type T 
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

    [Serializable]
    public class PositionKeyFrame : Keyframe
    {
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
            MelonLogger.Msg($"pos: {data} | this.snap: {snap} | time: {time}");
            var tm = obj.transform;
            var next = Next(obj.GetComponent<KeyframedObject>(), snap, this)!;

            tm.position = Vector3.Lerp(data, next.data, tValue(next, time));
        }
    }

    [Serializable]
    public class RotationKeyFrame : Keyframe
    {
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

    [Serializable]
    public class FovKeyFrame : Keyframe
    {
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

    [JsonProperty]
    public Dictionary<Type, SortedList<Keyframe.Snap, Keyframe>> Channels = new();
    
    [JsonIgnore]
    Dictionary<Type, Keyframe> constructors = new();

    void Start()
    {
        MelonLogger.Msg("RegKFO");
        ReplayAPI.onReplayTimeChanged += Apply;
    }

    void OnDestroy()
    {
        MelonLogger.Msg("DeregKFO");
        ReplayAPI.onReplayTimeChanged -= Apply;
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
        {
            throw new Exception("Lost Rich type K of Keyframe!");
        }
        if (!Channels.ContainsKey(t))
        {
            Channels[t] = new();
        }
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
        return new TimelineController.KeyframeMarker(gameObject, keyframe);
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
            Channels[t].Remove(snap);
        }
    }

    public void Remove(Keyframe keyframe)
    {
        RemoveAt(keyframe.GetType(), keyframe.snap);
    }
}