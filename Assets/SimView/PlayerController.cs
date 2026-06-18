using UnityEngine;
using UnityEngine.InputSystem;
using Perihelion.Sim;

namespace Perihelion.SimView
{
    /// <summary>
    /// Drives ONE human avatar that lives in the deterministic sim. Two jobs, cleanly split:
    ///   1) INPUT (the airlock): every sim tick, SimRunner pulls SampleInput() — the current raw
    ///      intent quantized to Fixed. That quantized Command is the ONLY thing the sim ever sees;
    ///      the float→Fixed conversion happens once, here, on the owning client.
    ///   2) RENDER (feel): predict the avatar's motion at frame rate for responsiveness, then gently
    ///      reconcile toward the authoritative sim position so prediction can't drift.
    ///
    /// This is the view layer, so float/UnityEngine are fine — but it NEVER writes sim state. The sim
    /// avatar (World.GetPlayer) is the authority; this only reads it.
    /// </summary>
    public sealed class PlayerController : MonoBehaviour
    {
        public SimRunner runner;
        public Camera cam;
        [Tooltip("The rendered body. Defaults to this transform.")]
        public Transform avatar;

        [Tooltip("Authoritative player id. In multiplayer, the LOCAL player's id; only the local " +
                 "controller sets SampleLocalInput.")]
        public int playerId = 100;
        public int team = 0;
        [Tooltip("Bitmask of teams this player attacks (bit i = team i). Match your SquadSpawner teams.")]
        public uint hostileMask = 0;
        public float groundHeight = 0f;

        [Tooltip("Visual predict speed (world units/sec). Match the sim: PlayerMoveSpeed × ticksPerSecond " +
                 "(default 0.3 × 10 = 3).")]
        public float renderMoveSpeed = 3f;
        [Tooltip("How hard the rendered avatar is pulled back to the authoritative sim position each frame.")]
        [Range(0f, 1f)] public float reconcile = 0.15f;

        // Latest raw intent, refreshed every frame; committed once per tick by SampleInput().
        private Vector2 _move;
        private Vector2 _aim = Vector2.up;
        private bool _fire;

        void Awake()
        {
            if (runner == null) runner = FindFirstObjectByType<SimRunner>();
            if (cam == null) cam = Camera.main;
            if (avatar == null) avatar = transform;
        }

        void Start()
        {
            if (runner == null || runner.World == null)
            {
                Debug.LogError("PlayerController needs a SimRunner with an initialized World.");
                enabled = false;
                return;
            }

            // Spawn the authoritative avatar into the sim at the rendered start position.
            // SEAM: in multiplayer, spawn ALL players from the lobby/handshake in a client-identical
            // order; only the LOCAL controller hooks SampleLocalInput.
            Vector3 s = avatar.position;
            runner.World.AddPlayer(new Player(playerId, new FixedVec2(F(s.x), F(s.z)))
            {
                Team = team,
                HostileMask = hostileMask
            });
            runner.SampleLocalInput = SampleInput;
        }

        void Update()
        {
            if (runner == null || runner.World == null) return;

            ReadInput();   // 1) sample raw input (float, frame rate — the feel layer)

            // 2) local prediction: move the rendered avatar now, at frame rate, for responsiveness.
            Vector3 mv = new Vector3(_move.x, 0f, _move.y);
            if (mv.sqrMagnitude > 1f) mv.Normalize();
            avatar.position += mv * (renderMoveSpeed * Time.deltaTime);

            // 3) reconcile toward the authoritative sim position (kills prediction drift).
            Player p = runner.World.GetPlayer(playerId);
            if (p != null)
            {
                Vector3 simPos = p.Pos.ToWorld(groundHeight);
                avatar.position = Vector3.Lerp(avatar.position, simPos, reconcile);
            }
        }

        private void ReadInput()
        {
            Keyboard k = Keyboard.current;
            Mouse m = Mouse.current;

            float x = 0f, y = 0f;
            if (k != null)
            {
                if (k.aKey.isPressed) x -= 1f;
                if (k.dKey.isPressed) x += 1f;
                if (k.sKey.isPressed) y -= 1f;
                if (k.wKey.isPressed) y += 1f;
            }
            _move = new Vector2(x, y);

            _fire = m != null && m.leftButton.isPressed;

            // Aim = from the avatar toward the cursor's ground point (XZ plane → sim XY).
            if (m != null && cam != null)
            {
                Plane ground = new Plane(Vector3.up, new Vector3(0f, groundHeight, 0f));
                Ray ray = cam.ScreenPointToRay(m.position.ReadValue());
                if (ground.Raycast(ray, out float enter))
                {
                    Vector3 d = ray.GetPoint(enter) - avatar.position;
                    d.y = 0f;
                    if (d.sqrMagnitude > 1e-4f) _aim = new Vector2(d.x, d.z).normalized;
                }
            }
        }

        // The airlock: SimRunner calls this exactly once per tick. Quantize the current raw intent to
        // Fixed and hand back a Command. Returning null skips input for this tick.
        private Command? SampleInput()
        {
            uint buttons = 0;
            if (_fire) buttons |= (uint)PlayerButton.Fire;

            return new Command
            {
                PlayerId = playerId,
                Move = new FixedVec2(F(_move.x), F(_move.y)),
                Aim  = new FixedVec2(F(_aim.x), F(_aim.y)),
                Buttons = buttons
                // Kind + IssueTick are stamped by SimRunner.
            };
        }

        // Setup-time / per-tick float→Fixed quantizer (cm precision), matching the rest of the view.
        private static Fixed F(float v) => Fixed.FromFraction(Mathf.RoundToInt(v * 100f), 100);
    }
}
