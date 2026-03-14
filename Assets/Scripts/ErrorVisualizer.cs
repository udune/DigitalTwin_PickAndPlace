using System.Linq;
using Unity.Cinemachine;
using UnityEngine;

public class ErrorVisualizer : MonoBehaviour
{
    [Header("카메라 설정")]
    public CinemachineCamera mainCamera;
    public CinemachineCamera errorCamera;

    [Header("오류 대상")]
    public Transform xAxis;
    public Transform yAxis;
    public Transform zAxis;

    [Header("카메라 동작 설정")]
    public Vector3 cameraOffset = new Vector3(0.5f, 0.3f, 0.5f);

    private ErrorSource? currentTarget = null;

    private void Update()
    {
        if (ErrorManager.Instance == null)
        {
            return;
        }

        var errors = ErrorManager.Instance.ErrorInfoList;

        if (errors.Count == 0)
        {
            // 에러 없음 → 카메라 복귀
            if (currentTarget != null)
            {
                errorCamera.Priority = 5;
                currentTarget = null;
            }
            return;
        }

        // 에러 있음 → 우선순위: Error > Warning
        var topError = errors.FirstOrDefault(e => e.Type == ErrorType.Error);
        if (topError.Id == null)
        {
            topError = errors[0];
        }

        // 타겟이 바뀌었을 때만 카메라 이동
        if (currentTarget != topError.Source)
        {
            currentTarget = topError.Source;
            FocusCamera(topError.Source);
        }
    }

    private void FocusCamera(ErrorSource source)
    {
        Transform target = source switch
        {
            ErrorSource.XAxis => xAxis,
            ErrorSource.YAxis => yAxis,
            ErrorSource.ZAxis => zAxis,
            _ => null
        };

        if (target == null)
        {
            return;
        }

        Vector3 targetPos = GetTargetCenter(target);
        errorCamera.transform.position = targetPos + cameraOffset;
        errorCamera.transform.LookAt(targetPos);
        errorCamera.Priority = 20;
    }

    private Vector3 GetTargetCenter(Transform target)
    {
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>();

        if (renderers.Length == 0)
        {
            return target.position;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        return bounds.center;
    }
}
