using UnityEngine;
using System.Collections;
using System;
using Unity.Mathematics;
using System.Collections.Generic;
using UnityEngine.Events;


[RequireComponent(typeof(Mob))]
public class Move : MonoBehaviour
{
    private Mob mob;
    private Rigidbody rb;

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
    public int maxJumps;
    public int jumps;
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
    [Header("Ground Check")]
    public LayerMask groundLayer;
    public float groundCheckDistance = 0.1f;
    public float fallSpeed;


    void Start()
    {
        mob = GetComponent<Mob>();
        rb = GetComponent<Rigidbody>();
        jumps = maxJumps;
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
                float velocityMagnitude = xzVelo.magnitude;
                float maxspeeed = maxGroundSpeed;
                float dcc = groundDeceleration;
                if (velocityMagnitude < 0.1f)
                {
                    moving = false;
                }
                else
                {
                    float remappedAcceleration = math.remap(0f, maxspeeed, 0f, dcc, velocityMagnitude);
                    rb.AddForce(new Vector3(-xzVelo.x, 0, -xzVelo.y).normalized * remappedAcceleration, ForceMode.Acceleration);
                }
            }
            return;
        }
        if (jump)
        {
            if (CanJump())
            {
                Jump();
            }
            else
            {
                jump = false;
            }
        }
        if (moving)
        {
            Vector2 xzVelo = new Vector2(rb.linearVelocity.x, rb.linearVelocity.z);
            Vector2 finalMoveDirection = moveDirection;
            float acc = acceleration;
            float dcc = groundDeceleration;
            float maxspeeed = maxGroundSpeed;       // maxspeed has 3 eee's. it's funny i swere
            float maxRotSpeeed = groundRotationSpeed;
            float velocityMagnitude = xzVelo.magnitude;
            float maxSpeedForce = maxSpeedForceGround;
            xzVelo = Vector2.ClampMagnitude(xzVelo, maxspeeed);
            mob.anim.SetFloat("Speed", math.clamp(math.remap(0f, maxspeeed, 0f, 1f, velocityMagnitude), 0f, 1f));
            if (mob.anim.GetFloat("Speed") > 0.3f) playFootsteps = true;
            if (mob.anim.GetFloat("Speed") < 0.3f) playFootsteps = false;
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
                if (velocityMagnitude < 0.1f)
                {
                    moving = false;
                }
                else
                {
                    float remappedAcceleration = math.remap(0f, maxspeeed, 0f, dcc, velocityMagnitude);
                    rb.AddForce(new Vector3(-xzVelo.x, 0, -xzVelo.y).normalized * remappedAcceleration, ForceMode.Acceleration);
                }
            }
            else
            {
                rb.AddForce(new Vector3(finalMoveDirection.x, 0, finalMoveDirection.y).normalized * acc, ForceMode.Acceleration);
                if (rb.transform.forward != new Vector3(finalMoveDirection.x, 0, finalMoveDirection.y))
                {
                    Quaternion targetRotation = Quaternion.LookRotation(new Vector3(finalMoveDirection.x, 0, finalMoveDirection.y), Vector3.up);
                    rb.transform.rotation = Quaternion.Slerp(rb.transform.rotation, targetRotation, maxRotSpeeed * Time.deltaTime);
                }
            }
            if (velocityMagnitude > maxspeeed)
            {
                xzVelo = xzVelo.normalized * maxspeeed;
                // rb.AddForce(new Vector3(-xzVelo.x, 0, -xzVelo.y).normalized * maxSpeedForce, ForceMode.Acceleration);
                // if (rb.linearVelocity.magnitude < velocityMagnitude)
                // {
                //     rb.linearVelocity = rb.linearVelocity.normalized * velocityMagnitude;
                // }
                float excessSpeed = velocityMagnitude - maxspeeed;
                Vector2 excessVelocity = xzVelo.normalized * excessSpeed;
                Vector3 forceToMaxSpeed = new Vector3(-excessVelocity.x, 0, -excessVelocity.y) * rb.mass / Time.fixedDeltaTime;
                if (forceToMaxSpeed.magnitude > maxSpeedForce)
                {
                    forceToMaxSpeed = forceToMaxSpeed.normalized * maxSpeedForce;
                }
                rb.AddForce(forceToMaxSpeed, ForceMode.Force);
            }
        }
        if (inAir)
        {
            mob.anim.SetFloat("Up", math.clamp(math.remap(maxFallSpeed, maxJumpSpeed, -1f, 1f, rb.linearVelocity.y), -1f, 1f));
            fallSpeed = math.abs(rb.linearVelocity.y);
            Debug.Log("rb.linearVelocity.y: " + rb.linearVelocity.y);
        }
    }

    bool CanJump()
    {
        if (mob.dead) return false;
        return jumps > 0;
    }

    void OnCollisionEnter(Collision collision)
    {
        if ((groundLayer.value & (1 << collision.gameObject.layer)) != 0)
        {
            var normal = collision.contacts[0].normal;
            if (normal.y > 0.5f)
            {
                mob.anim.SetTrigger("Land");
                PlayLandingSound();
                Vector3 jumpVector = (rb.linearVelocity.normalized + transform.up).normalized;
                mob.jumpFXPoint.rotation = Quaternion.LookRotation(jumpVector);
                var jumpfx = Instantiate(mob.groundJumpParticle, mob.jumpFXPoint.position, mob.jumpFXPoint.rotation);
                jumps = maxJumps;
                InAir();
                StartCoroutine(DelayAction(0.1f, InAir));
                Debug.Log("math.abs(rb.linearVelocity.y): " + fallSpeed);
                if (fallSpeed > mob.fallDamageSpeedMin)
                {
                    // fix this to be aligned with the normal later
                    mob.FallDamage(fallSpeed, normal.y);
                }
            }
        }
    }

    void OnCollisionExit(Collision collision)
    {
        if ((groundLayer.value & (1 << collision.gameObject.layer)) != 0)
        {
            InAir();
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if ((groundLayer.value & (1 << other.gameObject.layer)) != 0)
        {
            if (other.CompareTag("Platform"))
            {
                onEnterPlat.Invoke(other);
            }
        }
    }

    void OnTriggerExit(Collider other)
    {
        if ((groundLayer.value & (1 << other.gameObject.layer)) != 0)
        {
            if (other.CompareTag("Platform"))
            {
                onExitPlat.Invoke(other);
            }
        }
        if (other.gameObject.layer == LayerMask.NameToLayer("Bounds"))
        {
            Respawn();
        }
    }

    private IEnumerator DelayAction(float delay, Action action)
    {
        yield return new WaitForSeconds(delay);
        action?.Invoke();
    }



    public void InAir()
    {
        // Use a downward raycast to check for collisions with groundLayer only (ignores triggers)
        RaycastHit hit;
        Vector3 origin = new Vector3(transform.position.x, transform.position.y - mob.box.bounds.size.y / 2, transform.position.z);
        bool isGrounded = Physics.Raycast(origin, Vector3.down, out hit, groundCheckDistance, groundLayer, QueryTriggerInteraction.Ignore);

        inAir = !isGrounded;
        mob.anim.SetBool("inAir", inAir);
    }




    public void Jump()
    {
        if (CanJump())
        {
            StartCoroutine(DelayAction(0.05f, InAir));
            InAir();
            jump = false;
            mob.anim.SetTrigger("Jump");
            mob.adio.PlayOneShot(jumpSound);
            jumpforce = jumpForceGround;
            mob.anim.SetFloat("Flip", 0);
            ParticleSystem jumpParticle = mob.groundJumpParticle;
            horizontalWeight = math.clamp(math.remap(0f, maxGroundSpeed, 0f, horizontalWeightMax, rb.linearVelocity.magnitude), 0f, horizontalWeightMax);
            jumpForceMoveMult = math.clamp(math.remap(0f, maxGroundSpeed, 1f, jumpForceMoveMultMax, rb.linearVelocity.magnitude), 0f, jumpForceMoveMultMax);
            jumpForceUpMult = math.clamp(math.remap(0f, maxGroundSpeed, 1f, jumpForceUpMultMax, rb.linearVelocity.magnitude), 0f, jumpForceUpMultMax);
            Vector3 jumpVector = Vector3.up * (1 - horizontalWeight) * (jumpforce * jumpForceUpMult) + new Vector3(moveDirection.x, 0, moveDirection.y).normalized * horizontalWeight * (jumpforce * jumpForceMoveMult);
            if (inAir)
            {
                mob.anim.SetFloat("Flip", 1);
                jumpforce = jumpForceAir;
                float magnitude = moveDirection.magnitude;
                jumpParticle = mob.airJumpParticle;
                jumpForceMoveMult = math.clamp(math.remap(0f, 1f, 1f, jumpForceMoveMultMaxAir, magnitude), 0f, jumpForceMoveMultMaxAir);
                horizontalWeight = math.clamp(math.remap(0f, 1f, 0f, horizontalWeightMaxAir, magnitude), 0f, horizontalWeightMaxAir);
                jumpForceUpMult = math.clamp(math.remap(0f, 1f, 1f, jumpForceUpMultMaxAir, magnitude), 0f, jumpForceUpMultMaxAir);
                jumpVector = Vector3.up * (1 - horizontalWeight) * (jumpforce * jumpForceUpMult) + new Vector3(moveDirection.x, 0, moveDirection.y).normalized * horizontalWeight * (jumpforce * jumpForceMoveMult);
            }
            mob.jumpFXPoint.rotation = Quaternion.LookRotation(jumpVector.normalized);
            var jumpfx = Instantiate(jumpParticle, mob.jumpFXPoint.position, mob.jumpFXPoint.rotation);
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
            rb.AddForce(Vector3.up * (1 - horizontalWeight) * (jumpforce * jumpForceUpMult), ForceMode.Impulse);
            Debug.Log("move mult: " + jumpForceUpMult);
            rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, new Vector3(0, rb.linearVelocity.y, 0), horizontalWeight);
            rb.AddForce(new Vector3(moveDirection.x, 0, moveDirection.y).normalized * horizontalWeight * (jumpforce * jumpForceMoveMult), ForceMode.Impulse);
            jumps--;
        }
    }


    public void SetMoveDirection(Vector2 direction)
    {
        moveDirection = direction;
        moving = true;
    }

    public void PlayFootstepSound()
    {
        if (!playAudio) return;
        if (!playFootsteps) return;
        if (footstepSounds.Count > 0)
        {
            int index = UnityEngine.Random.Range(0, footstepSounds.Count);
            mob.adio.PlayOneShot(footstepSounds[index]);
        }
    }
    public void PlayLandingSound()
    {
        if (footstepSounds.Count > 0)
        {
            int index = UnityEngine.Random.Range(0, footstepSounds.Count);
            mob.adio.PlayOneShot(footstepSounds[index]);
        }
    }
}