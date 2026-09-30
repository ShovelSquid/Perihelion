using System.Collections.Generic;
using UnityEngine;
/// <summary>
/// Manages the hitbars for the game objects, updating their display based on damage taken and other relevant events.
/// </summary>
/// Spawns one screen-space panel per registered SetHealthbarAnchor, keeps it on the anchor through a
/// ScreenAnchor, and decides when it shows: for a few seconds after the object takes damage, and while the
/// crosshair is on it. The panel is any UI prefab: a Healthbar anywhere inside it becomes the object's
/// healthbar (Object keeps calling SetHealth as before), and every IObjectPanelWidget inside it is bound
/// to the object, so new info is added in the prefab, not here.

public class HitbarManager : MonoBehaviour
{
    [Header("Panels")]
    public GameObject panelPrefab; // screen-space UI root (RectTransform); may hold a Healthbar and any IObjectPanelWidgets; a ScreenAnchor is added if missing
    public Canvas canvas; // Screen Space Overlay/Camera canvas the bars live on; defaults to the canvas this sits under
    [UnityEngine.Serialization.FormerlySerializedAs("barScale")]
    public Vector3 panelScale = Vector3.one; // applied to each spawned panel, in case the prefab was authored at world-space scale
    [Header("Visibility")]
    public float showAfterDamage = 3f; // seconds a bar stays up after its object takes damage
    public bool showWhenAimedAt = true; // also show while the crosshair (camera center) is on the object
    public float aimRange = 200f; // meters the crosshair check reaches
    public LayerMask aimMask = Physics.DefaultRaycastLayers; // exclude the player's own layer so the check doesn't hit your body

    class Entry
    {
        public SetHealthbarAnchor anchor;
        public Object obj;
        public GameObject panel;
        public Healthbar bar; // optional; the panel may have no healthbar
        public int lastHp;
        public float lastDamageTime = -999f;
    }

    readonly List<Entry> entries = new List<Entry>();
    Camera cam;

    void Awake()
    {
        if (canvas == null) canvas = GetComponentInParent<Canvas>();
        cam = Camera.main;
        if (panelPrefab == null) Debug.LogWarning($"{name}: HitbarManager has no panel prefab, so no panels will spawn.", this);
        if (canvas == null) Debug.LogWarning($"{name}: HitbarManager needs a screen-space Canvas (assign one or put this under it).", this);
    }

    public void AddHitbar(SetHealthbarAnchor anchor)
    {
        // Called from every SetHealthbarAnchor at initiation to add to the hitbar manager.
        if (anchor == null || panelPrefab == null || canvas == null) return;
        Object obj = anchor.GetComponentInParent<Object>();
        if (obj == null)
        {
            Debug.LogWarning($"{anchor.name}: SetHealthbarAnchor has no Object above it, so there's no health to show.", anchor);
            return;
        }

        // An existing world-space bar would double up with the screen one; the screen bar takes over.
        if (obj.healthbar != null && obj.healthbar.worldSpace) obj.healthbar.gameObject.SetActive(false);

        GameObject panel = Instantiate(panelPrefab, canvas.transform);
        panel.transform.localScale = panelScale;
        // Parented before adding, so ScreenAnchor.Awake finds this canvas.
        ScreenAnchor screen = panel.GetComponent<ScreenAnchor>();
        if (screen == null) screen = panel.AddComponent<ScreenAnchor>();
        screen.target = anchor.healthbarAnchor;

        Healthbar bar = panel.GetComponentInChildren<Healthbar>(true);
        if (bar != null)
        {
            bar.worldSpace = false;
            bar.SetMaxHealth(obj.max_hp);
            bar.SetHealth((int)obj.hp);
            obj.healthbar = bar;
        }
        foreach (IObjectPanelWidget widget in panel.GetComponentsInChildren<IObjectPanelWidget>(true))
        {
            widget.Bind(obj);
        }
        panel.SetActive(false);

        entries.Add(new Entry { anchor = anchor, obj = obj, panel = panel, bar = bar, lastHp = (int)obj.hp });
    }

    void Update()
    {
        SetHealthbarAnchor aimed = showWhenAimedAt ? AimedAnchor() : null;
        float now = Time.time;

        for (int i = entries.Count - 1; i >= 0; i--)
        {
            Entry e = entries[i];
            // Unity null: the object (and with Object.End, its healthbar) can be destroyed at any time.
            // The panel is ours, so it goes when the object does.
            if (e.obj == null || e.anchor == null || e.panel == null)
            {
                if (e.panel != null) Destroy(e.panel);
                entries.RemoveAt(i);
                continue;
            }

            // Damage is read from the object's hp, which works whether or not the panel has a healthbar.
            int hp = (int)e.obj.hp;
            if (hp < e.lastHp) e.lastDamageTime = now;
            e.lastHp = hp;

            e.anchor.hovering = e.anchor == aimed;
            e.anchor.active = !e.obj.destroyed && (now - e.lastDamageTime < showAfterDamage || e.anchor.hovering);

            // Toggled here in Update so the panel's ScreenAnchor positions it in this same frame's LateUpdate.
            if (e.panel.activeSelf != e.anchor.active) e.panel.SetActive(e.anchor.active);
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
