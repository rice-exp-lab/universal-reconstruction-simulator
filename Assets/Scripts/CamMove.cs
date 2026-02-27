using Unity.VisualScripting;
using UnityEngine;

public class CamMove : MonoBehaviour
{
    public bool enable = false;
    public Transform centerPoint;
    public enum movement{
        linear = 0,
        rotation = 1,
        spiral=2
    }

    public movement movetype = movement.linear;

    [Header("Límites de la Habitación (3x3)")]
    [Range(0.5f, 2f)]
    public float radius = 2f;
    public float roomHeight = 3f;

    [Header("Linear movement")]
    public Vector3 axisMov = Vector3.up;
    public float speed = 0.5f;

    [Header("Rotation movement")]
    public Vector3 axisRot = Vector3.up;
    public float degreeSec = 1f;

    [Header("Spiral movement")]
    public float totalTurns = 3f;
    public float duration = 30f; 

    private float currentAngle = 0f;
    private float timer = 0f;
    private Vector3 initialOffset;

    void Start()
    {
        // Empezamos desde abajo
        initialOffset = new Vector3(radius, 0, 0);
    }

    void Update()
    {
        if (enable && centerPoint != null && timer < duration)
        {

            switch (movetype)
            {
                case movement.linear:
                    transform.Translate(axisMov * speed * Time.deltaTime);
                    break;

                case movement.rotation:
                    transform.RotateAround(centerPoint.position, axisRot, degreeSec * Time.deltaTime);
                    break;


                case movement.spiral:
                    timer += Time.deltaTime;
                    float progress = timer / duration;

                    currentAngle = progress * (totalTurns * 360f);

                    float yPos = progress * roomHeight;

                    float rad = currentAngle * Mathf.Deg2Rad;
                    float x = Mathf.Cos(rad) * radius;
                    float z = Mathf.Sin(rad) * radius;

                    transform.position = new Vector3(
                        centerPoint.position.x + x,
                        centerPoint.position.y + yPos,
                        centerPoint.position.z + z
                    );
                    transform.LookAt(centerPoint.position + Vector3.up * (yPos * 0.5f));
                    break;
            }

        }
    }
}