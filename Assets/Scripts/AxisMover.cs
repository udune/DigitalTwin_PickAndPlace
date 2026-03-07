using UnityEngine;

public class AxisMover : MonoBehaviour
{
    public PickAndPlaceController controller;

    public float speed = 200f; // mm/s 단위

    private float currentX;
    private float currentY;
    private float currentZ;

    void Start()
    {
        // 초기 위치 (mm 단위)
        currentX = 0f;
        currentY = 0f;
        currentZ = 0f;
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
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            currentX = 0f;
            currentY = 0f;
            currentZ = 0f;
            controller.MoveToPosition(0f, 0f, 0f);

            ErrorManager.Instance?.ClearAllErrors();
        }
    }
}
