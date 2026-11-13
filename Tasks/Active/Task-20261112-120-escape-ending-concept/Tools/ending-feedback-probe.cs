using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using CurtainCall.Campaign;
using CurtainCall.Dev;
using CurtainCall.EscapeRoute;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using CurtainCall.Reward;
using CurtainCall.SpaceLink;
using CurtainCall.Tightrope;
using CurtainCall.UI.EscapeResultsKit;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class EndingFeedbackProbe
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    const string ScenarioPath = "Assets/Settings/PlayMode/EndingFeedbackLocal.asset";
    const string Prefabs = "Assets/Resources/Prefabs/UIs/EscapeResultsKit/";

    public static object PatchPrefabs()
    {
        foreach (string file in new[] { "EscapeResultsCanvas.prefab", "EscapePlayerRow.prefab" })
        {
            var path = Prefabs + file;
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var view = root.GetComponentInChildren<EscapeResultsView>(true);
                foreach (var mask in root.GetComponentsInChildren<UnityEngine.UI.Mask>(true))
                {
                    var rect = (RectTransform)mask.transform;
                    var frame = (RectTransform)rect.parent.Find(rect.name + "Frame");
                    if (frame == null || rect.Find("Portrait") is not RectTransform portrait)
                        throw new Exception("Unexpected portrait hierarchy");
                    float size = frame.sizeDelta.x, inset = size * .025f;
                    rect.anchoredPosition = frame.anchoredPosition + new Vector2(inset, -inset);
                    rect.sizeDelta = Vector2.one * (size - 2 * inset);
                    portrait.anchoredPosition = new Vector2(-1, 1) * (size * .08f + inset);
                    portrait.sizeDelta = Vector2.one * size * 1.16f;
                    if (rect.name == "PersonalPortrait") view.PersonalBestPortraitFrame = frame.gameObject;
                }
                if (view != null)
                {
                    var personal = view.PersonalBest.rectTransform;
                    personal.anchoredPosition = new Vector2(380, -386);
                    personal.sizeDelta = new Vector2(336, 54);
                    foreach (string name in new[] { "TokensHeadingTMP", "EarnedIcon", "EarnedTMP", "SpentIcon", "SpentTMP" })
                    {
                        var rect = (RectTransform)view.StatisticsContent.transform.Find(name);
                        rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, name.EndsWith("Icon") ? -499 : -493);
                    }
                }
                if (PrefabUtility.SaveAsPrefabAsset(root, path) == null) throw new Exception("Could not save " + path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        return "Patched existing prefab instances in place";
    }

    public static async Task<object> StartScenario()
    {
        if (AssetDatabase.LoadMainAssetAtPath(ScenarioPath) == null)
        {
            if (!AssetDatabase.CopyAsset("Assets/Settings/PlayMode/EscapeEntryFourEditors.asset", ScenarioPath))
                throw new Exception("Could not copy temporary 2-player scenario");
            SessionState.SetBool("EndingFeedback.DevBefore", DevMode.Enabled);
        }
        var config = AssetDatabase.LoadMainAssetAtPath(ScenarioPath);
        var serialized = new SerializedObject(config);
        serialized.FindProperty("m_EditorInstances").arraySize = 1;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssetIfDirty(config);
        DevMode.SetEditorEnabled(true);
        config.GetType().GetMethod("OnConfigSelected", Flags).Invoke(config, null);
        var start = (Task)config.GetType().GetMethod("ExecuteStartAsync", Flags).Invoke(config, new object[] { System.Threading.CancellationToken.None });
        if (await Task.WhenAny(start, Task.Delay(1500)) == start) await start;
        return "Scenario launch: " + start.Status;
    }
    public static object Restore()
    {
        DevMode.SetEditorEnabled(SessionState.GetBool("EndingFeedback.DevBefore", false));
        if (AssetDatabase.LoadMainAssetAtPath(ScenarioPath) != null) AssetDatabase.DeleteAsset(ScenarioPath);
        return "Restored dev setting and removed temporary scenario";
    }
    static async Task Wait(Func<bool> ready, string step)
    {
        double limit = Time.realtimeSinceStartupAsDouble + 20;
        while (!ready())
        {
            if (Time.realtimeSinceStartupAsDouble > limit) throw new Exception(step + " : " + SceneManager.GetActiveScene().name + " / " + CampaignRun.Instance?.State.Phase);
            await Task.Delay(75);
        }
    }
    static NetworkSessionManager Session => NetworkSessionManager.EnsureExists();
    static CampaignRun Run => CampaignRun.Instance;
    static PerformanceRewards Rewards => PerformanceRewards.Instance;
    static EscapeResultsView View => EscapeEndingPresentation.Current?.GetComponent<EscapeEndingResults>()?.View;
    const ushort LanPort = 7796;
    public static async Task<object> Host()
    {
        if (!await Session.HostAsync(new LanSessionConnector(Session.NetworkManager, LanPort))) throw new Exception("Host failed");
        return new { host = Session.IsHost, port = LanPort };
    }
    public static async Task<object> Join()
    {
        if (!await Session.JoinAsync(new LanSessionConnector(Session.NetworkManager, LanPort), "127.0.0.1")) throw new Exception("Join failed");
        await Wait(() => NetworkPlayer.Local != null && NetworkPlayer.Local.IsSpawned, "Client spawn");
        return new { player = NetworkPlayer.Local.OwnerClientId, slot = NetworkPlayer.Local.Slot };
    }
    public static async Task<object> Performance()
    {
        await Wait(() => Session.PlayerCount == 2 && NetworkPlayer.All.Count(p => p != null && p.IsSpawned) == 2, "Two spawned players");
        var launcher = DevStartLauncher.EnsureExists();
        if (!launcher.TryLaunch(new RunStartPoint(2, RunStartPhase.Performance, 0, CampaignSnapshot.TightropeId, 1)))
            throw new Exception(launcher.Status);
        await Wait(() => !launcher.IsBusy && SceneManager.GetActiveScene().name == "Tightrope", "Load tightrope");
        if (!launcher.LastLaunchSucceeded) throw new Exception(launcher.Status);
        return new { scene = SceneManager.GetActiveScene().name, players = Session.PlayerCount, generation = Run.State.Generation };
    }
    public static object InjureClient()
    {
        var partner = NetworkPlayer.All.Single(p => !p.IsLocal && p.IsSpawned);
        if (partner.LostParts != CurtainCall.Player.BodyPart.None) partner.ServerRestoreAll();
        var cut = partner.ServerCut(1);
        if (partner.LostParts == CurtainCall.Player.BodyPart.None) throw new Exception("No injury applied: " + cut);
        return new { partner = partner.OwnerClientId, lostParts = partner.LostParts.ToString() };
    }
    public static async Task<object> AwaitPerformance()
    {
        await Wait(() => Rewards?.State?.IsOpen == true, "Start real tightrope attempt");
        return new { players = Session.PlayerCount, generation = Run.State.Generation };
    }
    public static object FinishPose()
    {
        var actor = NetworkPlayer.Local;
        if (TightropeCourse.Current == null) throw new Exception("No tightrope course");
        var target = TightropeCourse.Current.GetFinishPose(actor.Slot);
        if (!DevCheats.Teleport(actor, target.position, target.rotation, out var reason)) throw new Exception(reason);
        return new { slot = actor.Slot, injury = actor.LostParts.ToString(), position = target.position.ToString() };
    }
    public static async Task<object> EscapeSameRun()
    {
        await Wait(() => Run.State.Phase == CampaignPhase.RestTime && Run.State.Timer != RestTimerState.WaitingForExit
            && EscapeEntrance.Current != null && !SpaceLinkDirector.Instance.IsTransitioning, "Performance settlement and waiting room");
        var stats = Rewards.RunStatistics;
        if (stats.rows.Count != 1 || stats.rows[0].clears != 1) throw new Exception("Tightrope did not settle as a clear");
        var ids = NetworkPlayer.All.Where(p=>p.IsSpawned).Select(p=>p.OwnerClientId).ToArray();
        var transfer = EscapeSceneTransfer.EnsureExists();
        if ((!transfer.HasDeparture && !transfer.Begin(ids, Run.State.Generation)) || !Run.StartEscape())
            throw new Exception("Waiting room escape transfer rejected");
        return new { generation = stats.generation, attempts = stats.rows[0].attempts, clears = stats.rows[0].clears,
            stats.teamBest, stats.personalBest, stats.earned, players = ids.Length };
    }
    public static async Task<object> AssemblyPose()
    {
        await Wait(() => Run.State.Phase == CampaignPhase.RestTime && EscapeEntrance.Current != null
            && SpaceLinkDirector.Instance?.IsTransitioning != true, "Waiting room assembly");
        var actor = NetworkPlayer.Local;
        Run.NotifyTutorialDone();
        var zone = EscapeEntrance.Current.AssemblyArea;
        var local = zone.center + Vector3.right * (actor.Slot == 0 ? -.4f : .4f);
        var center = zone.transform.TransformPoint(local);
        var point = Physics.Raycast(center + Vector3.up, Vector3.down, out var hit, 8f, ~0, QueryTriggerInteraction.Ignore)
            ? hit.point : center;
        if (!DevCheats.Teleport(actor, point, zone.transform.rotation, out var reason)) throw new Exception(reason);
        return new { slot = actor.Slot, point = point.ToString() };
    }
    public static async Task<object> OpenExit()
    {
        await Wait(() => EscapeCampaignContent.IsTraversalActive && SpaceLinkDirector.Instance.EscapePrepared
            && !SpaceLinkDirector.Instance.IsTransitioning, "Campaign escape transfer");
        var panel = DevCheatPanel.EnsureExists();
        if (!panel.TryOpenEscapeExit()) throw new Exception(panel.Status);
        return new { campaignEscape = EscapeCampaignContent.Current.IsCampaign, open = EscapeCampaignContent.Current.ExitOpen };
    }
    public static async Task<object> GoMeadow()
    {
        await Wait(() => EscapeCampaignContent.IsTraversalActive && EscapeCampaignContent.Current.ExitOpen
            && SpaceLinkDirector.Instance?.IsTransitioning != true, "Client door sync");
        var actor = NetworkPlayer.Local;
        var points = new System.Collections.Generic.List<DevCheats.TeleportPoint>();
        DevCheats.CollectTeleportPoints(actor, points);
        var target = points.Single(p => p.Label.EndsWith("갈대밭 · 엔딩 도착"));
        if (!DevCheats.Teleport(actor, target.Position, target.Rotation, out var reason)) throw new Exception(reason);
        return new { slot = actor.Slot, injury = actor.LostParts.ToString() };
    }
    public static object GoMeadowAsClient()
    {
        var scene = SceneManager.GetSceneByName("escape_route");
        if (Session.IsHost || !scene.isLoaded || Run.State.Phase != CampaignPhase.Escape) throw new Exception("Expected a client in the escape scene");
        SceneManager.SetActiveScene(scene);
        var frame = EscapeCampaignContent.Current.transform;
        var anchor = frame.GetComponentsInChildren<Transform>(true).Single(x=>x.name=="Anchor_ExitToReeds");
        var actor = NetworkPlayer.Local;
        if (!DevCheats.Teleport(actor, anchor.position + frame.TransformVector(Vector3.right * 4),
            Quaternion.LookRotation(frame.TransformDirection(Vector3.right)), out var why)) throw new Exception(why);
        return new { clientDoorOpen = EscapeCampaignContent.Current.ExitOpen, injury = actor.LostParts.ToString() };
    }
    public static async Task<object> InspectResult()
    {
        await Wait(() => View != null && View.Visible, "Ending results visible");
        var actors = NetworkPlayer.All.Where(p => p.IsSpawned).Select(p=>new { p.Slot, injury = p.LostParts.ToString(), state = p.State.ToString(), p.AppearanceModel,
            position = p.transform.position.ToString("F2"), viewport = Camera.main.WorldToViewportPoint(p.transform.position + Vector3.up).ToString("F2") }).ToArray();
        return new { actors, generation = Run.State.Generation, view = View.PlayerRows.Where(p=>p.Root.activeSelf).Select(p=>new { player = p.Name.text, outcome = p.OutcomeLabel.text }).ToArray(),
            teamBest = View.TeamBest.text, personalBest = View.PersonalBest.text, earned = View.EarnedTokens.text, spent = View.SpentTokens.text };
    }
    public static object Statistics()
    {
        View.StatisticsTab.onClick.Invoke();
        UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(View.StatisticsTab.gameObject);
        return View.StatisticsSelected;
    }
    public static object Background() { View.SetVisible(false); return EscapeEndingPresentation.Current.GetComponent<EscapeEndingCinematic>().Playing; }
    public static object VerifyEmptyPortrait()
    {
        var clone = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "EscapeResultsCanvas.prefab"));
        try
        {
            var view = clone.GetComponentInChildren<EscapeResultsView>(true);
            view.SetVisible(false);
            view.SetStatistics(Array.Empty<EscapeStatisticData>(), "—", "—", null, "0", "0");
            bool maskHidden = !view.PersonalBestPortrait.transform.parent.gameObject.activeSelf;
            bool frameHidden = !view.PersonalBestPortraitFrame.activeSelf;
            if (!maskHidden || !frameHidden) throw new Exception("An empty portrait frame is still displayed");
            return new { maskHidden, frameHidden, personalBest = view.PersonalBest.text, alignment = view.PersonalBest.alignment.ToString() };
        }
        finally { UnityEngine.Object.Destroy(clone); }
    }
}
