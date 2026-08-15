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

    // 라벨용 Pretendard 폰트. 마커를 코드로 생성하므로 인스펙터 연결 없이 Resources에서 직접 불러온다.
    // 경로는 "Assets/TextMesh Pro/Resources/" 기준 상대 경로이며 확장자는 생략한다.
    // 애셋명 끝에 "SDF"가 두 번 붙는 것은 원본 폰트 파일 이름(Pretendard-Regular SDF.otf)에
    // 이미 SDF가 들어 있는 상태로 폰트 애셋이 생성됐기 때문이다.
    private const string LabelFontPath = "Fonts & Materials/Pretendard-Regular SDF SDF";

    private static TMP_FontAsset _labelFont;

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

        TMP_FontAsset labelFont = GetLabelFont();
        if (labelFont != null)
        {
            label.font = labelFont;
        }

        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 6f;
        label.rectTransform.sizeDelta = new Vector2(14f, 8f);

        labelObject.AddComponent<BillBoard>();

        marker._label = label;

        root.SetActive(false);
        return marker;
    }

    /// <summary>
    /// 라벨 폰트를 한 번만 로드해 캐싱한다. 축 개수만큼 Create가 호출되므로 중복 로드를 피한다.
    /// 로드에 실패하면 null을 돌려주고 TMP 기본 폰트를 그대로 쓴다.
    /// </summary>
    private static TMP_FontAsset GetLabelFont()
    {
        if (_labelFont == null)
        {
            _labelFont = Resources.Load<TMP_FontAsset>(LabelFontPath);

            if (_labelFont == null)
            {
                Debug.LogWarning($"[ErrorMarker] 라벨 폰트를 찾을 수 없다: {LabelFontPath}. TMP 기본 폰트로 대체한다.");
            }
        }

        return _labelFont;
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
