using CurtainCall.UI.SceneIntegration;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace CurtainCall.StartLobby
{
    /// <summary>One rope can slip once per visit. Further clicks only rock the sign.</summary>
    public sealed class StartMenuHangingTitle : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        StartMenu menu;
        RectTransform title, canvas, hitArea;
        TitleRopeGraphic ropes;
        readonly Vector3[] corners = new Vector3[4];
        float leftStress, rightStress, lastPress = -10, nextPress;
        float spring, velocity, drop, dropVelocity;
        bool holding, holdLeft, released, leftReleased;
        public bool HasSlipped => released;
        public bool LeftSlipped => released && leftReleased;
        public float Drop => drop;

        public static void Attach(StartMenu owner)
        {
            var rect = owner.transform.Find("TitleLogo") as RectTransform;
            if (rect == null || rect.GetComponent<StartMenuHangingTitle>() != null) return;
            var effect = rect.gameObject.AddComponent<StartMenuHangingTitle>();
            effect.Initialize(owner, rect);
        }

        void Initialize(StartMenu owner, RectTransform rect)
        {
            menu = owner;
            title = rect;
            canvas = rect.parent as RectTransform;
            title.anchorMin = title.anchorMax = new Vector2(.5f, 1);
            title.pivot = Vector2.one * .5f;
            var normal = title.Find("10_Logo");
            if (normal != null && normal.TryGetComponent<UnityEngine.UI.Image>(out var logo)) logo.color = Color.white;
            var shadow = title.Find("00_Shadow");
            if (shadow != null && shadow.TryGetComponent<UnityEngine.UI.Image>(out var shade))
            {
                shade.color = new Color(.02f, .01f, .015f, .86f);
                shadow.localScale = new Vector3(1.035f, 1.07f, 1);
                ((RectTransform)shadow).anchoredPosition = new Vector2(0, -7);
            }
            // The transparent PNG margins are not a clickable rectangle.
            hitArea = new GameObject("SignHitArea", typeof(RectTransform), typeof(UnityEngine.UI.Image)).GetComponent<RectTransform>();
            hitArea.SetParent(title, false);
            hitArea.anchorMin = new Vector2(.035f, .19f);
            hitArea.anchorMax = new Vector2(.965f, .68f);
            hitArea.offsetMin = hitArea.offsetMax = Vector2.zero;
            hitArea.GetComponent<UnityEngine.UI.Image>().color = Color.clear;
            var prefab = Resources.Load<GameObject>("Prefabs/UIs/StartMenu/TitleSuspensionRopes");
            var ropeObject = prefab != null ? Instantiate(prefab, canvas) : new GameObject("TitleSuspensionRopes", typeof(RectTransform), typeof(TitleRopeGraphic));
            if (prefab == null) ropeObject.transform.SetParent(canvas, false);
            ropes = ropeObject.GetComponent<TitleRopeGraphic>();
            ropes.raycastTarget = false;
            var ropeRect = (RectTransform)ropeObject.transform;
            ropeRect.anchorMin = Vector2.zero;
            ropeRect.anchorMax = Vector2.one;
            ropeRect.offsetMin = ropeRect.offsetMax = Vector2.zero;
            ropeObject.transform.SetSiblingIndex(title.GetSiblingIndex());
        }

        bool CanUse => menu != null && menu.isActiveAndEnabled && menu.mainPage.activeInHierarchy
            && menu.Current == StartMenu.Page.Main && !menu.IsBusy
            && (CommonMenuUI.Current == null || !CommonMenuUI.Current.IsOpen) && !SceneCurtainTransition.IsActive;

        public void OnPointerDown(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left || !CanUse) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(title, e.position, e.pressEventCamera, out var local)) return;
            holding = true;
            holdLeft = local.x < 0;
            Press(holdLeft);
        }

        public void OnPointerUp(PointerEventData e) => holding = false;
        public void OnPointerExit(PointerEventData e) => holding = false;

        void Press(bool left)
        {
            float now = Time.unscaledTime;
            if (now - lastPress > 1.2f) leftStress = rightStress = 0f;
            lastPress = now;
            nextPress = now + .17f;
            velocity = Mathf.Clamp(velocity + (left ? 7f : -7f), -24, 24);
            if (released) return;
            if (left) leftStress++; else rightStress++;
            if ((left ? leftStress : rightStress) < 6) return;
            released = true;
            leftReleased = left;
        }

        void LateUpdate()
        {
            if (title == null || canvas == null) return;
            if (!CanUse || !Application.isFocused || Mouse.current == null || !Mouse.current.leftButton.isPressed) holding = false;
            if (holding && Time.unscaledTime >= nextPress) Press(holdLeft);
            float width = Mathf.Min(1020f, canvas.rect.width * .88f);
            float height = width * .5f;
            title.sizeDelta = new Vector2(width, height);
            float baseY = canvas.rect.yMax - height * .5f + 12f;
            float menuTop = MenuTop();
            float available = Mathf.Max(0, baseY - height * .34f - menuTop - 28f);
            float limit = Mathf.Min(48f, available * .75f);
            float dt = Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);
            float target = released ? limit : 0;
            drop = Mathf.SmoothDamp(drop, target, ref dropVelocity, .2f, Mathf.Infinity, dt);
            velocity += (-30f * spring - 5.5f * velocity) * dt;
            spring = Mathf.Clamp(spring + velocity * dt, -2.4f, 2.4f);
            float t = Time.unscaledTime;
            float slope = Mathf.Atan2(drop, width * .78f) * Mathf.Rad2Deg * (leftReleased ? 1 : -1);
            title.anchoredPosition = new Vector2(Mathf.Sin(t * .48f) * 1.7f, -height * .5f + 12f - drop * .5f);
            title.localRotation = Quaternion.Euler(Mathf.Sin(t * .7f) * 1.5f, Mathf.Sin(t * .57f) * 1.1f,
                slope + spring + Mathf.Sin(t * .66f) * .18f);
            // Re-evaluate the visible artwork envelope after rotation, at every aspect ratio.
            float bottom = float.PositiveInfinity;
            for (int side = -1; side <= 1; side += 2)
            {
                var p = canvas.InverseTransformPoint(title.TransformPoint(new Vector3(width * side * .48f, -height * .34f)));
                bottom = Mathf.Min(bottom, p.y);
            }
            if (bottom < menuTop + 24) title.anchoredPosition += Vector2.up * (menuTop + 24 - bottom);
            Vector2 left = canvas.InverseTransformPoint(title.TransformPoint(new Vector3(-width * .39f, height * .12f)));
            Vector2 right = canvas.InverseTransformPoint(title.TransformPoint(new Vector3(width * .39f, height * .12f)));
            ropes.SetEndpoints(new Vector2(-width * .39f, canvas.rect.yMax + 16), left,
                new Vector2(width * .39f, canvas.rect.yMax + 16), right, t * .6f);
        }

        float MenuTop()
        {
            var main = menu.mainPage.transform as RectTransform;
            main.GetWorldCorners(corners);
            return Mathf.Max(canvas.InverseTransformPoint(corners[1]).y, canvas.InverseTransformPoint(corners[2]).y);
        }

        void OnDisable() => holding = false;
        void OnDestroy() { if (ropes != null) Destroy(ropes.gameObject); }
    }
}
