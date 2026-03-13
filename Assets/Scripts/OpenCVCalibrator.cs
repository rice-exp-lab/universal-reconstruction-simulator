using UnityEngine;
using UnityEngine.InputSystem;
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.ImgprocModule;
using OpenCVForUnity.ObjdetectModule;
using OpenCVForUnity.Calib3dModule;
using OpenCVForUnity.UnityIntegration;
using System.Collections.Generic;
using UnityEngine.UI;

public class OpenCVCalibrator : MonoBehaviour
{
    public Camera cameraCapture;
    public int width = 1280, height = 720;
    public RawImage display;

    // Configuración del tablero
    private int squaresX = 5, squaresY = 7;
    private float sqLen = 0.1f, markLen = 0.07f;

    private Texture2D tex;
    private RenderTexture rt;
    private Mat gray, rgba;
    private CharucoBoard board;
    private CharucoDetector detector;

    // Almacén de puntos para Intrínsecos
    private List<Mat> allImagePoints = new List<Mat>();
    private List<Mat> allObjectPoints = new List<Mat>();

    void Start()
    {
        Debug.Log("starting");

        // Setup texturas
        rt = new RenderTexture(width, height, 24);
        cameraCapture.targetTexture = rt;
        tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        rgba = new Mat(height, width, CvType.CV_8UC4);
        gray = new Mat(height, width, CvType.CV_8UC1);

        // Setup OpenCV
        Dictionary dict = Objdetect.getPredefinedDictionary(Objdetect.DICT_5X5_250);
        board = new CharucoBoard(new Size(squaresX, squaresY), sqLen, markLen, dict);
        board.setLegacyPattern(false);
        detector = new CharucoDetector(board);

        OpenCVDebug.SetDebugMode(true);
    }

    void Update()
    {
        // 1. Capturar frame de la cámara (se guarda en 'rt')
        RenderTexture.active = rt;
        tex.ReadPixels(new UnityEngine.Rect(0, 0, width, height), 0, 0);
        tex.Apply();
        RenderTexture.active = null; // Liberar el active para no interferir con otros renders

        // 2. Convertir a Mat y preparar versión en Grises para el detector
        OpenCVMatUtils.Texture2DToMat(tex, rgba);
        // IMPORTANTE: Unity renderiza al revés que OpenCV. Volteamos aquí.
        //Core.flip(rgba, rgba, -1);
        Imgproc.cvtColor(rgba, gray, Imgproc.COLOR_RGBA2GRAY);

        
        
        // 3. Detectar puntos Charuco
        Mat charucoCorners = new Mat(), charucoIds = new Mat();
        List<Mat> markerCorners = new List<Mat>();
        Mat markerIds = new Mat();

        Imgproc.cvtColor(rgba, gray, Imgproc.COLOR_RGBA2GRAY);
        // --- PRE-PROCESADO PARA HDRP (Imagen oscura/pixelada) ---
        // Esto limpia el aliasing y mejora el contraste drásticamente
        Imgproc.GaussianBlur(gray, gray, new Size(3, 3), 0);
        Imgproc.equalizeHist(gray, gray);

        detector.detectBoard(gray, charucoCorners, charucoIds, markerCorners, markerIds);
        Imgproc.equalizeHist(gray, gray);
        // 4. DIBUJO VISUAL (Para el RawImage)
        // Dibujamos sobre 'rgba' para que veas qué está detectando
        if (markerIds.total() > 0)
        {
            Objdetect.drawDetectedMarkers(rgba, markerCorners, markerIds);
            if (charucoIds.total() > 0)
            {
                Objdetect.drawDetectedCornersCharuco(rgba, charucoCorners, charucoIds, new Scalar(0, 255, 0));
            }
        }
        else
        {
            Debug.Log("No markers");
        }

        // 5. Mostrar resultado en el Raw Image
        if (display != null)
        {
            // Pasamos el Mat con los dibujos de vuelta a la textura 'tex'
            OpenCVMatUtils.MatToTexture2D(rgba, tex);
            display.texture = tex;
        }

        // 6. Input System: Guardar frame (Espacio) o Calibrar (C)
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.spaceKey.isPressed && charucoIds.total() > 4)
            {
                // Enviamos clones porque charucoCorners se liberará al final del Update
                Debug.Log($"space key pressed, corners: {charucoIds.total()}");
                AddFrame(charucoCorners.clone(), charucoIds.clone());
            }
            if (kb.cKey.isPressed && allImagePoints.Count > 5)
            {
                CalculateCalibration();
            }
        }

        // 7. Limpieza de memoria temporal de este frame
        charucoCorners.Dispose();
        charucoIds.Dispose();
        markerIds.Dispose();
        foreach (var m in markerCorners) m.Dispose();
    }

    void AddFrame(Mat corners, Mat ids)
    {
        Mat objPts = new Mat();
        // Obtenemos los puntos 3D del tablero correspondientes a lo detectado
        Mat allBoardCorners = board.getChessboardCorners();

        // Mapeo manual de puntos detectados a puntos 3D
        Mat frameObj = new Mat((int)ids.total(), 1, CvType.CV_32FC3);
        for (int i = 0; i < ids.total(); i++)
        {
            int id = (int)ids.get(i, 0)[0];
            float[] p3 = new float[3];
            allBoardCorners.get(id, 0, p3);
            frameObj.put(i, 0, p3);
        }

        allImagePoints.Add(corners.clone());
        allObjectPoints.Add(frameObj);
        Debug.Log($"Frame guardado ({allImagePoints.Count})");
    }

    void CalculateCalibration()
    {
        Mat K = Mat.eye(3, 3, CvType.CV_64FC1);
        MatOfDouble dist = new MatOfDouble(0, 0, 0, 0, 0);
        List<Mat> rvecs = new List<Mat>(), tvecs = new List<Mat>();

        // --- CALCULAR INTRÍNSECOS ---
        double err = Calib3d.calibrateCamera(allObjectPoints, allImagePoints, new Size(width, height), K, dist, rvecs, tvecs);

        Debug.Log("=== MATRIZ INTRÍNSECA (K) ===\n" + K.dump());
        Debug.Log("Error de reproyección: " + err);

        // --- CALCULAR EXTRÍNSECOS (Pose del último frame capturado) ---
        if (rvecs.Count > 0)
        {
            Mat R = new Mat();
            Calib3d.Rodrigues(rvecs[rvecs.Count - 1], R); // Convertir vector de rotación a matriz 3x3
            Mat t = tvecs[tvecs.Count - 1];

            Debug.Log("=== MATRIZ EXTRÍNSECA [R|t] (Último Frame) ===");
            Debug.Log($"R:\n{R.dump()}\nt:\n{t.dump()}");
        }
    }
}