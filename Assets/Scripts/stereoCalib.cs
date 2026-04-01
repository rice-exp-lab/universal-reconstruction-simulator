using OpenCVForUnity.Calib3dModule;
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.ImgprocModule;
using OpenCVForUnity.ObjdetectModule;
using OpenCVForUnity.UnityIntegration;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class stereoCalib : MonoBehaviour
{
    [Header("References")]
    public Camera cameraCapture1;
    public Camera cameraCapture2;
    public RawImage display1;
    public RawImage display2;

    [Header("Charuco Parameters")]
    public charucoParams charucoParams;
    private int squaresX;
    private int squaresY;
    private float squareLength;
    private float markerLength;
    private int dictionaryId;

    [Header("Image Resolution")]
    public int imgWidth = 1280;
    public int imgHeight = 720;

    //[Header("Calibration")]
    //[Tooltip("RMS threshold to consider calibration valid")]
    private double rmsThreshold = 1.0;
    private int minFrames = 3;
    private int minCommonIds = 6;

    [Header("Save Results")]
    public bool savePng = false;
    public string imgDir;

    private RenderTexture rt1, rt2;
    private Texture2D tex1, tex2;
    private Mat rgba1, gray1, rgba2, gray2;

    private CharucoBoard board;
    private CharucoDetector detector;
    private ArucoDetector arucoDetector;

    private List<Mat> allObjectPoints  = new List<Mat>();
    private List<Mat> allImagePoints1  = new List<Mat>();
    private List<Mat> allImagePoints2  = new List<Mat>();

    void Start()
    {
        rt1 = new RenderTexture(imgWidth, imgHeight, 24);
        rt1.name = "RT Cam1";
        rt2 = new RenderTexture(imgWidth, imgHeight, 24);
        rt2.name = "RT Cam2";

        CameraInic(cameraCapture1, rt1);
        CameraInic(cameraCapture2, rt2);

        tex1  = new Texture2D(imgWidth, imgHeight, TextureFormat.RGBA32, false);
        rgba1 = new Mat(imgHeight, imgWidth, CvType.CV_8UC4);
        gray1 = new Mat(imgHeight, imgWidth, CvType.CV_8UC1);

        tex2  = new Texture2D(imgWidth, imgHeight, TextureFormat.RGBA32, false);
        rgba2 = new Mat(imgHeight, imgWidth, CvType.CV_8UC4);
        gray2 = new Mat(imgHeight, imgWidth, CvType.CV_8UC1);

        squaresX = charucoParams.squaresX;
        squaresY = charucoParams.squaresY;
        squareLength = charucoParams.squareLength;
        markerLength = charucoParams.markerLength;
        dictionaryId = (int)charucoParams.dictionaryId;

        Dictionary dict = Objdetect.getPredefinedDictionary(dictionaryId);
        board = new CharucoBoard(new Size(squaresX, squaresY), squareLength, markerLength, dict);
        board.setLegacyPattern(false);

        DetectorParameters detectorParams = new DetectorParameters();
        detectorParams.set_polygonalApproxAccuracyRate(0.05);
        detectorParams.set_minMarkerPerimeterRate(0.02);
        detectorParams.set_maxErroneousBitsInBorderRate(0.6);

        CharucoParameters charucoParameters = new CharucoParameters();
        charucoParameters.set_minMarkers(2);

        RefineParameters refineParameters = new RefineParameters(10f, 3f, true);
        detector     = new CharucoDetector(board, charucoParameters, detectorParams);
        arucoDetector = new ArucoDetector(dict, detectorParams, refineParameters);
    }

    void Update()
    {
        cameraDetect(rt1, tex1, gray1, rgba1, display1, out Mat corners1, out Mat ids1);
        cameraDetect(rt2, tex2, gray2, rgba2, display2, out Mat corners2, out Mat ids2);

        try
        {
            var kb = Keyboard.current;
            if (kb.spaceKey.wasPressedThisFrame)
                SaveFrame(corners1, ids1, corners2, ids2);
            if (kb.cKey.wasPressedThisFrame)
                RunCalibration();
        }
        finally
        {
            corners1.Dispose();
            ids1.Dispose();
            corners2.Dispose();
            ids2.Dispose();
        }
    }

    void CameraInic(Camera camera, RenderTexture rt)
    {
        Debug.Log($"Initializing {camera.name} RT {rt.name}");
        camera.targetTexture = rt;
        float effectiveSensorHeight = camera.sensorSize.x / ((float)imgWidth / imgHeight);
        camera.sensorSize = new Vector2(camera.sensorSize.x, effectiveSensorHeight);
        camera.aspect     = (float)imgWidth / imgHeight;
    }

    private void cameraDetect(RenderTexture rt, Texture2D tex, Mat gray, Mat rgba,
                               RawImage display,
                               out Mat charucoCorners, out Mat charucoIds)
    {
        charucoCorners = new Mat();
        charucoIds     = new Mat();

        RenderTexture.active = rt;
        tex.ReadPixels(new UnityEngine.Rect(0, 0, tex.width, tex.height), 0, 0);
        tex.Apply();

        OpenCVMatUtils.Texture2DToMat(tex, rgba);
        Imgproc.cvtColor(rgba, gray, Imgproc.COLOR_RGBA2GRAY);

        List<Mat> markerCorners = new List<Mat>();
        List<Mat> rejected      = new List<Mat>();
        Mat       markerIds     = new Mat();

        arucoDetector.detectMarkers(gray, markerCorners, markerIds, rejected);
        detector.detectBoard(gray, charucoCorners, charucoIds, markerCorners, markerIds);

        Debug.Log($"Corners: {charucoIds.total()}, Markers: {markerIds.total()}");

        if (charucoIds.total() > 0){
            DrawResults(rgba, markerCorners, markerIds, charucoCorners, charucoIds);
        }
        OpenCVMatUtils.MatToTexture2D(rgba, tex);
        display.texture = tex;
        

        markerIds.Dispose();
        foreach (var m in markerCorners) m.Dispose();
        foreach (var m in rejected)      m.Dispose();
    }

    private void DrawResults(Mat rgba, List<Mat> markerCorners, Mat markerIds,
                              Mat charucoCorners, Mat charucoIds)
    {
        Mat rgbTemp = new Mat();
        Imgproc.cvtColor(rgba, rgbTemp, Imgproc.COLOR_RGBA2RGB);
        Objdetect.drawDetectedMarkers(rgbTemp, markerCorners, markerIds, new Scalar(255, 0, 0));
        Objdetect.drawDetectedCornersCharuco(rgbTemp, charucoCorners, charucoIds, new Scalar(0, 255, 0));
        Imgproc.cvtColor(rgbTemp, rgba, Imgproc.COLOR_RGB2RGBA);
        rgbTemp.Dispose();
    }

    void SaveFrame(Mat corners1, Mat ids1, Mat corners2, Mat ids2)
    {
        // check corners
        if (ids1.total() < minCommonIds || ids2.total() < minCommonIds
            || corners1.rows() <= 0 || corners2.rows() <= 0
            || corners1.cols() <= 0 || corners2.cols() <= 0)
        {
            Debug.LogWarning($"Frame discarded: cam1={ids1.total()} ids, cam2={ids2.total()} ids");
            return;
        }

        SaveSyncedFrame(corners1, ids1, corners2, ids2);
    }

    void SaveSyncedFrame(Mat corners1, Mat ids1, Mat corners2, Mat ids2)
    {
        var set1 = new HashSet<int>();
        var set2 = new HashSet<int>();

        for (int i = 0; i < (int)ids1.total(); i++)
            set1.Add((int)ids1.get(i, 0)[0]);
        for (int i = 0; i < (int)ids2.total(); i++)
            set2.Add((int)ids2.get(i, 0)[0]);

        var commonIds = new List<int>(set1);
        commonIds.RemoveAll(id => !set2.Contains(id));
        commonIds.Sort();
        if (commonIds.Count < minCommonIds)
        {
            Debug.LogWarning($"Frame discarded: {commonIds.Count} common IDs");
            return;
        }

        Mat filteredCorners1 = FilterCornersByIds(corners1, ids1, commonIds);
        Mat filteredCorners2 = FilterCornersByIds(corners2, ids2, commonIds);
        Mat objP             = BuildObjectPoints(commonIds);

        allObjectPoints.Add(objP);
        allImagePoints1.Add(filteredCorners1);
        allImagePoints2.Add(filteredCorners2);

        Debug.Log($"Frame {allObjectPoints.Count} saved: {commonIds.Count} common IDs");
    }

    Mat FilterCornersByIds(Mat corners, Mat ids, List<int> commonIds)
    {
        var idToIndex = new Dictionary<int, int>();
        for (int i = 0; i < (int)ids.total(); i++)
            idToIndex[(int)ids.get(i, 0)[0]] = i;

        // convert format
        Mat corners32f = new Mat();
        corners.convertTo(corners32f, CvType.CV_32FC2);

        Mat filtered = new Mat(commonIds.Count, 1, CvType.CV_32FC2);
        for (int i = 0; i < commonIds.Count; i++)
        {
            int    srcIdx = idToIndex[commonIds[i]];
            double[] c   = corners.get(srcIdx, 0);
            filtered.put(i, 0, c);
        }
        corners32f.Dispose();
        return filtered;
    }

    Mat BuildObjectPoints(List<int> commonIds)
    {
        Mat objP = new Mat(commonIds.Count, 1, CvType.CV_32FC3);
        for (int i = 0; i < commonIds.Count; i++)
        {
            double[] p3d = board.getChessboardCorners().get(commonIds[i], 0);
            objP.put(i, 0, p3d);
        }
        return objP;
    }

    void RunCalibration()
    {
        if (allObjectPoints.Count < minFrames)
        {
            Debug.LogWarning($"Not enough frames: {allObjectPoints.Count}/{minFrames}");
            return;
        }

        Mat             camMatrix1  = Mat.eye(3, 3, CvType.CV_64FC1);
        Mat             camMatrix2  = Mat.eye(3, 3, CvType.CV_64FC1);
        MatOfDouble     distCoeffs1 = new MatOfDouble(0, 0, 0, 0, 0);
        MatOfDouble     distCoeffs2 = new MatOfDouble(0, 0, 0, 0, 0);
        List<Mat>       rvecs       = new List<Mat>();
        List<Mat>       tvecs       = new List<Mat>();
        Size            imageSize   = gray1.size();

        // intrisincs
        double rms1 = Calib3d.calibrateCamera(
            allObjectPoints, allImagePoints1, imageSize,
            camMatrix1, distCoeffs1, rvecs, tvecs);

        double rms2 = Calib3d.calibrateCamera(
            allObjectPoints, allImagePoints2, imageSize,
            camMatrix2, distCoeffs2, rvecs, tvecs);

        Debug.Log($"RMS cam1: {rms1:F4}  |  RMS cam2: {rms2:F4}");

        if (rms1 > rmsThreshold || rms2 > rmsThreshold)
        {
            Debug.LogWarning("RMS high for individual calibration. Check captures");
            return;
        }

        // stereo
        Mat R = new Mat();
        Mat T = new Mat();
        Mat E = new Mat();
        Mat F = new Mat();

        double rmsS = Calib3d.stereoCalibrate(
            allObjectPoints,
            allImagePoints1, allImagePoints2,
            camMatrix1, distCoeffs1,
            camMatrix2, distCoeffs2,
            imageSize, R, T, E, F,
            Calib3d.CALIB_FIX_INTRINSIC);

        Debug.Log($"Stereo RMS: {rmsS:F4}");

        if (rmsS < rmsThreshold)
        {
            Debug.Log("Stereo Calibration OK");
            Debug.Log("K1:\n"  + camMatrix1.dump());
            Debug.Log("K2:\n"  + camMatrix2.dump());
            Debug.Log("R:\n"   + R.dump());
            Debug.Log("T:\n"   + T.dump());
            compareCalibration(R, T, F);
        }
        else
        {
            Debug.LogWarning($"RMS High({rmsS:F4})");
        }

        R.Dispose(); T.Dispose(); E.Dispose(); F.Dispose();
        foreach (var m in rvecs) m.Dispose();
        foreach (var m in tvecs) m.Dispose();
    }

    void compareCalibration(Mat R, Mat tvec, Mat F)
    {
        Quaternion boardRotCam = Quaternion.Inverse(cameraCapture2.transform.rotation) * cameraCapture1.transform.rotation;
        Matrix4x4 rotationMatrixUnity = Matrix4x4.Rotate(boardRotCam);

        Matrix4x4 M = Matrix4x4.identity;
        M.m11 = -1;

        Matrix4x4 rotMatrixUnityCV = M * rotationMatrixUnity * M;
        Mat rotMatrixUnityCV3 = new Mat(3, 3, CvType.CV_64FC1);
        rotMatrixUnityCV3.put(0, 0, rotMatrixUnityCV.m00); rotMatrixUnityCV3.put(0, 1, rotMatrixUnityCV.m01); rotMatrixUnityCV3.put(0, 2, rotMatrixUnityCV.m02);
        rotMatrixUnityCV3.put(1, 0, rotMatrixUnityCV.m10); rotMatrixUnityCV3.put(1, 1, rotMatrixUnityCV.m11); rotMatrixUnityCV3.put(1, 2, rotMatrixUnityCV.m12);
        rotMatrixUnityCV3.put(2, 0, rotMatrixUnityCV.m20); rotMatrixUnityCV3.put(2, 1, rotMatrixUnityCV.m21); rotMatrixUnityCV3.put(2, 2, rotMatrixUnityCV.m22);

        Debug.Log("Rotation UNITY:X: " + rotMatrixUnityCV3.dump());

        Mat R_cv_t = new Mat();
        Core.transpose(R, R_cv_t);
        Mat R_err = new Mat();
        Core.gemm(rotMatrixUnityCV3, R_cv_t, 1.0, new Mat(), 0.0, R_err);

        double trace = R_err.get(0, 0)[0] + R_err.get(1, 1)[0] + R_err.get(2, 2)[0];
        double cosTheta = (trace - 1.0) / 2.0;

        cosTheta = Mathf.Clamp((float)cosTheta, -1f, 1f);

        double thetaRad = System.Math.Acos(cosTheta);
        double thetaDeg = thetaRad * 180.0 / System.Math.PI;

        Debug.Log($"Rotation error: {thetaDeg:F4} degrees");

        // Get the position of the board in camera coord system to compare to tvec
        // y axis in OpenCV is negativa but both cameras have negative coordinates, no need to convert
        // x axis is right handed in openCV but left in Unity -> convert 
        Vector3 boardPosCam = cameraCapture2.transform.InverseTransformPoint(cameraCapture1.transform.position);

        Vector3 boardPosCamCV = new Vector3(
            boardPosCam.x,
            -boardPosCam.y,
            boardPosCam.z);

        Debug.Log($"Position Unity: X:{boardPosCamCV.x:F3}, Y:{boardPosCamCV.y:F3}, Z:{boardPosCamCV.z:F3}");

        // Conver tvec matrix to vector 
        double x_cv = tvec.get(0, 0)[0];
        double y_cv = tvec.get(1, 0)[0];
        double z_cv = tvec.get(2, 0)[0];

        // Get distance error
        float errorDist = Vector3.Distance(new Vector3((float)x_cv, (float)y_cv, (float)z_cv), boardPosCamCV);
        Debug.Log($"Distance error: {errorDist * 1000:F2} millimeters");


        // Get evaluation for F
        Mat pts1 = allImagePoints1[0];
        Mat pts2 = allImagePoints2[0];

        //u,v
        double[] p1 = pts1.get(0, 0); 
        double[] p2 = pts2.get(0, 0);

        // x,y,1
        Mat x1 = new Mat(3, 1, CvType.CV_64F);
        x1.put(0, 0, p1[0]);
        x1.put(1, 0, p1[1]);
        x1.put(2, 0, 1.0);

        Mat x2 = new Mat(3, 1, CvType.CV_64F);
        x2.put(0, 0, p2[0]);
        x2.put(1, 0, p2[1]);
        x2.put(2, 0, 1.0);

        // temp = F * x1
        Mat temp = new Mat();
        Core.gemm(F, x1, 1.0, new Mat(), 0.0, temp);

        // val = x2^T * temp
        Mat result = new Mat();
        Core.gemm(x2.t(), temp, 1.0, new Mat(), 0.0, result);

        double val = result.get(0, 0)[0];

        Debug.Log($"Epipolar error: {val}");

    }
}