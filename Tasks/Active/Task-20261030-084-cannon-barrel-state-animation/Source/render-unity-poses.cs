var view = UnityEngine.Object.FindAnyObjectByType<CurtainCall.HumanCannon.CannonBarrelPresentation>();
var camera = Camera.main;
camera.transform.position = new Vector3(4, 2.9f, 5.2f); camera.transform.LookAt(new Vector3(0, 1.15f, .2f));
camera.orthographicSize = 2.2f;
var oldRect = camera.rect; camera.rect = new Rect(0, 0, 1, 1);
var target = new RenderTexture(1200, 900, 24); var previous = RenderTexture.active;
camera.targetTexture = target;
try
{
    foreach (var phase in new[] { CurtainCall.HumanCannon.CannonBarrelPhase.Active, CurtainCall.HumanCannon.CannonBarrelPhase.Inactive, CurtainCall.HumanCannon.CannonBarrelPhase.FireDisable, CurtainCall.HumanCannon.CannonBarrelPhase.Recover })
    {
        view.Sample(phase, phase == CurtainCall.HumanCannon.CannonBarrelPhase.FireDisable ? .055f / 1.2f : .5f);
        camera.Render(); RenderTexture.active = target;
        var tex = new Texture2D(1200, 900, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 1200, 900), 0, 0); tex.Apply();
        System.IO.File.WriteAllBytes("Tasks/Active/Task-20261030-084-cannon-barrel-state-animation/Previews/unity-" + phase.ToString().ToLowerInvariant() + ".png", tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
    }
}
finally { camera.targetTexture = null; camera.rect = oldRect; RenderTexture.active = previous; target.Release(); UnityEngine.Object.DestroyImmediate(target); view.Sample(CurtainCall.HumanCannon.CannonBarrelPhase.Active, 0); }
return "Four poses captured";
