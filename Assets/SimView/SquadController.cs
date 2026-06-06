using UnityEngine;
using UnityEngine.InputSystem;
using Perihelion.Sim;

namespace Perihelion.SimView
{
    /// <summary>
    /// Runtime control + inspection.
    ///   • Squad mode (default): left-click selects the nearest squad, right-click marches it.
    ///   • Unit mode (hold the modifier): left-click selects the nearest unit, right-click
    ///     commands it (which promotes + detaches it — that's the only path that mutates).
    ///   • Inspector: hovering shows a read-only stat panel for the unit under the cursor.
    ///
    /// CRITICAL: selecting and inspecting only ever READ (Squad.Resolve is pure). A unit is
    /// promoted to an individual delta exclusively when you issue it a command. So you can read
    /// any unit's full stats, all day, without ever materializing storage for it.
    ///
    /// Commands go through World.Enqueue — the same path the netcode will use — so this is also
    /// your local input layer for lockstep.
    /// </summary>
    public sealed class SquadController : MonoBehaviour
    {
        public SimRunner runner;
        public Camera cam;
        public float groundHeight = 0f;

        [Tooltip("Max world distance from the click to a squad centroid to select it.")]
        public float selectRadius = 4f;
        [Tooltip("Max world distance from the cursor to a unit to pick it (unit mode / inspector).")]
        public float unitPickRadius = 1.5f;
        [Tooltip("Ticks ahead to schedule a command. In lockstep this is the input delay.")]
        public int commandDelayTicks = 0;
        [Tooltip("Hold this key to operate on individual units instead of whole squads.")]
        public Key unitModifier = Key.LeftCtrl;
        [Tooltip("Show the read-only stat panel for the unit under the cursor.")]
        public bool showInspector = true;

        public int SelectedSquadId { get; private set; } = -1;
        public bool HasSelectedUnit { get; private set; }
        public UnitId SelectedUnit { get; private set; }

        private Transform _squadRing;
        private Transform _unitRing;

        // Hover inspection (pure read; never promotes).
        private bool _hoverHas;
        private UnitId _hoverId;
        private UnitState _hoverState;

        void Awake()
        {
            if (runner == null) runner = GetComponent<SimRunner>();
            if (cam == null) cam = Camera.main;
        }

        void Update()
        {
            if (runner == null || runner.World == null || cam == null) return;
            Mouse mouse = Mouse.current;
            if (mouse == null) return;

            bool unitMode = unitModifier != Key.None && Keyboard.current != null
                            && Keyboard.current[unitModifier].isPressed;
            bool havePoint = RaycastGround(mouse, out Vector3 point);

            // Read-only hover pick for the inspector panel.
            _hoverHas = havePoint && (showInspector || unitMode) && TryPickUnit(point, out _hoverId, out _hoverState);

            if (mouse.leftButton.wasPressedThisFrame && havePoint)
            {
                if (unitMode)
                {
                    HasSelectedUnit = TryPickUnit(point, out UnitId uid, out _);
                    SelectedUnit = uid;                       // selection is a READ — no promotion
                }
                else
                {
                    Squad sq = NearestSquad(point, selectRadius);
                    SelectedSquadId = sq != null ? sq.Id : -1;
                }
            }

            if (mouse.rightButton.wasPressedThisFrame && havePoint)
            {
                if (unitMode && HasSelectedUnit)
                {
                    IssueUnitMove(SelectedUnit, point);   // THIS promotes
                }
                else if (!unitMode && SelectedSquadId >= 0)
                {
                    // Right-click a hostile squad => attack it; right-click ground => move there.
                    Squad sel = runner.World.GetSquad(SelectedSquadId);
                    Squad tgt = NearestSquad(point, selectRadius);
                    if (sel != null && tgt != null && tgt.Id != sel.Id && sel.IsHostileTo(tgt))
                        IssueAttack(SelectedSquadId, tgt.Id);
                    else
                        IssueSquadMove(SelectedSquadId, point);
                }
            }

            UpdateRings();
        }

        // ── Picking (all read-only) ───────────────────────────────────────────────────
        private Squad NearestSquad(Vector3 wp, float maxDist)
        {
            System.Collections.Generic.IReadOnlyList<Squad> squads = runner.World.Squads;
            Squad best = null;
            float bestSqr = maxDist * maxDist;
            for (int i = 0; i < squads.Count; i++)
            {
                float d = (squads[i].Centroid.ToWorld(groundHeight) - wp).sqrMagnitude;
                if (d <= bestSqr) { bestSqr = d; best = squads[i]; }
            }
            return best;
        }

        // SEAM: scans the nearest squad's units. At scale, back unit picking with a spatial index
        // (uniform grid) instead of resolving a whole squad each pick.
        private bool TryPickUnit(Vector3 wp, out UnitId id, out UnitState state)
        {
            id = default;
            state = default;
            Squad sq = NearestSquad(wp, selectRadius * 2f);
            if (sq == null) return false;

            int tick = runner.World.Tick;
            float bestSqr = unitPickRadius * unitPickRadius;
            bool found = false;
            foreach (UnitState u in sq.Expand(tick))
            {
                float d = (u.Pos.ToWorld(groundHeight) - wp).sqrMagnitude;
                if (d <= bestSqr) { bestSqr = d; id = u.Id; state = u; found = true; }
            }
            return found;
        }

        // ── Commands (the only things that mutate sim state) ──────────────────────────
        private void IssueSquadMove(int squadId, Vector3 wp) =>
            runner.World.Enqueue(new Command
            {
                Kind = CommandKind.MoveSquad,
                SquadId = squadId,
                Target = new FixedVec2(F(wp.x), F(wp.z)),
                IssueTick = runner.World.Tick + Mathf.Max(0, commandDelayTicks)
            });

        private void IssueAttack(int squadId, int targetSquadId) =>
            runner.World.Enqueue(new Command
            {
                Kind = CommandKind.AttackSquad,
                SquadId = squadId,
                TargetSquadId = targetSquadId,
                IssueTick = runner.World.Tick + Mathf.Max(0, commandDelayTicks)
            });

        private void IssueUnitMove(UnitId unit, Vector3 wp) =>
            runner.World.Enqueue(new Command
            {
                Kind = CommandKind.MoveUnit,
                Unit = unit,
                Target = new FixedVec2(F(wp.x), F(wp.z)),
                IssueTick = runner.World.Tick + Mathf.Max(0, commandDelayTicks)
            });

        private bool RaycastGround(Mouse mouse, out Vector3 point)
        {
            Plane ground = new Plane(Vector3.up, new Vector3(0f, groundHeight, 0f));
            Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
            if (ground.Raycast(ray, out float enter)) { point = ray.GetPoint(enter); return true; }
            point = default;
            return false;
        }

        // ── Selection markers ─────────────────────────────────────────────────────────
        private void UpdateRings()
        {
            Squad sel = SelectedSquadId >= 0 ? runner.World.GetSquad(SelectedSquadId) : null;
            PlaceRing(ref _squadRing, sel != null, sel != null ? sel.Centroid.ToWorld(groundHeight) : Vector3.zero, 3f, "SquadSelection");

            bool unitOk = HasSelectedUnit;
            Vector3 upos = Vector3.zero;
            if (unitOk)
            {
                Squad us = runner.World.GetSquad(SelectedUnit.Squad);
                if (us != null)
                {
                    UnitState st = us.Resolve(SelectedUnit.Index, runner.World.Tick);
                    unitOk = st.Alive;
                    upos = st.Pos.ToWorld(groundHeight);
                }
                else unitOk = false;
            }
            PlaceRing(ref _unitRing, unitOk, upos, 1f, "UnitSelection");
        }

        private void PlaceRing(ref Transform ring, bool active, Vector3 pos, float size, string ringName)
        {
            if (!active)
            {
                if (ring != null) ring.gameObject.SetActive(false);
                return;
            }
            if (ring == null)
            {
                GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                go.name = ringName;
                Collider col = go.GetComponent<Collider>();
                if (col != null) Destroy(col);
                go.transform.SetParent(transform, false);
                go.transform.localScale = new Vector3(size, 0.02f, size);
                ring = go.transform;
            }
            ring.gameObject.SetActive(true);
            ring.position = pos;
        }

        // ── Read-only stat panel (stopgap until your real UI) ─────────────────────────
        void OnGUI()
        {
            if (!showInspector || !_hoverHas || runner == null || runner.World == null) return;

            UnitArchetype arch = runner.World.Archetypes.Get(_hoverState.ArchetypeId);
            string archName = arch != null ? arch.Name : $"#{_hoverState.ArchetypeId}";
            float maxHp = arch != null ? arch.BaseHp.ToFloat() : 0f;

            // Loadout is DERIVED from the squad's bulk inventory — reading it does not promote.
            Squad sq = runner.World.GetSquad(_hoverId.Squad);
            UnitLoadout lo = sq != null ? sq.ResolveLoadout(_hoverId.Index) : UnitLoadout.Unarmed;
            string gear = lo.HasWeapon
                ? $"{runner.World.Items.Get(lo.WeaponItemId)?.Name ?? "weapon"}  (ammo {lo.Ammo})"
                : "unarmed";

            string text =
                $"Unit {_hoverId}  [{archName}]\n" +
                $"HP   {_hoverState.Hp.ToFloat():0} / {maxHp:0}\n" +
                $"Pos  ({_hoverState.Pos.X.ToFloat():0.0}, {_hoverState.Pos.Y.ToFloat():0.0})\n" +
                $"Gear {gear}\n" +
                $"Kind {(_hoverState.Promoted ? "Individual (commanded)" : "Pool member — not promoted")}";
            if (arch != null)
                text += $"\nSpd  {arch.MoveSpeed.ToFloat():0.##}";

            GUI.Box(new Rect(10, 10, 340, 112), text);
        }

        private static Fixed F(float v) => Fixed.FromFraction(Mathf.RoundToInt(v * 100f), 100);
    }
}
