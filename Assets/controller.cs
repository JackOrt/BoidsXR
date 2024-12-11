using UnityEngine;

public class FlyCameraController : MonoBehaviour
{
    public float movementSpeed = 10f;    // Speed of movement
    public float rotationSpeed = 2f;    // Speed of rotation
    public float boostMultiplier = 2f;  // Multiplier for speed boost when holding Shift

    private float yaw = 0f;
    private float pitch = 0f;

    void Update()
    {
        HandleMovement();

        // Only handle rotation when the right mouse button is held
        if (Input.GetMouseButton(1)) // 1 corresponds to the right mouse button
        {
            HandleRotation();
        }
    }

    private void HandleMovement()
    {
        float speed = movementSpeed;

        // Boost speed when holding Shift
        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
        {
            speed *= boostMultiplier;
        }

        // Get input for movement
        Vector3 moveDirection = new Vector3(
            Input.GetAxis("Horizontal"), // A/D or Left/Right Arrow for left/right
            0,
            Input.GetAxis("Vertical")   // W/S or Up/Down Arrow for forward/backward
        );

        // Handle upward and downward movement with Space and Ctrl
        if (Input.GetKey(KeyCode.Space))
        {
            moveDirection.y += 1;
        }
        if (Input.GetKey(KeyCode.LeftControl))
        {
            moveDirection.y -= 1;
        }

        // Move the camera in local space
        transform.Translate(moveDirection * speed * Time.deltaTime, Space.Self);
    }

    private void HandleRotation()
    {
        // Get mouse input for rotation
        yaw += Input.GetAxis("Mouse X") * rotationSpeed;
        pitch -= Input.GetAxis("Mouse Y") * rotationSpeed;

        // Clamp the pitch to avoid flipping the camera
        pitch = Mathf.Clamp(pitch, -90f, 90f);

        // Apply rotation
        transform.eulerAngles = new Vector3(pitch, yaw, 0f);
    }
}
