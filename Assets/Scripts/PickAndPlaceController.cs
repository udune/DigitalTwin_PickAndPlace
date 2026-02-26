using UnityEngine;

public class PickAndPlaceController : MonoBehaviour
{
    [Header("Axis Transforms")]
    public Transform xAxis;
    public Transform yAxis;
    public Transform zAxis;
    
    [Header("Settings")]
    public float smoothSpeed = 5f;
    
    private Vector3 targetXPos;
    private Vector3 targetYPos;
    private Vector3 targetZPos;
    
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
        // mm를 Unity 단위로 변환 (1 Blender unit = 100mm)
        targetXPos = new Vector3(x / 100f, 0, 0);
        targetYPos = new Vector3(0, y / 100f, 0);
        targetZPos = new Vector3(0, 0, z / 100f);
    }
}
