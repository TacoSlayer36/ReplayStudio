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
internal class KeyframedObject : MonoBehaviour
{
    public abstract class Keyframe
    {
        protected float time;

        public abstract Keyframe Capture(GameObject obj, float time);
        public abstract void Apply(GameObject obj, float t);

        public Keyframe(float time) { this.time = time; }

        public float Time()
        {
            return time;
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
            return (T?)Next(typeof(T), keys, time);
        }

        public static Keyframe? Previous(Type type, KeyframedObject keys, float time)
        {
            var frames = keys.channels.GetValueOrDefault(type);
            return frames?.Values.LastOrDefault((k) => k?.Time() <= time);
        }

        public static Keyframe? Next(Type type, KeyframedObject keys, float time)
        {
            var frames = keys.channels.GetValueOrDefault(type);
            return frames?.Values.FirstOrDefault((k) => k?.Time() > time);
        }

        public float tValue(Keyframe next, float time)
        {
            return Time() == next.Time() ? 0 : Mathf.Clamp01(time - Time() / (next.Time() - Time()));
        }
    }

    public class PositionKeyFrame : Keyframe
    {
        Vector3 data;

        public PositionKeyFrame() : base(0) {}

        public override Keyframe Capture(GameObject obj, float time)
        {
            var ret = new PositionKeyFrame();
            ret.data = obj.transform.position;
            ret.time = time;
            return ret;
        }

        public override void Apply(GameObject obj, float time)
        {
            var tm = obj.transform;
            var next = Next(obj.GetComponent<KeyframedObject>(), Time(), this)!;

            tm.position = Vector3.Lerp(data, next.data, tValue(next, time));
        }
    }

    public class RotationKeyFrame : Keyframe
    {
        Quaternion data;

        public RotationKeyFrame() : base(0) {}
        public RotationKeyFrame(Quaternion data) : base(ReplayAPI.CurrentTime)
        {
            this.data = data;
        }

        public override Keyframe Capture(GameObject obj, float time)
        {
            var ret = new RotationKeyFrame();
            ret.time = time;
            ret.data = obj.transform.rotation;
            return ret;
        }

        public override void Apply(GameObject obj, float time)
        {
            var tm = obj.transform;
            var next = Next(obj.GetComponent<KeyframedObject>(), Time(), this)!;

            tm.rotation = Quaternion.Slerp(data, next.data, tValue(next, time));
        }
    }

    public class FovKeyFrame : Keyframe
    {
        float data;

        public FovKeyFrame() : base(0) {}

        public override Keyframe Capture(GameObject obj, float time)
        {
            var ret = new FovKeyFrame();
            ret.time = time;
            ret.data = obj.GetComponent<Camera>().fieldOfView;
            return ret;
        }

        public override void Apply(GameObject obj, float time)
        {
            var tm = obj.GetComponent<Camera>();
            var next = Next(obj.GetComponent<KeyframedObject>(), Time(), this)!;

            tm.fieldOfView = Mathf.Lerp(data, next.data, tValue(next, time));
        }
    }

    Dictionary<Type, SortedList<float, Keyframe>> channels = new();
    Dictionary<Type, Keyframe> constructors = new();


    void OnUpdate()
    {
        var time = ReplayAPI.CurrentTime;

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