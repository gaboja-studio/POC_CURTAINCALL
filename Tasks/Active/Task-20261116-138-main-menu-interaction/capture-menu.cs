Application.runInBackground = true;
var gameType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
var gameView = EditorWindow.GetWindow(gameType);
gameView.Focus();
ScreenCapture.CaptureScreenshot("D:/github/POC_CURTAINCALL/.harness/local/start-after.png");
var menu = UnityEngine.Object.FindFirstObjectByType<CurtainCall.StartLobby.StartMenu>();
return new { title = UnityEngine.Object.FindFirstObjectByType<CurtainCall.StartLobby.StartMenuHangingTitle>() != null,
    credits = Resources.Load<CurtainCall.StartLobby.StartMenuCreditsSettings>(CurtainCall.StartLobby.StartMenuCreditsSettings.ResourcePath)?.contributors.Count,
    page = menu != null ? menu.Current.ToString() : "missing", screen = new Vector2(Screen.width, Screen.height) };


