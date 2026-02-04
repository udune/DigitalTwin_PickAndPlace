using UnityEngine;

public class AxisMover : MonoBehaviour
{
    public Transform xAxis;
    public Transform yAxis;
    public Transform zAxis;
    
    public float speed = 0.5f;
    
    void Update()
    {
        if (Input.GetKey(KeyCode.LeftArrow))
        {
            xAxis.localPosition += Vector3.left * speed * Time.deltaTime;
        }

        if (Input.GetKey(KeyCode.RightArrow))
        {
            xAxis.localPosition += Vector3.right * speed * Time.deltaTime;
        }

        if (Input.GetKey(KeyCode.UpArrow))
        {
            yAxis.localPosition += Vector3.forward * speed * Time.deltaTime;
        }

        if (Input.GetKey(KeyCode.DownArrow))
        {
            yAxis.localPosition += Vector3.back * speed * Time.deltaTime;
        }

        if (Input.GetKey(KeyCode.W))
        {
            zAxis.localPosition += Vector3.up * speed * Time.deltaTime;
        }

        if (Input.GetKey(KeyCode.S))
        {
            zAxis.localPosition += Vector3.down * speed * Time.deltaTime;
        }
        
        if (Input.GetKeyDown(KeyCode.Space))
        {
            xAxis.localPosition = Vector3.zero;
            yAxis.localPosition = Vector3.zero;
            zAxis.localPosition = Vector3.zero;
        }
    }
}
