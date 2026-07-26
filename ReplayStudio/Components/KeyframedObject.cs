using System;
using System.Collections.Generic;
using System.Linq;
using ReplayMod.Replay;
using UnityEngine;

namespace ReplayStudio.Components;

#nullable enable
internal class KeyframedObject : MonoBehaviour
{
    Dictionary<Type, SortedList<float, Keyframe>> framesStores = new();
    public abstract class Keyframe
    {
        readonly float time;

        public abstract void Apply(GameObject obj, float t);

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
            var frames = keys.framesStores[type];
            return frames.Values.LastOrDefault((k) => k?.Time() <= time);
        }

        public static Keyframe? Next(Type type, KeyframedObject keys, float time)
        {
            var frames = keys.framesStores[type];
            return frames.Values.FirstOrDefault((k) => k?.Time() > time);
        }

        public float tValue(Keyframe next, float time)
        {
            return time - Time() / (next.Time() - Time());
        }
    }

    public class PositionKeyFrame : Keyframe
    {
        readonly Vector3 data;

        public override void Apply(GameObject obj, float time)
        {
            var tm = obj.GetComponent<Transform>();
            var next = Next(obj.GetComponent<KeyframedObject>(), Time(), this)!;

            tm.position = Vector3.Lerp(data, next.data, tValue(next, time));
        }
    }

    public class RotationKeyFrame : Keyframe
    {
        readonly Quaternion data;

        public override void Apply(GameObject obj, float time)
        {
            var tm = obj.GetComponent<Transform>();
            var next = Next(obj.GetComponent<KeyframedObject>(), Time(), this)!;

            tm.rotation = Quaternion.Slerp(data, next.data, tValue(next, time));
        }
    }

    public class FovKeyFrame : Keyframe
    {
        readonly float data;

        public override void Apply(GameObject obj, float time)
        {
            var tm = obj.GetComponent<Camera>();
            var next = Next(obj.GetComponent<KeyframedObject>(), Time(), this)!;

            tm.fieldOfView = Mathf.Lerp(data, next.data, tValue(next, time));
        }
    }


    void OnUpdate()
    {
        var time = ReplayAPI.CurrentTime;

        foreach (var (type, _) in framesStores)
        {
            Keyframe.Next(type, this, time)?.Apply(gameObject, time);
        }
    }

    public void Add<K>(K keyframe) where K : Keyframe
    {
        framesStores[typeof(K)].Remove(keyframe.Time());
        framesStores[typeof(K)].Add(keyframe.Time(), keyframe);
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
        framesStores[typeof(K)].Remove(keyframe.Time());
    }
}
