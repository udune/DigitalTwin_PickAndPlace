using UnityEngine;

public class AxisMover : MonoBehaviour
{
    public PickAndPlaceController controller;

    public float speed = 100f; // mm/s 단위

    private float currentX;
    private float currentY;
    private float currentZ;

    void Start()
    {
        // 초기 위치 (mm 단위)
        currentX = 0f;
        currentY = 0f;
        currentZ = 0f;

        // WPF에서 데이터 수신 시 동기화
        if (IPCReceiver.Instance != null)
        {
            IPCReceiver.Instance.OnAxisDataReceived += OnWpfAxisDataReceived;
        }
    }

    void OnDestroy()
    {
        if (IPCReceiver.Instance != null)
        {
            IPCReceiver.Instance.OnAxisDataReceived -= OnWpfAxisDataReceived;
        }
    }

    private void OnWpfAxisDataReceived(float x, float y, float z)
    {
        // WPF에서 받은 값으로 현재 위치 동기화
        currentX = x;
        currentY = y;
        currentZ = z;
    }

    void Update()
    {
        if (controller == null)
        {
            return;
        }

        bool moved = false;

        if (Input.GetKey(KeyCode.LeftArrow))
        {
            currentX -= speed * Time.deltaTime;
            moved = true;
        }

        if (Input.GetKey(KeyCode.RightArrow))
        {
            currentX += speed * Time.deltaTime;
            moved = true;
        }

        if (Input.GetKey(KeyCode.UpArrow))
        {
            currentY += speed * Time.deltaTime;
            moved = true;
        }

        if (Input.GetKey(KeyCode.DownArrow))
        {
            currentY -= speed * Time.deltaTime;
            moved = true;
        }

        if (Input.GetKey(KeyCode.W))
        {
            currentZ += speed * Time.deltaTime;
            moved = true;
        }

        if (Input.GetKey(KeyCode.S))
        {
            currentZ -= speed * Time.deltaTime;
            moved = true;
        }

        if (moved)
        {
            controller.MoveToPosition(currentX, currentY, currentZ);

            // WPF로 전송
            IPCReceiver.Instance?.SendAxisData(currentX, currentY, currentZ);
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            currentX = 0f;
            currentY = 0f;
            currentZ = 0f;
            controller.MoveToPosition(0f, 0f, 0f);

            // WPF로 전송
            IPCReceiver.Instance?.SendAxisData(0f, 0f, 0f);

            ErrorManager.Instance?.ClearAllErrors();
        }
    }
}
