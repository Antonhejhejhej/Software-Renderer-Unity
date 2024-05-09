using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Rotate : MonoBehaviour
{
    public bool onlyYaxis;
    public float rotationSpeed;
    private float yRot;
    private float xRot;
    void Start()
    {
        yRot = transform.eulerAngles.y;
        xRot = transform.eulerAngles.x;
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        
    }

    private void Update()
    {
        xRot += rotationSpeed * Time.deltaTime;
        yRot += rotationSpeed * Time.deltaTime;

        xRot %= 360;
        yRot %= 360;
        if (!onlyYaxis)
        {
            transform.eulerAngles = new Vector3(xRot, yRot, 0);
        }
        else
        {
            transform.eulerAngles = new Vector3(0, yRot, 0);
        }
        
    }
}
