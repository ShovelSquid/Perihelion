using UnityEngine;
using System.Collections;
using System;
using Unity.Mathematics;
using System.Collections.Generic;
using UnityEngine.Events;
using UnityEngine.Serialization;

[RequireComponent(typeof(Mob))]
public class Move : MonoBehaviour
{
    private Mob mob;
    private Rigidbody rb;
    public Collider moveCollider;

    [Header("Events")]
    public UnityEvent<Collider> onEnterPlat;
    public UnityEvent<Collider> onExitPlat;

    [Header("Audio")]
    public bool playAudio = true;
    public bool playFootsteps = false;
    public List<AudioClip> footstepSounds = new List<AudioClip>();
    public AudioClip jumpSound;

    [Header("Movement")]
    public bool moving;
    public float acceleration;
    public float groundDeceleration;
    public float maxGroundSpeed;
    public float airAcceleration;
    public float airDeceleration;
    public float maxAirSpeed;
    public Vector2 moveDirection;
    public float groundRotationSpeed;
    public float airRotationSpeed;
    public float maxSpeedForceGround;
    public float maxSpeedForceAir;

    [Header("Jumping")]
    public float jumpforce;
    public float jumpForceGround;
    public float jumpForceAir;
    [Range(1f, 100f)]
    public float jumpForceMoveMult;
    [Range(1f, 100f)]
    public float jumpForceMoveMultMax;
    [Range(1f, 100f)]
    public float jumpForceMoveMultMaxAir;
    [Range(1f, 100f)]
    public float jumpForceUpMult;
    [Range(1f, 100f)]
    public float jumpForceUpMultMax;
    [Range(1f, 100f)]
    public float jumpForceUpMultMaxAir;
    [FormerlySerializedAs("maxJumps")]
    public int maxAirJumps;
    [FormerlySerializedAs("jumps")]
    public int airJumps;
    public float maxJumpSpeed;
    public float maxFallSpeed;
    [Range(0f, 1f)]
    public float horizontalWeight;
    [Range(0f, 1f)]
    public float horizontalWeightMax;
    [Range(0f, 1f)]
    public float horizontalWeightMaxAir;
    public bool jump;
    public bool inAir;
    public ParticleSystem airJumpParticle;
    public ParticleSystem groundJumpParticle;
    public Transform jumpFXPoint;

    [Header("Ground Check")]
    public LayerMask groundLayer;
    public float groundCheckDistance = 0.1f;
    public float fallSpeed;

    private readonly List<Vector3> wallNormals = new List<Vector3>();
    private Vector3 groundNormal = Vector3.up;
    private bool grounded;

    void Start()
    {
        mob = GetComponent<Mob>();
        rb = GetComponent<Rigidbody>();
        airJumps = maxAirJumps;
        InAir();
    }

    public void Respawn()
    {
        transform.position = mob.spawnPoint.position;
    }

    void FixedUpdate()
    {
        if (mob.dead)
        {
            if (moving)
            {
                Vector2 xzVelo = new Vector2(rb.linearVelocity.x, rb.linearVelocity.z);
                if (xzVelo.magnitude < 0.1f) moving = false;
                else rb.AddForce(new Vector3(-xzVelo.x, 0, -xzVelo.y).normalized * math.remap(0f, maxGroundSpeed, 0f, groundDeceleration, xzVelo.magnitude), ForceMode.Acceleration);
            }
            return;
        }
        if (grounded && groundNormal.y > 0.5f)
        {
            rb.AddForce(-Vector3.ProjectOnPlane(Physics.gravity, groundNormal), ForceMode.Acceleration);
        }
        if (jump)
        {
            if (CanJump()) Jump();
            else jump = false;
        }
        if (moving)
        {
            Vector2 xzVelo = new Vector2(rb.linearVelocity.x, rb.linearVelocity.z);
            float acc = acceleration;
            float dcc = groundDeceleration;
            float maxspeeed = maxGroundSpeed;       // maxspeed has 3 eee's. it's funny i swere
            float maxRotSpeeed = groundRotationSpeed;
            float velocityMagnitude = xzVelo.magnitude;
            float maxSpeedForce = maxSpeedForceGround;
            mob.anim.SetFloat("Speed", math.clamp(math.remap(0f, maxspeeed, 0f, 1f, velocityMagnitude), 0f, 1f));
            playFootsteps = mob.anim.GetFloat("Speed") > 0.3f;
            InAir();
            if (inAir)
            {
                playFootsteps = false;
                acc = airAcceleration;
                dcc = airDeceleration;
                maxspeeed = maxAirSpeed;
                maxRotSpeeed = airRotationSpeed;
                maxSpeedForce = maxSpeedForceAir;
            }
            if (moveDirection == Vector2.zero)
            {
                if (velocityMagnitude < 0.1f) moving = false;
                else rb.AddForce(new Vector3(-xzVelo.x, 0, -xzVelo.y).normalized * math.remap(0f, maxspeeed, 0f, dcc, velocityMagnitude), ForceMode.Acceleration);
            }
            else
            {
                Vector3 inputDir = new Vector3(moveDirection.x, 0, moveDirection.y).normalized;
                foreach (var n in wallNormals)
                {
                    float into = -Vector3.Dot(inputDir, n);
                    if (into > 0f) inputDir += n * into;
                }
                rb.AddForce(inputDir * acc, ForceMode.Acceleration);
                Vector3 target = new Vector3(moveDirection.x, 0, moveDirection.y);
                if (rb.transform.forward != target)
                {
                    rb.transform.rotation = Quaternion.Slerp(rb.transform.rotation, Quaternion.LookRotation(target, Vector3.up), maxRotSpeeed * Time.deltaTime);
                }
            }
            if (velocityMagnitude > maxspeeed)
            {
                Vector2 excessVelocity = xzVelo.normalized * (velocityMagnitude - maxspeeed);
                Vector3 forceToMaxSpeed = new Vector3(-excessVelocity.x, 0, -excessVelocity.y) * rb.mass / Time.fixedDeltaTime;
                if (forceToMaxSpeed.magnitude > maxSpeedForce) forceToMaxSpeed = forceToMaxSpeed.normalized * maxSpeedForce;
                rb.AddForce(forceToMaxSpeed, ForceMode.Force);
            }
        }
        if (inAir)
        {
            mob.anim.SetFloat("Up", math.clamp(math.remap(maxFallSpeed, maxJumpSpeed, -1f, 1f, rb.linearVelocity.y), -1f, 1f));
            fallSpeed = math.abs(rb.linearVelocity.y);
        }
        wallNormals.Clear();
        grounded = false;
    }

    bool CanJump() => !mob.dead && (!inAir || airJumps > 0);

    void OnCollisionEnter(Collision collision)
    {
        if ((groundLayer.value & (1 << collision.gameObject.layer)) == 0) return;
        Vector3 normal = Vector3.zero;
        foreach (var c in collision.contacts)
            if (c.normal.y > normal.y) normal = c.normal;
        if (normal.y <= 0.5f) return;
        mob.anim.SetTrigger("Land");
        PlayLandingSound();
        if (jumpFXPoint != null && groundJumpParticle != null)
        {
            jumpFXPoint.rotation = Quaternion.LookRotation((rb.linearVelocity.normalized + transform.up).normalized);
            Instantiate(groundJumpParticle, jumpFXPoint.position, jumpFXPoint.rotation);
        }
        airJumps = maxAirJumps;
        InAir();
        StartCoroutine(DelayAction(0.1f, InAir));
        if (fallSpeed > mob.fallDamageSpeedMin)
        {
            // fix this to be aligned with the normal later
            mob.FallDamage(fallSpeed, normal.y);
        }
    }

    void OnCollisionStay(Collision collision)
    {
        if ((groundLayer.value & (1 << collision.gameObject.layer)) == 0) return;
        foreach (var c in collision.contacts)
        {
            if (c.normal.y > 0.5f)
            {
                grounded = true;
                groundNormal = c.normal;
            }
            else if (c.normal.y < 0.4f) wallNormals.Add(c.normal);
        }
    }

    void OnCollisionExit(Collision collision)
    {
        if ((groundLayer.value & (1 << collision.gameObject.layer)) != 0) InAir();
    }

    void OnTriggerEnter(Collider other)
    {
        if ((groundLayer.value & (1 << other.gameObject.layer)) != 0 && other.CompareTag("Platform"))
            onEnterPlat.Invoke(other);
    }

    void OnTriggerExit(Collider other)
    {
        if ((groundLayer.value & (1 << other.gameObject.layer)) != 0 && other.CompareTag("Platform"))
            onExitPlat.Invoke(other);
        if (other.gameObject.layer == LayerMask.NameToLayer("Bounds")) Respawn();
    }

    private IEnumerator DelayAction(float delay, Action action)
    {
        yield return new WaitForSeconds(delay);
        action?.Invoke();
    }

    public void InAir()
    {
        Vector3 origin = new Vector3(transform.position.x, transform.position.y - moveCollider.bounds.size.y / 2, transform.position.z);
        inAir = !Physics.Raycast(origin, Vector3.down, out _, groundCheckDistance, groundLayer, QueryTriggerInteraction.Ignore);
        mob.anim.SetBool("inAir", inAir);
    }

    public void Jump()
    {
        if (!CanJump()) return;
        StartCoroutine(DelayAction(0.05f, InAir));
        InAir();
        jump = false;
        mob.anim.SetTrigger("Jump");
        mob.adio.PlayOneShot(jumpSound);
        jumpforce = jumpForceGround;
        mob.anim.SetFloat("Flip", 0);
        ParticleSystem jumpParticle = groundJumpParticle;
        Vector3 moveVec = new Vector3(moveDirection.x, 0, moveDirection.y).normalized;
        horizontalWeight = math.clamp(math.remap(0f, maxGroundSpeed, 0f, horizontalWeightMax, rb.linearVelocity.magnitude), 0f, horizontalWeightMax);
        jumpForceMoveMult = math.clamp(math.remap(0f, maxGroundSpeed, 1f, jumpForceMoveMultMax, rb.linearVelocity.magnitude), 0f, jumpForceMoveMultMax);
        jumpForceUpMult = math.clamp(math.remap(0f, maxGroundSpeed, 1f, jumpForceUpMultMax, rb.linearVelocity.magnitude), 0f, jumpForceUpMultMax);
        if (inAir)
        {
            mob.anim.SetFloat("Flip", 1);
            jumpforce = jumpForceAir;
            jumpParticle = airJumpParticle;
            float magnitude = moveDirection.magnitude;
            jumpForceMoveMult = math.clamp(math.remap(0f, 1f, 1f, jumpForceMoveMultMaxAir, magnitude), 0f, jumpForceMoveMultMaxAir);
            horizontalWeight = math.clamp(math.remap(0f, 1f, 0f, horizontalWeightMaxAir, magnitude), 0f, horizontalWeightMaxAir);
            jumpForceUpMult = math.clamp(math.remap(0f, 1f, 1f, jumpForceUpMultMaxAir, magnitude), 0f, jumpForceUpMultMaxAir);
        }
        Vector3 jumpVector = Vector3.up * (1 - horizontalWeight) * (jumpforce * jumpForceUpMult) + moveVec * horizontalWeight * (jumpforce * jumpForceMoveMult);
        if (jumpParticle != null && jumpFXPoint != null)
        {
            jumpFXPoint.rotation = Quaternion.LookRotation(jumpVector.normalized);
            Instantiate(jumpParticle, jumpFXPoint.position, jumpFXPoint.rotation);
        }
        rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
        rb.AddForce(Vector3.up * (1 - horizontalWeight) * (jumpforce * jumpForceUpMult), ForceMode.Impulse);
        rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, new Vector3(0, rb.linearVelocity.y, 0), horizontalWeight);
        rb.AddForce(moveVec * horizontalWeight * (jumpforce * jumpForceMoveMult), ForceMode.Impulse);
        if (inAir) airJumps--;
    }

    public void SetMoveDirection(Vector2 direction)
    {
        moveDirection = direction;
        moving = true;
    }

    public void PlayFootstepSound()
    {
        if (!playAudio || !playFootsteps) return;
        if (footstepSounds.Count > 0) mob.adio.PlayOneShot(footstepSounds[UnityEngine.Random.Range(0, footstepSounds.Count)]);
    }

    public void PlayLandingSound()
    {
        if (footstepSounds.Count > 0) mob.adio.PlayOneShot(footstepSounds[UnityEngine.Random.Range(0, footstepSounds.Count)]);
    }
}
