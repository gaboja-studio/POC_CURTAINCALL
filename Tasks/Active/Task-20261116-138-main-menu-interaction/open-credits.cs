var menu = UnityEngine.Object.FindFirstObjectByType<CurtainCall.StartLobby.StartMenu>();
menu.mainPage.transform.Find("Button_DevelopmentAndLicenses").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
ScreenCapture.CaptureScreenshot("D:/github/POC_CURTAINCALL/.harness/local/credits.png");
var panel = UnityEngine.Object.FindFirstObjectByType<CurtainCall.StartLobby.StartMenuCreditsPanel>();
var scroll = panel.GetComponentInChildren<UnityEngine.UI.ScrollRect>();
return new { page = menu.Current.ToString(), entries = scroll.content.childCount,
    contentHeight = scroll.content.rect.height, viewportHeight = scroll.viewport.rect.height };
