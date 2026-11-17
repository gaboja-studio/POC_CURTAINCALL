using System.Collections.Generic;
using CurtainCall.Cameras;
using CurtainCall.HumanCannon;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.Settings;
using UnityEngine;

namespace CurtainCall.WorldIndicators
{
    public sealed class CannonTrajectoryPresenter : MonoBehaviour
    {
        readonly List<Vector3> points=new(256);
        readonly RaycastHit[] hits=new RaycastHit[32];
        TrajectoryIndicatorView view;
        static CannonTrajectoryPresenter current;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatic() { current=null; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if(current) return;
            var go=new GameObject("Cannon trajectory (common)"); DontDestroyOnLoad(go);
            current=go.AddComponent<CannonTrajectoryPresenter>();
        }
        void LateUpdate()
        {
            var settings=GameSettings.WorldUi;
            var player=CameraSpectator.IsSpectating && settings.cannonForSpectator?CameraSpectator.Target:NetworkPlayer.Local;
            var system=HumanCannonSystem.Current;
            var station=player && system?system.SeatedStation(player.OwnerClientId):null;
            if(!settings.enabled || !station || !player || player.State!=PlayerState.Normal) { view?.Hide(); return; }
            if(!view) view=WorldIndicatorFactory.Trajectory(transform);
            if(!view) return;
            Vector3 position=station.Muzzle.position,velocity=station.LaunchVelocity;
            points.Clear(); points.Add(position); float distance=0; bool blocked=false;
            float dt=Mathf.Clamp(settings.cannonStepSeconds,.01f,.15f),limit=Mathf.Max(.2f,settings.cannonArcLength);
            var mover=player.GetComponent<PlayerMover>();
            for(int i=0;i<Mathf.Clamp(settings.cannonMaxSamples,32,512) && distance<limit;i++)
            {
                Vector3 wind=HumanCannonHazardCourse.Current?HumanCannonHazardCourse.Current.WindVelocity(position,mover?mover.BodyHeight:1.7f,true):Vector3.zero;
                Vector3 step=BallisticMotion.Step(ref velocity,wind,GameSettings.Base.Character.Gravity,dt);
                float length=step.magnitude; if(length<.00001f) continue;
                if(distance+length>limit) { step*= (limit-distance)/length; length=step.magnitude; }
                // Preview stops at a physical barrier; seated player and firing station are not barriers.
                int n=Physics.RaycastNonAlloc(position,step.normalized,hits,length,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
                float nearest=length;
                if(n==hits.Length) { blocked=true; break; }
                for(int h=0;h<n;h++)
                {
                    var tr=hits[h].transform;
                    if(tr.IsChildOf(station.transform) || tr.GetComponentInParent<NetworkPlayer>()) continue;
                    if(hits[h].distance<=nearest) { nearest=hits[h].distance; blocked=true; }
                }
                position+=step.normalized*nearest; distance+=nearest; points.Add(position);
                if(blocked) break;
            }
            view.Show(points,blocked);
        }
        void OnDisable() { view?.Hide(); }
    }
}
