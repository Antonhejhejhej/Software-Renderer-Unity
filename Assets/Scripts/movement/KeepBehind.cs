using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class KeepBehind : MonoBehaviour
{
    private Transform _parentTransform;
    void Start()
    {
        _parentTransform = transform.parent;
    }

    // Update is called once per frame
    void Update()
    {
        transform.position = _parentTransform.position + Vector3.forward * .25f;
    }
}
