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
    public float errorDuration = 10f;
    public int blinkTimes = 5;

    public void ShowError(string location, string errorType, string message)
    {
        StartCoroutine(ErrorSequence(location, errorType, message));
    }

    private IEnumerator ErrorSequence(string location, string errorType, string message)
    {
        Transform target = GetErrorLocation(location);

        if (target == null)
        {
            Debug.LogError($"Location not found: {location}");
            yield break;
        }

        if (currentWarning != null)
        {
            Destroy(currentWarning);
        }
        
        currentWarning = Instantiate(warningPrefab, target.position, Quaternion.identity);
        currentWarning.transform.SetParent(target);

        errorCamera.transform.position = target.position + new Vector3(0.5f, 0.3f, 0.5f);
        errorCamera.Target.LookAtTarget = target;
        errorCamera.Priority = 20;

        Renderer renderer = target.GetComponent<Renderer>();
        if (renderer != null)
        {
            StartCoroutine(BlinkEffect(renderer, Color.red, blinkTimes));
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
}
