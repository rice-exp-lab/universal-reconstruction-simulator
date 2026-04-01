using OpenCVForUnity.Calib3dModule;
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.ImgprocModule;
using OpenCVForUnity.ObjdetectModule;
using OpenCVForUnity.UnityIntegration;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class CalibrationSimple : MonoBehaviour
{
    public Camera cameraCapture;
    public RawImage preview;

    [Header("Configuración")]
    public int imgWidth = 640;
    public int imgHeight = 480;
    public int squaresX = 5;
    public int squaresY = 7;
    public float squareLength = 0.04f;
    public float markerLength = 0.02f;

    private Texture2D tex;
    private RenderTexture rt;
    private Mat rgba, gray, bgr;

    // Objetos clave de la nueva API
    private CharucoBoard board;
    private CharucoDetector detector;
    private Mat cameraMatrix;
    private MatOfDouble distCoeffs;

    // Almacenamiento de capturas
    private List<Mat> allCorners = new List<Mat>();
    private List<Mat> allIds = new List<Mat>();

    void Start()
    {
        // 1. Configurar Render To Texture
        rt = new RenderTexture(imgWidth, imgHeight, 24);
        cameraCapture.targetTexture = rt;
        tex = new Texture2D(imgWidth, imgHeight, TextureFormat.RGBA32, false);

        // 2. Inicializar Mats
        rgba = new Mat(imgHeight, imgWidth, CvType.CV_8UC4);
        gray = new Mat(imgHeight, imgWidth, CvType.CV_8UC1);
        bgr = new Mat(imgHeight, imgWidth, CvType.CV_8UC3);

        // 3. Configurar Tablero y Detector (API Nueva)
        Dictionary dict = Objdetect.getPredefinedDictionary(Objdetect.DICT_5X5_250);
        board = new CharucoBoard(new Size(squaresX, squaresY), squareLength, markerLength, dict);

        // El CharucoDetector maneja internamente la detección de marcadores e interpolación
        detector = new CharucoDetector(board);

        // 4. Matrices intrínsecas iniciales
        cameraMatrix = Mat.eye(3, 3, CvType.CV_64FC1);
        distCoeffs = new MatOfDouble(0, 0, 0, 0, 0);

        preview.texture = tex;
    }

    void Update()
    {
        // Capturar pantalla de Unity
        RenderTexture prevActive = RenderTexture.active;
        RenderTexture.active = rt;
        tex.ReadPixels(new UnityEngine.Rect(0, 0, imgWidth, imgHeight), 0, 0);
        tex.Apply();
        RenderTexture.active = prevActive;

        // Convertir a OpenCV
        OpenCVMatUtils.Texture2DToMat(tex, rgba);
        Core.flip(rgba, rgba, 0); // Corregir inversión de Unity
        Imgproc.cvtColor(rgba, gray, Imgproc.COLOR_RGBA2GRAY);
        Imgproc.cvtColor(rgba, bgr, Imgproc.COLOR_RGBA2BGR);

        // DETECCIÓN
        Mat charucoCorners = new Mat();
        Mat charucoIds = new Mat();
        List<Mat> markerCorners = new List<Mat>();
        Mat markerIds = new Mat();

        // La nueva API hace todo aquí:
        detector.detectBoard(gray, charucoCorners, charucoIds, markerCorners, markerIds);

        // DIBUJO
        if (markerIds.total() > 0)
        {
            Objdetect.drawDetectedMarkers(bgr, markerCorners, markerIds);
            if (charucoIds.total() > 0)
            {
                Objdetect.drawDetectedCornersCharuco(bgr, charucoCorners, charucoIds, new Scalar(0, 255, 0));
            }
        }

        // ACCIONES POR TECLADO

        if (Keyboard.current.spaceKey.wasPressedThisFrame && charucoIds.total() > 10)
        {
            allCorners.Add(charucoCorners.clone());
            allIds.Add(charucoIds.clone());
            Debug.Log($"Foto guardada. Capturas: {allCorners.Count}");
        }

        // Reemplaza Input.GetKeyDown(KeyCode.C) por:
        if (Keyboard.current.cKey.wasPressedThisFrame && allCorners.Count > 5)
        {
            Calibrate();
        }

        // Mostrar en UI
        OpenCVMatUtils.MatToTexture2D(bgr, tex);

        // Limpieza temporal
        charucoCorners.Dispose();
        charucoIds.Dispose();
        markerIds.Dispose();
        foreach (var m in markerCorners) m.Dispose();
    }

    void Calibrate()
    {
        List<Mat> objectPoints = new List<Mat>();
        List<Mat> imagePoints = new List<Mat>();

        // Obtenemos las posiciones 3D de todas las esquinas del tablero
        // Es un Mat de tipo CV_32FC3 que contiene los puntos (x, y, 0)
        Mat allBoardCorners = board.getChessboardCorners();

        for (int i = 0; i < allCorners.Count; i++)
        {
            Mat ids = allIds[i];
            Mat corners = allCorners[i];
            int nPoints = (int)ids.total();

            if (nPoints >= 4) // Necesitamos al menos 4 puntos por frame
            {
                MatOfPoint3f frameObjPoints = new MatOfPoint3f();
                MatOfPoint2f frameImgPoints = new MatOfPoint2f();

                Point3[] objArray = new Point3[nPoints];
                Point[] imgArray = new Point[nPoints];

                for (int j = 0; j < nPoints; j++)
                {
                    // El ID nos dice qué esquina del tablero es
                    int id = (int)ids.get(j, 0)[0];

                    // Extraemos el punto 3D correspondiente del tablero
                    float[] p3 = new float[3];
                    allBoardCorners.get(id, 0, p3);
                    objArray[j] = new Point3(p3[0], p3[1], p3[2]);

                    // Extraemos el punto 2D detectado en la imagen
                    double[] p2 = corners.get(j, 0);
                    imgArray[j] = new Point(p2[0], p2[1]);
                }

                frameObjPoints.fromArray(objArray);
                frameImgPoints.fromArray(imgArray);

                objectPoints.Add(frameObjPoints);
                imagePoints.Add(frameImgPoints);
            }
        }

        if (objectPoints.Count < 5)
        {
            Debug.LogError("No hay suficientes frames válidos para calibrar.");
            return;
        }

        List<Mat> rvecs = new List<Mat>();
        List<Mat> tvecs = new List<Mat>();

        // Calibración estándar universal
        double err = Calib3d.calibrateCamera(
            objectPoints,
            imagePoints,
            new Size(imgWidth, imgHeight),
            cameraMatrix,
            distCoeffs,
            rvecs,
            tvecs
        );

        Debug.Log("¡Calibración exitosa! Error: " + err);
        Debug.Log("K: " + cameraMatrix.dump());

        // Limpieza
        allBoardCorners.Dispose();
        foreach (var m in objectPoints) m.Dispose();
        foreach (var m in imagePoints) m.Dispose();
    }

    void OnDestroy()
    {
        if (rgba != null) rgba.Dispose();
        if (gray != null) gray.Dispose();
        if (bgr != null) bgr.Dispose();
        if (board != null) board.Dispose();
        if (detector != null) detector.Dispose();
    }
}
