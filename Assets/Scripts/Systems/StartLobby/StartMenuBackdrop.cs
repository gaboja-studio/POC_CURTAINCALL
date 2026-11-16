using System.Collections.Generic;
using CurtainCall.UI.SceneIntegration;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CurtainCall.StartLobby
{
    /// <summary>Start 전용 장식. 기존 씬/빌더의 무대를 런타임에 연결하며 원본 에셋은 변경하지 않는다.</summary>
    public sealed class StartMenuBackdrop : MonoBehaviour
    {
        readonly List<Curtain> curtains = new();
        readonly List<Collider> chairColliders = new();
        readonly List<RaycastResult> uiHits = new();
        readonly List<Component> ownedComponents = new();
        readonly List<GameObject> ownedObjects = new();
        StartMenu menu;
        Camera view;
        Rigidbody chair;
        Vector3 chairPosition;
        Quaternion chairRotation;
        Bounds chairArea;
        bool hasChairArea;
        Light pin;
        float pinIntensity, nextFlicker, flickerStarted = -1f;
        float nextPush, lastPush = -10f, pushChain;
        bool pointerArmed;
        Mesh foldTemplate;
        PhysicsMaterial chairMaterial;
        PointerEventData pointer;
        EventSystem pointerEvents;

        public static void Attach(StartMenu owner)
        {
            // Scope lookup to this scene; additive lobbies must never acquire these effects.
            foreach (var root in owner.gameObject.scene.GetRootGameObjects())
            {
                if (root.name != "Start Stage") continue;
                var effect = root.GetComponent<StartMenuBackdrop>();
                if (effect != null) return;
                effect = root.AddComponent<StartMenuBackdrop>();
                effect.Initialize(owner);
                return;
            }
        }

        void Initialize(StartMenu owner)
        {
            menu = owner;
            foreach (var root in owner.gameObject.scene.GetRootGameObjects())
                foreach (var camera in root.GetComponentsInChildren<Camera>())
                    if (camera.CompareTag("MainCamera")) view = camera;

            foldTemplate = CreateFoldMesh();
            foreach (var name in new[] { "Back Curtain", "Drape Left", "Drape Right" })
            {
                var root = transform.Find(name);
                if (root != null) curtains.Add(new Curtain(root, foldTemplate));
            }

            var spot = transform.Find("Pin Spot");
            pin = spot != null ? spot.GetComponent<Light>() : null;
            if (pin != null) pinIntensity = pin.intensity;
            nextFlicker = Time.unscaledTime + Random.Range(18f, 35f);
            SetupChair();
        }

        void SetupChair()
        {
            var floor = transform.Find("Stage Floor");
            Transform prop = null;
            foreach (Transform child in transform)
                if (child.name.StartsWith("SM_Prop_Chair_Folding_01", System.StringComparison.Ordinal)) prop = child;
            if (floor == null || prop == null || prop.GetComponent<Rigidbody>() != null) return;
            var meshes = prop.GetComponentsInChildren<MeshFilter>();
            if (meshes.Length == 0) return;

            chairMaterial = new PhysicsMaterial("Start chair friction")
            {
                dynamicFriction = 0.32f, staticFriction = 0.42f,
                bounciness = 0.02f, frictionCombine = PhysicsMaterialCombine.Minimum
            };
            // The scene builder removes prop colliders. Convex hulls support the chair on
            // its actual feet and allow AddForceAtPosition to produce off-centre torque.
            foreach (var mesh in meshes)
            {
                if (mesh.sharedMesh == null) continue;
                var collider = mesh.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = mesh.sharedMesh;
                collider.convex = true;
                collider.sharedMaterial = chairMaterial;
                chairColliders.Add(collider);
                ownedComponents.Add(collider);
            }
            if (chairColliders.Count == 0) return;
            if (floor.GetComponent<Collider>() == null) ownedComponents.Add(floor.gameObject.AddComponent<BoxCollider>());
            var floorRenderer = floor.GetComponent<Renderer>();
            if (floorRenderer != null)
            {
                var b = floorRenderer.bounds;
                float left = b.min.x + .15f, right = b.max.x - .15f, rear = b.max.z - .15f;
                // Reserve the full animated fabric envelope, including side drapes.
                foreach (var renderer in transform.GetComponentsInChildren<Renderer>())
                {
                    var parent = renderer.transform.parent;
                    if (parent == null) continue;
                    if (parent.name == "Back Curtain") rear = Mathf.Min(rear, renderer.bounds.min.z - .65f);
                    if (parent.name == "Drape Left") left = Mathf.Max(left, renderer.bounds.max.x + .2f);
                    if (parent.name == "Drape Right") right = Mathf.Min(right, renderer.bounds.min.x - .2f);
                }
                chairArea.SetMinMax(new Vector3(left, b.max.y, b.min.z + .2f), new Vector3(right, b.max.y + 8f, rear));
                hasChairArea = true;
                var a = chairArea;
                AddEdge(new Vector3(a.min.x, a.center.y, a.center.z), new Vector3(.2f, 8f, a.size.z));
                AddEdge(new Vector3(a.max.x, a.center.y, a.center.z), new Vector3(.2f, 8f, a.size.z));
                AddEdge(new Vector3(a.center.x, a.center.y, a.min.z), new Vector3(a.size.x, 8f, .2f));
                AddEdge(new Vector3(a.center.x, a.center.y, a.max.z), new Vector3(a.size.x, 8f, .2f));
            }
            chairPosition = prop.position;
            chairRotation = prop.rotation;
            chair = prop.gameObject.AddComponent<Rigidbody>();
            ownedComponents.Add(chair);
            chair.mass = 2.5f;
            chair.linearDamping = 0.6f;
            chair.angularDamping = 0.9f;
            chair.maxAngularVelocity = 6f;
            chair.interpolation = RigidbodyInterpolation.Interpolate;
            chair.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }

        void AddEdge(Vector3 position, Vector3 size)
        {
            var edge = new GameObject("Backdrop chair boundary");
            edge.transform.SetParent(transform, false);
            edge.transform.position = position;
            edge.AddComponent<BoxCollider>().size = size;
            ownedObjects.Add(edge);
        }

        void Update()
        {
            float now = Time.unscaledTime;
            // Decoration continues when a menu pauses scaled gameplay time.
            float dt = Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);
            foreach (var curtain in curtains) curtain.Tick(now, dt);
            UpdateLight(now);
            HandlePointer(now);
        }

        void LateUpdate()
        {
            if (!hasChairArea || chair == null || chairColliders.Count == 0) return;
            // Guard the whole rotated chair, not just its pivot, even after a strong impulse.
            var bounds = chairColliders[0].bounds;
            foreach (var collider in chairColliders) if (collider != null) bounds.Encapsulate(collider.bounds);
            float dx = bounds.min.x < chairArea.min.x ? chairArea.min.x - bounds.min.x :
                bounds.max.x > chairArea.max.x ? chairArea.max.x - bounds.max.x : 0f;
            float dz = bounds.min.z < chairArea.min.z ? chairArea.min.z - bounds.min.z :
                bounds.max.z > chairArea.max.z ? chairArea.max.z - bounds.max.z : 0f;
            if (dx == 0f && dz == 0f) return;
            chair.position += new Vector3(dx, 0f, dz);
            var velocity = chair.linearVelocity;
            if (dx * velocity.x < 0f) velocity.x = 0f;
            if (dz * velocity.z < 0f) velocity.z = 0f;
            chair.linearVelocity = velocity;
        }

        void UpdateLight(float now)
        {
            if (pin == null) return;
            if (flickerStarted < 0f && now >= nextFlicker) flickerStarted = now;
            if (flickerStarted < 0f) return;
            float elapsed = now - flickerStarted;
            // One short double dip, never a full blackout or a continuous strobe.
            pin.intensity = pinIntensity * (elapsed < .055f ? .48f : elapsed < .13f ? .95f : elapsed < .20f ? .65f : 1f);
            if (elapsed < .28f) return;
            pin.intensity = pinIntensity;
            flickerStarted = -1f;
            nextFlicker = now + Random.Range(25f, 55f);
        }

        bool CanInteract => menu != null && menu.isActiveAndEnabled && menu.Current == StartMenu.Page.Main
            && !menu.IsBusy && (CommonMenuUI.Current == null || !CommonMenuUI.Current.IsOpen)
            && !SceneCurtainTransition.IsActive && Application.isFocused;

        bool OverUI(Vector2 screen)
        {
            var events = EventSystem.current;
            if (events == null) return false;
            if (pointerEvents != events)
            {
                pointerEvents = events;
                pointer = new PointerEventData(events);
            }
            pointer.Reset();
            pointer.position = screen;
            uiHits.Clear();
            events.RaycastAll(pointer, uiHits);
            foreach (var hit in uiHits)
                if (hit.module is GraphicRaycaster) return true;
            return false;
        }

        void HandlePointer(float now)
        {
            var mouse = Mouse.current;
            if (mouse == null || view == null || !CanInteract)
            {
                pointerArmed = false;
                pushChain = 0f;
                return;
            }
            Vector2 screen = mouse.position.ReadValue();
            bool blocked = !view.pixelRect.Contains(screen) || OverUI(screen);
            // Presses begun on UI never drag through into background interactions.
            if (mouse.leftButton.wasPressedThisFrame) pointerArmed = !blocked;
            if (!mouse.leftButton.isPressed || blocked) pointerArmed = false;
            if (!pointerArmed || (!mouse.leftButton.wasPressedThisFrame && now < nextPush)) return;
            nextPush = now + .14f;

            var ray = view.ScreenPointToRay(screen);
            float nearest = view.farClipPlane;
            RaycastHit chairHit = default;
            bool hitChair = false;
            foreach (var collider in chairColliders)
            {
                if (collider == null || !collider.Raycast(ray, out var hit, nearest)) continue;
                nearest = hit.distance;
                chairHit = hit;
                hitChair = true;
            }
            Curtain selected = null;
            Vector3 point = default;
            foreach (var curtain in curtains)
            {
                if (!curtain.Hit(ray, out var distance, out var local) || distance >= nearest) continue;
                nearest = distance;
                selected = curtain;
                point = local;
                hitChair = false;
            }
            if (hitChair && chair != null)
            {
                pushChain = now - lastPush < .65f ? Mathf.Min(pushChain + 1f, 7f) : 0f;
                lastPush = now;
                var direction = Vector3.ProjectOnPlane(ray.direction, Vector3.up).normalized;
                chair.AddForceAtPosition(direction * Mathf.Lerp(.85f, 4.5f, pushChain / 7f), chairHit.point, ForceMode.Impulse);
            }
            else if (selected != null)
            {
                pushChain = 0f;
                selected.Push(point);
            }
        }

        void OnDisable()
        {
            pointerArmed = false;
            if (pin != null) pin.intensity = pinIntensity;
        }

        void OnDestroy()
        {
            foreach (var curtain in curtains) curtain.Dispose();
            if (pin != null) pin.intensity = pinIntensity;
            if (chair != null)
            {
                chair.isKinematic = true;
                chair.transform.SetPositionAndRotation(chairPosition, chairRotation);
            }
            foreach (var component in ownedComponents) if (component != null) Destroy(component);
            foreach (var obj in ownedObjects) if (obj != null) Destroy(obj);
            if (foldTemplate != null) Destroy(foldTemplate);
            if (chairMaterial != null) Destroy(chairMaterial);
        }

        const int Rows = 16, Sides = 20;
        // The original cylinders only have top/bottom rings. Extra rings let a click
        // deform the fabric at its own height instead of moving a rigid column.
        static Mesh CreateFoldMesh()
        {
            var vertices = new Vector3[(Rows + 1) * (Sides + 1)];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[Rows * Sides * 6];
            for (int row = 0; row <= Rows; row++)
                for (int side = 0; side <= Sides; side++)
                {
                    int index = row * (Sides + 1) + side;
                    float angle = side * Mathf.PI * 2f / Sides;
                    vertices[index] = new Vector3(Mathf.Cos(angle) * .5f, -1f + 2f * row / Rows, Mathf.Sin(angle) * .5f);
                    uv[index] = new Vector2((float)side / Sides, (float)row / Rows);
                    if (row == Rows || side == Sides) continue;
                    int t = (row * Sides + side) * 6;
                    triangles[t] = index;
                    triangles[t + 1] = index + Sides + 1;
                    triangles[t + 2] = index + 1;
                    triangles[t + 3] = index + 1;
                    triangles[t + 4] = index + Sides + 1;
                    triangles[t + 5] = index + Sides + 2;
                }
            var mesh = new Mesh { name = "Start curtain flexible fold", vertices = vertices, uv = uv, triangles = triangles };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        sealed class Curtain
        {
            readonly Transform root;
            readonly List<Fold> folds = new();
            readonly Bounds bounds;

            public Curtain(Transform root, Mesh template)
            {
                this.root = root;
                bool first = true;
                foreach (Transform child in root)
                {
                    if (child.name != "Fold" || child.GetComponent<MeshFilter>() == null) continue;
                    folds.Add(new Fold(child, template));
                    var b = new Bounds(child.localPosition, child.localScale);
                    // Unity's cylinder is two units high.
                    b.size = new Vector3(child.localScale.x, child.localScale.y * 2f, child.localScale.z);
                    if (first) { bounds = b; first = false; }
                    else { var combined = bounds; combined.Encapsulate(b); bounds = combined; }
                }
            }

            public bool Hit(Ray ray, out float distance, out Vector3 local)
            {
                local = default;
                var plane = new Plane(root.forward, root.position);
                if (!plane.Raycast(ray, out distance)) return false;
                local = root.InverseTransformPoint(ray.GetPoint(distance));
                return local.x >= bounds.min.x && local.x <= bounds.max.x && local.y >= bounds.min.y && local.y <= bounds.max.y;
            }

            public void Push(Vector3 local)
            {
                foreach (var fold in folds) fold.Push(local);
            }

            public void Tick(float now, float dt)
            {
                foreach (var fold in folds) fold.Tick(now, dt);
            }

            public void Dispose()
            {
                foreach (var fold in folds) fold.Dispose();
            }
        }

        sealed class Fold
        {
            readonly Transform transform;
            readonly MeshFilter filter;
            readonly Mesh original, mesh;
            readonly Vector3[] rest, vertices;
            readonly float[] offset = new float[Rows + 1], velocity = new float[Rows + 1];
            readonly Vector3 center, scale;

            public Fold(Transform transform, Mesh template)
            {
                this.transform = transform;
                filter = transform.GetComponent<MeshFilter>();
                original = filter.sharedMesh;
                mesh = Instantiate(template);
                mesh.MarkDynamic();
                filter.sharedMesh = mesh;
                rest = template.vertices;
                vertices = new Vector3[rest.Length];
                center = transform.localPosition;
                scale = transform.localScale;
            }

            public void Push(Vector3 point)
            {
                float dx = (point.x - center.x) / .7f;
                if (Mathf.Abs(dx) > 3f) return;
                for (int row = 0; row < Rows; row++)
                {
                    float y = center.y + (-1f + 2f * row / Rows) * scale.y;
                    float dy = (point.y - y) / 1.1f;
                    float weight = Mathf.Exp(-(dx * dx + dy * dy) * .5f);
                    velocity[row] = Mathf.Min(velocity[row] + weight * 2.4f, 3.5f);
                }
            }

            public void Tick(float now, float dt)
            {
                if (filter == null || transform == null) return;
                for (int row = 0; row <= Rows; row++)
                {
                    float hanging = 1f - (float)row / Rows;
                    velocity[row] += (-24f * offset[row] - 4.5f * velocity[row]) * dt;
                    offset[row] = Mathf.Clamp(offset[row] + velocity[row] * dt, -.4f, .4f);
                    float phase = center.x * 1.3f + hanging * 2.2f;
                    float breeze = Mathf.Sin(now * .85f + phase) * .025f + Mathf.Sin(now * .47f - phase * .7f) * .012f;
                    float z = (breeze * hanging + offset[row] * Mathf.SmoothStep(0f, 1f, hanging * 4f)) / scale.z;
                    float x = Mathf.Sin(now * .61f + phase) * .012f * hanging / scale.x;
                    for (int side = 0; side <= Sides; side++)
                    {
                        int i = row * (Sides + 1) + side;
                        vertices[i] = rest[i] + new Vector3(x, 0f, z);
                    }
                }
                mesh.vertices = vertices;
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
            }

            public void Dispose()
            {
                if (filter != null) filter.sharedMesh = original;
                if (mesh != null) Destroy(mesh);
            }
        }
    }
}
