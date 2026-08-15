using UnityEngine;

public class PickAndPlaceController : MonoBehaviour
{
    [Header("Axis Transforms")]
    public Transform xAxis;
    public Transform yAxis;
    public Transform zAxis;
    
    [Header("Settings")]
    public float smoothSpeed = 5f;

    [Header("입력 검증")]
    [Tooltip("허용할 좌표 절대값 상한 (mm). ErrorConditionMonitor의 리미트보다 넉넉하게 잡아야 " +
             "'리미트 초과' 오류가 정상적으로 발동한다.")]
    public float positionLimitMm = 1000f;

    private Vector3 targetXPos;
    private Vector3 targetYPos;
    private Vector3 targetZPos;

    // 범위 초과 경고는 이탈 1회당 한 번만 남긴다. MoveToPosition이 매 프레임 호출되기 때문.
    private bool _limitWarned;

    void Start()
    {
        targetXPos = xAxis.localPosition;
        targetYPos = yAxis.localPosition;
        targetZPos = zAxis.localPosition;
    }
    
    void Update()
    {
        // 부드럽게 이동
        xAxis.localPosition = Vector3.Lerp(xAxis.localPosition, targetXPos, 
            smoothSpeed * Time.deltaTime);
        yAxis.localPosition = Vector3.Lerp(yAxis.localPosition, targetYPos, 
            smoothSpeed * Time.deltaTime);
        zAxis.localPosition = Vector3.Lerp(zAxis.localPosition, targetZPos, 
            smoothSpeed * Time.deltaTime);
    }
    
    /// <summary>
    /// WPF에서 호출 (mm 단위)
    /// </summary>
    public void MoveToPosition(float x, float y, float z)
    {
        // NaN/무한대가 한 번 들어오면 Lerp를 타고 Transform에 영구히 눌어붙는다.
        // 특히 NaN은 비교 연산이 전부 false라 ErrorConditionMonitor의 리미트 검사에도 걸리지 않으므로
        // 감지조차 되지 않는다. 반드시 여기서 통째로 막는다.
        if (!IsFinite(x) || !IsFinite(y) || !IsFinite(z))
        {
            Debug.LogWarning($"[Controller] 유효하지 않은 좌표를 무시한다: ({x}, {y}, {z})");
            return;
        }

        // 비정상적으로 큰 값만 안전 범위로 자른다.
        // positionLimitMm은 오류 감지 리미트보다 넓으므로, 정상적인 '리미트 초과' 오류는 그대로 발동한다.
        float limit = Mathf.Abs(positionLimitMm);
        bool outOfRange = Mathf.Abs(x) > limit || Mathf.Abs(y) > limit || Mathf.Abs(z) > limit;

        if (outOfRange)
        {
            if (!_limitWarned)
            {
                Debug.LogWarning($"[Controller] 좌표가 안전 범위(±{limit}mm)를 벗어나 잘렸다: ({x}, {y}, {z})");
            }

            x = ClampToLimit(x);
            y = ClampToLimit(y);
            z = ClampToLimit(z);
        }

        _limitWarned = outOfRange;

        // mm를 Unity 단위로 변환 (1 Blender unit = 100mm)
        // yAxis는 Z 방향(forward/back), zAxis는 Y 방향(up/down)으로 이동
        targetXPos = new Vector3(x / 100f, 0, 0);
        targetYPos = new Vector3(0, 0, y / 100f);
        targetZPos = new Vector3(0, z / 100f, 0);
    }

    /// <summary>
    /// 좌표 하나를 안전 범위(±positionLimitMm) 안으로 자른다.
    /// 좌표 유효성의 기준을 이 컴포넌트 한 곳에 두기 위해 공개한다(AxisMover가 재사용).
    /// NaN은 비교가 전부 false라 Clamp로 걸러지지 않으므로 IsFinite로 따로 확인해야 한다.
    /// </summary>
    public float ClampToLimit(float valueMm)
    {
        float limit = Mathf.Abs(positionLimitMm);
        return Mathf.Clamp(valueMm, -limit, limit);
    }

    /// <summary>
    /// NaN도 무한대도 아닌 정상적인 실수인지 확인한다.
    /// float.IsFinite는 API 호환성 레벨에 따라 없을 수 있어 직접 구현한다.
    /// </summary>
    public static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
