using UnityEngine;
using UnityEngine.InputSystem;

public class moveBoardRandom : MonoBehaviour
{
    public float maxRotation = 1f;
    public float minRotation = -1f;
    public float maxTranslation = 0.5f;
    //public float minTranlation = -0.5f;
    Vector3 positionOrigin;

    void Start()
    {
        positionOrigin = transform.position;
        //Debug.Log($"[{name}]position = {transform.position} local = {transform.localPosition}");

    }

    void Update()
    {
        if (Keyboard.current.rKey.isPressed)
        {
            float randomRotX =  Random.Range(minRotation, maxRotation);
            //float randomRotY = Random.Range(minRotation, maxRotation);
            float randomRotZ = Random.Range(minRotation, maxRotation);

            Quaternion randomRotation = Quaternion.Euler(randomRotX, 0, randomRotZ);
            transform.rotation = randomRotation;

            //float randomX = Random.Range(minRotation, maxRotation);
            //float randomY = Random.Range(minRotation, maxRotation);
            //float randomZ = Random.Range(minRotation, maxRotation);
            //float x = positionOrigin.x;

            //Vector3 randomPosition = new Vector3(x, randomY, randomZ);
            Vector3 randomPosition = positionOrigin + Random.insideUnitSphere * maxTranslation;
            transform.position = randomPosition;
        }
    }
}
