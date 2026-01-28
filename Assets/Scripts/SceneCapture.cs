using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class RigCaptureSequence : MonoBehaviour
{
    [Header("Rig")]
    public Transform cameraRigRoot;

    [Header("Capture Settings")]
    public int width = 512;
    public int height = 512;

    [Tooltip("Desired capture rate (frames per second).")]
    public int captureFPS = 30;

    [Tooltip("Number of Frames to capture.")]
    public int numFrames = 0;

    [Header("Output")]
    public string runName = "run_001";

    private Camera[] cams;
    private Dictionary<Camera, RenderTexture> rts = new Dictionary<Camera, RenderTexture>();
    private Texture2D readTex;

    private string runDir;
    private bool isCapturing = false;

    void Start()
    {
        if (cameraRigRoot == null) cameraRigRoot = transform;

        cams = cameraRigRoot.GetComponentsInChildren<Camera>(true);
        if (cams == null || cams.Length == 0)
        {
            Debug.LogError("First create cameras.");
            return;
        }

        readTex = new Texture2D(width, height, TextureFormat.RGB24, false);
        

        // Build output folders
        string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        runDir = Path.Combine(Application.dataPath, "..", "captures", runName, stamp);
        Directory.CreateDirectory(runDir);

        // Create per-camera folders and RTs
        rts.Clear();
        foreach (var cam in cams)
        {
            Directory.CreateDirectory(Path.Combine(runDir, cam.name)); 
            var rt = new RenderTexture(width, height, 24);
            rts[cam] = rt;
        }

    }

 //   void Update()
 //   {
  //      if (Input.GetKeyDown(startKey) && !isCapturing)
 //       {
 //
 //           StartCoroutine(CaptureMetadata(numFrames));
 //       }
 //   }

    [ContextMenu("Start Capture")]
    public void StartCaptureFromMenu()
    {
        if (!isCapturing)
            StartCoroutine(CaptureMetadata(numFrames));
            // https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Coroutine.html
    }



    // post 4/13 Mate-O: 
    // https://discussions.unity.com/t/how-to-save-a-picture-take-screenshot-from-a-camera-in-game/5792/4
    
    IEnumerator CaptureMetadata(int numFrames)
    {
        isCapturing = true;
        // deterministic time
        // https://docs.unity3d.com/6000.3/Documentation/Manual/time-capture-frame-rate.html
        Time.captureFramerate = Mathf.Max(1, captureFPS);

        float dt = 1f / Mathf.Max(1, captureFPS);

        RunMetadata meta = new RunMetadata
        {
            runName = runName,
            createdUtcIso = DateTime.UtcNow.ToString("o"),
            width = width,
            height = height,
            captureFPS = captureFPS,
            numFrames = numFrames,
            cameras = new List<CameraStaticInfo>(),
            frames = new List<FrameMetadata>()
        };

        // Static camera info
        foreach (var cam in cams)
        {
            meta.cameras.Add(new CameraStaticInfo
            {
                name = cam.name,
                fov = cam.fieldOfView,
                near = cam.nearClipPlane,
                far = cam.farClipPlane
            });
        }

        for (int frame = 0; frame < numFrames; frame++)
        {
            yield return new WaitForEndOfFrame();

            float tRef = frame * dt;

            FrameMetadata fm = new FrameMetadata
            {
                frame = frame,
                t_ref = tRef,
                poses = new List<CameraPose>()
            };

            foreach (var cam in cams)
            {
                // Render
                cam.targetTexture = rts[cam];
                cam.Render();

                // Read back pixels
                RenderTexture.active = rts[cam];
                readTex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                readTex.Apply();

                cam.targetTexture = null;
                RenderTexture.active = null;

                // Save image
                byte[] png = readTex.EncodeToPNG();
                string imgPath = Path.Combine(runDir, cam.name, $"frame_{frame:D6}.png");
                File.WriteAllBytes(imgPath, png);

                // Save pose
                Transform tr = cam.transform;
                fm.poses.Add(new CameraPose
                {
                    camera = cam.name,
                    px = tr.position.x, py = tr.position.y, pz = tr.position.z,
                    qx = tr.rotation.x, qy = tr.rotation.y, qz = tr.rotation.z, qw = tr.rotation.w,
                    image = Path.Combine(cam.name, $"frame_{frame:D6}.png")
                });
            }

            meta.frames.Add(fm);
        }

        // Write JSON
        string jsonPath = Path.Combine(runDir, "frames.json");
        File.WriteAllText(jsonPath, JsonUtility.ToJson(meta, true));
        foreach (var kv in rts)
        {
            kv.Value.Release();
        }
        rts.Clear();

        // Cleanup deterministic mode
        Time.captureFramerate = 0;
        isCapturing = false;

        Debug.Log($"Capture complete. Saved to:\n{runDir}");
    }

    // ---------------- Structure to save metadata ----------------
    // https://docs.unity3d.com/2020.1/Documentation/Manual/JSONSerialization.html 

    [Serializable]
    public class RunMetadata
    {
        public string runName;
        public string createdUtcIso;
        public int width, height;
        public int captureFPS;
        public int numFrames;

        public List<CameraStaticInfo> cameras;
        public List<FrameMetadata> frames;
    }

    [Serializable]
    public class CameraStaticInfo
    {
        public string name;
        public float fov, near, far;
    }

    [Serializable]
    public class FrameMetadata
    {
        public int frame;
        public float t_ref;
        public List<CameraPose> poses;
    }

    [Serializable]
    public class CameraPose
    {
        public string camera;
        public float px, py, pz;
        public float qx, qy, qz, qw;
        public string image;
    }
}

