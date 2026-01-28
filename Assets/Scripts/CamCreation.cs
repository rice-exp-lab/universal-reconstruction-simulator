using UnityEngine;

public class CamCreation : MonoBehaviour
{   
    [Tooltip("Main object in scene")]
    public Transform target;
    [Tooltip("Prefab for cameras")]
    public GameObject cameraPrefab;

    [Header("Rig")]
    [Tooltip("Total number of cameras for rig")]
    public int numCam = 4;
    [Tooltip("Height from Target")]
    public float height = 1.2f;
    [Tooltip("Distance from object to cameras")]
    public float radius = 2.5f;
    [Tooltip("Distribution of cameras")]
    public float degrees = 180f;
    


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    //void Start()
    [ContextMenu("Build Rig")] // https://docs.unity3d.com/6000.3/Documentation/ScriptReference/ContextMenu.html
    public void BuildRig()
    {
        // Destry old cameras
        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);
        
        // start, character in middle
        float startAngle = -degrees / 2f;

        // step degrees
        float step;
        if (Mathf.Approximately(degrees, 360f))
            step = degrees / numCam;
        else    
            step = (numCam == 1) ? 0f : degrees / (numCam - 1);
        
        // create cameras
        for (int i = 0; i < numCam; i++)
        {
            float angleDeg = startAngle + step * i;
            float rad = angleDeg * Mathf.Deg2Rad;

            Vector3 pos = new Vector3(
                Mathf.Cos(rad) * radius,
                height,
                Mathf.Sin(rad) * radius
            );
            GameObject camGO = Instantiate(cameraPrefab, transform);
            camGO.name = $"Cam_{i+1:00}";
            camGO.transform.localPosition = pos;
            camGO.transform.LookAt(target.position);
            Camera cam = camGO.GetComponent<Camera>();
            cam.targetDisplay = i;
        }
        
    }

}
