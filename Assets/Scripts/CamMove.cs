using UnityEngine;

public class CamMove : MonoBehaviour
{   
    public bool enable = false;
    public float speed = 1f;
    public Transform centerPoint;
    public Vector3 axisMov = Vector3.up;
    public bool rotation = true;
    
    void Update()
    {
        if (enable)
        { 
            if (rotation)
            {
                transform.RotateAround(centerPoint.position, axisMov, speed);
            }
            else 
            {
                transform.Translate(axisMov *  speed * Time.deltaTime);
            
            }
        }
    }
}
