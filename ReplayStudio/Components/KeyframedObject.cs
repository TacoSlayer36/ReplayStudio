using Il2CppMS.Internal.Xml.XPath;
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
[JsonObject(MemberSerialization.OptIn)]
public class KeyframedObject : MonoBehaviour
{
    public abstract class Keyframe
    {
        [JsonProperty]
        protected float time;

        public abstract void Capture(GameObject obj, float time);
        public abstract void Apply(GameObject obj, float t);

        public float Time()
        {
            return time;
        }

        public static void SaveCapture<K>(GameObject obj, K k) where K : Keyframe
        {
            var keys = obj.GetComponent<KeyframedObject>();
            keys.Add(k);
        }
        
        /// <summary>
        /// Safety: This must be of type T 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="keys"></param>
        /// <param name="time"></param>
        /// <param name="def"></param>
        /// <returns></returns>
        public static T? Next<T>(KeyframedObject keys, float time, T? def) where T : Keyframe
        {
            return (T?)Next(typeof(T), keys, time) ?? def;
        }

        public static Keyframe? Previous(Type type, KeyframedObject keys, float time)
        {
            var frames = keys.channels.GetValueOrDefault(type);
            return frames?.Values.LastOrDefault((k) => k != null && k.Time() <= time);
        }

        public static Keyframe? Next(Type type, KeyframedObject keys, float time)
        {
            var frames = keys.channels.GetValueOrDefault(type);
            return frames?.Values.FirstOrDefault((k) => k != null && k.Time() > time);
        }

        public float tValue(Keyframe next, float time)
        {
            return System.Math.Clamp(Time() == next.Time() ? 0 : Mathf.Clamp01(time - Time() / (next.Time() - Time())), 0f, 1f);
        }
    }

    [Serializable]
    public class PositionKeyFrame : Keyframe
    {
        [JsonProperty]
        Vector3 data;

        public override void Capture(GameObject obj, float time)
        {
            var capture = new PositionKeyFrame
            {
                data = obj.transform.position,
                time = time
            };
            SaveCapture(obj, capture);
        }

        public override void Apply(GameObject obj, float time)
        {
            MelonLogger.Msg($"pos: {data} | this.time: {this.time} | time: {time}");
            var tm = obj.transform;
            var next = Next(obj.GetComponent<KeyframedObject>(), Time(), this)!;

            tm.position = Vector3.Lerp(data, next.data, tValue(next, time));
        }
    }

    [Serializable]
    public class RotationKeyFrame : Keyframe
    {
        [JsonProperty]
        Quaternion data;

        public override void Capture(GameObject obj, float time)
        {
            var capture = new RotationKeyFrame
            {
                time = time,
                data = obj.transform.rotation
            };
            SaveCapture(obj, capture);
        }

        public override void Apply(GameObject obj, float time)
        {
            var tm = obj.transform;
            var next = Next(obj.GetComponent<KeyframedObject>(), Time(), this)!;

            tm.rotation = Quaternion.Slerp(data, next.data, tValue(next, time));
        }
    }

    [Serializable]
    public class FovKeyFrame : Keyframe
    {
        [JsonProperty]
        float data;

        public override void Capture(GameObject obj, float time)
        {
            var capture = new FovKeyFrame
            {
                time = time,
                data = obj.GetComponent<Camera>().fieldOfView
            };
            SaveCapture(obj, capture);
        }

        public override void Apply(GameObject obj, float time)
        {
            var tm = obj.GetComponent<Camera>();
            var next = Next(obj.GetComponent<KeyframedObject>(), Time(), this)!;

            tm.fieldOfView = Mathf.Lerp(data, next.data, tValue(next, time));
        }
    }

    [JsonProperty]
    Dictionary<Type, SortedList<float, Keyframe>> channels = new();
    Dictionary<Type, Keyframe> constructors = new();


    void Start()
    {
        ReplayAPI.onReplaySeeked += Apply;
    }

    void Destroy()
    {
        ReplayAPI.onReplaySeeked -= Apply;
    }

    void Apply(float time)
    {
        if (!enabled) return;

        foreach (var (type, _) in channels)
        {
            var frame = Keyframe.Previous(type, this, time) ?? Keyframe.Next(type, this, time);
            frame?.Apply(gameObject, time);
        }
    }

    protected void Ensure<K>() where K : Keyframe
    {
        if (!channels.ContainsKey(typeof(K)))
        {
            channels[typeof(K)] = new();
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

    public void Add<K>(K keyframe) where K : Keyframe
    {
        Ensure<K>();
        channels[typeof(K)].Remove(keyframe.Time());
        channels[typeof(K)].Add(keyframe.Time(), keyframe);
    }

    //
    // use like
    // ```
    // keyframes.Capture<PositionKeyFrame>();
    // keyframes.Capture<RotationKeyframe, FovKeyFrame>();
    // ```
    //
    /// <summary>
    /// Capure a keyframe and save it to a channel
    /// </summary>
    /// <typeparam name="K"></typeparam>
    /// <param name="time"></param>
    public void Capture<K>(float? time) where K : Keyframe, new()
    {
        Register<K>();
        constructors[typeof(K)].Capture(gameObject, time ?? ReplayAPI.CurrentTime);
    }
    public void Capture<K1, K2>()
        where K1 : Keyframe, new()
        where K2 : Keyframe, new()
    {
        float time = ReplayAPI.CurrentTime;
        Capture<K1>(time);
        Capture<K2>(time);        
    }
    public void Capture<K1, K2, K3>()
        where K1 : Keyframe, new()
        where K2 : Keyframe, new()
        where K3 : Keyframe, new()
    {
        float time = ReplayAPI.CurrentTime;
        Capture<K1>(time);
        Capture<K2>(time);        
        Capture<K3>(time);        
    }
    

    public K? Next<K>(float time) where K : Keyframe
    {
        return (K?)Keyframe.Next(typeof(K), this, time);
    }

    public K? Previous<K>(float time) where K : Keyframe
    {
        return (K?)Keyframe.Previous(typeof(K), this, time);
    }

    public void Remove<K>(K keyframe) where K : Keyframe
    {
        Ensure<K>();
        channels[typeof(K)].Remove(keyframe.Time());
    }
}