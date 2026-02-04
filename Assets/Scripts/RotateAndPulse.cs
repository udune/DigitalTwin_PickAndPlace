using System;
using UnityEngine;

public class RotateAndPulse : MonoBehaviour
{
    public float rotateSpeed = 100.0f;
    public float pulseSpeed = 2.0f;
    public float minScale = 0.8f;
    public float maxScale = 1.2f;

    private Vector3 baseScale;
    
    private void Start()
    {
        baseScale = transform.localScale;
    }
    
    private void Update()
    {
        transform.Rotate(Vector3.up * rotateSpeed * Time.unscaledDeltaTime);
        
        float wave = (Mathf.Sin(Time.unscaledTime * pulseSpeed) + 1) / 2;
        
        float scale = Mathf.Lerp(minScale, maxScale, wave);
        transform.localScale = baseScale * scale;
    }
}
