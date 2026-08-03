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

    void UpdateRenderer(bool _oldvalue, bool enabled) {
        renderer?.SetActive(enabled);
    }

    /// <summary>
    /// Renderer constructor
    /// </summary>
    private BezierKeyframe(GameObject obj, Snap snap)
    {
        this.snap = snap;

        var r = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        {
            r.name = snap.Time().ToString();
            r.GetComponent<Renderer>().material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            r.transform.SetParent(KeyframesForObj(obj).transform);
            r.transform.localScale *= 0.5f;
            r.GetComponent<Collider>().excludeLayers = ~0;
            r.GetComponent<Collider>().includeLayers = 0;
            // GameObject.Destroy(r.GetComponent<Collider>());
            r.SetActive(Core.Settings.RenderBezierWidgets.Value);
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
    }

    /// <summary>
    /// Capturing constructor
    /// </summary>
    public BezierKeyframe(GameObject obj, BezierKeyframe? prev, BezierKeyframe? next) : this(obj, new Snap(ReplayAPI.CurrentTime)) {
        var position = obj.transform.position;

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

            var weight = 1f;
            var weightDiv = 0;
            if (dprevWeight != null) {
                weight += (float)dprevWeight;
                weightDiv += 1;
            }
            if (dnextWeight != null) {
                weight += (float)dnextWeight;
                weightDiv += 1;
            }
            weight /= weightDiv;
            PreHandle = Vector3.back * weight;
            PostHandle = Vector3.forward * weight;
            Handle = position;
            handle.rotation = Quaternion.LookRotation(computed, Vector3.up);
        } else {
            Handle = position;
            PreHandle = position;
            PostHandle = position;
        }

    }    
    public override void Remove() {
        if (renderer != null) {
            UnityEngine.Object.DestroyObject(renderer);
            Core.Settings.RenderBezierWidgets.OnEntryValueChanged.Unsubscribe(UpdateRenderer);
        }
    }

    public override void Move(GameObject obj, float time)
    {
        /// keep the renderer alive while moving
        var renderer = this.renderer;
        this.renderer = null;
        var keys = obj.GetComponent<KeyframedObject>();
        keys.Remove(this);
        snap = new Snap(time);
        // todo: reorder children in the rendererer and change the name
        keys.Add(this);
        this.renderer = renderer;
        if (this.renderer != null) {
            this.renderer.name = snap.Time().ToString();
        }
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

        var snap = new Snap(ReplayAPI.CurrentTime);
        var prev = (BezierKeyframe?)Previous(typeof(BezierKeyframe), obj.GetComponent<KeyframedObject>(), snap - 1);

        var next = (BezierKeyframe?)Next(typeof(BezierKeyframe), obj.GetComponent<KeyframedObject>(), snap);

        return new BezierKeyframe(obj, prev, next);
        
    }
}

class TrackingBezierKeyframe : BezierKeyframe {
    public BezierKeyframe Focus;
    public Vector3 UpVector;

    protected GameObject? FocusObject(GameObject obj) {
        return obj.GetComponent<CameraRig>()?.FocusObject;
    }

    public TrackingBezierKeyframe() : base() {
        Focus = new();
    }

    TrackingBezierKeyframe(GameObject obj) : 
        base(obj, 
            Previous<TrackingBezierKeyframe>(obj.GetComponent<KeyframedObject>(), new Snap(ReplayAPI.CurrentTime) - 1),
            Next<TrackingBezierKeyframe>(obj.GetComponent<KeyframedObject>(), new Snap(ReplayAPI.CurrentTime), null)
        )
    {
        UpVector = obj.GetComponent<CameraRig>()?.UpVector ?? Vector3.up;
        var focusObject = FocusObject(obj);
        if (focusObject != null) {
            Focus = new BezierKeyframe(
                focusObject,
                Previous<TrackingBezierKeyframe>(obj.GetComponent<KeyframedObject>(), new Snap(ReplayAPI.CurrentTime) - 1)?.Focus,
                Next<TrackingBezierKeyframe>(obj.GetComponent<KeyframedObject>(), new Snap(ReplayAPI.CurrentTime), null)?.Focus
            );
        } else {
            Focus = new();
        }
    }

    public override void Remove() {
        base.Remove();
        Focus.Remove();
    }

    public override void Move(GameObject obj, float time)
    {
        base.Move(obj, time);
        Focus.Move(obj, time);
    }

    public override void Apply(GameObject obj, float time)
    {
        var next = Next(obj.GetComponent<KeyframedObject>(), snap, this)!;

        float t = tValue(next, time);

        obj.transform.position = Qerp(this, next, t);

        var focusObject = FocusObject(obj);
        if (focusObject != null) {
            var focused = Qerp(Focus, next.Focus, t);
            var upVector = Vector3.Lerp(UpVector, next.UpVector, t);
            obj.transform.LookAt(focused, upVector);
            focusObject.transform.position = focused;
            // focusObject.transform.localPosition = Vector3(0, 0, focusObject.transform.localPosition.z);
        }
    }

    public override KeyframedObject.Keyframe Capture(GameObject obj)
    {
        return new TrackingBezierKeyframe(obj);
    }

    // [Newtonsoft.Json.JsonConstructor]
    // private TrackingBezierKeyframe(Transform preHandle, Transform handle, Transform postHandle) {


    // }
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
