using UnityEngine;
using System.Collections;

public enum GunStyles { nonautomatic, automatic }

public class GunScript : MonoBehaviour
{
    // Gun style setting: nonautomatic fires once per click, automatic fires continuously
    [Tooltip("Selects type of weapon: nonautomatic fires one bullet per click, automatic fires repeatedly.")]
    public GunStyles currentStyle;

    // References (cached in Awake)
    [HideInInspector]
    public MouseLookScript mls;
    private Transform player;
    private Camera cameraComponent;
    private PlayerMovementScript pmS;
    [HideInInspector]
    public Transform mainCamera;
    private Camera secondCamera;
    private TextMesh HUD_bullets;

    // Player movement speeds that affect gun behavior
    [Header("Player Movement Properties")]
    [Tooltip("Walking speed (affects gun weight and movement)")]
    public int walkingSpeed = 3;
    [Tooltip("Running speed (affects gun weight and movement)")]
    public int runningSpeed = 5;

    // Ammo settings
    [Header("Bullet Properties")]
    [Tooltip("Total bullets available for the weapon.")]
    public float bulletsIHave = 20;
    [Tooltip("Bullets currently loaded in the gun.")]
    public float bulletsInTheGun = 5;
    [Tooltip("Maximum bullets per magazine.")]
    public float amountOfBulletsPerLoad = 5;

    // Gun positioning (resting and aiming positions)
    [Header("Gun Positioning")]
    [Tooltip("Gun position when not aiming.")]
    public Vector3 restPlacePosition;
    [Tooltip("Gun position when aiming.")]
    public Vector3 aimPlacePosition;
    [Tooltip("Time for gun to transition between positions.")]
    public float gunAimTime = 0.1f;
    [HideInInspector]
    public Vector3 currentGunPosition;

    // Internal state variables
    [HideInInspector]
    public bool reloading;
    [HideInInspector]
    public bool meeleAttack;
    [HideInInspector]
    public bool aiming;
    private Vector3 gunPosVelocity;
    private float cameraZoomVelocity;
    private float secondCameraZoomVelocity;
    private float waitTillNextFire = 0f;

    // Sensitivity for camera adjustment
    [Header("Gun Sensitivity")]
    [Tooltip("Sensitivity when not aiming.")]
    public float mouseSensitvity_notAiming = 10;
    [Tooltip("Sensitivity when aiming.")]
    public float mouseSensitvity_aiming = 5;
    [Tooltip("Sensitivity when running.")]
    public float mouseSensitvity_running = 4;
    private float startLook, startAim, startRun;

    // Crosshair drawing properties
    [Header("Crosshair Properties")]
    public Texture horizontal_crosshair, vertical_crosshair;
    public Vector2 top_pos_crosshair, bottom_pos_crosshair, left_pos_crosshair, right_pos_crosshair;
    public Vector2 size_crosshair_vertical = new Vector2(1, 1), size_crosshair_horizontal = new Vector2(1, 1);
    [HideInInspector]
    public Vector2 expandValues_crosshair;
    private float fadeout_value = 1;

    // Animator for gun and hands animations
    public Animator handsAnimator;
    [Header("Animation Names")]
    public string reloadAnimationName = "Player_Reload";
    public string aimingAnimationName = "Player_AImpose";
    public string meeleAnimationName = "Character_Malee";

    // Recoil and rotation parameters
    [Header("Recoil and Rotation")]
    public float recoilAmount_z_non = 0.5f;
    public float recoilAmount_x_non = 0.5f;
    public float recoilAmount_y_non = 0.5f;
    public float recoilAmount_z_ = 0.5f;
    public float recoilAmount_x_ = 0.5f;
    public float recoilAmount_y_ = 0.5f;
    [HideInInspector] public float recoilAmount_z, recoilAmount_x, recoilAmount_y;
    public float recoilOverTime_z = 0.5f;
    public float recoilOverTime_x = 0.5f;
    public float recoilOverTime_y = 0.5f;
    private float currentRecoilZPos, currentRecoilXPos, currentRecoilYPos;
    private float velocity_x_recoil, velocity_y_recoil, velocity_z_recoil;

    [Header("Gun Precision and FOV")]
    [Tooltip("Gun precision (affects recoil impact) when not aiming.")]
    public float gunPrecision_notAiming = 200.0f;
    [Tooltip("Gun precision when aiming.")]
    public float gunPrecision_aiming = 100.0f;
    public float cameraZoomRatio_notAiming = 60;
    public float cameraZoomRatio_aiming = 40;
    public float secondCameraZoomRatio_notAiming = 60;
    public float secondCameraZoomRatio_aiming = 40;
    [HideInInspector]
    public float gunPrecision;

    // Audio sources and effects
    [Header("Audio")]
    [Tooltip("Sound played when shooting.")]
    public AudioSource shoot_sound_source;
    [Tooltip("Sound played during reload.")]
    public AudioSource reloadSound_source;
    [Tooltip("Sound played on bullet hit.")]
    public static AudioSource hitMarker;

    // Muzzle flash and bullet spawning
    [Header("Muzzle and Bullet")]
    [Tooltip("Bullet prefab to be fired.")]
    public GameObject bullet;
    [Tooltip("Bullet spawn point.")]
    public GameObject bulletSpawnPlace;
    [Tooltip("Array of muzzle flash prefabs; one will be chosen at random.")]
    public GameObject[] muzzelFlash;
    [Tooltip("Transform for muzzle flash spawn.")]
    public GameObject muzzelSpawn;
    private GameObject holdFlash;

    // Rotation helper variables
    private float rotationLastY, rotationLastX;
    private float rotationDeltaY, rotationDeltaX;
    private float angularVelocityY, angularVelocityX;
    private Vector2 velocityGunRotate;
    [Tooltip("Time for the gun to lag behind the camera.")]
    public float rotationLagTime = 0f;
    [Tooltip("Forward rotation multiplier.")]
    public Vector2 forwardRotationAmount = Vector2.one;
    private float gunWeightX, gunWeightY;

    void Awake()
    {
        // Cache frequently used components
        mls = GameObject.FindGameObjectWithTag("Player").GetComponent<MouseLookScript>();
        player = mls.transform;
        mainCamera = mls.myCamera;
        secondCamera = GameObject.FindGameObjectWithTag("SecondCamera").GetComponent<Camera>();
        cameraComponent = mainCamera.GetComponent<Camera>();
        pmS = player.GetComponent<PlayerMovementScript>();

        bulletSpawnPlace = GameObject.FindGameObjectWithTag("BulletSpawn");

        // Attempt to find the hit marker sound in a child named "hitMarkerSound"
        Transform hm = transform.Find("hitMarkerSound");
        if (hm != null)
            hitMarker = hm.GetComponent<AudioSource>();
        else
            Debug.LogWarning("hitMarkerSound child not found!");

        // Cache starting sensitivity values
        startLook = mouseSensitvity_notAiming;
        startAim = mouseSensitvity_aiming;
        startRun = mouseSensitvity_running;

        // Initialize rotation tracking from MouseLookScript
        rotationLastY = mls.currentYRotation;
        rotationLastX = mls.currentCameraXRotation;

        // Set initial gun position to rest position
        currentGunPosition = restPlacePosition;
    }

    void Update()
    {
        Animations();
        GiveCameraScriptMySensitvity();
        PositionGun();
        Shooting();
        MeeleAttack();
        LockCameraWhileMelee();
        Sprint();
        CrossHairExpansionWhenWalking();
    }

    void FixedUpdate()
    {
        RotationGun();
        MeeleAnimationsStates();

        // Adjust gun and camera FOV based on aiming state
        if (Input.GetAxis("Fire2") != 0 && !reloading && !meeleAttack)
        {
            gunPrecision = gunPrecision_aiming;
            recoilAmount_x = recoilAmount_x_;
            recoilAmount_y = recoilAmount_y_;
            recoilAmount_z = recoilAmount_z_;
            currentGunPosition = Vector3.SmoothDamp(currentGunPosition, aimPlacePosition, ref gunPosVelocity, gunAimTime);
            cameraComponent.fieldOfView = Mathf.SmoothDamp(cameraComponent.fieldOfView, cameraZoomRatio_aiming, ref cameraZoomVelocity, gunAimTime);
            secondCamera.fieldOfView = Mathf.SmoothDamp(secondCamera.fieldOfView, secondCameraZoomRatio_aiming, ref secondCameraZoomVelocity, gunAimTime);
        }
        else
        {
            gunPrecision = gunPrecision_notAiming;
            recoilAmount_x = recoilAmount_x_non;
            recoilAmount_y = recoilAmount_y_non;
            recoilAmount_z = recoilAmount_z_non;
            currentGunPosition = Vector3.SmoothDamp(currentGunPosition, restPlacePosition, ref gunPosVelocity, gunAimTime);
            cameraComponent.fieldOfView = Mathf.SmoothDamp(cameraComponent.fieldOfView, cameraZoomRatio_notAiming, ref cameraZoomVelocity, gunAimTime);
            secondCamera.fieldOfView = Mathf.SmoothDamp(secondCamera.fieldOfView, secondCameraZoomRatio_notAiming, ref secondCameraZoomVelocity, gunAimTime);
        }
    }

    // Pass gun-specific sensitivity settings to the MouseLookScript
    void GiveCameraScriptMySensitvity()
    {
        mls.mouseSensitvity_notAiming = mouseSensitvity_notAiming;
        mls.mouseSensitvity_aiming = mouseSensitvity_aiming;
        mls.mouseSensitvity_running = mouseSensitvity_running;
    }

    // Adjust crosshair expansion based on player velocity and shooting status
    void CrossHairExpansionWhenWalking()
    {
        if (player.GetComponent<Rigidbody>().linearVelocity.magnitude > 1 && Input.GetAxis("Fire1") == 0)
        {
            expandValues_crosshair += new Vector2(20, 40) * Time.deltaTime;
            if (pmS.maxSpeed < runningSpeed)
            {
                expandValues_crosshair.x = Mathf.Clamp(expandValues_crosshair.x, 0, 10);
                expandValues_crosshair.y = Mathf.Clamp(expandValues_crosshair.y, 0, 20);
                fadeout_value = Mathf.Lerp(fadeout_value, 1, Time.deltaTime * 2);
            }
            else
            {
                fadeout_value = Mathf.Lerp(fadeout_value, 0, Time.deltaTime * 10);
                expandValues_crosshair.x = Mathf.Clamp(expandValues_crosshair.x, 0, 20);
                expandValues_crosshair.y = Mathf.Clamp(expandValues_crosshair.y, 0, 40);
            }
        }
        else
        {
            expandValues_crosshair = Vector2.Lerp(expandValues_crosshair, Vector2.zero, Time.deltaTime * 5);
            expandValues_crosshair.x = Mathf.Clamp(expandValues_crosshair.x, 0, 10);
            expandValues_crosshair.y = Mathf.Clamp(expandValues_crosshair.y, 0, 20);
            fadeout_value = Mathf.Lerp(fadeout_value, 1, Time.deltaTime * 2);
        }
    }

    // Toggle sprinting: press Left Shift to switch between walking and running speeds
    void Sprint()
    {
        if (Input.GetAxis("Vertical") > 0 && Input.GetAxisRaw("Fire2") == 0 && !meeleAttack && Input.GetAxisRaw("Fire1") == 0)
        {
            if (Input.GetKeyDown(KeyCode.LeftShift))
            {
                pmS.maxSpeed = (pmS.maxSpeed == walkingSpeed) ? runningSpeed : walkingSpeed;
            }
        }
        else
        {
            pmS.maxSpeed = walkingSpeed;
        }
    }

    // Check current melee and aiming states via animator
    void MeeleAnimationsStates()
    {
        if (handsAnimator)
        {
            meeleAttack = handsAnimator.GetCurrentAnimatorStateInfo(0).IsName(meeleAnimationName);
            aiming = handsAnimator.GetCurrentAnimatorStateInfo(0).IsName(aimingAnimationName);
        }
    }

    // Trigger melee attack if Q is pressed and not already in a melee attack
    void MeeleAttack()
    {
        if (Input.GetKeyDown(KeyCode.Q) && !meeleAttack)
        {
            StartCoroutine(AnimationMeeleAttack());
        }
    }

    IEnumerator AnimationMeeleAttack()
    {
        if (handsAnimator)
        {
            handsAnimator.SetBool("meeleAttack", true);
            yield return new WaitForSeconds(0.1f);
            handsAnimator.SetBool("meeleAttack", false);
        }
    }

    // While melee attack is active, reduce camera sensitivity; otherwise, restore defaults
    void LockCameraWhileMelee()
    {
        if (meeleAttack)
        {
            mouseSensitvity_notAiming = 2;
            mouseSensitvity_aiming = 1.6f;
            mouseSensitvity_running = 1;
        }
        else
        {
            mouseSensitvity_notAiming = startLook;
            mouseSensitvity_aiming = startAim;
            mouseSensitvity_running = startRun;
        }
    }

    // Update the gun’s position based on the camera position and recoil offsets
    void PositionGun()
    {
        Vector3 targetPosition = mainCamera.transform.position
            - (mainCamera.transform.right * (currentGunPosition.x + currentRecoilXPos))
            + (mainCamera.transform.up * (currentGunPosition.y + currentRecoilYPos))
            + (mainCamera.transform.forward * (currentGunPosition.z + currentRecoilZPos));

        transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref gunPosVelocity, 0);

        // Update camera recoil offset in the player movement script
        pmS.cameraPosition = new Vector3(currentRecoilXPos, currentRecoilYPos, 0);

        // Smoothly reduce recoil over time
        currentRecoilZPos = Mathf.SmoothDamp(currentRecoilZPos, 0, ref velocity_z_recoil, recoilOverTime_z);
        currentRecoilXPos = Mathf.SmoothDamp(currentRecoilXPos, 0, ref velocity_x_recoil, recoilOverTime_x);
        currentRecoilYPos = Mathf.SmoothDamp(currentRecoilYPos, 0, ref velocity_y_recoil, recoilOverTime_y);
    }

    // Rotate the gun based on the camera’s rotation changes (simulating weapon weight)
    void RotationGun()
    {
        rotationDeltaY = mls.currentYRotation - rotationLastY;
        rotationDeltaX = mls.currentCameraXRotation - rotationLastX;

        rotationLastY = mls.currentYRotation;
        rotationLastX = mls.currentCameraXRotation;

        angularVelocityY = Mathf.Lerp(angularVelocityY, rotationDeltaY, Time.deltaTime * 5);
        angularVelocityX = Mathf.Lerp(angularVelocityX, rotationDeltaX, Time.deltaTime * 5);

        gunWeightX = Mathf.SmoothDamp(gunWeightX, mls.currentCameraXRotation, ref velocityGunRotate.x, rotationLagTime);
        gunWeightY = Mathf.SmoothDamp(gunWeightY, mls.currentYRotation, ref velocityGunRotate.y, rotationLagTime);

        transform.rotation = Quaternion.Euler(gunWeightX + (angularVelocityX * forwardRotationAmount.x),
                                              gunWeightY + (angularVelocityY * forwardRotationAmount.y),
                                              0);
    }

    // Recoil calculation: adjusts recoil offsets and influences camera rotation
    void RecoilMath()
    {
        currentRecoilZPos -= recoilAmount_z;
        currentRecoilXPos -= (Random.value - 0.5f) * recoilAmount_x;
        currentRecoilYPos -= (Random.value - 0.5f) * recoilAmount_y;

        mls.wantedCameraXRotation -= Mathf.Abs(currentRecoilYPos * gunPrecision);
        mls.wantedYRotation -= (currentRecoilXPos * gunPrecision);

        expandValues_crosshair += new Vector2(6, 12);
    }

    // Handle shooting input based on gun style (automatic vs nonautomatic)
    void Shooting()
    {
        if (!meeleAttack)
        {
            if (currentStyle == GunStyles.nonautomatic && Input.GetButtonDown("Fire1"))
            {
                ShootMethod();
            }
            else if (currentStyle == GunStyles.automatic && Input.GetButton("Fire1"))
            {
                ShootMethod();
            }
        }
        waitTillNextFire -= roundsPerSecond * Time.deltaTime;
    }

    // Rounds per second (set externally)
    public float roundsPerSecond;

    // Instantiate bullet, spawn muzzle flash, play sounds, apply recoil and update ammo
    void ShootMethod()
    {
        if (waitTillNextFire <= 0 && !reloading && pmS.maxSpeed < 5)
        {
            if (bulletsInTheGun > 0)
            {
                int randomIndex = Random.Range(0, muzzelFlash.Length);
                if (bullet != null)
                    Instantiate(bullet, bulletSpawnPlace.transform.position, bulletSpawnPlace.transform.rotation);
                else
                    Debug.Log("Missing the bullet prefab");

                holdFlash = Instantiate(muzzelFlash[randomIndex], muzzelSpawn.transform.position,
                                        muzzelSpawn.transform.rotation * Quaternion.Euler(0, 0, 90));
                holdFlash.transform.parent = muzzelSpawn.transform;

                if (shoot_sound_source)
                    shoot_sound_source.Play();
                else
                    Debug.Log("Missing Shoot Sound Source.");

                RecoilMath();
                waitTillNextFire = 1;
                bulletsInTheGun -= 1;
            }
            else
            {
                StartCoroutine(Reload_Animation());
            }
        }
    }

    // Coroutine to handle reload animation and ammo refill logic
    [Header("Reload Settings")]
    [Tooltip("Time delay after reload animation before bullets are refilled.")]
    public float reloadChangeBulletsTime;
    IEnumerator Reload_Animation()
    {
        if (bulletsIHave > 0 && bulletsInTheGun < amountOfBulletsPerLoad && !reloading)
        {
            if (reloadSound_source != null && !reloadSound_source.isPlaying)
                reloadSound_source.Play();
            else
                Debug.Log("Missing Reload Sound Source.");

            if (handsAnimator)
                handsAnimator.SetBool("reloading", true);
            yield return new WaitForSeconds(0.5f);
            if (handsAnimator)
                handsAnimator.SetBool("reloading", false);

            yield return new WaitForSeconds(reloadChangeBulletsTime - 0.5f);
            if (!meeleAttack && pmS.maxSpeed != runningSpeed)
            {
                if (player.GetComponent<PlayerMovementScript>()._freakingZombiesSound)
                    player.GetComponent<PlayerMovementScript>()._freakingZombiesSound.Play();
                else
                    Debug.Log("Missing Freaking Zombies Sound");

                float needed = amountOfBulletsPerLoad - bulletsInTheGun;
                if (bulletsIHave >= needed)
                {
                    bulletsIHave -= needed;
                    bulletsInTheGun = amountOfBulletsPerLoad;
                }
                else
                {
                    bulletsInTheGun += bulletsIHave;
                    bulletsIHave = 0;
                }
            }
            else
            {
                reloadSound_source.Stop();
                Debug.Log("Reload interrupted via melee attack");
            }
        }
    }

    // OnGUI displays ammo count and draws the crosshair
    void OnGUI()
    {
        if (HUD_bullets == null)
        {
            try
            {
                HUD_bullets = GameObject.Find("HUD_bullets").GetComponent<TextMesh>();
            }
            catch (System.Exception ex)
            {
                Debug.Log("Couldn't find HUD_bullets: " + ex.StackTrace);
            }
        }
        if (mls != null && HUD_bullets != null)
            HUD_bullets.text = bulletsIHave.ToString() + " - " + bulletsInTheGun.ToString();

        DrawCrosshair();
    }

    // Draw crosshair using GUI.DrawTexture and helper methods for positioning
    void DrawCrosshair()
    {
        GUI.color = new Color(GUI.color.r, GUI.color.g, GUI.color.b, fadeout_value);
        if (Input.GetAxis("Fire2") == 0)
        {
            // Left crosshair
            GUI.DrawTexture(new Rect(vec2(left_pos_crosshair).x + position_x(-expandValues_crosshair.x) + Screen.width / 2,
                Screen.height / 2 + vec2(left_pos_crosshair).y,
                vec2(size_crosshair_horizontal).x, vec2(size_crosshair_horizontal).y), vertical_crosshair);
            // Right crosshair
            GUI.DrawTexture(new Rect(vec2(right_pos_crosshair).x + position_x(expandValues_crosshair.x) + Screen.width / 2,
                Screen.height / 2 + vec2(right_pos_crosshair).y,
                vec2(size_crosshair_horizontal).x, vec2(size_crosshair_horizontal).y), vertical_crosshair);
            // Top crosshair
            GUI.DrawTexture(new Rect(vec2(top_pos_crosshair).x + Screen.width / 2,
                Screen.height / 2 + vec2(top_pos_crosshair).y + position_y(-expandValues_crosshair.y),
                vec2(size_crosshair_vertical).x, vec2(size_crosshair_vertical).y), horizontal_crosshair);
            // Bottom crosshair
            GUI.DrawTexture(new Rect(vec2(bottom_pos_crosshair).x + Screen.width / 2,
                Screen.height / 2 + vec2(bottom_pos_crosshair).y + position_y(expandValues_crosshair.y),
                vec2(size_crosshair_vertical).x, vec2(size_crosshair_vertical).y), horizontal_crosshair);
        }
    }

    // Helper methods for GUI positioning (percentage to pixel conversion)
    private float position_x(float var)
    {
        return Screen.width * var / 100;
    }
    private float position_y(float var)
    {
        return Screen.height * var / 100;
    }
    private Vector2 vec2(Vector2 _vec2)
    {
        return new Vector2(Screen.width * _vec2.x / 100, Screen.height * _vec2.y / 100);
    }

    // Update animation parameters and trigger reload on input
    void Animations()
    {
        if (handsAnimator)
        {
            reloading = handsAnimator.GetCurrentAnimatorStateInfo(0).IsName(reloadAnimationName);
            handsAnimator.SetFloat("walkSpeed", pmS.currentSpeed);
            handsAnimator.SetBool("aiming", Input.GetButton("Fire2"));
            handsAnimator.SetInteger("maxSpeed", pmS.maxSpeed);
            if (Input.GetKeyDown(KeyCode.R) && pmS.maxSpeed < 5 && !reloading && !meeleAttack)
            {
                StartCoroutine(Reload_Animation());
            }
        }
    }
}
