using TMPro;
using UnityEngine;

/// <summary>
/// 오류 지점에 띄우는 경고 마커.
/// 회전 + 스케일 펄스(RotateAndPulse)가 붙은 구체와, 항상 카메라를 향하는 라벨(BillBoard)로 구성된다.
///
/// 축마다 1개씩 미리 만들어 두고 SetActive로 켜고 끈다. ErrorSource가 3종으로 고정이라
/// 오브젝트 풀이나 Instantiate/Destroy가 필요 없고, 런타임 할당도 발생하지 않는다.
/// </summary>
public class ErrorMarker : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private const float EmissionIntensity = 4f;

    private Renderer _sphereRenderer;
    private TMP_Text _label;
    private MaterialPropertyBlock _block;

    /// <summary>
    /// 구체 + 라벨 계층을 코드로 생성한다(프리팹 불필요).
    /// </summary>
    public static ErrorMarker Create(Transform parent, string name, Material sphereMaterial,
        float sphereScale, float labelScale)
    {
        GameObject root = new GameObject(name);
        root.transform.SetParent(parent, false);

        ErrorMarker marker = root.AddComponent<ErrorMarker>();
        marker._block = new MaterialPropertyBlock();

        // --- 경고 구체 ---
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = "Sphere";
        sphere.transform.SetParent(root.transform, false);
        sphere.transform.localScale = Vector3.one * sphereScale;

        // 그리퍼의 OverlapSphere 탐색 대상이 되지 않도록 프리미티브 콜라이더 제거
        Destroy(sphere.GetComponent<Collider>());

        marker._sphereRenderer = sphere.GetComponent<Renderer>();
        if (sphereMaterial != null)
        {
            marker._sphereRenderer.sharedMaterial = sphereMaterial;
        }

        sphere.AddComponent<RotateAndPulse>();

        // --- 오류 메시지 라벨 ---
        // TextMeshPro(3D)는 RectTransform을 요구하므로 생성 시점에 함께 붙인다
        GameObject labelObject = new GameObject("Label", typeof(RectTransform));
        labelObject.transform.SetParent(root.transform, false);
        labelObject.transform.localPosition = Vector3.up * (sphereScale * 1.2f);
        labelObject.transform.localScale = Vector3.one * labelScale;

        TextMeshPro label = labelObject.AddComponent<TextMeshPro>();
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 6f;
        label.rectTransform.sizeDelta = new Vector2(14f, 8f);

        labelObject.AddComponent<BillBoard>();

        marker._label = label;

        root.SetActive(false);
        return marker;
    }

    public void Show(ErrorInfo errorInfo, Color color)
    {
        _label.text = $"{errorInfo.Location}\n{errorInfo.Message}";
        _label.color = color;

        // 머티리얼 인스턴스를 만들지 않고 심각도별 색만 덮어쓴다
        _sphereRenderer.GetPropertyBlock(_block);
        _block.SetColor(BaseColorId, color);
        _block.SetColor(EmissionColorId, color * EmissionIntensity);
        _sphereRenderer.SetPropertyBlock(_block);

        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    public void SetPosition(Vector3 worldPosition)
    {
        transform.position = worldPosition;
    }
}
