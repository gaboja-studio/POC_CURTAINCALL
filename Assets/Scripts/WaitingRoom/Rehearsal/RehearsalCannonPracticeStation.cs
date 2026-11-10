using System.Collections.Generic;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>F on the original unlocked practice prop starts the same cannon lesson again.</summary>
    public sealed class RehearsalCannonPracticeStation : MonoBehaviour, IShopTarget, IShopPresentation
    {
        static readonly HashSet<RehearsalCannonPracticeStation> all = new();
        CurtainCall.HumanCannon.HumanCannonStation cannon;
        Renderer visual;
        Vector3 Point => cannon != null ? cannon.BoardingPoint.position : visual != null ? visual.bounds.center : transform.position;
        public Transform VisualRoot => cannon != null ? cannon.transform : transform;
        public Vector3 PresentationPoint => Point + Vector3.up;
        public string ActionLabel => WaitingRoomText.Get("cannon-rehearsal.station", "인간대포 리허설 시작");
        public bool IsAvailable => isActiveAndEnabled && WaitingRoomCannonRehearsal.CanPractice;
        void OnEnable() { cannon = GetComponentInChildren<CurtainCall.HumanCannon.HumanCannonStation>(true); visual = GetComponentInChildren<Renderer>(true); all.Add(this); ShopReach.Add(this); }
        void OnDisable() { all.Remove(this); ShopReach.Remove(this); }
        void Update() => ShopReach.Pump();
        public float Reach(Vector3 feet, Component actor = null)
        {
            return IsAvailable ? ExplorationReach.From(VisualRoot, Point, feet,
                CurtainCall.Settings.GameSettings.Base.Interaction.Distance, actor: actor) : -1f;
        }
        public void Interact() { if (IsAvailable) WaitingRoomCannonRehearsal.RequestPractice(); }
        public static bool IsNear(Vector3 feet)
        {
            foreach (var station in all) if (station != null && station.Reach(feet) >= 0f) return true;
            return false;
        }
    }
}
