using System;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

public class BillBoard : MonoBehaviour
{
    private void Start()
    {
        TextMeshProUGUI text = GetComponent<TextMeshProUGUI>();
        if (text != null)
        {
            text.fontMaterial.SetFloat("_ZTest", (float) CompareFunction.Always);
        }
    }

    private void LateUpdate()
    {
        transform.LookAt(transform.position + Camera.main.transform.forward);
    }
}
