using UnityEngine;

namespace CurtainCall.Player
{
    /// <summary>Surface motion in metres per second, along the marked tread's forward axis.</summary>
    [RequireComponent(typeof(BoxCollider)), DisallowMultipleComponent]
    public sealed class ConveyorSurface : MonoBehaviour
    {
        [SerializeField, Min(0f)] float speed = 2.4f;
        public Vector3 Velocity => transform.forward * speed;
    }
}
