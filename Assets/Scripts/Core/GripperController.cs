using UnityEngine;

/// <summary>
/// 공압 그리퍼 제어 시스템
/// 물체를 집고(Pick) 놓는(Place) 기능 제공
/// </summary>
public class GripperController : MonoBehaviour
{
    [Header("그리퍼 Transform")]
    [Tooltip("왼쪽 집게 Transform")]
    public Transform gripperLeft;

    [Tooltip("오른쪽 집게 Transform")]
    public Transform gripperRight;

    [Tooltip("물체가 붙을 위치 (Empty Transform)")]
    public Transform gripPoint;

    [Header("그리퍼 동작 설정")]
    [Tooltip("열린 상태 간격 (Unity units, 0.05 = 50mm)")]
    public float openDistance = 0.05f;

    [Tooltip("닫힌 상태 간격 (Unity units, 0.01 = 10mm)")]
    public float closeDistance = 0.01f;

    [Tooltip("개폐 속도 (높을수록 빠름)")]
    public float gripSpeed = 2.0f;

    [Header("공압 시뮬레이션")]
    [Tooltip("진공 그리퍼 모드 사용")]
    public bool useVacuum = true;

    [Tooltip("진공 흡착 범위 (Unity units, 0.03 = 30mm)")]
    public float vacuumRange = 0.03f;

    [Header("디버그")]
    public bool showDebugInfo = true;

    // Private Fields
    private bool _isGripping = false;
    private GameObject _grippedObject = null;
    private float _currentDistance;

    // Properties
    public bool IsGripping => _isGripping;
    public GameObject GrippedObject => _grippedObject;

    void Start()
    {
        _currentDistance = openDistance;

        if (gripPoint == null)
        {
            Debug.LogError("[GripperController] GripPoint is not assigned!");
        }

        Debug.Log("[GripperController] Initialized");
    }

    void Update()
    {
        // 그리퍼 개폐 애니메이션
        float targetDistance = _isGripping ? closeDistance : openDistance;
        _currentDistance = Mathf.Lerp(_currentDistance, targetDistance,
            gripSpeed * Time.deltaTime);

        // 양쪽 집게 위치 업데이트
        if (gripperLeft != null)
        {
            Vector3 leftPos = gripperLeft.localPosition;
            leftPos.x = -_currentDistance;
            gripperLeft.localPosition = leftPos;
        }

        if (gripperRight != null)
        {
            Vector3 rightPos = gripperRight.localPosition;
            rightPos.x = _currentDistance;
            gripperRight.localPosition = rightPos;
        }
    }

    /// <summary>
    /// 물체 집기 (공압 ON)
    /// </summary>
    /// <returns>성공 여부</returns>
    public bool Pick()
    {
        // 이미 물체를 잡고 있는 경우
        if (_grippedObject != null)
        {
            if (showDebugInfo)
                Debug.LogWarning("[Gripper] Already holding an object!");
            return false;
        }

        // 범위 내 물체 찾기
        GameObject target = FindNearestPickableObject();

        if (target == null)
        {
            if (showDebugInfo)
                Debug.LogWarning("[Gripper] No pickable object in range!");
            return false;
        }

        // 물체 집기
        _grippedObject = target;
        _isGripping = true;

        // 물체의 물리 비활성화
        Rigidbody rb = target.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
        }

        // 물체를 그리퍼에 부착
        target.transform.SetParent(gripPoint);
        target.transform.localPosition = Vector3.zero;
        target.transform.localRotation = Quaternion.identity;

        // PickableObject에 알림
        var pickable = target.GetComponent<PickableObject>();
        if (pickable != null)
        {
            pickable.OnPicked();
        }

        if (showDebugInfo)
            Debug.Log($"[Gripper] Picked: {target.name}");

        return true;
    }

    /// <summary>
    /// 물체 놓기 (공압 OFF)
    /// </summary>
    /// <returns>성공 여부</returns>
    public bool Place()
    {
        // 물체를 잡고 있지 않은 경우
        if (_grippedObject == null)
        {
            if (showDebugInfo)
                Debug.LogWarning("[Gripper] No object to place!");
            return false;
        }

        // 물체 정보 저장
        GameObject obj = _grippedObject;

        // 물체 분리
        obj.transform.SetParent(null);

        // 물체의 물리 재활성화
        Rigidbody rb = obj.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
        }

        // PickableObject에 알림
        var pickable = obj.GetComponent<PickableObject>();
        if (pickable != null)
        {
            pickable.OnPlaced();
        }

        if (showDebugInfo)
            Debug.Log($"[Gripper] Placed: {obj.name}");

        // 상태 초기화
        _grippedObject = null;
        _isGripping = false;

        return true;
    }

    /// <summary>
    /// 가장 가까운 집을 수 있는 물체 찾기
    /// </summary>
    /// <returns>찾은 GameObject 또는 null</returns>
    private GameObject FindNearestPickableObject()
    {
        if (gripPoint == null)
            return null;

        // "Pickable" 레이어의 Collider 검색
        int pickableLayer = LayerMask.GetMask("Pickable");

        // 레이어가 없으면 모든 PickableObject 컴포넌트를 가진 오브젝트 검색
        if (pickableLayer == 0)
        {
            if (showDebugInfo)
                Debug.LogWarning("[Gripper] 'Pickable' layer not found. Searching by component...");

            return FindNearestByComponent();
        }

        Collider[] colliders = Physics.OverlapSphere(
            gripPoint.position,
            vacuumRange,
            pickableLayer
        );

        if (colliders.Length == 0)
            return null;

        // 가장 가까운 물체 찾기
        GameObject nearest = null;
        float minDistance = float.MaxValue;

        foreach (Collider col in colliders)
        {
            float distance = Vector3.Distance(gripPoint.position, col.transform.position);
            if (distance < minDistance)
            {
                minDistance = distance;
                nearest = col.gameObject;
            }
        }

        return nearest;
    }

    /// <summary>
    /// PickableObject 컴포넌트로 물체 찾기 (레이어 없을 때 폴백)
    /// </summary>
    private GameObject FindNearestByComponent()
    {
        // 정렬 순서는 지정하지 않는다. 아래에서 거리로 최근접을 고르므로 순서가 의미 없고,
        // Unity 6000.4부터 FindObjectsSortMode를 받는 오버로드가 Obsolete가 됐다.
        // 인자 없는 오버로드는 비활성 오브젝트를 제외한다(FindObjectsInactive.Exclude와 동일).
        PickableObject[] pickables = FindObjectsByType<PickableObject>();

        GameObject nearest = null;
        float minDistance = float.MaxValue;

        foreach (var pickable in pickables)
        {
            float distance = Vector3.Distance(gripPoint.position, pickable.transform.position);
            if (distance <= vacuumRange && distance < minDistance)
            {
                minDistance = distance;
                nearest = pickable.gameObject;
            }
        }

        return nearest;
    }

    /// <summary>
    /// 디버그용: 진공 범위 시각화
    /// </summary>
    void OnDrawGizmosSelected()
    {
        if (gripPoint != null)
        {
            Gizmos.color = _isGripping ? Color.red : Color.green;
            Gizmos.DrawWireSphere(gripPoint.position, vacuumRange);
        }
    }
}
