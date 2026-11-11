using System;
using CurtainCall.Player;
using UnityEngine;

namespace CurtainCall.UI.SceneIntegration
{
    public sealed class PerformanceHudProfile : ScriptableObject
    {
        public const string ResourcePath = "Settings/UI/PerformanceHud";
        static PerformanceHudProfile current;
        public static PerformanceHudProfile Current => current ? current : current = Resources.Load<PerformanceHudProfile>(ResourcePath);

        [Serializable] public sealed class Cue
        {
            [InspectorName("제목")] public string title;
            [InspectorName("내용"), TextArea(1,4)] public string body;
            [InspectorName("키 배지"), Tooltip("학습 가이드에 표시합니다. 비워 두면 기본 배지를 사용합니다.")] public string badge;
            public Cue(string title,string body,string badge) { this.title=title;this.body=body;this.badge=badge; }
        }
        [Header("팀 목표")]
        [InspectorName("외줄타기 · 공연 진행 중"), Tooltip("외줄타기 · 공연 진행 중")] public Cue ropeGoal = new("도착 발판까지 건너기", "외줄의 장애물을 지나 반대편 도착 발판에 도달하세요.", "");
        [InspectorName("인간대포 · 공연 진행 중"), Tooltip("인간대포 · 공연 진행 중")] public Cue cannonGoal = new("목적지에 착지하기", "대포와 트램펄린으로 코스를 건너세요. 살아 있는 단원 한 명이 마지막 착지판에 닿으면 성공합니다.", "");
        [Header("외줄타기 · 첫날 학습 가이드")]
        [InspectorName("외줄 · 출발대에서 자유 이동 중"), Tooltip("외줄 · 출발대에서 자유 이동 중")] public Cue approach = new("외줄에 올라가기", "줄 위로 걸어가면 자동으로 올라탑니다.", "{move}");
        [InspectorName("외줄 · 줄 위에 올라선 순간"), Tooltip("외줄 · 줄 위에 올라선 순간")] public Cue balance = new("외줄에서 균형 잡기", "{forward}로 이동하고 {balance}로 기울어지는 반대 방향을 누르세요.", "{balance}");
        [InspectorName("외줄 · 첫 장애물 안내 이후"), Tooltip("외줄 · 첫 장애물 안내 이후")] public Cue lane = new("옆줄로 이동하기", "{side}를 누른 채 {jump}로 점프하세요.", "{side}");
        [InspectorName("외줄 · 세로 톱날 또는 닫힌 협동 게이트 접근"), Tooltip("외줄 · 세로 톱날 또는 닫힌 협동 게이트 접근")] public Cue sawGate = new("톱날 뒤 버튼 누르기", "목마를 이용하거나 옆줄로 이동해 톱날 뒤의 버튼을 누르세요.", "");
        [InspectorName("외줄 · 움직이는 가로 톱날 접근"), Tooltip("외줄 · 움직이는 가로 톱날 접근")] public Cue sawJump = new("톱날 뛰어넘기", "다가오는 톱날에 맞춰 {jump}로 점프하세요.", "{jump}");
        [InspectorName("외줄 · 중계봉에 매달림"), Tooltip("외줄 · 중계봉에 매달림")] public Cue relayHeld = new("동료가 건너도록 버티기", "최대 {seconds}초 동안 버틸 수 있습니다.", "");
        [InspectorName("외줄 · 중계봉 진입 또는 출구에 접근"), Tooltip("외줄 · 중계봉 진입 또는 출구에 접근")] public Cue relayNear = new("중계봉에 매달리기", "{interact}로 봉에 매달려 동료가 건널 길을 만드세요.", "{interact}");
        [InspectorName("외줄 · 공중그네 아래쪽 단원"), Tooltip("외줄 · 공중그네 아래쪽 단원")] public Cue lower = new("다음 봉으로 건너가기", "타이밍에 맞춰 점프한 뒤 다음 봉을 잡으세요.", "{jump}");
        [InspectorName("외줄 · 공중그네 위쪽 단원"), Tooltip("외줄 · 공중그네 위쪽 단원")] public Cue upper = new("공중그네 흔들기", "{forward}로 반동을 만드세요.", "{forward}");
        [InspectorName("외줄 · 첫 봉에 매달린 동료 근처"), Tooltip("외줄 · 첫 봉에 매달린 동료 근처")] public Cue attach = new("동료에게 매달리기", "매달린 동료에게 가까이 가서 {interact}를 누르세요.", "{interact}");
        [InspectorName("외줄 · 공중그네 진입 지점 접근"), Tooltip("외줄 · 공중그네 진입 지점 접근")] public Cue pole = new("공중그네 잡기", "봉 가까이에서 {interact}로 잡으세요. 점프 중에도 잡을 수 있습니다.", "{interact}");
        [Header("인간대포 · 장치 학습 가이드")]
        [InspectorName("인간대포 · 탑승 가능한 대포에 처음 접근"), Tooltip("인간대포 · 탑승 가능한 대포에 처음 접근")] public Cue cBoard = new("대포에 탑승하기", "대포 가까이에서 {interact}를 누르세요.", "{interact}");
        [InspectorName("인간대포 · 대포에 탑승"), Tooltip("인간대포 · 대포에 탑승")] public Cue cFire = new("발사하거나 내리기", "{interact}를 짧게 눌러 발사하고, 길게 눌러 내리세요.", "{interact}");
        [InspectorName("인간대포 · 트램펄린을 들었을 때"), Tooltip("인간대포 · 트램펄린을 들었을 때")] public Cue cCarry = new("트램펄린 운반하기", "동료가 착지할 위치로 트램펄린을 옮기세요. {interact}를 길게 누르면 내려놓습니다.", "{interact}");
        [InspectorName("인간대포 · 잡을 수 있는 트램펄린 근처"), Tooltip("인간대포 · 잡을 수 있는 트램펄린 근처")] public Cue cGrab = new("트램펄린 잡기", "손잡이 가까이에서 {interact}를 눌러 트램펄린을 드세요.", "{interact}");
        [InspectorName("인간대포 · 봉을 잡은 순간"), Tooltip("인간대포 · 봉을 잡은 순간")] public Cue cPole = new("봉에서 뛰어내리기", "{jump}를 눌러 봉을 놓으세요.", "{jump}");

        [InspectorName("인간대포 · 출발 전 리프트 접근"), Tooltip("탑승할 트램펄린 없이 대기 중인 리프트에 접근했을 때 한 번 표시합니다.")] public Cue liftCarry = new("리프트 이용하기","트램펄린을 들고 혼자 발판에 올라가세요.","");
        [InspectorName("인간대포 · 리프트 출발 준비"), Tooltip("트램펄린을 든 단원이 리프트 출발 조건을 충족했을 때 한 번 표시합니다.")] public Cue liftBoard = new("자동 출발 기다리기","발판 위에서 기다리면 리프트가 자동으로 출발합니다.","");

        public static string Format(string template, PlayerInputReader input)
        {
            string text = FormatKeys(template, input);
            return text.Contains("{seconds}")
                ? text.Replace("{seconds}", CurtainCall.Settings.GameSettings.Poles.HoldSeconds.ToString("0.#"))
                : text;
        }

        internal static string FormatKeys(string template, PlayerInputReader input, string interact = "F", string jump = "Space")
        {
            if (string.IsNullOrEmpty(template) || !template.Contains("{")) return template ?? "";
            string Action(string name,string fallback) => input ? input.BindingLabel(name,fallback) : fallback;
            string Part(string name,string part,string fallback) => input ? input.BindingPartLabel(name,part,fallback) : fallback;
            string ws = Part("Move","positive","W")+"/"+Part("Move","negative","S");
            string ad = Part("Posture","negative","A")+"/"+Part("Posture","positive","D");
            string action = Action("Action",jump);
            return template.Replace("{interact}",Action("Interact",interact)).Replace("{jump}",action).Replace("{action}",action)
                .Replace("{forward}",ws).Replace("{balance}",ad).Replace("{move}",ws+" · "+ad)
                .Replace("{side}",Action("DirectionLeft","Q")+"/"+Action("DirectionRight","E"));
        }
    }
}
