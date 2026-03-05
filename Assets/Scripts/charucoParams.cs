using OpenCVForUnity.ObjdetectModule;
using UnityEngine;
using UnityEngine.UI;

public class charucoParams : MonoBehaviour
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

    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
