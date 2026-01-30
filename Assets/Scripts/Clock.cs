using UnityEngine;
using System;
using TMPro;
using NUnit.Framework.Internal.Commands;
using UnityEditor;

public class Clock : MonoBehaviour
{
    public TextMeshPro textObj;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        textObj = GetComponent<TextMeshPro>();
    }

    // Update is called once per frame
    void Update()
    {
        textObj.text = DateTime.Now.ToString("HH:mm:ss.fff");
    }
}
