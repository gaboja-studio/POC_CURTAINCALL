using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace CurtainCall.Player.Validation
{
    /// <summary>원본 동작을 정상/부상 메시에서 비교하는 독립 미리보기. 게임 이동이나 부상 판정은 변경하지 않는다.</summary>
    public sealed class AnimationClipPreview : MonoBehaviour
    {
        public Animator Animator;
        public AnimationClip[] Clips;
        public string[] Labels;
        public SkinnedMeshRenderer Original;
        public SkinnedMeshRenderer[] Bodies;
        public int CurrentIndex { get; private set; } = -1;
        public int BodyStage { get; private set; }
        public bool IsPaused { get; private set; }
        public bool HasGraph => graph.IsValid();
        public double PlaybackTime => graph.IsValid() ? playable.GetTime() : 0;
        public string Failure { get; private set; }

        PlayableGraph graph;
        AnimationClipPlayable playable;
        float previousSpeed;
        bool previousRootMotion;
        bool capturedSettings;

        void OnEnable()
        {
            if (Animator == null || Clips == null || Clips.Length == 0) return;
            SelectClip(0);
        }

        public void SelectClip(int index)
        {
            if (Clips == null || index < 0 || index >= Clips.Length) throw new ArgumentOutOfRangeException(nameof(index));
            var clip = Clips[index];
            if (Animator == null || Animator.avatar == null || !Animator.avatar.isValid || !Animator.isHuman || clip == null || !clip.humanMotion)
                throw new InvalidOperationException("Valid Humanoid target and clip required.");
            if (!capturedSettings)
            {
                previousSpeed = Animator.speed;
                previousRootMotion = Animator.applyRootMotion;
                capturedSettings = true;
            }
            DestroyGraph();
            Animator.speed = 1;
            Animator.applyRootMotion = false;
            CurrentIndex = index;
            IsPaused = false;
            Failure = null;
            playable = AnimationPlayableUtilities.PlayClip(Animator, clip, out graph);
            playable.SetDuration(clip.length);
            graph.Evaluate(0);
        }

        public void SetPaused(bool paused)
        {
            if (!graph.IsValid()) return;
            IsPaused = paused;
            if (paused) graph.Stop(); else graph.Play();
        }

        public void SeekNormalized(float time)
        {
            if (!graph.IsValid()) return;
            playable.SetTime(Mathf.Clamp01(time) * Clips[CurrentIndex].length);
            playable.SetDone(false);
            graph.Evaluate(0);
        }

        public void SelectBody(int stage)
        {
            if (stage < 0 || stage > 3 || Original == null || Bodies == null || Bodies.Length < 3)
                throw new ArgumentOutOfRangeException(nameof(stage));
            BodyStage = stage;
            Original.enabled = stage == 0;
            for (int i = 0; i < Bodies.Length; i++) if (Bodies[i] != null) Bodies[i].enabled = stage == i + 1;
        }

        public void StopPreview()
        {
            DestroyGraph();
            CurrentIndex = -1;
            IsPaused = false;
            if (Animator != null && capturedSettings)
            {
                Animator.speed = previousSpeed;
                Animator.applyRootMotion = previousRootMotion;
                if (Application.isPlaying && Animator.isActiveAndEnabled) { Animator.Rebind(); Animator.Update(0); }
                capturedSettings = false;
            }
        }

        void Update()
        {
            if (!graph.IsValid() || IsPaused || CurrentIndex < 0) return;
            if (PlaybackTime >= Clips[CurrentIndex].length)
            {
                SeekNormalized(1);
                SetPaused(true);
            }
        }

        void DestroyGraph() { if (graph.IsValid()) graph.Destroy(); }
        void OnDisable() { StopPreview(); }

        void OnGUI()
        {
            GUILayout.BeginArea(new Rect(12, 12, 380, 480), GUI.skin.box);
            GUILayout.Label("Task062 / Humanoid clip preview");
            GUILayout.Label("Raw retarget preview; NOT injury locomotion");
            if (Clips != null) for (int i = 0; i < Clips.Length; i++)
            {
                int index = i;
                if (GUILayout.Button(Labels != null && i < Labels.Length ? Labels[i] : Clips[i].name))
                {
                    try { SelectClip(index); }
                    catch (Exception exception) { Failure = exception.Message; }
                }
            }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(IsPaused ? "Resume" : "Pause")) SetPaused(!IsPaused);
            if (GUILayout.Button("Restart") && CurrentIndex >= 0) SelectClip(CurrentIndex);
            if (GUILayout.Button("Stop")) StopPreview();
            GUILayout.EndHorizontal();
            GUILayout.Label("Mesh only comparison (no cutting/physics)");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Intact")) SelectBody(0);
            if (GUILayout.Button("1 leg")) SelectBody(1);
            if (GUILayout.Button("No legs")) SelectBody(2);
            if (GUILayout.Button("1 arm")) SelectBody(3);
            GUILayout.EndHorizontal();
            GUILayout.Label($"Clip: {CurrentIndex + 1} / Time: {PlaybackTime:F2}s / Body: {BodyStage}");
            if (!string.IsNullOrEmpty(Failure)) GUILayout.Label(Failure);
            GUILayout.EndArea();
        }
    }
}
