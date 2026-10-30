var gallery = UnityEngine.Object.FindAnyObjectByType<CurtainCall.HumanCannon.CannonBarrelGallery>();
if (gallery == null || !EditorApplication.isPlaying) throw new System.Exception("Play the barrel gallery first");
var rows = new System.Collections.Generic.List<string>();
for (int mode = 0; mode < 5; mode++)
{
    gallery.Select(mode);
    foreach (float time in new[] { 0f, .25f, .5f, .75f, 1f })
    {
        gallery.Seek(time);
        if (!gallery.Paused) throw new System.Exception("Seek must pause");
        var pivot = gallery.Cannon.BarrelPivot;
        if (float.IsNaN(pivot.localRotation.x)) throw new System.Exception("Nonfinite pose");
        rows.Add(mode+" "+time+" "+gallery.Cannon.Phase+" "+pivot.localEulerAngles);
    }
}
foreach (string name in new[] { "Front / muzzle", "Side", "3/4 view", "Rear" })
{
    var button = UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None).Single(b => b.name == name);
    button.onClick.Invoke(); rows.Add(name+" yaw="+gallery.ViewCamera.transform.eulerAngles.y);
}
UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None).Single(b => b.name == "3/4 view").onClick.Invoke();
gallery.Select(4); gallery.Seek(.75f);
ScreenCapture.CaptureScreenshot("D:/github/POC_CURTAINCALL/Tasks/Active/Task-20261030-084-cannon-barrel-state-animation/Previews/gallery-ui.png");
System.IO.File.WriteAllLines("Tasks/Active/Task-20261030-084-cannon-barrel-state-animation/Source/gallery-check.txt", rows);
gallery.SetPaused(false);
return "25 seek samples and four camera buttons passed; frame="+Time.frameCount+" elapsed="+gallery.Elapsed;
