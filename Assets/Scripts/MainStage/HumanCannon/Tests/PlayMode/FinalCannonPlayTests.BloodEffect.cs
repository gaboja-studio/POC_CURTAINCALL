using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CurtainCall.HumanCannon.Tests
{
    public sealed partial class FinalCannonPlayTests
    {
        /// <summary>
        /// Task-090: 실제 호스트 절단 요청으로 혈흔이 잘린 부위(몸에 남은 위팔·허벅지 뼈)에서 재생되고,
        /// 내 1인칭이면 카메라 앞(카메라 자식)에서 재생되며, 개인 설정을 끄면 재생하지 않는다.
        /// </summary>
        [UnityTest]
        public IEnumerator BloodEffect_PlaysAtStump_OnScreenInFirstPerson_AndRespectsClientSetting()
        {
            DisableEnhancementThreats();
            const string key = "CurtainCall.Settings.ShowBloodEffect";
            int? previous = PlayerPrefs.HasKey(key) ? PlayerPrefs.GetInt(key) : null;
            var effects = GameType("CurtainCall.Damage.InjuryBloodEffects");
            var spurtType = GameType("CurtainCall.Damage.BloodSpurt");
            var bodyPart = GameType("CurtainCall.Player.BodyPart");
            var source = new GameObject("BloodEffect first-person request");
            Component[] Spurts() => Object.FindObjectsByType(spurtType, FindObjectsSortMode.None).Cast<Component>().ToArray();
            int Lost() => Convert.ToInt32(Get(player, "LostParts"));
            try
            {
                PlayerPrefs.SetInt(key, 1);
                Assert.IsTrue((bool)StaticGet("CurtainCall.Damage.InjuryBloodEffects", "IsVisible"));
                effects.GetMethod("EnsureExists").Invoke(null, null);
                yield return null; // 새 플레이어 연결은 다음 Update에서 한다.
                foreach (var old in Spurts()) Object.Destroy(old.gameObject);
                yield return null;

                // 3인칭: 잘린 부위 뼈 근처, 카메라와 분리된 월드 연출.
                Call(player, "RequestCutPart", Enum.ToObject(bodyPart, 0));
                yield return WaitUntil(() => Lost() != 0, 2, "첫 절단");
                yield return null;
                var spurts = Spurts();
                Assert.AreEqual(1, spurts.Length, "새로 잃은 부위 1곳에서 한 번");
                var cut = (int)Lost();
                var model = (GameObject)Get(player.GetComponent(GameType("CurtainCall.Player.PlayerModelSlot")), "CurrentModel");
                var animator = model.GetComponentInChildren<Animator>();
                var bone = animator.GetBoneTransform(cut switch
                {
                    1 => HumanBodyBones.LeftUpperArm,
                    2 => HumanBodyBones.RightUpperArm,
                    4 => HumanBodyBones.LeftUpperLeg,
                    _ => HumanBodyBones.RightUpperLeg,
                });
                Assert.IsNotNull(bone);
                Assert.Less(Vector3.Distance(spurts[0].transform.position, bone.position), .05f, "몸에 남은 절단 부위 뼈를 따라간다");
                Assert.Less(Vector3.Distance(spurts[0].transform.position, player.transform.position), 2f, "내 몸 근처");
                Assert.AreNotEqual(Camera.main.transform, spurts[0].transform.parent);

                // 1인칭: 카메라 앞 화면 연출.
                var director = (Component)StaticGet("CurtainCall.Cameras.CameraViewDirector", "Current");
                var first = Resources.Load("Settings/Camera/DefaultFirstPerson", GameType("CurtainCall.Cameras.CameraViewPreset"));
                Call(director, "RequestView", source, first, 1000, Vector3.zero);
                yield return WaitUntil(() => (bool)Get(director, "IsFirstPerson"), 2, "1인칭 전환");
                int before = Lost();
                Call(player, "RequestCutPart", Enum.ToObject(bodyPart, 0));
                yield return WaitUntil(() => Lost() != before, 2, "두 번째 절단");
                yield return null;
                var onScreen = Spurts().Where(s => s.transform.parent == Camera.main.transform).ToArray();
                Assert.AreEqual(1, onScreen.Length, "1인칭은 카메라 앞에서 재생");
                Vector3 viewport = Camera.main.WorldToViewportPoint(onScreen[0].transform.position);
                Assert.That(viewport.z, Is.GreaterThan(Camera.main.nearClipPlane), "카메라 앞");
                Assert.That(viewport.x, Is.InRange(0f, 1f));
                Assert.That(viewport.y, Is.InRange(0f, 1f));
                Call(director, "ReleaseView", source);

                // 개인 설정을 끄면 재생하지 않는다.
                PlayerPrefs.SetInt(key, 0);
                int count = Spurts().Length;
                before = Lost();
                Call(player, "RequestCutPart", Enum.ToObject(bodyPart, 0));
                yield return WaitUntil(() => Lost() != before, 2, "세 번째 절단");
                yield return null;
                Assert.LessOrEqual(Spurts().Length, count, "개인 설정이 꺼지면 새 혈흔 없음");
            }
            finally
            {
                Object.Destroy(source);
                if (previous.HasValue) PlayerPrefs.SetInt(key, previous.Value); else PlayerPrefs.DeleteKey(key);
            }
        }
    }
}
