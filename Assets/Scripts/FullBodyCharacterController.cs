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

    void HandleMouseLook()
    {
        float mx = Input.GetAxis("Mouse X") * mouseSensitivity;
        float my = Input.GetAxis("Mouse Y") * mouseSensitivity;
        transform.Rotate(Vector3.up, mx);
        pitch -= my;
        pitch = Mathf.Clamp(pitch, -clampAngle, clampAngle);
        cameraTransform.localEulerAngles = new Vector3(pitch, 0f, 0f);
    }

    void HandleMoveInput ()
    {
        Vector2 moveAxis = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));
        Vector3 forward = Quaternion.Euler(0, transform.eulerAngles.y, 0) * Vector3.forward;
        Vector3 right = Quaternion.Euler(0, transform.eulerAngles.y, 0) * Vector3.right;
        Vector3 inputDir = (forward * moveAxis.y + right * moveAxis.x);
        inputDir = Vector3.ClampMagnitude(inputDir, 1f);

        bool sprint = Input.GetKey(KeyCode.LeftShift) && !isCrouched;
        isSprinting = sprint;
        float desiredSpeed = sprint ? runSpeed : (isCrouched ? crouchSpeed :walkSpeed);
        currentSpeed = Mathf.MoveTowards(currentSpeed, desiredSpeed, acceleration * Time.deltaTime);

        Vector3 horizontalVel = inputDir * currentSpeed;

        if (controller.isGrounded)
        {
            if (!grounded) { grounded = true; }
            velocity.y = -1f;
            if (Input.GetButtonDown("Jump") && !isCrouched)
            {
                velocity.y = Mathf.Sqrt(2f * jumpHeight * -gravity);
                animator.SetTrigger("Jump");
                grounded = false;
            }
        }
        else
        {
            velocity.y += gravity * Time.deltaTime;
        }

        Vector3 move = horizontalVel + Vector3.up * velocity.y;
        controller.Move(move * Time.deltaTime);
    }

    void HandleCrouchAndHeight()
    {
        if (Input.GetKeyDown(KeyCode.C))
        {
            isCrouched = !isCrouched;
        }

        float targetHeight = isCrouched ? crouchHeight : standingHeight;
        controller.height = Mathf.Lerp(controller.height, targetHeight, 10f * Time.deltaTime);
        controller.center = Vector3.Lerp(controller.center,
                                         isCrouched ? controllerCenterCrouch : controllerCenterStanding,
                                         10f * Time.deltaTime);

        Vector3 camTarget = cameraInitialLocalPos + new Vector3(0, isCrouched ? -0.4f : 0f, 0);
        cameraTransform.localPosition = Vector3.Lerp(cameraTransform.localPosition, camTarget, 8f * Time.deltaTime);
    }


    void HandleHeadBob()
    {
        if (!enableHeadBob) return;
        Vector3 horizontalVel = new Vector3(controller.velocity.x, 0f, controller.velocity.z);
        float speed = horizontalVel.magnitude;
        if (controller.isGrounded && speed > 0.1f)
        {
            bobTimer += Time.deltaTime * bobFrequency * (speed / runSpeed + 0.5f);
            float bobOffset = Mathf.Sin(bobTimer) * bobHeight;
            cameraTransform.localPosition = cameraInitialLocalPos + new Vector3(0, bobOffset, 0);
        }
        else
        {
            // reset
            bobTimer = 0f;
            cameraTransform.localPosition = Vector3.Lerp(cameraTransform.localPosition, cameraInitialLocalPos, 8F * Time.deltaTime);
        }
    }

    void UpdateAnimatorParameters()
    {
        if (!animator) return;

        Vector3 vel = controller.velocity;
        vel.y = 0f;

        Vector3 localVel = transform.InverseTransformDirection(vel);


        float moveX = Mathf.Clamp(localVel.x / runSpeed, -1f, 1f);
        float moveY = Mathf.Clamp(localVel.z / runSpeed, -1f, 1f);

        animator.SetFloat("MoveX", moveX, 0.1f, Time.deltaTime);
        animator.SetFloat("MoveY", moveY, 0.1f, Time.deltaTime);

        animator.SetBool("Sprint", isSprinting);

        animator.SetBool("IsGrounded", controller.isGrounded);
    }



    private void OnAnimatorIK(int layerIndex)
    {
        if (!animator || !enableFootIK) return;

        UpdateFootIK(AvatarIKGoal.LeftFoot, ref leftFootWeight, ref leftFootPos, ref leftFootRot, ref leftFootVel);
        UpdateFootIK(AvatarIKGoal.RightFoot, ref rightFootWeight, ref rightFootPos, ref rightFootRot, ref rightFootVel);

    }


    void UpdateFootIK(AvatarIKGoal goal, ref float weight, ref Vector3 footPos, ref Quaternion footRot, ref Vector3 vel)
    {
        Vector3 origin = animator.GetIKPosition(goal) + Vector3.up * 0.5f;
        RaycastHit hit;

        if (Physics.Raycast(origin, Vector3.down, out hit, footIKRayDistance, groundMask))
        {
            Vector3 targetPos = hit.point + Vector3.up * 0.02f;
            Quaternion targetRot = Quaternion.FromToRotation(Vector3.up, hit.normal) * transform.rotation;

            footPos = Vector3.SmoothDamp(footPos, targetPos, ref vel, footIKSmoothTime);
            footRot = Quaternion.Slerp(footRot, targetRot, 1f - Mathf.Exp(-20f * Time.deltaTime));
            weight = Mathf.MoveTowards(weight, footIKWeight, Time.deltaTime * 5f);
        }
        else
        {
            weight = Mathf.MoveTowards(weight, 0f, Time.deltaTime * 5f);
        }

        animator.SetIKPositionWeight(goal, weight);
        animator.SetIKRotationWeight(goal, weight);

        if (weight > 0.01f)
        {
            animator.SetIKPosition(goal, footPos);
            animator.SetIKRotation(goal, footRot);
        }
    }


    // Draw debug
    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.1f, 0.05f);
    }
}
