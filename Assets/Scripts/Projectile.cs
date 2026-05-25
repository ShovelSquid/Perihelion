using System.Collections.Generic;
using UnityEngine;

public class Projectile : MonoBehaviour
{
    public Transform Proj;
    public BulletManager bm;
    public LayerMask hitMask = ~0;
    public int prewarmCount = 20;
    public ParticleSystem hitEffect;
    public float radius;
    public float damage;
    public float mass;
    public float explosionForce;
    public float explosionRadius;
    public float explosionFalloff;
    public float damp;
    public bool dampMultBySpeed;
    public float limitSpeed;
    public float drag;
    public bool dragMultBySpeed;
    public float speed;
    public float gravity;
    public float terminalVelocity;
    public Vector3 direction;
    public Vector3 s0;
    public Vector3 a0;
    public float lifetime;
    public float deathtime;
    public List<Vector3> points = new List<Vector3>();
    [HideInInspector] public Vector3 previousPosition;
    [HideInInspector] public GameObject prefabKey;
    private Vector3 v0;
    private Vector3 vel;
    private float startTime;

    public void Init()
    {
        s0 = Proj.position;
        v0 = direction * speed;
        a0 = new Vector3(0f, -gravity, 0f);
        vel = v0;
        startTime = Time.time;
        deathtime = Time.time + lifetime;
    }

    public Vector3 CalculatePosition(float t) => s0 + v0 * t + 0.5f * a0 * t * t;
    public Vector3 CalculateVelocity(float t) => v0 + a0 * t;

    private void ApplyDrag(ref Vector3 v, float dt)
    {
        if (drag <= 0f) return;
        float fallY = v.y < 0f ? v.y : 0f;
        Vector3 draggable = new Vector3(v.x, v.y - fallY, v.z);
        float factor = drag * dt;
        if (dragMultBySpeed) factor *= draggable.magnitude;
        draggable *= Mathf.Max(0f, 1f - factor);
        v = new Vector3(draggable.x, draggable.y + fallY, draggable.z);
    }

    private void ApplyDamping(ref Vector3 v, float dt)
    {
        if (limitSpeed <= 0f) return;
        float fallY = v.y < 0f ? v.y : 0f;
        Vector3 dampable = new Vector3(v.x, v.y - fallY, v.z);
        float spd = dampable.magnitude;
        if (spd <= limitSpeed) return;
        float rate = damp * dt;
        if (dampMultBySpeed) rate *= spd;
        float reduction = Mathf.Min(rate, spd - limitSpeed);
        dampable -= (dampable / spd) * reduction;
        v = new Vector3(dampable.x, dampable.y + fallY, dampable.z);
    }

    public void CalculateTrajectoryPoints(float life, float interval)
    {
        points.Clear();
        Vector3 s = s0;
        Vector3 v = v0;
        points.Add(s);
        for (float t = interval; t <= life; t += interval)
        {
            v += a0 * interval;
            if (terminalVelocity > 0f && v.y < -terminalVelocity) v.y = -terminalVelocity;
            ApplyDrag(ref v, interval);
            ApplyDamping(ref v, interval);
            s += v * interval;
            points.Add(s);
        }
    }

    public void Fire(Vector3 direction)
    {
        this.direction = direction;
        Init();
        CalculateTrajectoryPoints(lifetime, 0.1f);
        Proj.position = s0;
        previousPosition = Proj.position;
        if (direction.sqrMagnitude > 1e-10f) Proj.rotation = Quaternion.LookRotation(direction);
    }

    public void Move()
    {
        previousPosition = Proj.position;
        float dt = Time.deltaTime;
        vel += a0 * dt;
        if (terminalVelocity > 0f && vel.y < -terminalVelocity) vel.y = -terminalVelocity;
        ApplyDrag(ref vel, dt);
        ApplyDamping(ref vel, dt);
        Proj.position += vel * dt;
        if (vel.sqrMagnitude > 1e-10f) Proj.rotation = Quaternion.LookRotation(vel);
    }

    public void End()
    {
        bm.Return(this);
    }
}
