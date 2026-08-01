using ReplayMod.Replay;
using UnityEngine;


namespace ReplayStudio.Components;

#nullable enable
class BezierKeyframe : KeyframedObject.Keyframe
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
    static GameObject KeyframesForObj(GameObject obj)  {
        var found = KeyframesRoot.transform.FindChild(obj.name + obj.GetEntityId())?.gameObject;
            
        if (found == null) {
            found = new GameObject(obj.name + obj.GetEntityId());
            found.transform.SetParent(KeyframesRoot.transform);
        }
        return found;
    } 

    protected readonly Transform handle;
    protected readonly Transform preHandle;
    protected readonly Transform postHandle;

    protected GameObject? renderer;

    public Vector3 Handle { get => handle.position; set => handle.position = value; }

    public Vector3 PreHandle { get => preHandle.position; set => preHandle.position = value; }

    public Vector3 PostHandle { get => postHandle.position; set => postHandle.position = value; }

    // todo: locked and unlocked beziers
    // public enum ESubtype {
    //     Vector,
    //     Locked,
    //     Auto
    // };

    // public BezierKeyframe.ESubtype SubType;

    public BezierKeyframe() {
        renderer = null;
        handle = new();
        preHandle = new();
        postHandle = new();
    }

    ~BezierKeyframe() {
        if (renderer != null) {
            UnityEngine.Object.DestroyObject(renderer);
            Core.Settings.RenderBezierWidgets.OnEntryValueChanged.Unsubscribe(UpdateRenderer);
        }
    }

    void UpdateRenderer(bool _oldvalue, bool enabled) {
        renderer?.SetActive(enabled);
    }

    /// <summary>
    /// Capturing constructor
    /// </summary>
    protected BezierKeyframe(GameObject obj) {
        snap = new Snap(ReplayAPI.CurrentTime);
        var r = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        {
            r.name = snap.Time().ToString();
            r.GetComponent<Renderer>().material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            r.transform.SetParent(KeyframesForObj(obj).transform);
            r.transform.localScale *= 0.5f;
            r.GetComponent<Collider>().excludeLayers = ~0;
            r.GetComponent<Collider>().includeLayers = 0;
            // GameObject.Destroy(r.GetComponent<Collider>());
            // r.SetActive(true);
            renderer = r;

            handle = renderer.transform;

            Core.Settings.RenderBezierWidgets.OnEntryValueChanged.Subscribe(UpdateRenderer);
        }

        {
            var pre = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            pre.name = "pre";
            pre.GetComponent<Renderer>().material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            pre.transform.SetParent(renderer.transform);
            pre.transform.localScale *= 0.2f;
            r.GetComponent<Collider>().excludeLayers = ~0;
            r.GetComponent<Collider>().includeLayers = 0;
            // pre.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);
            // GameObject.Destroy(pre.GetComponent<Collider>());
            // pre.SetActive(true);

            preHandle = pre.transform;
        }

        {
            var post = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            post.name = "post";
            post.GetComponent<Renderer>().material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            post.transform.SetParent(r.transform);
            post.transform.localScale *= 0.2f;
            r.GetComponent<Collider>().excludeLayers = ~0;
            r.GetComponent<Collider>().includeLayers = 0;
            // post.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);
            // GameObject.Destroy(post.GetComponent<Collider>());
            // post.SetActive(true);

            postHandle = post.transform;
        }


        var position = obj.transform.position;

        var prev = (BezierKeyframe?)Previous(typeof(BezierKeyframe), obj.GetComponent<KeyframedObject>(), snap - 1);

        var next = (BezierKeyframe?)Next(typeof(BezierKeyframe), obj.GetComponent<KeyframedObject>(), snap);

        if (prev != null || next != null) {
            var dprev = (prev == null) ? (Vector3?)null : Vector3.Reflect(prev.PostHandle - prev.Handle, Vector3.Normalize(prev.Handle - position));
            
            var dnext = (next == null) ? (Vector3?)null : Vector3.Reflect(next.PreHandle - next.Handle, Vector3.Normalize(next.Handle - position)); 

            var dprevWeight = dprev?.magnitude;
            if (prev != null && dprevWeight != null && dprevWeight == 0) {
                dprev = (prev.Handle - position) / 3f;
                dprevWeight = dprev?.magnitude;
                prev.preHandle.localPosition = Vector3.back * (dprevWeight ?? 1);
                prev.postHandle.localPosition = Vector3.forward * (dprevWeight ?? 1);
                prev.handle.rotation = Quaternion.LookRotation(-(Vector3)dprev!, Vector3.up);
            }
            
            var dnextWeight = dnext?.magnitude;
            if (next != null && dnextWeight != null && dnextWeight == 0) {
                dnext = (next.Handle - position) / 3;
                dnextWeight = dnext?.magnitude;
                next.preHandle.localPosition = Vector3.back * (dnextWeight ?? 1);
                next.postHandle.localPosition = Vector3.forward * (dnextWeight ?? 1);
                next.handle.rotation = Quaternion.LookRotation((Vector3)dnext!, Vector3.up);
            }

            var computed = Vector3.Normalize((dnext ?? Vector3.zero) - (dprev ?? Vector3.zero));
            if (next != null && prev != null && Vector3.Dot(computed, next.Handle - prev.Handle) < 0) {
                // special case: in-betweens can favor 'loops' where we would prefer just linear
                computed = -computed;
            }

            PreHandle = Vector3.back * (dprevWeight ?? 1);
            PostHandle = Vector3.forward * (dnextWeight ?? 1);
            Handle = position;
            handle.rotation = Quaternion.LookRotation(computed, Vector3.up);
        } else {
            Handle = position;
            PreHandle = position;
            PostHandle = position;
        }

    }    

    public override void Move(GameObject obj, float time)
    {
        var keys = obj.GetComponent<KeyframedObject>();
        keys.Remove(this);
        snap = new Snap(ReplayAPI.CurrentTime);
        // todo: reorder children in the rendererer and change the name
        keys.Add(this);
    }
    protected static Vector3 Qerp(BezierKeyframe _0, BezierKeyframe _1, float t)
    {
        Vector3 a = Vector3.Lerp(_0.Handle, _0.PostHandle, t);
        Vector3 b = Vector3.Lerp(_0.PostHandle, _1.PreHandle, t);
        Vector3 c = Vector3.Lerp(_1.PreHandle, _1.Handle, t);

        Vector3 u = Vector3.Lerp(a, b, t);
        Vector3 v = Vector3.Lerp(b, c, t);

        return Vector3.Lerp(u, v, t);
    }

    public override void Apply(GameObject obj, float time)
    {
        var next = Next(obj.GetComponent<KeyframedObject>(), snap, this)!;

        float t = tValue(next, time);

        obj.transform.position = Qerp(this, next, t);
    }

    public override KeyframedObject.Keyframe Capture(GameObject obj)
    {
        return new BezierKeyframe(obj);
    }
}

// bad; unused
// class BezierRotKeyframe : BezierKeyframe {
//     public Quaternion Rotation;

//     public BezierRotKeyframe() : base() {}

//     public Quaternion PreRotation { get => preHandle.rotation; set => preHandle.rotation = value; }

//     public Quaternion PostRotation { get => postHandle.rotation; set => postHandle.rotation = value; }


//     public BezierRotKeyframe(GameObject obj) : base(obj) {
//         if (renderer != null) {
//             renderer.transform.rotation = obj.transform.rotation;
//         }
//         PreRotation = obj.transform.rotation;
//         PostRotation = obj.transform.rotation;
//     }

//     private static Quaternion Sqerp(BezierRotKeyframe _0, BezierRotKeyframe _1, float t)
//     {
//         Quaternion a = Quaternion.Slerp(_0.Rotation, _0.postHandle.rotation, t);
//         Quaternion b = Quaternion.Slerp(_0.postHandle.rotation, _1.preHandle.rotation, t);
//         Quaternion c = Quaternion.Slerp(_1.preHandle.rotation, _1.Rotation, t);

//         Quaternion u = Quaternion.Slerp(a, b, t);
//         Quaternion v = Quaternion.Slerp(b, c, t);

//         return Quaternion.Slerp(u, v, t);
//     }

//     public override KeyframedObject.Keyframe Capture(GameObject obj) {
//         return new BezierRotKeyframe(obj);
//     }

//     public override void Apply(GameObject obj, float time)
//     {
//         base.Apply(obj, time);

//         var tm = obj.transform;
//         var next = Next(obj.GetComponent<KeyframedObject>(), snap, this)!;

//         float t = tValue(next, time);

//         obj.transform.position = Qerp(this, next, t);
//         obj.transform.rotation = Sqerp(this, next, t);
//     }
// }
