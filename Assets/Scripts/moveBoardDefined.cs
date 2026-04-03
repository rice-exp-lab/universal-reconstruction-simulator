using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[System.Serializable]
public class MovementStep
{
    public float x;
    //public float y;
    public float z;
    public float rotX;
    public float rotY;
}

public class moveBoardDefined : MonoBehaviour
{
    [Header("Configuración de movimiento")]
    public List<MovementStep> steps;

    private int currentStep = 0;

    private Vector3 positionOrigin;

    private void Start()
    {
        positionOrigin = transform.position;
    }
    

    void Update()
    {
        if (Keyboard.current.rKey.isPressed)
        {
            MoveToNextStep();
        }
    }

    public void MoveToNextStep()
    {
        if (currentStep >= steps.Count) 
        {
            Debug.Log("No more movements");
            return;
        }
        transform.position = positionOrigin;

        Debug.Log($"Movement {currentStep}");
        MovementStep step = steps[currentStep];

        transform.position = new Vector3(step.x, 0, step.z);
        transform.rotation = Quaternion.Euler(step.rotX, step.rotY, 0f);

        currentStep++;
    }
}
