using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CurtainCall.UI.SceneIntegration.Tests
{
    public sealed partial class DevStartLauncherPlayTests
    {
        void SelectUnifiedRoom() => WaitingVariantField.SetValue(WaitingSpaces, Enum.Parse(WaitingVariantField.FieldType, "Integrated"));

        [UnityTest]
        public IEnumerator UnifiedWaitingRoom_RehearsalStagesAndPursuitStaySeparate()
        {
            SelectUnifiedRoom();
            yield return Launch(1, "Rehearsal", 0, "Tightrope", "WaitingRoom");
            yield return WaitStage(0, 10f);
            var director = Find("CurtainCall.WaitingRoom.Pursuit.PursuitDirector");
            Assert.IsNotNull(director);
            Assert.AreEqual(-1, Get(director, "CurrentStageIndex"), "Rehearsal must not start a chase.");
            Assert.IsFalse((bool)Call(director, "DebugExpireTimer"));
            var monitor = SceneManager.GetSceneByName("WaitingRoom").GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<Transform>(true)).Single(t => t.name == "PracticeMonitor");
            Assert.AreEqual(new Vector3(14.4f, 8.1f, 1), monitor.Find("Screen").localScale);
            for (int stage = 0; stage < 5; stage++) {
                yield return WaitStage(stage, 10f);
                Assert.IsNotNull(Priv(Rehearsal, "course"));
                float until = Time.realtimeSinceStartup + 10;
                while (!(bool)Get(Rehearsal, "CanClearCurrentStage") && Time.realtimeSinceStartup < until) yield return null;
                Assert.IsTrue((bool)Call(Rehearsal, "TryClearCurrentStage"));
                yield return null; yield return null;
            }
            Assert.IsTrue((bool)Static(RehearsalType, "IsCompleted"));
        }

        [UnityTest]
        public IEnumerator UnifiedWaitingRoom_CameraMenuInventoryAndPursuitCheats()
        {
            SelectUnifiedRoom();
            yield return Launch(2, "Morning", 0, "Tightrope", "WaitingRoom");
            yield return MenuReady();
            Assert.AreEqual("WaitingRoom", ((Scene)Static("CurtainCall.UI.SceneIntegration.CommonMenuUI", "CurrentScene")).name);
            var menu = Find("CurtainCall.UI.SceneIntegration.CommonMenuUI");
            Call(menu, "OpenMenu"); yield return null;
            Assert.IsTrue((bool)Get(menu, "IsOpen"));
            Call(menu, "CloseAll"); yield return null;
            Assert.IsFalse((bool)Get(menu, "IsOpen"));
            var inventory = Find("CurtainCall.UI.SceneIntegration.CommonInventoryUI");
            Assert.AreEqual(1f, ((CanvasGroup)Field(inventory, "group")).alpha);
            var camera = Find("CurtainCall.Cameras.CameraViewDirector");
            Assert.IsNotNull(camera);
            Assert.AreEqual("WaitingRoom", camera.gameObject.scene.name);
            Assert.IsTrue((bool)Get(camera, "UsesMouseLook"));
            var director = Find("CurtainCall.WaitingRoom.Pursuit.PursuitDirector");
            Assert.IsTrue((bool)Get(director, "IsInitialized"));
            Assert.IsTrue((bool)Call(director, "DebugSetInvulnerable", true));
            Assert.IsTrue((bool)Call(director, "DebugSetRecognition", false));
            Assert.IsTrue((bool)Call(director, "DebugSetStage", 1));
            yield return new WaitForSecondsRealtime(.3f);
            Assert.AreEqual("Pursuit", Field(State, "Timer").ToString());
            Assert.AreEqual(1, Get(director, "CurrentStageIndex"));
            Assert.IsTrue((bool)Get(director, "DebugInvulnerable"));
            Assert.IsFalse((bool)Get(director, "DebugRecognitionEnabled"));
            Assert.IsFalse((bool)Static(RehearsalType, "IsActive"));
            Assert.IsTrue((bool)Call(director, "DebugResetTimer"));
        }

        /// <summary>Task-129: 정비 시간에는 로비 → 무대 입구 통로 철창이 닫히고 통로 조명이 꺼진다. 쉬는 시간에는 열려 있다.</summary>
        [UnityTest]
        public IEnumerator UnifiedWaitingRoom_MaintenanceClosesStageCorridor()
        {
            SelectUnifiedRoom();
            var closure = T("CurtainCall.WaitingRoom.StageCorridorClosure");
            Component Gate() => SceneManager.GetSceneByName("WaitingRoom").GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren(T("CurtainCall.WaitingRoom.NightDoorLock"), true))
                .Single(c => c.name == (string)closure.GetField("GateName").GetValue(null));
            Light[] CorridorLights() => (Light[])closure.GetMethod("CorridorLights").Invoke(null, new object[] {
                SceneManager.GetSceneByName("WaitingRoom").GetRootGameObjects().Single(r => r.name == "WaitingRoom_LayoutA").transform });
            // 로비 쪽(입구 앞)에서 통로 안쪽으로 사람 높이에서 쏜다.
            bool Blocked() => Physics.Raycast(new Vector3(-9.7f, 1f, 89.8f), Vector3.forward, out var hit, 4f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                && hit.collider.name == "GateCollision";

            yield return Launch(2, "Morning", 0, "Tightrope", "WaitingRoom");
            yield return new WaitForSecondsRealtime(.3f);
            var gate = Gate();
            var power = Find("CurtainCall.WaitingRoom.NightPower");
            var lamps = CorridorLights();
            Assert.Greater(lamps.Length, 0, "Corridor lights must be found under S06/S07.");
            Assert.IsFalse((bool)Get(gate, "IsLocked"), "Rest time keeps the stage corridor open.");
            Assert.IsFalse((bool)Get(power, "IsStageCorridorDark"));
            Assert.IsTrue(lamps.Any(l => l.enabled));
            Assert.IsFalse(Blocked(), "Open gate must not block the corridor mouth.");

            yield return Launch(2, "Night", 0, "", "WaitingRoom");
            gate = Gate(); power = Find("CurtainCall.WaitingRoom.NightPower"); lamps = CorridorLights();
            float until = Time.realtimeSinceStartup + 5f;
            while (!(bool)Get(gate, "IsFullyClosed") && Time.realtimeSinceStartup < until) yield return null;
            yield return null;
            Assert.IsTrue((bool)Get(gate, "IsLocked"), "Maintenance closes the stage corridor gate.");
            Assert.IsTrue((bool)Get(gate, "IsFullyClosed"));
            Assert.IsTrue((bool)Get(power, "IsStageCorridorDark"));
            Assert.IsFalse(lamps.Any(l => l.enabled), "Every stage corridor light is off during maintenance.");
            Assert.IsTrue(Blocked(), "Closed gate blocks the lobby side of the corridor mouth.");
        }
    }
}
