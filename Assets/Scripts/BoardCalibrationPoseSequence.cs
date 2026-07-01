using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class BoardCalibrationPoseSequence : MonoBehaviour
{
    private enum TiltKind
    {
        None,
        LowerLeft,
        LowerRight,
        Forward,
        Backward
    }

    private struct BoardPose
    {
        public float spinDegrees;
        public bool laidDown;
        public TiltKind tilt;

        public BoardPose(float spinDegrees, bool laidDown, TiltKind tilt)
        {
            this.spinDegrees = spinDegrees;
            this.laidDown = laidDown;
            this.tilt = tilt;
        }
    }

    [Header("Sequence")]
    public float spinStepDegrees = 10f;
    public int spinStepsEachSide = 2;
    public float tiltDegrees = 10f;
    public float laidDownDegrees = 90f;

    [Header("Local Axes")]
    public Vector3 spinAxis = Vector3.up;
    public Vector3 laidDownAxis = Vector3.right;
    public Vector3 forwardTiltAxis = Vector3.right;
    public Vector3 sideTiltAxis = Vector3.forward;

    [Header("Optional Corner Pivot")]
    public bool useCornerPivot = false;
    public charucoParams charucoParams;
    public Vector3 boardWidthAxis = Vector3.right;
    public Vector3 boardHeightAxis = Vector3.up;
    public float fallbackBoardWidth = 0.3f;
    public float fallbackBoardHeight = 0.2f;

    private readonly List<BoardPose> poses = new List<BoardPose>();
    private Vector3 originLocalPosition;
    private Quaternion originLocalRotation;
    private int currentPose;

    private void Start()
    {
        CaptureOriginFromCurrentTransform();
        BuildSequence();
        ApplyPose(0);
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        if (keyboard.rKey.wasPressedThisFrame)
            MoveToNextPose();
    }

    [ContextMenu("Move To Next Pose")]
    public void MoveToNextPose()
    {
        currentPose++;
        if (currentPose >= poses.Count)
        {
            Debug.Log("No more board calibration poses");
            currentPose = poses.Count - 1;
            return;
        }

        ApplyPose(currentPose);
    }

    [ContextMenu("Reset Pose Sequence")]
    public void ResetPoseSequence()
    {
        currentPose = 0;
        ApplyPose(currentPose);
    }

    [ContextMenu("Capture Current Transform As Origin")]
    public void CaptureOriginFromCurrentTransform()
    {
        originLocalPosition = transform.localPosition;
        originLocalRotation = transform.localRotation;
    }

    private void BuildSequence()
    {
        poses.Clear();
        List<int> spinSteps = new List<int> { 0 };
        for (int i = 1; i <= spinStepsEachSide; i++)
            spinSteps.Add(i);
        for (int i = 1; i <= spinStepsEachSide; i++)
            spinSteps.Add(-i);

        for (int laidDownIndex = 0; laidDownIndex < 2; laidDownIndex++)
        {
            bool laidDown = laidDownIndex == 1;

            foreach (int spinStep in spinSteps)
            {
                float spinDegrees = spinStep * spinStepDegrees;
                poses.Add(new BoardPose(spinDegrees, laidDown, TiltKind.None));
                poses.Add(new BoardPose(spinDegrees, laidDown, TiltKind.LowerLeft));
                poses.Add(new BoardPose(spinDegrees, laidDown, TiltKind.LowerRight));
                poses.Add(new BoardPose(spinDegrees, laidDown, TiltKind.Forward));
                poses.Add(new BoardPose(spinDegrees, laidDown, TiltKind.Backward));
            }
        }
    }

    private void ApplyPose(int poseIndex)
    {
        if (poses.Count == 0)
            BuildSequence();

        BoardPose pose = poses[poseIndex];

        transform.localPosition = originLocalPosition;
        transform.localRotation = originLocalRotation;

        Quaternion poseRotation = AxisAngle(spinAxis, pose.spinDegrees);
        if (pose.laidDown)
            poseRotation *= AxisAngle(laidDownAxis, laidDownDegrees);

        Quaternion tiltRotation = GetTiltRotation(pose.tilt);
        Vector3 localPosition = originLocalPosition;

        if (useCornerPivot && (pose.tilt == TiltKind.LowerLeft || pose.tilt == TiltKind.LowerRight))
        {
            Vector3 localPivot = GetLocalPivot(pose.tilt);
            localPosition += poseRotation * (localPivot - tiltRotation * localPivot);
        }

        transform.localPosition = localPosition;
        transform.localRotation = originLocalRotation * poseRotation * tiltRotation;

        Debug.Log(
            $"Board pose {poseIndex + 1}/{poses.Count}: spin={pose.spinDegrees:F1}, " +
            $"laidDown={pose.laidDown}, tilt={pose.tilt}");
    }

    private Quaternion GetTiltRotation(TiltKind tilt)
    {
        switch (tilt)
        {
            case TiltKind.LowerLeft:
                return AxisAngle(sideTiltAxis, tiltDegrees);
            case TiltKind.LowerRight:
                return AxisAngle(sideTiltAxis, -tiltDegrees);
            case TiltKind.Forward:
                return AxisAngle(forwardTiltAxis, tiltDegrees);
            case TiltKind.Backward:
                return AxisAngle(forwardTiltAxis, -tiltDegrees);
            default:
                return Quaternion.identity;
        }
    }

    private Vector3 GetLocalPivot(TiltKind tilt)
    {
        Vector2 boardSize = GetBoardSize();
        float x = tilt == TiltKind.LowerRight ? boardSize.x * 0.5f : -boardSize.x * 0.5f;
        return AxisOffset(boardWidthAxis, x) + AxisOffset(boardHeightAxis, -boardSize.y * 0.5f);
    }

    private Vector2 GetBoardSize()
    {
        if (charucoParams == null)
            return new Vector2(fallbackBoardWidth, fallbackBoardHeight);

        return new Vector2(
            charucoParams.squaresX * charucoParams.squareLength,
            charucoParams.squaresY * charucoParams.squareLength);
    }

    private static Quaternion AxisAngle(Vector3 axis, float degrees)
    {
        if (axis.sqrMagnitude < 0.000001f)
            return Quaternion.identity;

        return Quaternion.AngleAxis(degrees, axis.normalized);
    }

    private static Vector3 AxisOffset(Vector3 axis, float distance)
    {
        if (axis.sqrMagnitude < 0.000001f)
            return Vector3.zero;

        return axis.normalized * distance;
    }
}
