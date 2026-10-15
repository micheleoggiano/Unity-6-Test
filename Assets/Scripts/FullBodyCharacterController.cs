using JetBrains.Annotations;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class FullBodyCharacterController : MonoBehaviour
{
                                            // "Header" to create the header in the inspector
    [Header("References")]                 // Basic references of the character controller
    public Transform cameraTransform;
    public Transform meshRoot;
    public Animator animator;

    [Header("Movement")]                     // Movement management.
    public float walkSpeed = 3f;
    public float runSpeed = 6f;
    public float crouchSpeed = 1.8f;
    public float acceleration = 15f;             // To not instantly reach the maximum speed
    public float gravity = -20f;                // To simulate physics
    public float jumpHeight = 1.6f;

    [Header("Character Controller")]            // To resize character's collider and camera height during crouch
    public float standingHeight = 1.8f;
    public float crouchHeight = 1.0f;
    public Vector3 controllerCenterStanding = new Vector3(0, 0.9f, 0);
    public Vector3 controllerCenterCrouch = new Vector3(0, 0.5f, 0);

    [Header("Mouse Look")]                      // Separating the head/camera vertical rotation and the body horizontal rotation
    public float mouseSensitivity = 2.0f;
    public float clampAngle = 89f;
    float pitch = 0f;

    [Header("Head Bob")]                        // Head bob runs while grounded/moving, resets during idle ora in air
    public bool enableHeadBob = true;
    public float bobFrequency = 10f;
    public float bobHeight = 0.04f;
    Vector3 cameraInitialLocalPos;
    float bobTimer = 0f;

    [Header("Foot IK")]                         // Raycasts down from each foot and aligns with the ground normal, smoothing position and rotation over time.
    public bool enableFootIK = true;
    public float footIKRayDistance = 1.2f;
    public LayerMask groundMask = ~0;
    public float footIKWeight = 1.0f;
    public float footIKSmoothTime = 0.08f;

    //Internal                                  
    CharacterController controller;
    Vector3 velocity;
    float currentSpeed;
    bool isCrouched = false;
    bool grounded;
    public bool isSprinting;

    //IK smoothing
    float leftFootWeight = 0f;
    float rightFootWeight = 0f;
    Vector3 leftFootPos;
    Vector3 rightFootPos;
    Quaternion leftFootRot;
    Quaternion rightFootRot;
    Vector3 leftFootVel, rightFootVel;

    public Transform headBone;
    Quaternion baseHeadRotation;
    Quaternion baseSpineRotation;
    public Transform spineBone;
    public float spineRotationWeight = 0.4f;


    int hashJumpTrigger;

    // Awake is called during the script loading
    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (cameraTransform == null) cameraTransform = Camera.main.transform;
        cameraInitialLocalPos = cameraTransform.localPosition;
    }


    // Start is called before the first frame update
    void Start()
    {                                           
        currentSpeed = walkSpeed;
        controller.height = standingHeight;
        controller.center = controllerCenterStanding;
        if (meshRoot == null) meshRoot = transform;
        baseHeadRotation = headBone.localRotation;
    }

    // Update is called once per frame
    void Update()
    {
        HandleMouseLook();                  // Rotates camera/player
        HandleMoveInput();                  // Reads input, moves CharacterController
        HandleCrouchAndHeight();            // Toggles crouch and adjusts collider
        HandleHeadBob();                    // Adds subtle motion
        UpdateAnimatorParameters();         // Updates animation values
    }


    // Bone Corrections
    private void LateUpdate()
    {
        if (headBone == null) return;

        Quaternion rot = Quaternion.Euler(pitch, 0f, 0f);
        headBone.localRotation = rot * baseHeadRotation;
        Quaternion spineRot = Quaternion.Euler(pitch * spineRotationWeight, 0f, 0f);
        spineBone.localRotation = spineRot * baseSpineRotation;
    }
}
