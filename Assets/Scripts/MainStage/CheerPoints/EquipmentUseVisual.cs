using CurtainCall.Network.PlayerSync;
using CurtainCall.Settings;
using CurtainCall.WaitingRoom;
using UnityEngine;

namespace CurtainCall.MainStage.CheerPoints
{
    /// <summary>Cosmetic, server-confirmed effects. Never owns movement, collision or durability.</summary>
    public sealed class EquipmentUseVisual : MonoBehaviour
    {
        public Transform model;
        public LineRenderer[] strokes;
        public ParticleSystem[] motes;
        public Color tint = Color.white;
        NetworkPlayer actor;
        Animator animator;
        string item;
        Vector3 origin, destination, modelScale;
        Transform rescueTarget;
        float began, duration, ended = -1;
        const float Tail = .28f;

        public static void ShowRescue(NetworkPlayer player,Transform target,Vector3 destination,float seconds)
        {
            var prefab=GameSettings.PerformanceEquipment.UseVisual(ItemUses.RescueGloves);
            if(!player || !target || !prefab || !prefab.GetComponent<EquipmentUseVisual>())return;
            var effect=Instantiate(prefab).GetComponent<EquipmentUseVisual>();
            effect.actor=player;effect.item=ItemUses.RescueGloves;effect.rescueTarget=target;
            effect.origin=target.position;effect.destination=destination;effect.duration=seconds+.2f;effect.began=Time.unscaledTime;
            effect.animator=player.GetComponentInChildren<Animator>();effect.LateUpdate();
        }

        public static void Show(NetworkPlayer player, string id, float seconds, Vector3 origin = default, Vector3 destination = default)
        {
            var prefab = GameSettings.PerformanceEquipment.UseVisual(id);
            if (!player || !prefab || !prefab.GetComponent<EquipmentUseVisual>()) return;
            var effect = Instantiate(prefab).GetComponent<EquipmentUseVisual>();
            effect.actor = player; effect.item = id; effect.duration = Mathf.Max(.1f, seconds);
            effect.began = Time.unscaledTime; effect.origin = id == ItemUses.RescueGloves ? origin : player.transform.position;
            effect.destination = destination;
            effect.animator = player.GetComponentInChildren<Animator>();
            if (effect.model) effect.modelScale = effect.model.localScale;
            effect.LateUpdate();
        }

        Vector3 Hand(bool right)
        {
            if (animator && animator.isHuman)
            {
                var bone = animator.GetBoneTransform(right ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
                if (bone) return bone.position;
            }
            return actor.transform.TransformPoint(new Vector3(right ? .28f : -.28f, 1.2f, .15f));
        }

        void LateUpdate()
        {
            if (!actor || actor.State != PlayerState.Normal) { Destroy(gameObject); return; }
            float elapsed = Time.unscaledTime - began;
            bool sustained = item == ItemUses.StickyGloves || item == ItemUses.BalloonPants;
            bool active = item == ItemUses.StickyGloves ? actor.StickyEquipmentActive : actor.BalloonEquipmentActive;
            // A short grace period allows network variables to catch up with the visual RPC on remote peers.
            if (ended < 0 && (elapsed >= duration || sustained && elapsed > .12f && !active))
            {
                ended = Time.unscaledTime;
                foreach (var ps in motes) if (ps) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
            float fade = ended < 0 ? 1 : Mathf.Clamp01(1 - (Time.unscaledTime - ended) / Tail);
            if (fade <= 0) { Destroy(gameObject); return; }
            float t = Mathf.Clamp01(elapsed / duration);
            var feet = actor.transform.position;
            transform.SetPositionAndRotation(feet, actor.transform.rotation);
            foreach (var line in strokes) { var c = tint; c.a *= fade; line.startColor = line.endColor = c; }
            if (item == ItemUses.SpringShoes)
            {
                // The take-off ring stays in the world while the actor rises away from it.
                Circle(strokes[0], origin + Vector3.up * .06f, Vector3.up, .25f + elapsed * 1.6f, 1, elapsed);
                Circle(strokes[1], origin + Vector3.up * (.1f + elapsed * .7f), Vector3.up, .12f + elapsed, 1, -elapsed);
                foreach (var ps in motes) ps.transform.position = origin;
                foreach (var line in strokes) line.widthMultiplier = .06f * (1 - t) * fade;
            }
            else if (item == ItemUses.RescueGloves)
            {
                var hand = Hand(true); var from = (rescueTarget ? rescueTarget.position : origin) + Vector3.up * .75f;
                var to = destination + Vector3.up * .75f;
                for (int i = 0; i < strokes[0].positionCount; i++)
                {
                    float u = (float)i / (strokes[0].positionCount - 1);
                    var p = Vector3.Lerp(from, hand, u) + Vector3.up * (Mathf.Sin(u * Mathf.PI) * .45f);
                    strokes[0].SetPosition(i, p + Vector3.right * (Mathf.Sin(u * 36 - elapsed * 28) * .035f));
                }
                strokes[0].widthMultiplier = .04f * fade;
                Circle(strokes[1], destination + Vector3.up * .07f, Vector3.up, .52f + .12f * t, 1, 0);
                Circle(strokes[2], from, Vector3.up, .25f, 1, 0);
                foreach (var ps in motes) ps.transform.position = destination + Vector3.up * .4f;
            }
            else if (item == ItemUses.StickyGloves)
            {
                for (int i = 0; i < 2; i++)
                {
                    Vector3 hand = Hand(i == 1);
                    Circle(strokes[i], hand, actor.transform.forward, .16f + .015f * Mathf.Sin(elapsed * 9), 1 - t, elapsed * .2f);
                    if (i < motes.Length) motes[i].transform.position = hand;
                }
            }
            else
            {
                float inflate = Mathf.SmoothStep(0, 1, elapsed / .2f) * fade;
                if (model) { model.localPosition = Vector3.up * .65f; model.localScale = Vector3.Scale(modelScale, new Vector3(inflate, .85f + .15f * inflate, inflate)); }
                for (int j = 0; j < strokes.Length; j++)
                    for (int i = 0; i < strokes[j].positionCount; i++)
                    {
                        float u = (float)i / (strokes[j].positionCount - 1);
                        float a = u * 3.6f + elapsed * 2 + j * Mathf.PI;
                        var p = feet + new Vector3(Mathf.Cos(a) * (.58f + .12f * u), .05f + u * 1.25f, Mathf.Sin(a) * (.58f + .12f * u));
                        strokes[j].SetPosition(i, p);
                    }
            }
        }

        static void Circle(LineRenderer line, Vector3 center, Vector3 normal, float radius, float portion, float phase)
        {
            var rotation = Quaternion.FromToRotation(Vector3.forward, normal);
            for (int i = 0; i < line.positionCount; i++)
            {
                float a = phase + (float)i / (line.positionCount - 1) * Mathf.PI * 2 * portion;
                line.SetPosition(i, center + rotation * new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * radius);
            }
        }
    }
}
