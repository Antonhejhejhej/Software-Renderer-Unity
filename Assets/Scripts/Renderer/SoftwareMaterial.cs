using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SoftwareMaterial : MonoBehaviour
{
    public Texture2D texture2D;


    private void Start()
    {
        if (TryGetComponent(out MeshRenderer renderer))
        {
            renderer.material.mainTexture = texture2D;
        }
    }
}
