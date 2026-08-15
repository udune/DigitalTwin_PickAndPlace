using UnityEngine;

public class AxisMover : MonoBehaviour
{
    public PickAndPlaceController controller;
    public GripperController gripper;

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
        // NaN이 누적값에 한 번 들어오면 이후 모든 덧셈이 NaN이 되어 축이 영구히 멈춘다
        // (컨트롤러가 NaN을 계속 거부하므로 키를 눌러도 반응이 없다).
        // 컨트롤러와 동일한 기준으로 통째로 무시한다.
        if (!PickAndPlaceController.IsFinite(x)
            || !PickAndPlaceController.IsFinite(y)
            || !PickAndPlaceController.IsFinite(z))
        {
            Debug.LogWarning($"[Input] 유효하지 않은 좌표를 무시한다: ({x}, {y}, {z})");
            return;
        }

        // WPF에서 받은 값으로 현재 위치 동기화
        currentX = x;
        currentY = y;
        currentZ = z;

        ClampToControllerLimit();
    }

    /// <summary>
    /// 누적 좌표를 컨트롤러의 안전 범위 안으로 자른다.
    /// 컨트롤러가 값을 잘라도 여기 누적값이 계속 자라면 두 값이 어긋나서,
    /// 되돌아올 때 그만큼 키를 더 눌러야 축이 다시 움직인다.
    /// 상한은 컨트롤러를 단일 기준으로 삼아 중복 정의하지 않는다.
    /// </summary>
    private void ClampToControllerLimit()
    {
        if (controller == null)
        {
            return;
        }

        currentX = controller.ClampToLimit(currentX);
        currentY = controller.ClampToLimit(currentY);
        currentZ = controller.ClampToLimit(currentZ);
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
            // 컨트롤러에 넘기기 전에 누적값 자체를 자른다.
            // WPF로도 잘린 값이 나가야 두 프로세스가 같은 위치를 보게 된다.
            ClampToControllerLimit();

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

        // ========== 그리퍼 제어 ==========
        if (gripper == null) return;

        // G키: 집기 (Pick)
        if (Input.GetKeyDown(KeyCode.G))
        {
            bool success = gripper.Pick();
            if (success)
            {
                Debug.Log("[Input] Pick successful!");
                IPCReceiver.Instance?.SendGripperStatus(gripper);
            }
            else
            {
                Debug.Log("[Input] Pick failed!");
            }
        }

        // R키: 놓기 (Release/Place)
        if (Input.GetKeyDown(KeyCode.R))
        {
            bool success = gripper.Place();
            if (success)
            {
                Debug.Log("[Input] Place successful!");
                IPCReceiver.Instance?.SendGripperStatus(gripper);
            }
            else
            {
                Debug.Log("[Input] Place failed!");
            }
        }
    }
}
