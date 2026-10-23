using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.InputSystem;


[RequireComponent(typeof(CharacterController))]
public class FullBodyFPController : MonoBehaviour
{
    [Header("Riferimenti")]
    public Animator animator;
    public Transform headBone;          // bone Head dell'armatura
    public Transform cameraRig;         // vuoto con la Camera figlia (fuori dal Player)
    public Renderer[] hideRenderers;    // testa, capelli, occhi...

    [Header("Movimento")]
    public float walkSpeed = 2.5f;
    public float runSpeed = 5f;
    public float jumpHeight = 1.1f;
    public float gravity = -20f;
    public float acceleration = 10f;

    [Header("Camera")]
    public float sensitivity = 0.12f;
    public Vector2 pitchLimits = new Vector2(-80f, 80f);
    public Vector3 headOffset = new Vector3(0f, 0.05f, 0.12f); // locale al corpo: avanti per uscire dalla faccia
    public float eyeHeight = 1.65f;     // altezza "stabile" degli occhi
    [Range(0f, 1f)] public float headBobAmount = 0.5f; // 0 = camera rigida, 1 = segue tutta l'animazione

    CharacterController cc;
    float yaw, pitch, speed, verticalVel;
    Vector3 planarVel;

    static readonly int SpeedHash = Animator.StringToHash("Speed");
    static readonly int GroundedHash = Animator.StringToHash("Grounded");
    static readonly int JumpHash = Animator.StringToHash("Jump");

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        yaw = transform.eulerAngles.y;
        Cursor.lockState = CursorLockMode.Locked;

        foreach (var r in hideRenderers)
            r.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
    }

    void Update()
    {
        var kb = Keyboard.current;
        var mouse = Mouse.current;
        if (kb == null || mouse == null) return;

        // --- Look (yaw ruota il corpo, pitch solo la camera) ---
        Vector2 look = mouse.delta.ReadValue() * sensitivity;
        yaw += look.x;
        pitch = Mathf.Clamp(pitch - look.y, pitchLimits.x, pitchLimits.y);
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        // --- Move ---
        Vector2 input = Vector2.zero;
        if (kb.wKey.isPressed) input.y += 1;
        if (kb.sKey.isPressed) input.y -= 1;
        if (kb.dKey.isPressed) input.x += 1;
        if (kb.aKey.isPressed) input.x -= 1;
        input = Vector2.ClampMagnitude(input, 1f);

        bool running = kb.leftShiftKey.isPressed && input.y > 0.1f;
        float targetSpeed = input.magnitude * (running ? runSpeed : walkSpeed);
        Vector3 targetVel = (transform.right * input.x + transform.forward * input.y).normalized * targetSpeed;
        planarVel = Vector3.Lerp(planarVel, targetVel, acceleration * Time.deltaTime);

        bool grounded = cc.isGrounded;
        if (grounded && verticalVel < 0f) verticalVel = -2f; // tiene il controller incollato al suolo

        if (grounded && kb.spaceKey.wasPressedThisFrame)
        {
            verticalVel = Mathf.Sqrt(jumpHeight * -2f * gravity);
            animator.SetTrigger(JumpHash);
        }
        verticalVel += gravity * Time.deltaTime;

        cc.Move((planarVel + Vector3.up * verticalVel) * Time.deltaTime);

        // --- Animator ---
        speed = planarVel.magnitude;
        animator.SetFloat(SpeedHash, speed, 0.1f, Time.deltaTime);
        animator.SetBool(GroundedHash, cc.isGrounded);
    }

    // LateUpdate = dopo che l'Animator ha valutato la posa: camera sempre sincronizzata
    void LateUpdate()
    {
        // Posizione stabile (capsula) vs posizione del bone (con bobbing dell'animazione)
        Vector3 stablePos = transform.position + Vector3.up * eyeHeight + transform.rotation * headOffset;
        Vector3 bonePos = headBone.position + transform.rotation * headOffset;

        // Orizzontale sempre dalla capsula (niente dislocamento), verticale mixato col bone
        Vector3 pos = stablePos;
        pos.y = Mathf.Lerp(stablePos.y, bonePos.y, headBobAmount);
        cameraRig.position = pos;
        cameraRig.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }
}
