using Il2CppInterop.Runtime.InteropTypes.Arrays;
using MelonLoader;
using ReplayMod.Replay;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace ReplayStudio;

// Basically entirely vibecoded :(
public static class Rendering
{
    public static bool RENDERING = false;
    public static float StartTime = 0f;
    public static float EndTime = 10f;
    public static int FPS = 60;
    public static int width = 1920;
    public static int height = 1080;
    public static string outputFolder = "UserData/ReplayStudio/Render";

    // How many undrained frames we'll hold in memory before the main thread
    // blocks on Add(). Tune based on width*height*4 bytes per frame.
    private const int MaxBufferedFrames = 120;

    private static RenderTexture renderTexture;
    private static Texture2D currentTexture;
    private static int frameIndex = 0;
    private static string path;
    private static object renderRoutine;

    private static BlockingCollection<FrameData> frameQueue;
    private static Task writerTask;
    private static CancellationTokenSource writerCts;

    static float stepSize => 1f / FPS;

    public static void Render()
    {
        if (!ReplayAPI.IsPlaying) return;
        renderRoutine = MelonCoroutines.Start(_());

        IEnumerator _()
        {
            ReplayAPI.TogglePlayback(false);
            ReplayAPI.Seek(StartTime);

            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            Time.captureFramerate = FPS;

            renderTexture = new RenderTexture(width, height, 24);
            currentTexture = new Texture2D(width, height, TextureFormat.RGB24, false);
            path = Path.Combine(Application.dataPath, "..", outputFolder);
            Directory.CreateDirectory(path);

            CameraController.Camera.targetTexture = renderTexture;

            frameIndex = 0;
            frameQueue = new BlockingCollection<FrameData>(boundedCapacity: MaxBufferedFrames);
            writerCts = new CancellationTokenSource();
            writerTask = Task.Run(() => WriterLoop(writerCts.Token));

            yield return new WaitForSeconds(2f);

            RENDERING = true;
            ReplayAPI.TogglePlayback(true);

            while (ReplayAPI.CurrentTime + stepSize < EndTime)
            {
                yield return null;
            }

            StopRender();
        }
    }

    public static void StopRender()
    {
        if (renderRoutine != null) MelonCoroutines.Stop(renderRoutine);
        renderRoutine = null;
        RENDERING = false;

        ReplayAPI.TogglePlayback(false);
        Time.captureFramerate = 0;
        CameraController.Camera.targetTexture = null;
        renderTexture.Release();

        // Stop accepting new frames, let the writer drain what's queued,
        // then wait for it to finish so we don't lose buffered frames.
        frameQueue?.CompleteAdding();
        try
        {
            writerTask?.Wait();
        }
        catch (Exception ex)
        {
            MelonLogger.Error($"Writer thread error: {ex}");
        }

        frameQueue?.Dispose();
        frameQueue = null;
        writerCts?.Dispose();
        writerCts = null;
    }

    // Runs on the main thread. Reads pixels only — no PNG encoding here.
    public static void HandleRendering()
    {
        RenderTexture.active = renderTexture;
        CameraController.Camera.Render();
        currentTexture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        currentTexture.Apply();

        // Encode on the main thread — plain Unity API, avoids all Il2Cpp array issues.
        byte[] png = currentTexture.EncodeToPNG();

        var frame = new FrameData
        {
            Index = frameIndex,
            PngBytes = png
        };

        frameIndex++;
        RenderTexture.active = null;

        try
        {
            frameQueue.Add(frame);
        }
        catch (InvalidOperationException)
        {
            // Queue completed — drop the frame.
        }
    }

    private struct FrameData
    {
        public int Index;
        public byte[] PngBytes;
    }

    private static void WriterLoop(CancellationToken token)
    {
        foreach (var frame in frameQueue.GetConsumingEnumerable())
        {
            if (token.IsCancellationRequested) break;

            try
            {
                string filePath = Path.Combine(path, $"frame_{frame.Index:D5}.png");
                File.WriteAllBytes(filePath, frame.PngBytes);
            }
            catch (global::System.Exception ex)
            {
                MelonLogger.Error($"Failed writing frame {frame.Index}: {ex}");
            }
        }
    }
}