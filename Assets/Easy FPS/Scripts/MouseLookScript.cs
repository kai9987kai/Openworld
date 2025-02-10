using UnityEngine;

public class SimpleFPSController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float walkSpeed = 5f;           // Base walking speed
    public float sprintMultiplier = 2f;    // Sprint speed multiplier

    [Header("Mouse Look Settings")]
    public float mouseSensitivity = 2f;    // Mouse sensitivity factor
    public float verticalLookLimit = 80f;  // Maximum angle for vertical rotation

    private CharacterController characterController;
    private Transform cameraTransform;
    private float verticalRotation = 0f;

    void Start()
    {
        // Get the CharacterController attached to the player
        characterController = GetComponent<CharacterController>();
        if (characterController == null)
        {
            Debug.LogError("No CharacterController found on this GameObject.");
        }
        // Get the main camera (assumed to be a child of this GameObject)
        cameraTransform = Camera.main.transform;
        if (cameraTransform.parent != transform)
        {
            Debug.LogWarning("The Main Camera should be a child of the player object for proper rotation.");
        }
        // Lock and hide the cursor for an immersive FPS experience
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        HandleMouseLook();
        HandleMovement();
    }

    // Handles vertical camera rotation and horizontal player rotation.
    void HandleMouseLook()
    {
        // Get mouse input for horizontal and vertical movement.
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        // Adjust and clamp vertical rotation.
        verticalRotation -= mouseY;
        verticalRotation = Mathf.Clamp(verticalRotation, -verticalLookLimit, verticalLookLimit);
        // Apply the vertical rotation to the camera only.
        cameraTransform.localRotation = Quaternion.Euler(verticalRotation, 0f, 0f);

        // Apply horizontal rotation to the player.
        transform.Rotate(0f, mouseX, 0f);
    }

    // Handles WASD movement and sprinting.
    void HandleMovement()
    {
        float moveX = Input.GetAxis("Horizontal"); // A/D or left/right arrow keys
        float moveZ = Input.GetAxis("Vertical");   // W/S or up/down arrow keys

        // If LeftShift is held, sprint by multiplying the walkSpeed.
        bool sprint = Input.GetKey(KeyCode.LeftShift);
        float speed = sprint ? walkSpeed * sprintMultiplier : walkSpeed;

        // Calculate the movement vector relative to the player's orientation.
        Vector3 move = transform.right * moveX + transform.forward * moveZ;

        // Move the player using the CharacterController component.
        characterController.Move(move * speed * Time.deltaTime);

        // Optionally, apply gravity if the player is not grounded.
        if (!characterController.isGrounded)
        {
            characterController.Move(Physics.gravity * Time.deltaTime);
        }
    }
}
