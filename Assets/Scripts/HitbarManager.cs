using System.Collections.Generic;
using UnityEngine;
/// <summary>
/// Manages the hitbars for the game objects, updating their display based on damage taken and other relevant events.
/// </summary>
/// Spawns one screen-space Healthbar per registered SetHealthbarAnchor, keeps it on the anchor through a
/// ScreenAnchor, and decides when it shows: for a few seconds after the object takes damage, and while the
/// crosshair is on it. Object keeps calling healthbar.SetHealth as before; this just assigns that healthbar.

public class HitbarManager : MonoBehaviour
{
    [Header("Bars")]
    public Healthbar healthbarPrefab; // screen-space bar (worldSpace off); a ScreenAnchor is added if the prefab lacks one
    public Canvas canvas; // Screen Space Overlay/Camera canvas the bars live on; defaults to the canvas this sits under
    public Vector3 barScale = Vector3.one; // applied to each spawned bar, in case the prefab was authored at world-space scale
    [Header("Visibility")]
    public float showAfterDamage = 3f; // seconds a bar stays up after its object takes damage
    public bool showWhenAimedAt = true; // also show while the crosshair (camera center) is on the object
    public float aimRange = 200f; // meters the crosshair check reaches
    public LayerMask aimMask = Physics.DefaultRaycastLayers; // exclude the player's own layer so the check doesn't hit your body

    class Entry
    {
        public SetHealthbarAnchor anchor;
        public Object obj;
        public Healthbar bar;
        public int lastHp;
        public float lastDamageTime = -999f;
    }

    readonly List<Entry> entries = new List<Entry>();
    Camera cam;

    void Awake()
    {
        if (canvas == null) canvas = GetComponentInParent<Canvas>();
        cam = Camera.main;
        if (healthbarPrefab == null) Debug.LogWarning($"{name}: HitbarManager has no healthbar prefab, so no bars will spawn.", this);
        if (canvas == null) Debug.LogWarning($"{name}: HitbarManager needs a screen-space Canvas (assign one or put this under it).", this);
    }

    public void AddHitbar(SetHealthbarAnchor anchor)
    {
        // Called from every SetHealthbarAnchor at initiation to add to the hitbar manager.
        if (anchor == null || healthbarPrefab == null || canvas == null) return;
        Object obj = anchor.GetComponentInParent<Object>();
        if (obj == null)
        {
            Debug.LogWarning($"{anchor.name}: SetHealthbarAnchor has no Object above it, so there's no health to show.", anchor);
            return;
        }

        // An existing world-space bar would double up with the screen one; the screen bar takes over.
        if (obj.healthbar != null && obj.healthbar.worldSpace) obj.healthbar.gameObject.SetActive(false);

        Healthbar bar = Instantiate(healthbarPrefab, canvas.transform);
        bar.worldSpace = false;
        bar.transform.localScale = barScale;
        // Parented before adding, so ScreenAnchor.Awake finds this canvas.
        ScreenAnchor screen = bar.GetComponent<ScreenAnchor>();
        if (screen == null) screen = bar.gameObject.AddComponent<ScreenAnchor>();
        screen.target = anchor.healthbarAnchor;

        bar.SetMaxHealth(obj.max_hp);
        bar.SetHealth((int)obj.hp);
        obj.healthbar = bar;
        bar.gameObject.SetActive(false);

        entries.Add(new Entry { anchor = anchor, obj = obj, bar = bar, lastHp = (int)obj.hp });
    }

    void Update()
    {
        SetHealthbarAnchor aimed = showWhenAimedAt ? AimedAnchor() : null;
        float now = Time.time;

        for (int i = entries.Count - 1; i >= 0; i--)
        {
            Entry e = entries[i];
            // Unity null: Object.End destroys the bar along with the object.
            if (e.bar == null || e.obj == null || e.anchor == null)
            {
                entries.RemoveAt(i);
                continue;
            }

            // Damage is read from the bar itself, which Object already updates on every hit.
            if (e.bar.hp < e.lastHp) e.lastDamageTime = now;
            e.lastHp = e.bar.hp;

            e.anchor.hovering = e.anchor == aimed;
            e.anchor.active = !e.obj.destroyed && (now - e.lastDamageTime < showAfterDamage || e.anchor.hovering);

            // Toggled here in Update so the bar's ScreenAnchor positions it in this same frame's LateUpdate.
            if (e.bar.gameObject.activeSelf != e.anchor.active) e.bar.gameObject.SetActive(e.anchor.active);
        }
    }

    // One camera-center raycast per frame instead of one per bar.
    SetHealthbarAnchor AimedAnchor()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return null;
        Transform c = cam.transform;
        if (!Physics.Raycast(c.position, c.forward, out RaycastHit hit, aimRange, aimMask, QueryTriggerInteraction.Ignore)) return null;
        return hit.collider.GetComponentInParent<SetHealthbarAnchor>();
    }
}
