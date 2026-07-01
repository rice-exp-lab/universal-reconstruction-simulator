using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEngine;

[Serializable]
public class CalibratedCameraSpec
{
    public string cameraName = "Cam_01";
    public GameObject prefabOverride;
    public float[] rotationCv = new float[9]
    {
        1f, 0f, 0f,
        0f, 1f, 0f,
        0f, 0f, 1f
    };
    public Vector3 translationCv;
    public float[] intrinsicK = new float[9];
}

public class CalibrationRigBuilder : MonoBehaviour
{
    [Header("Prefabs")]
    public GameObject cameraPrefab;

    [Header("Reference Camera")]
    public string referenceCameraName = "Cam_00";
    public float[] referenceIntrinsicK = new float[9];

    [Header("Stereo Calibration JSON")]
    public TextAsset stereoCalibrationJson;
    public bool loadJsonBeforeBuild = true;

    [Header("Generated Cameras")]
    public List<CalibratedCameraSpec> calibratedCameras = new List<CalibratedCameraSpec>();

    [Header("Intrinsics")]
    public bool applyIntrinsics = true;
    public int imageWidth = 1920;
    public int imageHeight = 1080;

    private static readonly Matrix4x4 CvUnityAxisFlip = Matrix4x4.Scale(new Vector3(1f, -1f, 1f));

    [ContextMenu("Build Rig From Calibration")]
    public void BuildRig()
    {
        if (loadJsonBeforeBuild && stereoCalibrationJson != null)
            LoadStereoJsonIntoCameraList();

        ClearChildren();

        GameObject referenceCamera = InstantiateCamera(referenceCameraName, cameraPrefab, transform);
        referenceCamera.transform.localPosition = Vector3.zero;
        referenceCamera.transform.localRotation = Quaternion.identity;
        ApplyIntrinsics(referenceCamera.GetComponent<Camera>(), referenceIntrinsicK);

        for (int i = 0; i < calibratedCameras.Count; i++)
        {
            CalibratedCameraSpec spec = calibratedCameras[i];
            GameObject prefab = spec.prefabOverride != null ? spec.prefabOverride : cameraPrefab;
            GameObject camObject = InstantiateCamera(spec.cameraName, prefab, transform);
            ApplyOpenCvExtrinsics(camObject.transform, spec.rotationCv, spec.translationCv);
            ApplyIntrinsics(camObject.GetComponent<Camera>(), spec.intrinsicK);

            Camera cam = camObject.GetComponent<Camera>();
            if (cam != null)
                cam.targetDisplay = i + 1;
        }
    }

    [ContextMenu("Load Stereo JSON Into Camera List")]
    public void LoadStereoJsonIntoCameraList()
    {
        if (stereoCalibrationJson == null)
        {
            Debug.LogWarning("No stereo calibration JSON assigned.");
            return;
        }

        string json = stereoCalibrationJson.text;
        float[] r = ReadFloatArray(json, "R", 9);
        float[] t = ReadFloatArray(json, "T", 3);
        float[] cam0K = ReadFloatArray(json, "cam0_K", 9);
        float[] cam1K = ReadFloatArray(json, "cam1_K", 9);
        float[] imgSize = ReadFloatArray(json, "img_size", 2);

        if (imgSize.Length >= 2)
        {
            imageWidth = Mathf.RoundToInt(imgSize[0]);
            imageHeight = Mathf.RoundToInt(imgSize[1]);
        }

        referenceIntrinsicK = cam0K;
        calibratedCameras.Clear();
        calibratedCameras.Add(new CalibratedCameraSpec
        {
            cameraName = "Cam_01",
            rotationCv = r,
            translationCv = new Vector3(t[0], t[1], t[2]),
            intrinsicK = cam1K
        });

        Debug.Log("Loaded stereo calibration JSON into rig builder.");
    }

    private void ApplyOpenCvExtrinsics(Transform cameraTransform, float[] rotationCv, Vector3 translationCv)
    {
        Matrix4x4 rCv = MatrixFromRowMajor(rotationCv);
        Matrix4x4 rUnityRelative = CvUnityAxisFlip * rCv * CvUnityAxisFlip;

        Quaternion referenceToCamera = QuaternionFromMatrix(rUnityRelative).normalized;
        Quaternion cameraRotation = Quaternion.Inverse(referenceToCamera);

        Vector3 translationUnityCameraSpace = new Vector3(
            translationCv.x,
            -translationCv.y,
            translationCv.z);

        Vector3 cameraPosition = -(cameraRotation * translationUnityCameraSpace);

        cameraTransform.localPosition = cameraPosition;
        cameraTransform.localRotation = cameraRotation;
    }

    private void ApplyIntrinsics(Camera cam, float[] k)
    {
        if (!applyIntrinsics || cam == null || k == null || k.Length < 9)
            return;

        float fx = k[0];
        float fy = k[4];
        float cx = k[2];
        float cy = k[5];

        if (Mathf.Approximately(fx, 0f) || Mathf.Approximately(fy, 0f))
            return;

        float near = cam.nearClipPlane;
        float far = cam.farClipPlane;
        float left = -cx * near / fx;
        float right = (imageWidth - cx) * near / fx;
        float top = cy * near / fy;
        float bottom = -(imageHeight - cy) * near / fy;

        cam.projectionMatrix = Matrix4x4.Frustum(left, right, bottom, top, near, far);
    }

    private GameObject InstantiateCamera(string cameraName, GameObject prefab, Transform parent)
    {
        if (prefab == null)
        {
            GameObject fallback = new GameObject(cameraName);
            fallback.transform.SetParent(parent, false);
            fallback.AddComponent<Camera>();
            return fallback;
        }

        GameObject camObject = Instantiate(prefab, parent);
        camObject.name = cameraName;
        return camObject;
    }

    private void ClearChildren()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);
    }

    private static Matrix4x4 MatrixFromRowMajor(float[] values)
    {
        Matrix4x4 matrix = Matrix4x4.identity;
        if (values == null || values.Length < 9)
            return matrix;

        matrix.m00 = values[0]; matrix.m01 = values[1]; matrix.m02 = values[2];
        matrix.m10 = values[3]; matrix.m11 = values[4]; matrix.m12 = values[5];
        matrix.m20 = values[6]; matrix.m21 = values[7]; matrix.m22 = values[8];
        return matrix;
    }

    private static Quaternion QuaternionFromMatrix(Matrix4x4 matrix)
    {
        Vector3 forward = new Vector3(matrix.m02, matrix.m12, matrix.m22);
        Vector3 upwards = new Vector3(matrix.m01, matrix.m11, matrix.m21);
        return Quaternion.LookRotation(forward, upwards);
    }

    private static float[] ReadFloatArray(string json, string key, int expectedCount)
    {
        string arrayText = ExtractJsonArray(json, key);
        MatchCollection matches = Regex.Matches(
            arrayText,
            @"[-+]?(?:\d+\.?\d*|\.\d+)(?:[eE][-+]?\d+)?");

        List<float> values = new List<float>();
        foreach (Match match in matches)
            values.Add(float.Parse(match.Value, CultureInfo.InvariantCulture));

        if (values.Count < expectedCount)
            Debug.LogWarning($"Key '{key}' had {values.Count} values, expected {expectedCount}.");

        return values.ToArray();
    }

    private static string ExtractJsonArray(string json, string key)
    {
        int keyIndex = json.IndexOf($"\"{key}\"", StringComparison.Ordinal);
        if (keyIndex < 0)
            return string.Empty;

        int start = json.IndexOf('[', keyIndex);
        if (start < 0)
            return string.Empty;

        int depth = 0;
        for (int i = start; i < json.Length; i++)
        {
            if (json[i] == '[')
                depth++;
            else if (json[i] == ']')
            {
                depth--;
                if (depth == 0)
                    return json.Substring(start, i - start + 1);
            }
        }

        return string.Empty;
    }
}
