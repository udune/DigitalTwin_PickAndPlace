using System.Collections;
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

    [Header("시각 효과")] 
    public GameObject warningPrefab;
    private GameObject currentWarning;

    [Header("카메라 동작 설정")] 
    public float zoomDistance = 1.5f;
    public Vector3 cameraOffset = new Vector3(0.5f, 0.3f, 0.5f);
    public bool autoReset = true;
    public float errorDuration = 10f;
    public int blinkTimes = 5;

    public void ShowError(string location, string errorType, string message)
    {
        StartCoroutine(ErrorSequence(location, errorType, message));
    }

    private IEnumerator ErrorSequence(string location, string errorType, string message)
    {
        Debug.Log(errorType);
        Debug.Log(message);
        
        Transform target = GetErrorLocation(location);

        if (target == null)
        {
            Debug.LogError($"Location not found: {location}");
            yield break;
        }
        
        Vector3 targetWorldPos = GetTargetCenter(target);

        if (currentWarning != null)
        {
            Destroy(currentWarning);
        }
        
        currentWarning = Instantiate(warningPrefab, targetWorldPos, Quaternion.identity);
        currentWarning.transform.SetParent(target, true);

        errorCamera.transform.position = targetWorldPos + cameraOffset;
        errorCamera.transform.LookAt(targetWorldPos);
        errorCamera.Priority = 20;

        Renderer renderer = target.GetComponent<Renderer>();
        if (renderer != null)
        {
            StartCoroutine(BlinkEffect(renderer, Color.red, blinkTimes));
        }

        if (!autoReset)
        {
            yield break;
        }
        
        yield return new WaitForSeconds(errorDuration);
        
        errorCamera.Priority = 5;
        
        if (currentWarning != null)
        {
            Destroy(currentWarning);
        }
    }

    private Transform GetErrorLocation(string location)
    {
        switch (location.ToUpper())
        {
            case "X_AXIS":
            case "X-AXIS":
                return xAxis;
            case "Y_AXIS":
            case "Y-AXIS":
                return yAxis;
            case "Z_AXIS":
            case "Z-AXIS":
                return zAxis;
            case "GRIPPER": 
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

    private IEnumerator BlinkEffect(Renderer renderer, Color color, int times)
    {
        Material originalMat = renderer.material;
        Material errorMat = new Material(originalMat);
        errorMat.color = color;
        errorMat.SetFloat("_Metallic", 0f);
        errorMat.EnableKeyword("_EMISSION");
        errorMat.SetColor("_EmissionColor", color * 2f);
        
        for (int i = 0; i < times; i++)
        {
            renderer.material = errorMat;
            yield return new WaitForSeconds(0.3f);
            
            renderer.material = originalMat;
            yield return new WaitForSeconds(0.3f);
        }
        
        renderer.material = originalMat;
    }

    private void ClearError()
    {
        StopAllCoroutines();

        if (currentWarning != null)
        {
            Destroy(currentWarning);
        }
        
        errorCamera.Priority = 5;
    }
    
    [ContextMenu("Test X-Axis Error")]
    private void TestXAxisError()
    {
        ShowError("X-AXIS", "Error", "X축 리미트 오버");
    }
    
    [ContextMenu("Test Y-Axis Error")]
    private void TestYAxisError()
    {
        ShowError("Y-AXIS", "Warning", "Y축 과속 경고");
    }
    
    [ContextMenu("Test Z-Axis Error")]
    private void TestZAxisError()
    {
        ShowError("Z-AXIS", "Error", "Z축 안전 높이 미달");
    }
    
    [ContextMenu("Test Gripper Error")]
    private void TestGripperError()
    {
        ShowError("GRIPPER", "Warning", "진공 압력 부족");
    }
    
    [ContextMenu("Clear Error")]
    private void ClearErrorTest()
    {
        ClearError();
    }
}
