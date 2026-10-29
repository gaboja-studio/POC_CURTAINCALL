using UnityEngine;
using UnityEngine.InputSystem;

namespace CurtainCall.EscapeRoute
{
    public sealed partial class EscapeSoloReview
    {
        int ropeIndex, swingIndex = -1;
        float ropeProgress, swingAngle, swingSpeed;
        bool airFromCannon;
        Vector3 swingOriginalPosition;
        Quaternion swingOriginalRotation;

        bool TryInteract(Keyboard k)
        {
            if (cannonBoarding != null && Vector3.Distance(Feet,cannonBoarding.position) < 3)
            {
                prompt = "E 대포 탑승 → Space 발사";
                if (k.eKey.wasPressedThisFrame) { mode = Mode.Cannon; body.enabled = false; Place(cannonMuzzle.position); return true; }
            }
            for (int i = 0; i < ropeStarts.Length; i++)
            {
                if (Vector3.Distance(Feet,ropeStarts[i].position) > 1.8f) continue;
                prompt = "E 외줄 올라가기 · W/S 전진/후진 · Space 내려가기";
                if (k.eKey.wasPressedThisFrame) { ropeIndex = i; ropeProgress = 0; mode = Mode.Rope; body.enabled = false; Place(ropeStarts[i].position); return true; }
            }
            int near = NearestGrip(-1);
            if (near >= 0)
            {
                prompt = "E 그네 잡기 · W/S 반동 · Space 놓기";
                if (k.eKey.wasPressedThisFrame) { AttachSwing(near); return true; }
            }
            return false;
        }

        int NearestGrip(int exclude)
        {
            int best = -1; float distance = 2.5f;
            for (int i = 0; i < grips.Length; i++)
            {
                if (i == exclude) continue;
                float d = Vector3.Distance(Feet + Vector3.up * 1.5f, grips[i].position);
                if (d < distance) { distance = d; best = i; }
            }
            return best;
        }

        void AttachSwing(int index)
        {
            ReleaseSwingVisual();
            swingIndex = index; mode = Mode.Swing; body.enabled = false;
            float length = Vector3.Distance(grips[index].position,pivots[index].position);
            swingAngle = Mathf.Asin(Mathf.Clamp((Feet.x - pivots[index].position.x) / length,-.6f,.6f));
            swingSpeed = Mathf.Clamp(velocity.x / length,.2f,.8f);
            if (swingVisuals[index] != null) { swingOriginalPosition = swingVisuals[index].position; swingOriginalRotation = swingVisuals[index].rotation; }
        }

        void ReleaseSwingVisual()
        {
            if (swingIndex >= 0 && swingVisuals != null && swingIndex < swingVisuals.Length && swingVisuals[swingIndex] != null)
                swingVisuals[swingIndex].SetPositionAndRotation(swingOriginalPosition,swingOriginalRotation);
            swingIndex = -1;
        }

        void MoveTrick(Keyboard k, float dt)
        {
            if (mode == Mode.Cannon)
            {
                prompt = "Space 발사 · 1 공중 모드";
                if (k.spaceKey.wasPressedThisFrame)
                { velocity = new Vector3(10.9375f,2.848f,0); body.enabled = true; mode = Mode.Air; airFromCannon = true; }
                return;
            }
            if (mode == Mode.Rope)
            {
                prompt = "W/S 외줄 이동 · Space 내려가기";
                var start = ropeStarts[ropeIndex].position; var end = ropeEnds[ropeIndex].position;
                ropeProgress += ((k.wKey.isPressed ? 1 : 0) - (k.sKey.isPressed ? 1 : 0)) * 3.3f * dt / Vector3.Distance(start,end);
                transform.position = Vector3.Lerp(start,end,Mathf.Clamp01(ropeProgress));
                if (k.spaceKey.wasPressedThisFrame || ropeProgress >= 1 || ropeProgress < 0)
                {
                    velocity = Vector3.zero; mode = Mode.Walk; body.enabled = true;
                    if (ropeProgress >= 1) Place(end + (end-start).normalized * .15f + Vector3.up * .05f);
                }
                return;
            }
            if (mode == Mode.Swing)
            {
                prompt = "W/S 반동 · Space 놓기 · 다음 그네 가까이서 E";
                var pivot = pivots[swingIndex].position;
                float length = Vector3.Distance(grips[swingIndex].position,pivot);
                float pump = (k.wKey.isPressed ? 1 : 0) - (k.sKey.isPressed ? 1 : 0);
                swingSpeed += (-gravity / length * Mathf.Sin(swingAngle) + pump * .9f) * dt;
                swingSpeed = Mathf.Clamp(swingSpeed,-1.1f,1.1f);
                swingAngle = Mathf.Clamp(swingAngle + swingSpeed * dt,-.7f,.7f);
                var bar = pivot + new Vector3(Mathf.Sin(swingAngle),-Mathf.Cos(swingAngle),0) * length;
                transform.position = bar - Vector3.up * 1.5f;
                if (swingVisuals[swingIndex] != null)
                {
                    var rotation = Quaternion.AngleAxis(swingAngle * Mathf.Rad2Deg,Vector3.forward);
                    swingVisuals[swingIndex].SetPositionAndRotation(pivot + rotation * (swingOriginalPosition-pivot), rotation * swingOriginalRotation);
                }
                velocity = new Vector3(Mathf.Cos(swingAngle),Mathf.Sin(swingAngle),0) * (length * swingSpeed);
                int next = NearestGrip(swingIndex);
                if (next >= 0 && k.eKey.wasPressedThisFrame) { AttachSwing(next); return; }
                if (k.spaceKey.wasPressedThisFrame)
                {
                    ReleaseSwingVisual(); velocity += Vector3.up * 3;
                    mode = Mode.Air; airFromCannon = true; body.enabled = true;
                }
            }
        }
    }
}
