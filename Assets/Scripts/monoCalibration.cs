using OpenCVForUnity.ArucoModule;
using OpenCVForUnity.Calib3dModule;
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.ImgcodecsModule;
using OpenCVForUnity.ImgprocModule;
using OpenCVForUnity.ObjdetectModule;
using OpenCVForUnity.UnityIntegration;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using static OpenCVForUnityExample.ArUcoCreateMarkerExample;

namespace OpenCVForUnityExample
{
    /// <summary>
    /// ArUco Camera Calibration Example
    /// An example of camera calibration using the objdetect module. (ChessBoard, CirclesGlid, AsymmetricCirclesGlid and ChArUcoBoard)
    /// Referring to https://docs.opencv.org/master/d4/d94/tutorial_camera_calibration.html
    /// https://github.com/opencv/opencv/blob/master/samples/cpp/tutorial_code/calib3d/camera_calibration/camera_calibration.cpp
    /// https://docs.opencv.org/3.4.0/d7/d21/tutorial_interactive_calibration.html
    /// https://github.com/opencv/opencv/tree/master/apps/interactive-calibration
    /// https://docs.opencv.org/3.2.0/da/d13/tutorial_aruco_calibration.html
    /// https://github.com/opencv/opencv_contrib/blob/master/modules/aruco/samples/calibrate_camera_charuco.cpp
    /// </summary>
    public class monoCalibration : MonoBehaviour
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


        public Camera cameraCapture;
        public RectTransform charuco;
        public RawImage preview;

        [Header("Image Capture")]
        public int imgWidth = 512;
        public int imgHeight = 512;
        public bool enablePreview = false;

        [Header("Output")]
        public string folderName = "calibration";
        public string runName = "run_001";

        //[Header("Keys")]
        //public KeyCode keyDetect = KeyCode.D;
        //public KeyCode keySave = KeyCode.S;
        //public KeyCode keyCalibrate = KeyCode.C;
        //public KeyCode keyReset = KeyCode.Q;

        [Header("Board")]
        public int squaresX = 5;
        public int squaresY = 7;
        public float squareLengthMeters = 0.04f;
        public float markerLengthMeters = 0.05f;
        public int minCharucoCorners = 20;
        public ArUcoDictionary dictionaryId = ArUcoDictionary.DICT_5X5_250;

        // Private Fields
        private Texture2D tex;
        private RenderTexture rt;
        private Mat gray, rgba, bgr, overlayBgr;
        private Size imageSize;
        ArucoDetector arucoDetector;
        MatOfDouble dist_cv;
        Mat K_cv;
        CharucoBoard charucoBoard;
        readonly List<Mat> allCharucoCorners = new();
        readonly List<Mat> allCharucoIds = new();
        int captureIndex = 0;
        string baseOutDir, imgDir, jsonDir;
        // ---------- JSON structs ----------
        [Serializable]
        public class JsonMat
        {
            public int rows;
            public int cols;
            public double[] data; // row-major
        }

        [Serializable]
        public class CalibrationResultJson
        {
            public string runName;
            public int width;
            public int height;

            public JsonMat K_cv;
            public JsonMat dist_cv;     // 1x5 or 1xN
            public JsonMat K_unity;

            public JsonMat T_cam_from_board_cv;    // 4x4
            public JsonMat T_cam_from_board_unity; // 4x4

            public double reprojectionError;
            public int usedFrames;
            public string notes;
        }



        void Start() {

            // Camera is physical
            cameraCapture.usePhysicalProperties = true;


            // output folder
            baseOutDir = Path.Combine(Application.dataPath, "..", folderName, runName);
            imgDir = Path.Combine(baseOutDir, "images");
            jsonDir = Path.Combine(baseOutDir, "results");

            Directory.CreateDirectory(imgDir);
            Directory.CreateDirectory(jsonDir);

            Debug.Log("Output: " + baseOutDir);

            // Set up texture render
            SetupRender();
            SetupOpenCV();

        }

        void SetupRender()
        {
            rt = new RenderTexture(imgWidth, imgHeight, 24, RenderTextureFormat.ARGB32);
            rt.Create();
            cameraCapture.targetTexture = rt;
            tex = new Texture2D(imgWidth, imgHeight, TextureFormat.RGBA32, false);
            imageSize = new Size(imgWidth, imgHeight);
            rgba = new Mat(imgHeight, imgWidth, CvType.CV_8UC4);
            bgr = new Mat(imgHeight, imgWidth, CvType.CV_8UC3);
            gray = new Mat(imgHeight, imgWidth, CvType.CV_8UC1);
            overlayBgr = new Mat(imgHeight, imgWidth, CvType.CV_8UC3);

            preview.texture = new Texture2D(imgWidth, imgHeight, TextureFormat.RGBA32, false);
            preview.rectTransform.sizeDelta = new Vector2(imgWidth, imgHeight);
        }

        void SetupOpenCV()
        {
            Dictionary dict = Objdetect.getPredefinedDictionary((int)dictionaryId);
            charucoBoard = new CharucoBoard(new Size(squaresX, squaresY),
                squareLengthMeters, markerLengthMeters, dict);

            DetectorParameters detectorParameters = new DetectorParameters();
            RefineParameters refineParameters = new RefineParameters();

            arucoDetector = new ArucoDetector(dict, detectorParameters, refineParameters);

            K_cv = CreateInitialCameraMatrix(imgWidth, imgHeight);
            dist_cv = new MatOfDouble(0, 0, 0, 0, 0);
        }

        void Update()
        {
            if (enablePreview)
            {
                if (!TryDetectAndPreview(out _, out _)) { /* no-op */ }
            }

            //if (Input.GetKeyDown(keyDetect))
            if (Keyboard.current.dKey.isPressed)
            {
                Debug.Log("Detect press");
                TryDetectAndPreview(out _, out _);
            }

            if (Keyboard.current.sKey.isPressed)
            {
                if (TryDetectAndPreview(out Mat charCorners, out Mat charIds))
                {
                    SavePngCurrentFrame();
                    BufferDetection(charCorners, charIds);
                    Debug.Log($"C: saved+buffered. Frames={allCharucoCorners.Count}");
                }
                else
                {
                    Debug.Log("C: detection failed (not enough corners).");
                }
            }

            if (Keyboard.current.cKey.isPressed)
            {
                CalibrateAndSaveJson();
            }

            if (Keyboard.current.qKey.isPressed)
            {
                ResetBuffer();
            }
        }


        bool TryDetectAndPreview(out Mat charucoCorners, out Mat charucoIds)
        {
            charucoCorners = null;
            charucoIds = null;

            if (rt == null) return false;
            RenderTexture preview = RenderTexture.active;
            RenderTexture.active = rt;
            tex.ReadPixels(new UnityEngine.Rect(0, 0, imgWidth, imgHeight), 0, 0);
            tex.Apply();
            RenderTexture.active = preview;

            OpenCVMatUtils.Texture2DToMat(tex, rgba);
            Core.flip(rgba, rgba, 0);
            Imgproc.cvtColor(rgba, bgr, Imgproc.COLOR_RGBA2BGR);
            Imgproc.cvtColor(rgba, gray, Imgproc.COLOR_RGBA2GRAY);

            List<Mat> markerCorners = new();
            Mat markersId = new Mat();
            List<Mat> rejected = new();

            arucoDetector.detectMarkers(gray, markerCorners, markersId, rejected);
            bgr.copyTo(overlayBgr);

            if (markersId.total() <= 0)
            {
                ShowPreview(overlayBgr);
                Cleanup(markerCorners, markersId, rejected);
                return false;
            }

            // Draw markers
            Objdetect.drawDetectedMarkers(overlayBgr, markerCorners, markersId);

            // Interpolate Charuco corners
            Mat cc = new Mat();
            Mat ci = new Mat();
            Aruco.interpolateCornersCharuco(markerCorners, markersId, gray, charucoBoard, cc, ci, K_cv, dist_cv);

            long n = ci.total();

            if (n > 0)
            {
                // Draw Charuco corners
                Objdetect.drawDetectedCornersCharuco(overlayBgr, cc, ci, new Scalar(0, 255, 0));
            }

            ShowPreview(overlayBgr);
            Cleanup(markerCorners, markersId, rejected);

            if (n < minCharucoCorners)
            {
                cc.Dispose();
                ci.Dispose();
                return false;
            }

            charucoCorners = cc;
            charucoIds = ci;
            return true;
        }

        void BufferDetection(Mat charCorners, Mat charIds)
        {
            allCharucoCorners.Add(charCorners.clone());
            allCharucoIds.Add(charIds.clone());
            charCorners.Dispose();
            charIds.Dispose();
        }

        void SavePngCurrentFrame()
        {
            // Save the overlay image (or bgr if you prefer)
            string filename = Path.Combine(imgDir, $"img_{captureIndex:D4}.png");
            Imgcodecs.imwrite(filename, overlayBgr);
            captureIndex++;
            Debug.Log("Saved PNG: " + filename);
        }

        void CalibrateAndSaveJson()
        {
            if (allCharucoCorners.Count < 5)
            {
                Debug.LogWarning("K: Need more buffered frames (try 15–30).");
                return;
            }

            // Re-init guesses
            K_cv.Dispose();
            dist_cv.Dispose();
            K_cv = CreateInitialCameraMatrix(imgWidth, imgHeight);
            dist_cv = new MatOfDouble(0, 0, 0, 0, 0);

            List<Mat> rvecs = new();
            List<Mat> tvecs = new();

            double reproj = -1;
            bool usedCharucoCalib = false;

            try
            {
                reproj = Aruco.calibrateCameraCharuco(
                    allCharucoCorners, allCharucoIds,
                    charucoBoard,
                    imageSize,
                    K_cv, dist_cv,
                    rvecs, tvecs,
                    0
                );
                usedCharucoCalib = true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("calibrateCameraCharuco not available/failed, fallback. " + e.Message);
            }

            if (!usedCharucoCalib)
            {
                // Fallback: build correspondences and Calib3d.calibrateCamera
                var objectPoints = new List<Mat>();
                var imagePoints = new List<Mat>();

                for (int i = 0; i < allCharucoCorners.Count; i++)
                {
                    Mat ids = allCharucoIds[i];
                    Mat corners = allCharucoCorners[i];

                    MatOfPoint3f obj = new MatOfPoint3f();
                    MatOfPoint2f img = new MatOfPoint2f();
                    BuildCharucoCorrespondences(charucoBoard, corners, ids, obj, img);

                    if (obj.rows() >= minCharucoCorners)
                    {
                        objectPoints.Add(obj);
                        imagePoints.Add(img);
                    }
                    else { obj.Dispose(); img.Dispose(); }
                }

                reproj = Calib3d.calibrateCamera(
                    objectPoints, imagePoints, imageSize,
                    K_cv, dist_cv,
                    rvecs, tvecs, 0
                );

                foreach (var m in objectPoints) m.Dispose();
                foreach (var m in imagePoints) m.Dispose();
            }

            Debug.Log($"Calibration done. Reprojection error: {reproj}");
            Debug.Log("K_cv:\n" + K_cv.dump());
            Debug.Log("dist_cv:\n" + dist_cv.dump());

            // Compute K_unity from Physical Camera
            Mat K_unity = ComputeUnityK(cameraCapture, imgWidth, imgHeight);

            // Pose estimates (taking board as origin)
            // OpenCV pose from the *last buffered detection* using solvePnP
            Mat T_cam_from_board_cv = EstimatePosePnP_LastFrame();

            // Unity ground truth pose cam<-board in the SAME board frame convention
            Mat T_cam_from_board_unity = ComputeUnityPose_CamFromBoard();

            // Save JSON
            var result = new CalibrationResultJson
            {
                runName = runName,
                width = imgWidth,
                height = imgHeight,
                K_cv = ToJsonMat(K_cv),
                dist_cv = ToJsonMat(dist_cv),
                K_unity = ToJsonMat(K_unity),
                T_cam_from_board_cv = ToJsonMat(T_cam_from_board_cv),
                T_cam_from_board_unity = ToJsonMat(T_cam_from_board_unity),
                reprojectionError = reproj,
                usedFrames = allCharucoCorners.Count,
                notes = "Board frame: X=board.right, Y=-board.up, Z=board.forward, origin=top-left corner (if enabled)."
            };

            string jsonPath = Path.Combine(jsonDir, $"calibration_{DateTime.Now:yyyyMMdd_HHmmss}.json");
            File.WriteAllText(jsonPath, JsonUtility.ToJson(result, true));
            Debug.Log("Saved JSON: " + jsonPath);

            K_unity.Dispose();
            T_cam_from_board_cv.Dispose();
            T_cam_from_board_unity.Dispose();
        }

        // ---------- Pose helpers ----------
        Mat EstimatePosePnP_LastFrame()
        {
            // Use last buffered charuco corners/ids, solvePnP against Charuco board chessboard corners
            int i = allCharucoCorners.Count - 1;
            Mat ids = allCharucoIds[i];
            Mat corners = allCharucoCorners[i];

            MatOfPoint3f obj = new MatOfPoint3f();
            MatOfPoint2f img = new MatOfPoint2f();
            BuildCharucoCorrespondences(charucoBoard, corners, ids, obj, img);

            Mat rvec = new Mat();
            Mat tvec = new Mat();
            bool ok = Calib3d.solvePnP(obj, img, K_cv, dist_cv, rvec, tvec);

            obj.Dispose();
            img.Dispose();

            if (!ok)
            {
                Debug.LogWarning("solvePnP failed; returning identity.");
                rvec.Dispose(); tvec.Dispose();
                return Mat.eye(4, 4, CvType.CV_64FC1);
            }

            Mat R = new Mat();
            Calib3d.Rodrigues(rvec, R);

            Mat T = Mat.eye(4, 4, CvType.CV_64FC1);
            // Put R
            for (int rr = 0; rr < 3; rr++)
                for (int cc = 0; cc < 3; cc++)
                    T.put(rr, cc, R.get(rr, cc)[0]);
            // Put t
            T.put(0, 3, tvec.get(0, 0)[0]);
            T.put(1, 3, tvec.get(1, 0)[0]);
            T.put(2, 3, tvec.get(2, 0)[0]);

            rvec.Dispose(); tvec.Dispose(); R.Dispose();
            return T; // T_cam_from_board (OpenCV convention)
        }

        Mat ComputeUnityPose_CamFromBoard()
        {
            // Build board->world using our board-frame convention (OpenCV-like board frame)
            // board origin at top-left corner on the board plane.
            // axes: X=right, Y=-up, Z=forward

            // Camera world transform
            Matrix4x4 Twc_unity = cameraCapture.transform.localToWorldMatrix; // world<-cam (Unity)
            Matrix4x4 Tcw_unity = Twc_unity.inverse;                    // cam<-world (Unity)

            // Board world transform (world<-boardCV)
            Matrix4x4 Twb = BuildWorldFromBoardCV(charuco);

            // Unity GT cam<-board
            Matrix4x4 Tcb = Tcw_unity * Twb;

            // Convert Matrix4x4 -> OpenCV Mat (4x4)
            return UnityMat4ToCvMat(Tcb);
        }

        Matrix4x4 BuildWorldFromBoardCV(RectTransform rtBoard)
        {
            Vector3 originWorld;
            Quaternion rotWorld;

            // axes
            Vector3 x = rtBoard.right.normalized;
            Vector3 y = (-rtBoard.up).normalized; // down
            Vector3 z = rtBoard.forward.normalized;

            rotWorld = Quaternion.LookRotation(z, -y); // because our "up" for LookRotation is Unity-up
                                                       // But we want basis exactly {x,y,z}; easiest is build matrix directly:
            Matrix4x4 M = Matrix4x4.identity;
            M.SetColumn(0, new Vector4(x.x, x.y, x.z, 0));
            M.SetColumn(1, new Vector4(y.x, y.y, y.z, 0));
            M.SetColumn(2, new Vector4(z.x, z.y, z.z, 0));

            // Origin position: top-left corner (in our board frame)
            // RectTransform pivot is often center; we compute offset in meters along x/y on the plane.
            float W = squaresX * squareLengthMeters;
            float H = squaresY * squareLengthMeters;

            // If pivot is center, top-left is (-W/2, -H/2) in (x,right) and (y,down) axes.
            // (because y points down)
            Vector3 offset = (-0.5f * W) * x + (-0.5f * H) * y;

            originWorld = rtBoard.position + offset;

            M.SetColumn(3, new Vector4(originWorld.x, originWorld.y, originWorld.z, 1));
            return M; // world<-board
        }

        // ---------- Intrinsics helpers ----------
        Mat ComputeUnityK(Camera cam, int w, int h)
        {
            // Physical camera: focalLength in mm, sensor in mm
            float fmm = cam.focalLength;
            float sw = cam.sensorSize.x;
            float sh = cam.sensorSize.y;

            // Map mm to pixels (pinhole)
            double fx = fmm * (w / sw);
            double fy = fmm * (h / sh);

            // Principal point from lensShift (units of sensor size)
            // cx = w/2 + shiftX*w, cy = h/2 + shiftY*h
            Vector2 shift = cam.lensShift;
            double cx = (w * 0.5) + shift.x * w;
            double cy = (h * 0.5) + shift.y * h;

            Mat K = new Mat(3, 3, CvType.CV_64FC1);
            K.put(0, 0, fx); K.put(0, 1, 0); K.put(0, 2, cx);
            K.put(1, 0, 0); K.put(1, 1, fy); K.put(1, 2, cy);
            K.put(2, 0, 0); K.put(2, 1, 0); K.put(2, 2, 1);
            return K;
        }

        Mat CreateInitialCameraMatrix(int w, int h)
        {
            int maxD = Mathf.Max(w, h);
            double fx = maxD, fy = maxD;
            double cx = w / 2.0, cy = h / 2.0;

            Mat K = new Mat(3, 3, CvType.CV_64FC1);
            K.put(0, 0, fx); K.put(0, 1, 0); K.put(0, 2, cx);
            K.put(1, 0, 0); K.put(1, 1, fy); K.put(1, 2, cy);
            K.put(2, 0, 0); K.put(2, 1, 0); K.put(2, 2, 1);
            return K;
        }

        void BuildCharucoCorrespondences(CharucoBoard board, Mat charucoCorners, Mat charucoIds, MatOfPoint3f objPts, MatOfPoint2f imgPts)
        {
            Mat chessboardCorners = board.getChessboardCorners(); // Nx1x3 (float)
            int n = (int)charucoIds.total();

            Point3[] obj = new Point3[n];
            Point[] img = new Point[n];

            for (int i = 0; i < n; i++)
            {
                int id = (int)charucoIds.get(i, 0)[0];

                float[] p3 = new float[3];
                chessboardCorners.get(id, 0, p3);
                obj[i] = new Point3(p3[0], p3[1], p3[2]);

                double[] p2 = charucoCorners.get(i, 0); // x,y
                img[i] = new Point(p2[0], p2[1]);
            }

            objPts.fromArray(obj);
            imgPts.fromArray(img);

            chessboardCorners.Dispose();
        }

        // ---------- UI preview ----------
        void ShowPreview(Mat bgrImage)
        {
            Texture2D t = (Texture2D)preview.texture;
            Mat rgbaTmp = new Mat();
            Imgproc.cvtColor(bgrImage, rgbaTmp, Imgproc.COLOR_BGR2RGBA);
            OpenCVMatUtils.MatToTexture2D(rgbaTmp, t);
            rgbaTmp.Dispose();
        }

        // ---------- Utils ----------
        void Cleanup(List<Mat> corners, Mat ids, List<Mat> rejected)
        {
            ids?.Dispose();
            if (corners != null) { foreach (var m in corners) m.Dispose(); corners.Clear(); }
            if (rejected != null) { foreach (var m in rejected) m.Dispose(); rejected.Clear(); }
        }

        Mat UnityMat4ToCvMat(Matrix4x4 m)
        {
            Mat T = new Mat(4, 4, CvType.CV_64FC1);
            for (int r = 0; r < 4; r++)
                for (int c = 0; c < 4; c++)
                    T.put(r, c, m[r, c]);
            return T;
        }

        JsonMat ToJsonMat(Mat m)
        {
            int rows = m.rows();
            int cols = m.cols();

            var jm = new JsonMat { rows = rows, cols = cols, data = new double[rows * cols] };

            int k = 0;
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                    jm.data[k++] = m.get(r, c)[0];

            return jm;
        }

        void ResetBuffer()
        {
            foreach (var m in allCharucoCorners) m.Dispose();
            foreach (var m in allCharucoIds) m.Dispose();
            allCharucoCorners.Clear();
            allCharucoIds.Clear();
            captureIndex = 0;
            Debug.Log("R: buffer cleared.");
        }

        void OnDestroy()
        {
            ResetBuffer();

            if (cameraCapture != null) cameraCapture.targetTexture = null;
            rt?.Release();
            Destroy(rt);

            Destroy(tex);

            rgba?.Dispose();
            bgr?.Dispose();
            gray?.Dispose();
            overlayBgr?.Dispose();

            K_cv?.Dispose();
            dist_cv?.Dispose();

            charucoBoard?.Dispose();
            arucoDetector?.Dispose();
        }
    }
}

