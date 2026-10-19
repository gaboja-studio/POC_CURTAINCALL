using System;

namespace CurtainCall.Tightrope.Control
{
    /// <summary>착지 직전 한 번 누른 점프와 그때의 옆줄 방향만 기억한다. 공중 궤적은 바꾸지 않는다.</summary>
    public sealed class TightropeJumpBuffer
    {
        bool pending;
        int direction;
        double pressedAt;

        public void Clear() => pending = false;

        /// <summary>접지 때 한 번만 반환한다. 금지된 옆줄 요청도 다시 시도하지 않는다.</summary>
        public bool Resolve(bool pressed, int laneDirection, bool grounded, double now, float window, out int jumpDirection)
        {
            jumpDirection = 0;
            if (pressed)
            {
                pending = true;
                direction = Math.Sign(laneDirection);
                pressedAt = now;
            }
            if (!pending) return false;
            if (now < pressedAt || now - pressedAt > Math.Max(0f, window) || (!grounded && window <= 0f))
            {
                Clear();
                return false;
            }
            if (!grounded) return false;
            jumpDirection = direction;
            Clear();
            return true;
        }
    }
}
