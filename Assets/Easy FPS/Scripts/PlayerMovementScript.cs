using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovementScript : MonoBehaviour
{
    // Components and Transforms
    private Rigidbody rb;
    [Tooltip("Current player's speed")]
    public float currentSpeed;
    [Tooltip("Assign player's camera here")]
    [HideInInspector] public Transform cameraMain;
    [Tooltip("Bullet spawn transform (child of camera)")]
    [HideInInspector] public Transform bulletSpawn;

    // Movement and sprint variables
    [Tooltip("Force applied for jumping")]
    public float jumpForce = 500f;
    [Tooltip("Normal maximum speed")]
    public int normalMaxSpeed = 5;
    [Tooltip("Maximum speed when sprinting (holding Shift)")]
    public int sprintMaxSpeed = 8;
    [Tooltip("Acceleration force when moving")]
    public float accelerationSpeed = 50000f;
    [Tooltip("Multiplier applied to acceleration when sprinting")]
    public float sprintMultiplier = 1.5f;
    [Tooltip("Deceleration factor when no movement input is given")]
    public float deaccelerationSpeed = 15.0f;

    // Mouse camera control variables
    [Tooltip("Mouse sensitivity for camera control")]
    public float mouseSensitivity = 100f;
    // This stores the accumulated vertical rotation (pitch) so that we can clamp it.
    private float verticalRotation = 0f;

    // Audio sources for various sounds
    [Tooltip("Sound played when player jumps")]
    public AudioSource _jumpSound;
    [Tooltip("Sound played when player is walking")]
    public AudioSource _walkSound;
    [Tooltip("Sound played when player is running")]
    public AudioSource _runSound;
    [Tooltip("Sound played when bullet hits target")]
    public AudioSource _hitSound;
    [Tooltip("Sound played when reloading weapon or related actions")]
    public AudioSource _freakingZombiesSound;

    // Internal variables for movement smoothing
    private Vector3 slowdownV;
    private Vector2 horizontalMovement;
    // Ground check flag
    public bool grounded;

    // Variables for melee and shooting (kept intact from your original script)
    private float meleeAttack_cooldown;
    private string currentWeapo;
    private LayerMask ignoreLayer;
    private float rayDetectorMeeleSpace = 0.15f;
    private float offsetStart = 0.05f;
    // Melee attack flag to prevent repeat calls
    public bool been_to_meele_anim = false;
    // Blood effect prefab for melee hit feedback
    [Tooltip("Blood effect prefab for melee attacks")]
    public GameObject bloodEffect;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        // Assumes the main camera is a child named "Main Camera"
        cameraMain = transform.Find("Main Camera").transform;
        // Assumes a child of the camera called "BulletSpawn" exists for raycasting melee/shooting hits
        bulletSpawn = cameraMain.Find("BulletSpawn").transform;
        // Set the ignoreLayer to the "Player" layer so that raycasts ignore the player’s collider
        ignoreLayer = 1 << LayerMask.NameToLayer("Player");
    }

    void FixedUpdate()
    {
        RaycastForMeleeAttacks();
        PlayerMovementLogic();
    }

    void Update()
    {
        // Handle mouse input to control the camera
        HandleMouseCameraControl();
        // Handle jumping input
        Jumping();
        // Handle crouching (scales the player down/up)
        Crouching();
        // Manage footstep sounds based on movement state
        WalkingSound();
    }

    /// <summary>
    /// Processes movement input (WSAD) and applies forces.
    /// Sprinting is enabled when the left Shift key is held while grounded.
    /// </summary>
    void PlayerMovementLogic()
    {
        // Determine the effective maximum speed and acceleration based on sprint input.
        int currentMaxSpeed = normalMaxSpeed;
        float currentAcceleration = accelerationSpeed;
        if (Input.GetKey(KeyCode.LeftShift) && grounded)
        {
            currentMaxSpeed = sprintMaxSpeed;
            currentAcceleration = accelerationSpeed * sprintMultiplier;
        }

        // Calculate the current horizontal speed
        currentSpeed = rb.linearVelocity.magnitude;
        horizontalMovement = new Vector2(rb.linearVelocity.x, rb.linearVelocity.z);
        if (horizontalMovement.magnitude > currentMaxSpeed)
        {
            horizontalMovement = horizontalMovement.normalized * currentMaxSpeed;
        }
        rb.linearVelocity = new Vector3(horizontalMovement.x, rb.linearVelocity.y, horizontalMovement.y);

        // Smooth deceleration if no input is detected
        if (grounded)
        {
            rb.linearVelocity = Vector3.SmoothDamp(rb.linearVelocity, new Vector3(0, rb.linearVelocity.y, 0), ref slowdownV, deaccelerationSpeed * Time.deltaTime);
        }

        // Read input for movement
        float moveHorizontal = Input.GetAxis("Horizontal");
        float moveVertical = Input.GetAxis("Vertical");

        // Apply movement force (use half force when airborne)
        if (grounded)
        {
            rb.AddRelativeForce(moveHorizontal * currentAcceleration * Time.deltaTime, 0, moveVertical * currentAcceleration * Time.deltaTime);
        }
        else
        {
            rb.AddRelativeForce(moveHorizontal * currentAcceleration / 2 * Time.deltaTime, 0, moveVertical * currentAcceleration / 2 * Time.deltaTime);
        }

        // Adjust deceleration speed based on whether there is input or not
        if (moveHorizontal != 0 || moveVertical != 0)
        {
            deaccelerationSpeed = 0.5f;
        }
        else
        {
            deaccelerationSpeed = 0.1f;
        }
    }

    /// <summary>
    /// Handles mouse input for camera control.
    /// The player's transform rotates horizontally (yaw) and the camera rotates vertically (pitch) with clamping.
    /// </summary>
    void HandleMouseCameraControl()
    {
        // Get mouse input along the X and Y axes
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        // Rotate the player horizontally based on mouse X movement
        transform.Rotate(Vector3.up * mouseX);

        // Adjust vertical rotation for the camera pitch and clamp it between -90 and 90 degrees
        verticalRotation -= mouseY;
        verticalRotation = Mathf.Clamp(verticalRotation, -90f, 90f);

        // Apply the vertical rotation to the camera's local rotation so that only the pitch is affected
        cameraMain.localRotation = Quaternion.Euler(verticalRotation, 0, 0);
    }

    /// <summary>
    /// Processes jumping when the Space key is pressed and the player is grounded.
    /// </summary>
    void Jumping()
    {
        if (Input.GetKeyDown(KeyCode.Space) && grounded)
        {
            rb.AddRelativeForce(Vector3.up * jumpForce);
            if (_jumpSound)
                _jumpSound.Play();
            else
                Debug.Log("Missing jump sound.");
            if (_walkSound)
                _walkSound.Stop();
            if (_runSound)
                _runSound.Stop();
        }
    }

    /// <summary>
    /// Scales the player to simulate crouching when the 'C' key is held down.
    /// </summary>
    void Crouching()
    {
        if (Input.GetKey(KeyCode.C))
        {
            transform.localScale = Vector3.Lerp(transform.localScale, new Vector3(1, 0.6f, 1), Time.deltaTime * 15);
        }
        else
        {
            transform.localScale = Vector3.Lerp(transform.localScale, new Vector3(1, 1, 1), Time.deltaTime * 15);
        }
    }

    /// <summary>
    /// Plays walking or running sounds based on the player’s movement and speed.
    /// </summary>
    void WalkingSound()
    {
        if (_walkSound && _runSound)
        {
            if (RayCastGrounded())
            {
                if (currentSpeed > 1)
                {
                    // Here, we use the normalMaxSpeed value to decide which sound to play.
                    // (Customize these checks as needed based on your project’s logic.)
                    if (normalMaxSpeed == 3)
                    {
                        if (!_walkSound.isPlaying)
                        {
                            _walkSound.Play();
                            _runSound.Stop();
                        }
                    }
                    else if (normalMaxSpeed == 5)
                    {
                        if (!_runSound.isPlaying)
                        {
                            _walkSound.Stop();
                            _runSound.Play();
                        }
                    }
                }
                else
                {
                    _walkSound.Stop();
                    _runSound.Stop();
                }
            }
            else
            {
                _walkSound.Stop();
                _runSound.Stop();
            }
        }
        else
        {
            Debug.Log("Missing walk and running sounds.");
        }
    }

    /// <summary>
    /// Uses a downward raycast to determine if the player is grounded.
    /// </summary>
    private bool RayCastGrounded()
    {
        RaycastHit groundedInfo;
        if (Physics.Raycast(transform.position, -transform.up, out groundedInfo, 1, ~ignoreLayer))
        {
            Debug.DrawRay(transform.position, -transform.up, Color.red, 0.0f);
            return groundedInfo.transform != null;
        }
        return false;
    }

    /// <summary>
    /// When colliding, sets grounded to true if the contact angle is less than 60°.
    /// </summary>
    void OnCollisionStay(Collision other)
    {
        foreach (ContactPoint contact in other.contacts)
        {
            if (Vector3.Angle(contact.normal, Vector3.up) < 60)
            {
                grounded = true;
            }
        }
    }

    /// <summary>
    /// Resets the grounded flag when collision ends.
    /// </summary>
    void OnCollisionExit()
    {
        grounded = false;
    }

    /// <summary>
    /// Casts several rays in a spread pattern to detect melee attacks.
    /// </summary>
    private void RaycastForMeleeAttacks()
    {
        if (meleeAttack_cooldown > -5)
        {
            meleeAttack_cooldown -= 1 * Time.deltaTime;
        }

        // Check for a gun component (example logic from your original script)
        GunInventory gunInv = GetComponent<GunInventory>();
        if (gunInv && gunInv.currentGun)
        {
            if (gunInv.currentGun.GetComponent<GunScript>())
                currentWeapo = "gun";
        }

        // Create rays in multiple directions from the bullet spawn position for extended melee hit detection
        Ray ray1 = new Ray(bulletSpawn.position + (bulletSpawn.right * offsetStart), bulletSpawn.forward + (bulletSpawn.right * rayDetectorMeeleSpace));
        Ray ray2 = new Ray(bulletSpawn.position - (bulletSpawn.right * offsetStart), bulletSpawn.forward - (bulletSpawn.right * rayDetectorMeeleSpace));
        Ray ray3 = new Ray(bulletSpawn.position, bulletSpawn.forward);
        Ray ray4 = new Ray(bulletSpawn.position + (bulletSpawn.right * offsetStart) + (bulletSpawn.up * offsetStart), bulletSpawn.forward + (bulletSpawn.right * rayDetectorMeeleSpace) + (bulletSpawn.up * rayDetectorMeeleSpace));
        Ray ray5 = new Ray(bulletSpawn.position - (bulletSpawn.right * offsetStart) + (bulletSpawn.up * offsetStart), bulletSpawn.forward - (bulletSpawn.right * rayDetectorMeeleSpace) + (bulletSpawn.up * rayDetectorMeeleSpace));
        Ray ray6 = new Ray(bulletSpawn.position + (bulletSpawn.up * offsetStart), bulletSpawn.forward + (bulletSpawn.up * rayDetectorMeeleSpace));
        Ray ray7 = new Ray(bulletSpawn.position + (bulletSpawn.right * offsetStart) - (bulletSpawn.up * offsetStart), bulletSpawn.forward + (bulletSpawn.right * rayDetectorMeeleSpace) - (bulletSpawn.up * rayDetectorMeeleSpace));
        Ray ray8 = new Ray(bulletSpawn.position - (bulletSpawn.right * offsetStart) - (bulletSpawn.up * offsetStart), bulletSpawn.forward - (bulletSpawn.right * rayDetectorMeeleSpace) - (bulletSpawn.up * rayDetectorMeeleSpace));
        Ray ray9 = new Ray(bulletSpawn.position - (bulletSpawn.up * offsetStart), bulletSpawn.forward - (bulletSpawn.up * rayDetectorMeeleSpace));

        // Draw debug rays to visualize the spread
        Debug.DrawRay(ray1.origin, ray1.direction, Color.cyan);
        Debug.DrawRay(ray2.origin, ray2.direction, Color.cyan);
        Debug.DrawRay(ray3.origin, ray3.direction, Color.cyan);
        Debug.DrawRay(ray4.origin, ray4.direction, Color.red);
        Debug.DrawRay(ray5.origin, ray5.direction, Color.red);
        Debug.DrawRay(ray6.origin, ray6.direction, Color.red);
        Debug.DrawRay(ray7.origin, ray7.direction, Color.yellow);
        Debug.DrawRay(ray8.origin, ray8.direction, Color.yellow);
        Debug.DrawRay(ray9.origin, ray9.direction, Color.yellow);

        // Check if the current gun is performing a melee attack and, if so, start the melee attack coroutine.
        if (gunInv && gunInv.currentGun)
        {
            GunScript gunScript = gunInv.currentGun.GetComponent<GunScript>();
            if (gunScript != null)
            {
                if (!gunScript.meeleAttack)
                {
                    been_to_meele_anim = false;
                }
                if (gunScript.meeleAttack && !been_to_meele_anim)
                {
                    been_to_meele_anim = true;
                    StartCoroutine(MeeleAttackWeaponHit());
                }
            }
        }
    }

    /// <summary>
    /// Coroutine that performs a melee attack by checking multiple raycasts and applying effects if a hit is detected.
    /// </summary>
    IEnumerator MeeleAttackWeaponHit()
    {
        if (Physics.Raycast(bulletSpawn.position + (bulletSpawn.right * offsetStart), bulletSpawn.forward + (bulletSpawn.right * rayDetectorMeeleSpace), out RaycastHit hitInfo, 2f, ~ignoreLayer) ||
            Physics.Raycast(bulletSpawn.position - (bulletSpawn.right * offsetStart), bulletSpawn.forward - (bulletSpawn.right * rayDetectorMeeleSpace), out hitInfo, 2f, ~ignoreLayer) ||
            Physics.Raycast(bulletSpawn.position, bulletSpawn.forward, out hitInfo, 2f, ~ignoreLayer) ||
            Physics.Raycast(bulletSpawn.position + (bulletSpawn.right * offsetStart) + (bulletSpawn.up * offsetStart), bulletSpawn.forward + (bulletSpawn.right * rayDetectorMeeleSpace) + (bulletSpawn.up * rayDetectorMeeleSpace), out hitInfo, 2f, ~ignoreLayer) ||
            Physics.Raycast(bulletSpawn.position - (bulletSpawn.right * offsetStart) + (bulletSpawn.up * offsetStart), bulletSpawn.forward - (bulletSpawn.right * rayDetectorMeeleSpace) + (bulletSpawn.up * rayDetectorMeeleSpace), out hitInfo, 2f, ~ignoreLayer) ||
            Physics.Raycast(bulletSpawn.position + (bulletSpawn.up * offsetStart), bulletSpawn.forward + (bulletSpawn.up * rayDetectorMeeleSpace), out hitInfo, 2f, ~ignoreLayer) ||
            Physics.Raycast(bulletSpawn.position + (bulletSpawn.right * offsetStart) - (bulletSpawn.up * offsetStart), bulletSpawn.forward + (bulletSpawn.right * rayDetectorMeeleSpace) - (bulletSpawn.up * rayDetectorMeeleSpace), out hitInfo, 2f, ~ignoreLayer) ||
            Physics.Raycast(bulletSpawn.position - (bulletSpawn.right * offsetStart) - (bulletSpawn.up * offsetStart), bulletSpawn.forward - (bulletSpawn.right * rayDetectorMeeleSpace) - (bulletSpawn.up * rayDetectorMeeleSpace), out hitInfo, 2f, ~ignoreLayer) ||
            Physics.Raycast(bulletSpawn.position - (bulletSpawn.up * offsetStart), bulletSpawn.forward - (bulletSpawn.up * rayDetectorMeeleSpace), out hitInfo, 2f, ~ignoreLayer))
        {
            if (hitInfo.transform.CompareTag("Dummie"))
            {
                Transform _other = hitInfo.transform.root;
                if (_other.CompareTag("Dummie"))
                {
                    Debug.Log("Hit a dummie");
                }
                InstantiateBlood(hitInfo, false);
            }
        }
        yield return new WaitForEndOfFrame();
    }

    /// <summary>
    /// Instantiates a blood effect at the point of impact if the current weapon is a gun.
    /// </summary>
    /// <param name="_hitPos">The raycast hit information.</param>
    /// <param name="swordHitWithGunOrNot">Flag for differentiating melee types.</param>
    void InstantiateBlood(RaycastHit _hitPos, bool swordHitWithGunOrNot)
    {
        if (currentWeapo == "gun")
        {
            GunScript.HitMarkerSound();
            if (_hitSound)
                _hitSound.Play();
            else
                Debug.Log("Missing hit sound");

            if (!swordHitWithGunOrNot)
            {
                if (bloodEffect)
                    Instantiate(bloodEffect, _hitPos.point, Quaternion.identity);
                else
                    Debug.Log("Missing blood effect prefab in the inspector.");
            }
        }
    }
}
