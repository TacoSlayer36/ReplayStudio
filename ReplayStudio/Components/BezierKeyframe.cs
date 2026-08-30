using System;
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

    static String NameForObj(GameObject obj) {
        return obj.name + obj.GetEntityId();
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

    internal Transform handle;
    internal Transform preHandle;
    internal Transform postHandle;
    internal GameObject? renderer;
    internal LineRenderer? preLine;
    internal LineRenderer? postLine;
    internal LineRenderer? spline;
    [Newtonsoft.Json.JsonIgnore]
    public Vector3[] PointsAlong;

    public String Obj;

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
        Obj = "empty";
        PointsAlong = Array.Empty<Vector3>();
    }

    void UpdateRenderer(bool _oldvalue, bool enabled) {
        renderer?.SetActive(enabled);
        preLine?.gameObject.SetActive(enabled);
        postLine?.gameObject.SetActive(enabled);
        spline?.gameObject.SetActive(enabled);
    }

    public void RenderInternal(BezierKeyframe? next)
    {
        if (handle != null)
        {
            if (preHandle != null && preLine != null) // TODO: Get Sy to fix the error that happens without these checks >:3
                preLine.SetPositions(new Vector3[] { PreHandle, Handle });
            if (postHandle != null && postLine != null)
                postLine.SetPositions(new Vector3[] { PostHandle, Handle });
        }

        if (next != null)
        {            
            PointsAlong = new Vector3[Core.Settings.SplineResolution.EditedValue];
            for (int i = 0; i < PointsAlong.Length; i++)
            {
                float t = i / (float)PointsAlong.Length;
                PointsAlong[i] = Qerp(this, next, t);
            }
            spline?.gameObject.SetActive(Core.Settings.RenderKeyframeWidgets.Value);
        }
        else
        {
            PointsAlong = Array.Empty<Vector3>();
            spline?.gameObject.SetActive(false);
        }

        if (spline != null) {
            spline.positionCount = PointsAlong.Length;
            spline.SetPositions(PointsAlong);
        }
    }

    public override void Render(KeyframedObject keys)
    {
        BezierKeyframe? next = (BezierKeyframe?)Next(typeof(BezierKeyframe), keys, snap);
        RenderInternal(next);
    }

    // internal void LateInitializeRenderer(GameObject parent) {
    //     var handle = this.handle.position;
    //     var preHandle = this.preHandle.position;
    //     var postHandle = this.postHandle.position;
    //     InitializeRenderer(parent);
    //     this.handle.position = handle; 
    //     this.preHandle.position = preHandle; 
    //     this.postHandle.position = postHandle; 
    // }

    /// <summary>
    /// Renderer constructor
    /// </summary>
    private BezierKeyframe(String obj, Snap snap)
    {
        this.snap = snap;
        this.Obj = obj;
        PointsAlong = Array.Empty<Vector3>();

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

            // renderer.transform.position = handle?.position ?? new();
            // renderer.transform.rotation = handle?.rotation ?? Quaternion.identity;
            handle = renderer.transform;

            Core.Settings.RenderKeyframeWidgets.OnEntryValueChanged.Subscribe(UpdateRenderer);
        }

        {
            var pre = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pre.name = "pre";
            pre.transform.SetParent(r.transform);
            pre.transform.localScale *= 0.1f;
            pre.GetComponent<Collider>().excludeLayers = ~0;
            pre.GetComponent<Collider>().includeLayers = 0;
            pre.SetActive(Core.Settings.RenderKeyframeWidgets.Value);

            Material mat = new(Shader.Find("Universal Render Pipeline/Unlit"))
            {
                color = Color.blue
            };
            pre.GetComponent<Renderer>().material = mat;

            // pre.transform.position = preHandle?.position ?? new();
            // pre.transform.rotation = preHandle?.rotation ?? Quaternion.identity;
            preHandle = pre.transform;

            var pl = new GameObject("line");
            pl.transform.SetParent(pre.transform);
            preLine = pl.AddComponent<LineRenderer>();
            preLine.material = mat;
            preLine.widthMultiplier = 0.01f;
        }

        {
            var post = GameObject.CreatePrimitive(PrimitiveType.Cube);
            post.name = "post";
            post.transform.SetParent(r.transform);
            post.transform.localScale *= 0.1f;
            post.GetComponent<Collider>().excludeLayers = ~0;
            post.GetComponent<Collider>().includeLayers = 0;
            post.SetActive(Core.Settings.RenderKeyframeWidgets.Value);

            Material mat = new(Shader.Find("Universal Render Pipeline/Unlit"))
            {
                color = Color.blue
            };
            post.GetComponent<Renderer>().material = mat;

            // post.transform.position = postHandle?.position ?? new();
            // post.transform.rotation = postHandle?.rotation ?? Quaternion.identity;
            postHandle = post.transform;

            var pl = new GameObject("line");
            pl.transform.SetParent(post.transform);
            postLine = pl.AddComponent<LineRenderer>();
            postLine.material = mat;
            postLine.widthMultiplier = 0.01f;
        }

        {
            var spl = new GameObject("spline");
            spl.transform.SetParent(parent.transform);
            spline = spl.AddComponent<LineRenderer>();
            spline.material = Core.DottedLineMat;
            spline.widthMultiplier = 0.01f;
            spline.gameObject.SetActive(Core.Settings.RenderKeyframeWidgets.Value);
        }
    }

    /// <summary>
    /// Capturing constructor
    /// </summary>
    public BezierKeyframe(GameObject obj, BezierKeyframe? prev, BezierKeyframe? next) : this(NameForObj(obj), new Snap(ReplayAPI.CurrentTime)) {
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

            var weight = 0f;
            var weightDiv = 0;
            if (dprevWeight != null) {
                weight += (float)dprevWeight;
                weightDiv += 1;
            }
            if (dnextWeight != null) {
                weight += (float)dnextWeight;
                weightDiv += 1;
            }
            if (weightDiv == 0) {
                weight = 1;
            } else {
                weight /= weightDiv;
            }
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
            Core.Settings.RenderKeyframeWidgets.OnEntryValueChanged.Unsubscribe(UpdateRenderer);
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

    [Newtonsoft.Json.JsonConstructor]
    public BezierKeyframe(String obj, Snap snap, Vector3 PreHandle, Vector3 Handle, Vector3 PostHandle) : this(obj, snap)
    {
        this.Handle = Handle;
        this.PreHandle = PreHandle;
        this.PostHandle = PostHandle;
    }
}

class TrackingBezierKeyframe : BezierKeyframe {
    public BezierKeyframe Focus;
    public Vector3 UpVector;

    protected static GameObject? FocusObject(GameObject obj) {
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

    public override void Render(KeyframedObject keys)
    {
        TrackingBezierKeyframe? next = (TrackingBezierKeyframe?)Next(typeof(TrackingBezierKeyframe), keys, snap);
        RenderInternal(next);
        Focus.RenderInternal(next?.Focus);
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

    [Newtonsoft.Json.JsonConstructor]
    public TrackingBezierKeyframe(String obj, Snap snap, Vector3 PreHandle, Vector3 Handle, Vector3 PostHandle, BezierKeyframe Focus) : base(obj, snap, PreHandle, Handle, PostHandle)
    {
        this.Focus = Focus;
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
