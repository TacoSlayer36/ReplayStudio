using Il2CppPlayFab.MultiplayerModels;
using MelonLoader;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ReplayStudio;

/*
 * Handles everything regarding bezier curves.
 * 
 * Such curves are defined with the following hierarchy:
 * - BezierPath: the entire encapsulated path
 *   - segment: the path is divided into elementary segments, which are placed one after the other but do not affect each other directly
 *       - BezierControl: a combination of three controls that are manipulated in together
 *           - handle: a point for defining the curve
 */

// https://github.com/shamim-akhtar/bezier-curve
//[RegisterTypeInIl2Cpp]
public class BezierCurve
{
                        public static Vector3 Point3(float t, List<Vector3> controlPoints)
    {
        int N = controlPoints.Count - 1;
        if (N > 16)
        {
            Debug.Log("You have used more than 16 control points.");
            Debug.Log("The maximum control points allowed is 16.");
            controlPoints.RemoveRange(16, controlPoints.Count - 16);
        }
        if (t <= 0) return controlPoints[0];
        if (t >= 1) return controlPoints[controlPoints.Count - 1];

        Vector3 p = new Vector3();

        for (int i = 0; i < controlPoints.Count; ++i)
        {
            Vector3 bn = HelperFunctions.Bernstein(N, i, t) * controlPoints[i];
            p += bn;
        }

        return p;
    }

                    public Vector3 Point3(float t)
    {
        return Point3(t, getControls());
    }

    public List<BezierControl> BezierControls = new();

    private Vector3[] curvePoints;
    private GameObject lineRendererGO;
    private LineRenderer _lineRenderer;
    private LineRenderer lineRenderer
    {
        get
        {
            if (_lineRenderer == null)
                _lineRenderer = lineRendererGO?.GetComponent<LineRenderer>();

            return _lineRenderer;
        }
    }

    public void GeneratePoints(int pointCount)
    {
        List<Vector3> controls = getControls();
        Vector3[] points = new Vector3[pointCount];

        for (int i = 0; i < pointCount; i++)
        {
            points[i] = Point3(i / (pointCount - 1), controls);
        }
    }

    public void RenderPoints(int? pointCount = null)
    {
        if (pointCount != null)
            GeneratePoints((int)pointCount);

        lineRenderer.SetPositions(curvePoints);
    }

    public void InitializeLineRenderer()
    {
        lineRendererGO = GameObject.Instantiate(Core.LineTemplate);
        lineRendererGO.transform.SetParent(Core.DDOL_GameObjects.transform);
        lineRendererGO.SetActive(true);
    }

    public BezierCurve(List<BezierControl> bezierControls)
    {
        this.BezierControls = bezierControls;
    }

    private List<Vector3> getControls()
    {
        List<Vector3> points = new();
        foreach (BezierControl bezierControl in BezierControls)
            points.AddRange(bezierControl.GetControlPoints());
        return points;
    }
}

//[RegisterTypeInIl2Cpp]
public class BezierControl
{
    public enum ControlType
    {
        Smooth,
        Corner
    }
    public enum HandleInclusion
    {
        None,
        Left,
        Right,
        Both
    }
    public enum HandleType
    {
        Left = -1,
        Main = 0,
        Right = 1
    }

    private ControlType currentControlType = ControlType.Smooth;
    private HandleInclusion currentHandleInclusion = HandleInclusion.None;

    private Vector3 mainHandlePos = Vector3.zero;

    private Vector3 leftHandleOffset = Vector3.zero;
    private Vector3 rightHandleOffset = Vector3.zero;
    private Vector3 leftHandlePos
    {
        get
        {
            return mainHandlePos + leftHandleOffset;
        }
        set
        {
            leftHandleOffset = value - mainHandlePos;
        }
    }
    private Vector3 rightHandlePos
    {
        get
        {
            return mainHandlePos + rightHandleOffset;
        }
        set
        {
            rightHandleOffset = value - mainHandlePos;
        }
    }

    private GameObject leftHandleRenderer;
    private GameObject mainHandleRenderer;
    private GameObject rightHandleRenderer;

    public List<Vector3> GetControlPoints()
    {
        List<Vector3> controls = new();

        if (currentHandleInclusion is HandleInclusion.Left or HandleInclusion.Both)
            controls.Add(leftHandlePos);

        controls.Add(mainHandlePos);

        if (currentHandleInclusion is HandleInclusion.Right or HandleInclusion.Both)
            controls.Add(rightHandlePos);

        return controls;
    }

    public void MoveHandle(HandleType handleType, Vector3 pos)
    {
        if (handleType is HandleType.Left)
        {
            leftHandlePos = pos;

            if (currentControlType is ControlType.Smooth)
                rightHandleOffset = -leftHandleOffset;
        }

        if (handleType is HandleType.Main)
        {
            mainHandlePos = pos;
        }

        if (handleType is HandleType.Right)
        {
            rightHandlePos = pos;

            if (currentControlType is ControlType.Smooth)
                leftHandleOffset = -rightHandleOffset;
        }

        updateRenderers();
    }

    private void updateRenderers()
    {
        leftHandleRenderer.transform.position = leftHandlePos;
        mainHandleRenderer.transform.position = mainHandlePos;
        rightHandleRenderer.transform.position = rightHandlePos;

        leftHandleRenderer.SetActive(currentHandleInclusion is HandleInclusion.Left or HandleInclusion.Both);
        rightHandleRenderer.SetActive(currentHandleInclusion is HandleInclusion.Right or HandleInclusion.Both);
    }

    private void initializeRenderers()
    {
        removeRenderers();

        // TODO
    }

    private void removeRenderers()
    {
        GameObject.DestroyImmediate(leftHandleRenderer);
        GameObject.DestroyImmediate(mainHandleRenderer);
        GameObject.DestroyImmediate(rightHandleRenderer);
    }
}