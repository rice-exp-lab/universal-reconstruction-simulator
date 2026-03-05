using OpenCVForUnity.ArucoModule;
using OpenCVForUnity.Calib3dModule;
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.ImgcodecsModule;
using OpenCVForUnity.ImgprocModule;
using OpenCVForUnity.ObjdetectModule;
using OpenCVForUnity.UnityIntegration;
using OpenCVForUnity.UnityUtils.MOT.ByteTrack;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Playables;
using UnityEngine.UI;

public class CharucoCalibratorPro : MonoBehaviour
{
    [Header("Referencias")]
    public Camera cameraCapture;
    public RawImage display;

    [Header("Mismas medidas que el Generador")]
    public int squaresX = 2;
    public int squaresY = 2;
    public float squareLength = 0.5f;
    public float markerLength = 0.7f;

    [Header("Image Resolution")]
    public int width;
    public int height;

    [Header("Save Results")]
    public bool savePng = false;
    public string imgDir;


    private RenderTexture rt;
    private Texture2D tex;
    private Mat rgba, gray;
    private CharucoBoard board;
    private CharucoDetector detector;
    private ArucoDetector arucoDetector;
    private int captureIndex = 0;
    private List<Mat> allImagePoints = new List<Mat>();
    private List<Mat> allObjectPoints = new List<Mat>();
    private Mat _camMatrix;
    private MatOfDouble _distCoeffs;
    private Size imageSize = new Size();
    List<Mat> allCharucoCorners = new List<Mat>();
    List<Mat> allCharucoIds = new List<Mat>();
    List<Mat> filteredImages = new List<Mat>();

    void Start()
    {
        /*
         mi physical camera tiene:
            - sensor size= 5.568; 3.84
            - focal length=3.317
            -FOV=80 (horizontal)
            -aperture=f/2.7
         */

        // create render texture -> same aspect as camera
        float sensorAspect = cameraCapture.sensorSize.x / cameraCapture.sensorSize.y;
        int targetWidth = 1280;
        int targetHeight = Mathf.RoundToInt(targetWidth / sensorAspect);
        //int w = 1280; int h = 720;
        rt = new RenderTexture(targetWidth, targetHeight, 24);
        // set camera same size as render texture
        cameraCapture.targetTexture = rt;
        cameraCapture.aspect = sensorAspect; //(float)rt.width / (float)rt.height;

        tex = new Texture2D(targetWidth, targetHeight, TextureFormat.RGBA32, false);
        rgba = new Mat(targetHeight, targetWidth, CvType.CV_8UC4);
        gray = new Mat(targetHeight, targetWidth, CvType.CV_8UC1);


        // configure board (same as charuco in scene)
        Dictionary dict = Objdetect.getPredefinedDictionary(Objdetect.DICT_4X4_50);
        board = new CharucoBoard(new Size(squaresX, squaresY), squareLength, markerLength, dict);
        board.setLegacyPattern(false);
        
        // advanced parameters? make detection more flexible?? 
        DetectorParameters detectorParams = new DetectorParameters();
        //detectorParams.set_minDistanceToBorder(3);
        //detectorParams.set_useAruco3Detection(true);
        //detectorParams.set_cornerRefinementMethod(Objdetect.CORNER_REFINE_SUBPIX);

        //RefineParameters refineParameters = new RefineParameters(10f, 3f, true);

        //detector = new CharucoDetector(board, new CharucoParameters(), detectorParams, refineParameters);

        detectorParams.set_polygonalApproxAccuracyRate(0.05);
        detectorParams.set_minMarkerPerimeterRate(0.02);
        detectorParams.set_maxErroneousBitsInBorderRate(0.6);

        CharucoParameters charucoParameters = new CharucoParameters();
        charucoParameters.set_minMarkers(2);
        RefineParameters refineParameters = new RefineParameters(10f, 3f, true);
        detector = new CharucoDetector(board, charucoParameters, detectorParams);

        arucoDetector = new ArucoDetector(dict, detectorParams, refineParameters);
    }

    void Update()
    {
        // --- DETECTION ---
        // Read camera
        RenderTexture.active = rt;
        tex.ReadPixels(new UnityEngine.Rect(0, 0, tex.width, tex.height), 0, 0);
        tex.Apply();
        // Convert texture to mat
        OpenCVMatUtils.Texture2DToMat(tex, rgba);
        Imgproc.cvtColor(rgba, gray, Imgproc.COLOR_RGBA2GRAY);

        // PRE-PROCESADO PARA HDRP ?? 
        Core.normalize(gray, gray, 0, 255, Core.NORM_MINMAX);
        Imgproc.threshold(gray, gray, 0, 255, Imgproc.THRESH_BINARY | Imgproc.THRESH_OTSU);

        // Mat to save results
        Mat charucoCorners = new Mat(), charucoIds = new Mat();
        List<Mat> markerCorners = new List<Mat>();
        List<Mat> rejected = new List<Mat>();
        Mat markerIds = new Mat();

        arucoDetector.detectMarkers(gray,markerCorners,markerIds,rejected);
        if (rejected.Count > 0)
        {
            Debug.Log($"rejected: {rejected.Count}");
        }

        // Detection. detectBoard runs detectMarkers if none pass
        detector.detectBoard(gray, charucoCorners, charucoIds, markerCorners, markerIds);
        Debug.Log($"Corners: {charucoIds.total()}, Markers: {markerIds.total()}");
        Debug.Log($"Mat size: {gray.cols()}x{gray.rows()} | RT size: {rt.width}x{rt.height}");

        if (charucoIds.total() > 0)
        {
            Debug.Log("Drawing results");
            Mat rgbTemp = new Mat();
            Imgproc.cvtColor(rgba, rgbTemp, Imgproc.COLOR_RGBA2RGB);
            Objdetect.drawDetectedMarkers(rgbTemp, markerCorners, markerIds, new Scalar(255, 0, 0));
            Objdetect.drawDetectedCornersCharuco(rgbTemp, charucoCorners, charucoIds, new Scalar(0, 255, 0));
            Imgproc.cvtColor(rgbTemp, rgba, Imgproc.COLOR_RGB2RGBA);
            rgbTemp.Dispose();
        }

        // Show results in the preview
        OpenCVMatUtils.MatToTexture2D(rgba, tex);
        display.texture = tex;


        // --- CALIBRATION ---
        var kb = Keyboard.current;
        if (kb.spaceKey.wasPressedThisFrame)
        {
            SaveFrame(charucoCorners, charucoIds);
        }
        if (kb.cKey.wasPressedThisFrame)
        {
            RunCalibration();
        }
    }

    void SaveFrame(Mat corners, Mat ids)
    {
        if (ids.total() > 0)
        {
            allCharucoCorners.Add(corners.clone());
            allCharucoIds.Add(ids.clone());
            // filteredImages.Add(gray.clone()); 

            Debug.Log($"Frame {allCharucoCorners.Count} capturado exitosamente.");
        }
        else
        {
            Debug.LogWarning("No se detectaron suficientes puntos Charuco para guardar.");
        }

        if (savePng)
        {
            string filename = Path.Combine(imgDir, $"img_{captureIndex:D4}.png");
            Imgcodecs.imwrite(filename, rgba);
            captureIndex++;
        }
    }

    void RunCalibration()
    {
        if (allCharucoCorners.Count < 3) return;

        List<Mat> allObjectPoints = new List<Mat>();
        List<Mat> allImagePoints = new List<Mat>();

        for (int i = 0; i < allCharucoIds.Count; i++)
        {
            Mat corners = allCharucoCorners[i];
            Mat ids = allCharucoIds[i];

            if (ids.total() > 0)
            {
                Mat objP = new Mat((int)ids.total(), 1, CvType.CV_32FC3);

                for (int j = 0; j < ids.total(); j++)
                {
                    int id = (int)ids.get(j, 0)[0];
                    double[] p3d = board.getChessboardCorners().get(id, 0);
                    objP.put(j, 0, p3d);
                }

                allObjectPoints.Add(objP);
                allImagePoints.Add(corners);
            }
        }

        Mat camMatrix = Mat.eye(3, 3, CvType.CV_64FC1);
        MatOfDouble distCoeffs = new MatOfDouble(0, 0, 0, 0, 0);
        List<Mat> rvecs = new List<Mat>(), tvecs = new List<Mat>();

        // get intrinsics
        double err = Calib3d.calibrateCamera(
            allObjectPoints,
            allImagePoints,
            gray.size(),
            camMatrix,
            distCoeffs,
            rvecs,
            tvecs
        );

        // get extrinsics
        int last = allCharucoCorners.Count - 1;


        MatOfPoint3f obj = new MatOfPoint3f(allObjectPoints[last]);
        MatOfPoint2f img = new MatOfPoint2f(allImagePoints[last]);

        Mat rvec = new Mat();
        Mat tvec = new Mat();
        bool ok = Calib3d.solvePnP(obj, img, camMatrix, distCoeffs, rvec, tvec);

        if (ok)
        {
            Mat R = new Mat();
            Calib3d.Rodrigues(rvec, R);

            Debug.Log("Distancia (Z): " + tvec.get(2, 0)[0]);
        }

        obj.Dispose();
        img.Dispose();

        Debug.Log("Error RMS: " + err);
        Debug.Log("Matriz K:\n" + camMatrix.dump());
        
    }
}