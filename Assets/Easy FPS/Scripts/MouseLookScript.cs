using UnityEngine;

public class SimpleFPSController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float walkSpeed = 5f;            // Base walking speed
    public float sprintMultiplier = 2f;     // Sprint multiplier when LeftShift is held
    public float jumpForce = 5f;            // Initial jump velocity (upward force)
    public float gravity = -9.81f;          // Gravity applied per second

    [Header("Mouse Look Settings")]
    public float mouseSensitivity = 2f;     // Mouse look sensitivity
    public float verticalLookLimit = 80f;   // Maximum vertical angle

    private CharacterController characterController;
    private Transform cameraTransform;
    private float verticalRotation = 0f;    // Current pitch of the camera
    private float verticalVelocity = 0f;    // Separate vertical velocity for jumping & gravity

    void Start()
    {
        // Get the CharacterController component attached to the player.
        characterController = GetComponent<CharacterController>();
        if (characterController == null)
        {
            Debug.LogError("No CharacterController found on this GameObject.");
        }

        // Get the main camera (ensure it is tagged "MainCamera").
        cameraTransform = Camera.main.transform;
        if (cameraTransform.parent != transform)
        {
            Debug.LogWarning("Main Camera is not a child of the player object. For proper look behavior, please parent it to the player.");
        }

        // Lock and hide the cursor.
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        HandleMouseLook();
        HandleMovement();
    }

    // Handles mouse look so that moving the mouse rotates the camera and player appropriately.
    void HandleMouseLook()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        // Unity’s default for "Mouse Y" is inverted (moving the mouse up gives a negative value).
        // Subtracting mouseY makes the camera look up when you move the mouse up.
        verticalRotation -= mouseY;
        verticalRotation = Mathf.Clamp(verticalRotation, -verticalLookLimit, verticalLookLimit);

        // Apply vertical rotation only to the camera.
        if (cameraTransform != null)
        {
            cameraTransform.localRotation = Quaternion.Euler(verticalRotation, 0f, 0f);
        }
        // Rotate the player horizontally.
        transform.Rotate(0f, mouseX, 0f);
    }

    // Handles WASD movement, sprinting, and jumping.
    void HandleMovement()
    {
        // Get horizontal and vertical input.
        float moveX = Input.GetAxis("Horizontal");
        float moveZ = Input.GetAxis("Vertical");

        // Determine speed (sprint if LeftShift is held).
        bool sprint = Input.GetKey(KeyCode.LeftShift);
        float speed = sprint ? walkSpeed * sprintMultiplier : walkSpeed;

        // Calculate horizontal movement relative to the player's orientation.
        Vector3 move = (transform.right * moveX + transform.forward * moveZ) * speed;

        // Check if the character is grounded.
        if (characterController.isGrounded)
        {
            // When grounded, reset vertical velocity.
            verticalVelocity = 0f;
            // Jump if the Space key is pressed.
            if (Input.GetKeyDown(KeyCode.Space))
            {
                verticalVelocity = jumpForce;
            }
        }
        else
        {
            // Apply gravity over time when not grounded.
            verticalVelocity += gravity * Time.deltaTime;
        }

        // Combine horizontal movement with vertical velocity.
        Vector3 velocity = move + Vector3.up * verticalVelocity;
        characterController.Move(velocity * Time.deltaTime);
    }
}
