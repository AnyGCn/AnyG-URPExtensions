using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

public class WaveMove : MonoBehaviour
{
    // Update is called once per frame
    private void Start()
    {
        Debug.Log(GraphicsFormatUtility.GetGraphicsFormat(RenderTextureFormat.DefaultHDR, false));
    }

    void Update()
    {
        Vector3 pos = transform.position;
        pos.x = Mathf.Cos(Time.time) * 3;
        transform.position = pos;
    }
}
