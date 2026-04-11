using UnityEngine;

/// <summary>
/// 그리퍼로 집을 수 있는 물체
/// 이 컴포넌트가 있는 GameObject만 Pick 가능
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class PickableObject : MonoBehaviour
{
    [Header("물체 정보")]
    [Tooltip("물체 이름 (UI 표시용)")]
    public string objectName = "Cube";

    [Tooltip("물체 무게 (kg)")]
    public float weight = 0.1f;

    [Header("시각 효과")]
    [Tooltip("범위 내 진입 시 하이라이트 색상")]
    public Color highlightColor = Color.yellow;

    [Tooltip("하이라이트 활성화")]
    public bool enableHighlight = true;

    [Header("상태")]
    [SerializeField]
    private bool _isPicked = false;

    private Color _originalColor;
    private Renderer _renderer;
    private bool _isHighlighted = false;
    private MaterialPropertyBlock _propertyBlock;

    // Properties
    public bool IsPicked => _isPicked;

    void Start()
    {
        // "Pickable" 레이어 설정 시도
        int pickableLayer = LayerMask.NameToLayer("Pickable");
        if (pickableLayer != -1)
        {
            gameObject.layer = pickableLayer;
            Debug.Log($"[PickableObject] {name} set to Pickable layer");
        }
        else
        {
            Debug.LogWarning($"[PickableObject] 'Pickable' layer not found. Please create it in Project Settings.");
        }

        // Renderer 캐싱
        _renderer = GetComponent<Renderer>();
        if (_renderer != null)
        {
            _propertyBlock = new MaterialPropertyBlock();
            _renderer.GetPropertyBlock(_propertyBlock);
            _originalColor = _renderer.material.color;
        }

        // Rigidbody 설정
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.mass = weight;
        }

        // 이름 자동 설정
        if (string.IsNullOrEmpty(objectName))
        {
            objectName = gameObject.name;
        }

        Debug.Log($"[PickableObject] {objectName} initialized (weight: {weight}kg)");
    }

    /// <summary>
    /// 그리퍼에 의해 집어졌을 때 호출
    /// </summary>
    public void OnPicked()
    {
        _isPicked = true;
        ResetHighlight();
    }

    /// <summary>
    /// 그리퍼에서 놓였을 때 호출
    /// </summary>
    public void OnPlaced()
    {
        _isPicked = false;
    }

    /// <summary>
    /// 그리퍼 범위 내에 들어왔을 때
    /// </summary>
    public void Highlight()
    {
        if (!enableHighlight || _isHighlighted || _isPicked)
        {
            return;
        }

        if (_renderer != null)
        {
            _renderer.material.color = highlightColor;
            _isHighlighted = true;
        }
    }

    /// <summary>
    /// 그리퍼 범위에서 벗어났을 때
    /// </summary>
    public void ResetHighlight()
    {
        if (!_isHighlighted)
        {
            return;
        }

        if (_renderer != null)
        {
            _renderer.material.color = _originalColor;
            _isHighlighted = false;
        }
    }

    /// <summary>
    /// 디버그 정보 표시
    /// </summary>
    void OnDrawGizmos()
    {
        // Pick 가능 상태 시각화
        if (_isPicked)
        {
            Gizmos.color = Color.red;
        }
        else
        {
            Gizmos.color = Color.cyan;
        }

        Gizmos.DrawWireCube(transform.position, transform.localScale * 1.1f);
    }
}
