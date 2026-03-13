using OpenCVForUnity.CoreModule;
using OpenCVForUnity.ImgprocModule;
using OpenCVForUnity.ObjdetectModule;
using OpenCVForUnity.UnityIntegration;
using OpenCVForUnity.UnityUtils;
using System;//.Collections.Generic;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class ArUco : MonoBehaviour
{
    public enum ArUcoDictionary
    {
        DICT_4X4_50 = Objdetect.DICT_4X4_50,
        DICT_4X4_100 = Objdetect.DICT_4X4_100,
        DICT_4X4_250 = Objdetect.DICT_4X4_250,
        DICT_4X4_1000 = Objdetect.DICT_4X4_1000,
        DICT_5X5_50 = Objdetect.DICT_5X5_50,
        DICT_5X5_100 = Objdetect.DICT_5X5_100,
        DICT_5X5_250 = Objdetect.DICT_5X5_250,
        DICT_5X5_1000 = Objdetect.DICT_5X5_1000,
        DICT_6X6_50 = Objdetect.DICT_6X6_50,
        DICT_6X6_100 = Objdetect.DICT_6X6_100,
        DICT_6X6_250 = Objdetect.DICT_6X6_250,
        DICT_6X6_1000 = Objdetect.DICT_6X6_1000,
        DICT_7X7_50 = Objdetect.DICT_7X7_50,
        DICT_7X7_100 = Objdetect.DICT_7X7_100,
        DICT_7X7_250 = Objdetect.DICT_7X7_250,
        DICT_7X7_1000 = Objdetect.DICT_7X7_1000,
        DICT_ARUCO_ORIGINAL = Objdetect.DICT_ARUCO_ORIGINAL,
    }

    [Header("UI")]
    public RawImage targetRawImage;

    [Header("Dictionary")]
    public ArUcoDictionary dictionaryId = ArUcoDictionary.DICT_5X5_250;

    [Header("GridBoard")]
    [Tooltip("Number of cols")]
    public int markersX = 5;
    [Tooltip("Number of rows")]
    public int markersY = 7;
    [Tooltip("Marker side length")]
    public float markerLength = 0.07f;
    [Tooltip("Checker side length")]
    public float checkerLength = 0.1f;
    //[Tooltip("Board side length")]
    //public float boardWidthMeters = 0.20f;
    //public float markerSeparation = 0.01f; // unidad “real” relativa
    private const int CHARUCO_MARKER_FIRST_MARKER = 1;
    private const int CHARUCO_BOARD_MARGIN_SIZE = 10;
    private const bool USE_LEGACY_PATTERN = false;

    //[Header("Output image")]
    public int pixelsPerMarker = 240; 
    public int marginPixels = 20;
    //[Tooltip("The size of the output marker image (px).")]
    //public int MarkerSize = 1000;

    private Texture2D _tex;
    private Mat _markerImg;


    [ContextMenu("Build board")]
    public void board()
    {
        if (targetRawImage == null)
        {
            Debug.LogError("Assign Raw Image");
            return;
        }

        Dictionary dict = Objdetect.getPredefinedDictionary((int)dictionaryId);

        float squareLength = checkerLength; //boardWidthMeters / markersX;
        float markerLengthM = Math.Min(markerLength, squareLength * 0.6f); 

        CharucoBoard charucoBoard = new CharucoBoard(new Size(markersX, markersY),
            squareLength, markerLengthM,
            dict
        );
        charucoBoard.setLegacyPattern(USE_LEGACY_PATTERN);

        int outW = markersX * pixelsPerMarker + 2 * marginPixels;
        int outH = markersY * pixelsPerMarker + 2 * marginPixels;

        Mat gray = new Mat(outH, outW, CvType.CV_8UC1);
        charucoBoard.generateImage(new Size(outW, outH), gray, marginPixels, 2);

        Mat rgb = new Mat();
        Imgproc.cvtColor(gray, rgb, Imgproc.COLOR_GRAY2RGB);

        if (_tex == null || _tex.width != outW || _tex.height != outH)
            _tex = new Texture2D(outW, outH, TextureFormat.RGB24, false);

        OpenCVMatUtils.MatToTexture2D(rgb, _tex);

        targetRawImage.color = Color.white;
        targetRawImage.texture = _tex;

        float boardWidthMeters = squareLength * markersX;

        float contentWidthMeters = squareLength * markersX;
        float contentHeightMeters = squareLength * markersY;

        float metersPerPixel = (squareLength * markersX) / (markersX * pixelsPerMarker);

        float totalWidthMeters = outW * metersPerPixel;
        float totalHeightMeters = outH * metersPerPixel;

        Debug.Log($"Total Width: {totalWidthMeters} m");
        Debug.Log($"Total Height: {totalHeightMeters} m");

        RectTransform rt = targetRawImage.rectTransform;
        float aspect = (float)_tex.height / _tex.width;
        rt.sizeDelta = new Vector2(totalWidthMeters, totalHeightMeters);

        gray.Dispose();
        rgb.Dispose();
    }

}