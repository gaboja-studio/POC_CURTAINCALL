using UnityEngine;

namespace CurtainCall.StartLobby
{
    /// <summary>
    /// 로비에서 내 캐릭터가 가까이 가서 F(상호작용 짧게)로 쓰는 물건. 가장 가까운 하나를 <see cref="LobbyRoom"/>이 골라 안내를 띄운다.
    /// </summary>
    public abstract class LobbyInteractable : MonoBehaviour
    {
        [Tooltip("이 거리(m, 수평) 안에 들어오면 쓸 수 있다.")]
        [SerializeField, Min(0.1f)] float reach = 1.8f;

        public float Reach => reach;

        /// <summary>안내 문구. <paramref name="usable"/>가 false면 회색으로 이유만 보인다.</summary>
        public abstract string Prompt(out bool usable);

        /// <summary>F를 눌렀다(쓸 수 있을 때만 불린다).</summary>
        public abstract void Use();

        public float HorizontalDistance(Vector3 point)
        {
            Vector3 d = point - transform.position;
            d.y = 0f;
            return d.magnitude;
        }
    }
}
