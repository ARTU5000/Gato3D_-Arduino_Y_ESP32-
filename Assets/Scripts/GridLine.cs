using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GridLine : MonoBehaviour
{
    private void Start()
    {
        LineRenderer lr = GetComponent<LineRenderer>();
        lr.startWidth = 0.05f;
        lr.endWidth = 0.05f;
        lr.material = new Material(Shader.Find("Standard"));
        lr.material.color = Color.white;
    }
}
