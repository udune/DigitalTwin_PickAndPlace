using System;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

public class BillBoard : MonoBehaviour
{
    private Transform _mainCameraTransform;

    private void Start()
    {
        _mainCameraTransform = Camera.main.transform;

        TextMeshProUGUI text = GetComponent<TextMeshProUGUI>();
        if (text != null)
        {
            text.fontMaterial.SetFloat("_ZTest", (float) CompareFunction.Always);
        }
    }

    private void LateUpdate()
    {
        transform.LookAt(transform.position + _mainCameraTransform.forward);
    }
}
