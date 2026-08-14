using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 텍스트가 항상 카메라를 향하도록 회전시킨다.
/// 카메라 방향(forward)에 정렬하는 방식이라 화면 가장자리에서도 글자가 기울지 않는다.
/// </summary>
public class BillBoard : MonoBehaviour
{
    private static readonly int ZTestId = Shader.PropertyToID("_ZTest");
    private static readonly int ZTestModeId = Shader.PropertyToID("_ZTestMode");

    private Transform _mainCameraTransform;

    private void Start()
    {
        // TMP_Text로 받으면 UGUI(TextMeshProUGUI)와 3D(TextMeshPro) 양쪽에 붙일 수 있다
        TMP_Text text = GetComponent<TMP_Text>();
        if (text == null)
        {
            return;
        }

        // 부품에 가려도 라벨이 보이도록 깊이 테스트 해제.
        // 프로퍼티명이 TMP 셰이더 버전에 따라 다르므로 존재하는 쪽에만 쓴다.
        // (기본 TMP_SDF 셰이더는 전역 unity_GUIZTestMode를 쓰므로 여기서 걸리지 않을 수 있음)
        Material fontMaterial = text.fontMaterial;
        float always = (float) CompareFunction.Always;

        if (fontMaterial.HasProperty(ZTestId))
        {
            fontMaterial.SetFloat(ZTestId, always);
        }

        if (fontMaterial.HasProperty(ZTestModeId))
        {
            fontMaterial.SetFloat(ZTestModeId, always);
        }
    }

    private void LateUpdate()
    {
        if (_mainCameraTransform == null)
        {
            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                return;
            }

            _mainCameraTransform = mainCamera.transform;
        }

        transform.LookAt(transform.position + _mainCameraTransform.forward);
    }
}
