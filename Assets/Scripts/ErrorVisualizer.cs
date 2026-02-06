using System.Collections;
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
    public Transform gripper;

    [Header("카메라 동작 설정")] 
    public Vector3 cameraOffset = new Vector3(0.5f, 0.3f, 0.5f);

    private void Start()
    {
        if (ErrorManager.Instance != null)
        {
            ErrorManager.Instance.ErrorRaisedAction += HandleErrorRaisedAction;
            ErrorManager.Instance.AllClearedAction += HandleAllClearedAction;
        }
    }

    private void OnDestroy()
    {
        if (ErrorManager.Instance != null)
        {
            ErrorManager.Instance.ErrorRaisedAction -= HandleErrorRaisedAction;
            ErrorManager.Instance.AllClearedAction -= HandleAllClearedAction;
        }
    }

    private void HandleErrorRaisedAction(ErrorInfo _errorInfo)
    {
        bool hasError = ErrorManager.Instance.ErrorInfoList.Any(errorInfo => errorInfo.Type == ErrorType.Error);
        
        if (hasError && _errorInfo.Type == ErrorType.Warning)
        {
            return;
        }

        FocusCamera(_errorInfo.Source);
    }

    private void HandleAllClearedAction()
    {
        errorCamera.Priority = 5;
    }

    private void FocusCamera(ErrorSource source)
    {
        Transform target = GetTransformForSource(source);
        if (target == null)
        {
            return;
        }

        Vector3 targetWorldPos = GetTargetCenter(target);
        
        errorCamera.transform.position = targetWorldPos + cameraOffset;
        errorCamera.transform.LookAt(targetWorldPos);
        errorCamera.Priority = 20;
    }

    private Transform GetTransformForSource(ErrorSource source)
    {
        switch (source)
        {
            case ErrorSource.XAxis: 
                return xAxis;
            case ErrorSource.YAxis: 
                return yAxis;
            case ErrorSource.ZAxis: 
                return zAxis;
            case ErrorSource.Gripper: 
                return gripper;
            default: 
                return null;
        }
    }

    private Vector3 GetTargetCenter(Transform target)
    {
        if (target.childCount == 0)
        {
            return target.position;
        }
        
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            Vector3 sum = Vector3.zero;
            int count = 0;
            
            foreach (Transform child in target)
            {
                sum += child.position;
                count++;
            }
            
            return count > 0 ? sum / count : target.position;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }
        
        return bounds.center;
    }
}
