using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// 오류 시각화 조정자.
/// ErrorManager 이벤트를 받아 축별 "최상위 오류" 상태를 유지하고,
/// 카메라 포커스 / 경고 마커 / 부품 강조 세 가지 표현을 함께 구동한다.
/// </summary>
public class ErrorVisualizer : MonoBehaviour
{
    // 순회 순서를 고정해 심각도가 같을 때 카메라 대상이 프레임마다 바뀌지 않게 한다
    private static readonly ErrorSource[] AllSources =
    {
        ErrorSource.XAxis, ErrorSource.YAxis, ErrorSource.ZAxis
    };

    [Header("카메라 설정")]
    public CinemachineCamera errorCamera;
    public int defaultPriority = 5;
    public int focusPriority = 20;

    [Header("오류 대상")]
    public Transform xAxis;
    public Transform yAxis;
    public Transform zAxis;

    [Header("카메라 동작 설정")]
    public Vector3 cameraOffset = new Vector3(0.5f, 0.3f, 0.5f);

    [Header("경고 마커")]
    [Tooltip("경고 구체에 사용할 머티리얼 (WarningMaterial.mat)")]
    public Material markerMaterial;
    public float markerScale = 0.2f;
    [Tooltip("부품 위쪽으로 마커를 띄우는 높이")]
    public float markerHeight = 0.3f;
    public float labelScale = 0.05f;

    [Header("색상 / 깜빡임")]
    public Color errorColor = new Color(1f, 0.1f, 0.1f);
    public Color warningColor = new Color(1f, 0.55f, 0f);
    [Tooltip("Error 깜빡임 속도 (rad/s). 16 ≈ 2.5Hz")]
    public float errorBlinkSpeed = 16f;
    [Tooltip("Warning 깜빡임 속도 (rad/s). 6 ≈ 1Hz")]
    public float warningBlinkSpeed = 6f;

    private readonly Dictionary<ErrorSource, ErrorMarker> _markers = new Dictionary<ErrorSource, ErrorMarker>();
    private readonly Dictionary<ErrorSource, ErrorHighlighter> _highlighters = new Dictionary<ErrorSource, ErrorHighlighter>();
    private readonly Dictionary<ErrorSource, ErrorInfo> _topBySource = new Dictionary<ErrorSource, ErrorInfo>();

    private ErrorSource? _cameraTarget;

    private void Awake()
    {
        Dictionary<ErrorSource, Transform> axes = new Dictionary<ErrorSource, Transform>();

        CollectAxis(axes, ErrorSource.XAxis, xAxis);
        CollectAxis(axes, ErrorSource.YAxis, yAxis);
        CollectAxis(axes, ErrorSource.ZAxis, zAxis);

        foreach (KeyValuePair<ErrorSource, Transform> axis in axes)
        {
            RegisterAxis(axis.Key, axis.Value, FindNestedAxes(axes, axis.Key, axis.Value));
        }
    }

    private void Start()
    {
        // Start()는 모든 Awake() 이후에 호출되므로 ErrorManager 싱글턴이 준비된 상태다
        if (ErrorManager.Instance == null)
        {
            Debug.LogWarning("[ErrorVisualizer] ErrorManager를 찾을 수 없어 오류 시각화가 비활성화됩니다.");
            return;
        }

        ErrorManager.Instance.ErrorRaisedAction += OnErrorRaised;
        ErrorManager.Instance.ErrorClearedAction += OnErrorCleared;
        ErrorManager.Instance.AllClearedAction += OnAllErrorsCleared;

        RebuildState();
    }

    private void OnDestroy()
    {
        if (ErrorManager.Instance == null)
        {
            return;
        }

        ErrorManager.Instance.ErrorRaisedAction -= OnErrorRaised;
        ErrorManager.Instance.ErrorClearedAction -= OnErrorCleared;
        ErrorManager.Instance.AllClearedAction -= OnAllErrorsCleared;
    }

    private void LateUpdate()
    {
        // 축 이동은 PickAndPlaceController.Update()에서 일어나므로,
        // 그 결과를 반영하려면 LateUpdate에서 추적해야 한다
        if (_topBySource.Count == 0)
        {
            return;
        }

        foreach (ErrorSource source in AllSources)
        {
            if (!_topBySource.TryGetValue(source, out ErrorInfo errorInfo))
            {
                continue;
            }

            ErrorHighlighter highlighter = _highlighters[source];
            bool isError = errorInfo.Type == ErrorType.Error;

            highlighter.Blink(GetColor(errorInfo.Type), isError ? errorBlinkSpeed : warningBlinkSpeed);

            if (!highlighter.TryGetWorldBounds(out Bounds bounds))
            {
                continue;
            }

            _markers[source].SetPosition(bounds.center + Vector3.up * (bounds.extents.y + markerHeight));

            // 매 프레임 재계산하므로 축이 움직이는 동안에도 카메라가 계속 따라간다
            if (_cameraTarget == source && errorCamera != null)
            {
                errorCamera.transform.position = bounds.center + cameraOffset;
                errorCamera.transform.LookAt(bounds.center);
            }
        }
    }

    // === 이벤트 핸들러 ===

    private void OnErrorRaised(ErrorInfo errorInfo) => RebuildState();

    private void OnErrorCleared(string id) => RebuildState();

    private void OnAllErrorsCleared() => RebuildState();

    // === 내부 ===

    private static void CollectAxis(Dictionary<ErrorSource, Transform> axes, ErrorSource source, Transform axis)
    {
        if (axis == null)
        {
            Debug.LogWarning($"[ErrorVisualizer] {source} Transform이 할당되지 않아 시각화에서 제외됩니다.");
            return;
        }

        axes[source] = axis;
    }

    /// <summary>
    /// 이 축 안에 중첩된 다른 축들을 계층에서 직접 찾는다.
    /// 하드코딩하지 않으므로 프리팹 구조가 바뀌어도 따라간다.
    /// </summary>
    private static List<Transform> FindNestedAxes(Dictionary<ErrorSource, Transform> axes,
        ErrorSource source, Transform axis)
    {
        List<Transform> nested = new List<Transform>();

        foreach (KeyValuePair<ErrorSource, Transform> other in axes)
        {
            if (other.Key != source && other.Value.IsChildOf(axis))
            {
                nested.Add(other.Value);
            }
        }

        return nested;
    }

    private void RegisterAxis(ErrorSource source, Transform axis, List<Transform> nestedAxes)
    {
        ErrorHighlighter highlighter = new ErrorHighlighter(axis, nestedAxes);

        if (highlighter.RendererCount == 0)
        {
            Debug.LogWarning($"[ErrorVisualizer] {source}에 고유 Renderer가 없어 강조/포커스가 동작하지 않습니다.");
        }

        _highlighters[source] = highlighter;
        _markers[source] = ErrorMarker.Create(transform, $"ErrorMarker_{source}",
            markerMaterial, markerScale, labelScale);
    }

    /// <summary>
    /// 오류 목록에서 축별 최상위 오류를 다시 계산하고, 표현을 갱신한다.
    /// 이벤트가 올 때만 호출되므로 매 프레임 ErrorInfoList를 만들지 않는다.
    /// </summary>
    private void RebuildState()
    {
        _topBySource.Clear();

        foreach (ErrorInfo errorInfo in ErrorManager.Instance.ErrorInfoList)
        {
            // Transform이 할당되지 않아 등록에서 빠진 축은 표현할 수단이 없으므로 제외한다
            if (!_markers.ContainsKey(errorInfo.Source))
            {
                continue;
            }

            // 같은 축에 여러 오류가 있으면 Error가 Warning을 이긴다
            if (_topBySource.TryGetValue(errorInfo.Source, out ErrorInfo current)
                && current.Type >= errorInfo.Type)
            {
                continue;
            }

            _topBySource[errorInfo.Source] = errorInfo;
        }

        _cameraTarget = null;

        foreach (ErrorSource source in AllSources)
        {
            if (!_markers.TryGetValue(source, out ErrorMarker marker))
            {
                continue;
            }

            if (!_topBySource.TryGetValue(source, out ErrorInfo errorInfo))
            {
                marker.Hide();
                _highlighters[source].Clear();
                continue;
            }

            marker.Show(errorInfo, GetColor(errorInfo.Type));

            // 마커와 깜빡임은 오류가 있는 모든 축에 동시에 표시하지만,
            // 카메라는 하나뿐이므로 전역 최상위 1건만 비춘다
            if (_cameraTarget == null || errorInfo.Type > _topBySource[_cameraTarget.Value].Type)
            {
                _cameraTarget = source;
            }
        }

        if (errorCamera != null)
        {
            errorCamera.Priority = _cameraTarget == null ? defaultPriority : focusPriority;
        }
    }

    private Color GetColor(ErrorType type) => type == ErrorType.Error ? errorColor : warningColor;
}
