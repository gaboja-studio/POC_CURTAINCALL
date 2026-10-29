using UnityEngine;
using UnityEngine.InputSystem;

namespace CurtainCall.EscapeRoute
{
    /// <summary>Current-scene, offline layout rehearsal. Does not replace the network player or damage rules.</summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed partial class EscapeSoloReview : MonoBehaviour
    {
        public Camera view;
        public Behaviour[] suspendBehaviours;
        public GameObject[] suspendObjects;
        public EscapeSoloHazard[] hazards;
        public Transform cannonMuzzle, cannonBoarding, bounceSurface;
        public Transform[] ropeStarts, ropeEnds, grips, pivots;
        public Transform[] swingVisuals;
        public Vector3 startFeet = new Vector3(15, 0, 10);
        [Min(1)] public float walkSpeed = 4.5f, runSpeed = 7.5f;
        [Min(1)] public float gravity = 9.81f;
        public bool Flying => mode == Mode.Fly;
        public bool Paused => paused || finished;
        public Vector3 Feet => transform.position;
        public CharacterController Body => body;
        enum Mode { Walk, Fly, Cannon, Rope, Swing, Air }
        Mode mode;
        CharacterController body;
        Vector3 velocity, checkpoint;
        float yaw = 0, pitch, immuneUntil, messageUntil;
        bool paused, finished;
        string message = "", prompt = "";
        int failures;

        void Start()
        {
            body = GetComponent<CharacterController>();
            foreach (var b in suspendBehaviours) if (b != null) b.enabled = false;
            foreach (var g in suspendObjects) if (g != null) g.SetActive(false);
            checkpoint = startFeet;
            RestartAt(startFeet);
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || !Application.isFocused) return;
            if (keyboard.escapeKey.wasPressedThisFrame) { paused = !paused; LockCursor(!paused && !finished); }
            if (Paused) return;
            if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame) ToggleFlight();
            if (keyboard.rKey.wasPressedThisFrame) { RestartAt(checkpoint); return; }
            var mouse = Mouse.current;
            if (mouse != null && Cursor.lockState == CursorLockMode.Locked)
            {
                Vector2 look = mouse.delta.ReadValue() * .1f;
                yaw += look.x; pitch = Mathf.Clamp(pitch - look.y, -85, 85);
            }
            float dt = Mathf.Min(Time.deltaTime, .05f);
            prompt = "";
            if (Flying) MoveFly(keyboard, dt);
            else if (mode == Mode.Rope || mode == Mode.Swing || mode == Mode.Cannon) MoveTrick(keyboard, dt);
            else MoveWalking(keyboard, dt);
            if (!Flying)
            {
                UpdateCheckpoint();
                if (Feet.y < (Feet.x > 140 ? -12 : -20)) Fail("추락 — 가까운 구간에서 다시 시작");
                if (Feet.x > 402 && Feet.z > 69 && Feet.z < 87)
                { finished = true; LockCursor(false); }
            }
        }

        void LateUpdate()
        {
            if (view == null) return;
            view.transform.SetPositionAndRotation(Feet + Vector3.up * 1.5f, Quaternion.Euler(pitch, yaw, 0));
        }

        Vector2 Axes(Keyboard k) => new Vector2((k.dKey.isPressed ? 1 : 0) - (k.aKey.isPressed ? 1 : 0),
            (k.wKey.isPressed ? 1 : 0) - (k.sKey.isPressed ? 1 : 0)).normalized;
        Vector3 Wish(Keyboard k) { var a = Axes(k); return Quaternion.Euler(0,yaw,0) * new Vector3(a.x,0,a.y); }

        void MoveWalking(Keyboard k, float dt)
        {
            if (TryInteract(k)) return;
            bool grounded = body.isGrounded;
            if (grounded && velocity.y <= 0) { velocity.y = -2; mode = Mode.Walk; }
            if (!grounded && mode == Mode.Walk) { mode = Mode.Air; airFromCannon = false; }
            Vector3 horizontal;
            if (mode == Mode.Air && airFromCannon) horizontal = new Vector3(velocity.x,0,velocity.z) + Wish(k) * 1.2f;
            else horizontal = Wish(k) * (mode == Mode.Air ? 3 : (k.leftShiftKey.isPressed ? runSpeed : walkSpeed));
            if (grounded && k.spaceKey.wasPressedThisFrame)
            { velocity = horizontal + Vector3.up * 6.5f; mode = Mode.Air; airFromCannon = true; }
            velocity.y -= gravity * dt;
            var before = Feet;
            var flags = body.Move((horizontal + Vector3.up * velocity.y) * dt);
            if ((flags & CollisionFlags.Above) != 0 && velocity.y > 0) velocity.y = 0;
            if (bounceSurface != null && velocity.y < 0 && before.y >= bounceSurface.position.y && Feet.y <= bounceSurface.position.y)
            {
                var flat = Feet - bounceSurface.position; flat.y = 0;
                if (flat.magnitude < 1.9f)
                {
                    Place(new Vector3(Feet.x, bounceSurface.position.y + .04f, Feet.z));
                    velocity = Vector3.up * 12; mode = Mode.Air; airFromCannon = false;
                    Note("반동! E로 가까운 그네 잡기");
                }
            }
        }

        void MoveFly(Keyboard k, float dt)
        {
            var a = Axes(k);
            var direction = Quaternion.Euler(pitch,yaw,0) * new Vector3(a.x,0,a.y);
            direction += Vector3.up * ((k.spaceKey.isPressed ? 1 : 0) - (k.leftCtrlKey.isPressed ? 1 : 0));
            transform.position += Vector3.ClampMagnitude(direction,1) * (k.leftShiftKey.isPressed ? 48 : 12) * dt;
        }

        void ToggleFlight()
        {
            ReleaseSwingVisual();
            if (Flying)
            {
                mode = Mode.Walk; velocity = Vector3.zero;
                bool blocked = false;
                foreach (var c in Physics.OverlapCapsule(Feet + Vector3.up * .34f, Feet + Vector3.up * 1.2f, .3f, ~0, QueryTriggerInteraction.Ignore))
                    if (c != body) blocked = true;
                if (blocked) Place(checkpoint);
                body.enabled = true; immuneUntil = Time.time + 1;
            }
            else { mode = Mode.Fly; body.enabled = false; velocity = Vector3.zero; }
            Note(Flying ? "공중 모드 — 1로 지상 복귀" : "지상 플레이");
        }

        void Place(Vector3 feet) { bool active = body.enabled; body.enabled = false; transform.position = feet; body.enabled = active; }
        void UpdateCheckpoint()
        {
            if (!body.enabled || !body.isGrounded) return;
            var p = Feet;
            if (p.x > 22 && p.x < 32 && p.z > 70) checkpoint = new Vector3(27,0,75);
            if (p.x > 79 && p.x < 90 && p.z < 35) checkpoint = new Vector3(86,0,28);
            if (p.x > 118 && p.x < 128 && p.z > 45 && p.z < 50) checkpoint = new Vector3(123,-8,48);
            if (p.z > 71 && p.z < 85)
            {
                if (p.x >= 145 && p.x < 164) checkpoint = new Vector3(150,0,78);
                if (p.x >= 226 && p.x < 232) checkpoint = new Vector3(228,0,78);
                if (p.x >= 300 && p.x < 315) checkpoint = new Vector3(305,0,78);
                if (p.x >= 366 && p.x < 380) checkpoint = new Vector3(370,0,78);
            }
        }

        public void Fail(string reason)
        {
            if (Flying || Paused || Time.time < immuneUntil) return;
            failures++; RestartAt(checkpoint); Note(reason);
        }
        public void Push(Vector3 displacement) { if (!Flying && !Paused && body.enabled) body.Move(displacement); }
        void RestartAt(Vector3 feet)
        {
            ReleaseSwingVisual();
            mode = Mode.Walk; finished = paused = false; velocity = Vector3.zero; airFromCannon = false;
            body.enabled = false; transform.position = feet + Vector3.up * .05f; body.enabled = true;
            yaw = feet.x < 22 ? 0 : 90; pitch = 0; immuneUntil = Time.time + 1.5f;
            foreach (var h in hazards) if (h != null) h.ResetCycle();
            LockCursor(true);
        }
        void Note(string text) { message = text; messageUntil = Time.time + 3; }
        static void LockCursor(bool locked) { Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None; Cursor.visible = !locked; }
        void OnApplicationFocus(bool focused) { if (!focused) { paused = true; LockCursor(false); } }
        void OnDisable() { ReleaseSwingVisual(); LockCursor(false); }

        void OnGUI()
        {
            GUI.Box(new Rect(12,12,530,76), "A · 혼자 동선 검토" + (Flying ? "  [공중]" : "  [지상]"));
            GUI.Label(new Rect(25,36,510,23), "WASD 이동 · Shift 가속 · Space 점프/놓기 · E 묘기 · 1 공중/지상");
            GUI.Label(new Rect(25,58,510,23), "R 구간 재시작 · Esc 메뉴  |  공중: Space 상승 / Ctrl 하강");
            GUI.Label(new Rect(12,96,620,24), Time.time < messageUntil ? message : prompt);
            if (!Paused) return;
            var box = new Rect(Screen.width / 2f - 180, Screen.height / 2f - 130, 360,260);
            GUI.Box(box, finished ? "탈출!  재시도 " + failures + "회" : "A 레벨 검토 · 일시정지");
            if (!finished && GUI.Button(new Rect(box.x+30,box.y+40,300,32), "계속하기")) { paused = false; LockCursor(true); }
            if (GUI.Button(new Rect(box.x+30,box.y+82,300,32), "처음부터")) { checkpoint = startFeet; RestartAt(checkpoint); }
            if (GUI.Button(new Rect(box.x+30,box.y+124,145,32), "03 대포·외줄")) { checkpoint = new Vector3(27,0,75); RestartAt(checkpoint); }
            if (GUI.Button(new Rect(box.x+185,box.y+124,145,32), "05 묘기 융합")) { checkpoint = new Vector3(86,0,28); RestartAt(checkpoint); }
            if (GUI.Button(new Rect(box.x+30,box.y+166,300,32), "07 마지막 질주")) { checkpoint = new Vector3(150,0,78); RestartAt(checkpoint); }
            GUI.Label(new Rect(box.x+30,box.y+213,310,40), "동선 검토용 1인 조작/피격입니다.\n실제 다인전의 균형·부상 규칙과 별도입니다.");
        }
    }
}
