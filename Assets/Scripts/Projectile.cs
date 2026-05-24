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
    public float damp;
    public float limitSpeed;
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

    private void ApplyDamping(ref Vector3 v, float dt)
    {
        if (limitSpeed <= 0f) return;
        Vector3 horiz = new Vector3(v.x, 0f, v.z);
        float spd = horiz.magnitude;
        if (spd <= limitSpeed) return;
        float reduction = Mathf.Min(damp * dt, spd - limitSpeed);
        Vector3 newHoriz = horiz - (horiz / spd) * reduction;
        v = new Vector3(newHoriz.x, v.y, newHoriz.z);
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
        ApplyDamping(ref vel, dt);
        Proj.position += vel * dt;
        if (vel.sqrMagnitude > 1e-10f) Proj.rotation = Quaternion.LookRotation(vel);
    }

    public void End()
    {
        bm.Return(this);
    }
}
