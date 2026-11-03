using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CurtainCall.WaitingRoom.Pursuit.Editor
{
    /// <summary>Concepts/01..07: character-space joint directions and planted-foot two-bone IK.
    /// Imported bone-local axes are never assumed to be anatomical flexion axes.</summary>
    public static class PursuitMotionBuilder
    {
        static readonly string[] Names = { "Hips", "Chest", "Head", "UpperArm_L", "Forearm_L", "Hand_L",
            "UpperArm_R", "Forearm_R", "Hand_R", "Thigh_L", "Shin_L", "Foot_L", "Thigh_R", "Shin_R", "Foot_R" };
        public static float Duration(PursuitMotion m) => m == PursuitMotion.Run ? .58f : m == PursuitMotion.Walk || m == PursuitMotion.Grab ? 1.2f
            : m == PursuitMotion.Awaken || m == PursuitMotion.Display ? 2 : m == PursuitMotion.Tear ? 1.3f : 1;

        public static AnimationClip Build(GameObject root, PursuitMotion motion, bool advanced = false)
        {
            var bones = root.GetComponentsInChildren<Transform>().Where(t => Names.Contains(t.name)).ToDictionary(t => t.name);
            var bindP = bones.ToDictionary(p => p.Key, p => p.Value.localPosition);
            var bindQ = bones.ToDictionary(p => p.Key, p => p.Value.localRotation);
            var curves = bones.ToDictionary(p => p.Key, p => Enumerable.Range(0, 7).Select(_ => new AnimationCurve()).ToArray());
            var feet = new[] { bones["Foot_L"].position, bones["Foot_R"].position };
            var upper = new[] { Vector3.Distance(bones["Thigh_L"].position,bones["Shin_L"].position), Vector3.Distance(bones["Thigh_R"].position,bones["Shin_R"].position) };
            var lower = new[] { Vector3.Distance(bones["Shin_L"].position,feet[0]), Vector3.Distance(bones["Shin_R"].position,feet[1]) };
            var clip = new AnimationClip { name = motion.ToString(), legacy = true, frameRate = 60,
                wrapMode = motion == PursuitMotion.Awaken || motion == PursuitMotion.Grab ? WrapMode.ClampForever : WrapMode.Loop };
            float duration = Duration(motion) * (advanced ? .86f : 1);
            int frames = Mathf.CeilToInt(duration * 60);
            var right = root.transform.right; var up = root.transform.up; var forward = root.transform.forward;
            Quaternion Basis(Vector3 direction) {
                direction.Normalize();
                var front = forward - Vector3.Project(forward, direction);
                if (front.sqrMagnitude < .0001f) front = up - Vector3.Project(up,direction);
                return Quaternion.LookRotation(front.normalized, direction);
            }
            void Flex(string name, float angle, Vector3 axis, Vector3 neutral) => bones[name].rotation = Quaternion.AngleAxis(angle, axis) * Basis(neutral);
            for (int frame = 0; frame <= frames; frame++) {
                float t = frame / (float)frames, phase = t * Mathf.PI * 2;
                foreach (var pair in bones) { pair.Value.localPosition = bindP[pair.Key]; pair.Value.localRotation = bindQ[pair.Key]; }
                bool display = motion == PursuitMotion.Display || motion == PursuitMotion.Twitch;
                if (display) {
                    float jerk = motion == PursuitMotion.Twitch ? Pulse(t, .18f, .24f, .52f) : 0;
                    bones["Head"].rotation = Quaternion.AngleAxis(jerk * 12, up) * Quaternion.AngleAxis(Mathf.Sin(phase) * (motion == PursuitMotion.Display ? 2 : 0), forward) * bones["Head"].rotation;
                    bones["Chest"].rotation = Quaternion.AngleAxis(Mathf.Sin(phase) * .6f, right) * bones["Chest"].rotation;
                    bones["UpperArm_L"].rotation = Quaternion.AngleAxis(jerk * 6, forward) * bones["UpperArm_L"].rotation;
                } else {
                    bool walk = motion == PursuitMotion.Walk, run = motion == PursuitMotion.Run;
                    bool grab = motion == PursuitMotion.Grab, tear = motion == PursuitMotion.Tear;
                    float crouch = grab ? Mathf.SmoothStep(0, 1, t) : tear ? 1 : 0;
                    float pull = tear ? Pulse(t,.30f,.62f,.94f) : 0;
                    float awake = motion == PursuitMotion.Awaken ? Mathf.SmoothStep(0,1,t) : 1;
                    bones["Hips"].rotation = Basis(up);
                    bones["Hips"].position += up * (-.025f - .43f * crouch + .025f * pull);
                    Flex("Chest", grab || tear ? 34 - 12 * pull : run ? (advanced ? 29 : 12) : advanced ? 20 : 8, right, up);
                    Flex("Head", grab || tear ? 20 - 10 * pull : 0, right, up);
                    bones["Head"].rotation = Quaternion.AngleAxis(walk ? 10 : run ? 6 : 3, forward) * bones["Head"].rotation;
                    for (int side = 0; side < 2; side++) {
                        string suffix = side == 0 ? "_L" : "_R";
                        float cycle = Mathf.Repeat(t + side * .5f, 1);
                        float stride = Mathf.Cos(cycle * Mathf.PI * 2);
                        var foot = feet[side];
                        if (walk || run) {
                            foot += forward * stride * (run ? (advanced ? .41f : .34f) : advanced ? .23f : .19f);
                            // First half is stance travelling backward; second half is swing returning forward.
                            if (cycle > .5f) foot += up * Mathf.Sin((cycle-.5f)*Mathf.PI*2) * (run ? .22f : .10f);
                        }
                        SolveLeg(bones["Thigh"+suffix],bones["Shin"+suffix],bones["Foot"+suffix],foot,upper[side],lower[side],forward,Basis);
                        bones["Foot"+suffix].rotation = Basis((forward*.13f-up*.04f).normalized);
                        float shoulder = grab ? Mathf.Lerp(72,12,crouch) : tear ? 12-12*pull : run ? 25-stride*32 : walk ? 8-stride*15 : 8;
                        float elbow = grab ? Mathf.Lerp(15,12,crouch) : tear ? 12+100*pull : run ? 70 : walk ? 15 : 12;
                        if (advanced && (walk || run)) { shoulder += side == 0 ? 20 : -8; elbow += side == 0 ? -20 : 10; }
                        Flex("UpperArm"+suffix, -shoulder, right, -up);
                        Flex("Forearm"+suffix, -shoulder-elbow, right, -up);
                        bones["Hand"+suffix].rotation = bones["Forearm"+suffix].rotation;
                    }
                    if (motion == PursuitMotion.Awaken) {
                        // Local interpolation preserves segment lengths through the display-to-alert transition.
                        foreach (string name in Names) {
                            var b=bones[name];
                            b.localPosition=Vector3.Lerp(bindP[name],b.localPosition,awake);
                            b.localRotation=Quaternion.Slerp(bindQ[name],b.localRotation,awake);
                        }
                    }
                }
                foreach (var pair in bones) {
                    var b=pair.Value; var c=curves[pair.Key]; var q=b.localRotation; var p=b.localPosition;
                    float time=t*duration;
                    c[0].AddKey(time,q.x);c[1].AddKey(time,q.y);c[2].AddKey(time,q.z);c[3].AddKey(time,q.w);
                    c[4].AddKey(time,p.x);c[5].AddKey(time,p.y);c[6].AddKey(time,p.z);
                }
            }
            foreach(var pair in bones) {
                string path=AnimationUtility.CalculateTransformPath(pair.Value,root.transform);
                string[] props={"localRotation.x","localRotation.y","localRotation.z","localRotation.w","localPosition.x","localPosition.y","localPosition.z"};
                for(int i=0;i<props.Length;i++) {
                    var curve=curves[pair.Key][i];
                    for(int key=0;key<curve.length;key++) { AnimationUtility.SetKeyLeftTangentMode(curve,key,AnimationUtility.TangentMode.Linear);AnimationUtility.SetKeyRightTangentMode(curve,key,AnimationUtility.TangentMode.Linear); }
                    clip.SetCurve(path,typeof(Transform),props[i],curve);
                }
                pair.Value.localPosition=bindP[pair.Key];pair.Value.localRotation=bindQ[pair.Key];
            }
            clip.EnsureQuaternionContinuity();
            return clip;
        }

        static float Pulse(float t,float start,float peak,float end) => t<start || t>end ? 0 : t<peak ? Mathf.SmoothStep(0,1,(t-start)/(peak-start)) : 1-Mathf.SmoothStep(0,1,(t-peak)/(end-peak));

        static void SolveLeg(Transform thigh,Transform shin,Transform foot,Vector3 target,float upper,float lower,Vector3 pole,System.Func<Vector3,Quaternion> basis)
        {
            Vector3 hip=thigh.position, delta=target-hip;
            float distance=Mathf.Clamp(delta.magnitude,Mathf.Abs(upper-lower)+.001f,upper+lower-.001f);
            Vector3 direction=delta.normalized;
            float along=(upper*upper-lower*lower+distance*distance)/(2*distance);
            float height=Mathf.Sqrt(Mathf.Max(0,upper*upper-along*along));
            Vector3 bend=(pole-Vector3.Project(pole,direction)).normalized;
            Vector3 knee=hip+direction*along+bend*height;
            Vector3 ankle=hip+direction*distance;
            thigh.rotation=basis(knee-hip); shin.SetPositionAndRotation(knee,basis(ankle-knee)); foot.position=ankle;
        }
    }
}
