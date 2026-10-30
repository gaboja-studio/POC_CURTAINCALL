var view = UnityEngine.Object.FindAnyObjectByType<CurtainCall.HumanCannon.CannonBarrelPresentation>();
var filter = view.GetComponentsInChildren<MeshFilter>().Single(f => f.name == "Barrel");
var lines = new System.Collections.Generic.List<string>();
foreach (var phase in new[] { CurtainCall.HumanCannon.CannonBarrelPhase.Active, CurtainCall.HumanCannon.CannonBarrelPhase.Inactive })
{
    view.Sample(phase, 0);
    var vertices = filter.sharedMesh.vertices.Select(v => filter.transform.TransformPoint(v)).ToArray();
    lines.Add(phase+" pivotLocal="+view.BarrelPivot.localPosition+" rot="+view.BarrelPivot.localEulerAngles+" pivotWorld="+view.BarrelPivot.position+" minY="+vertices.Min(v=>v.y)+" maxY="+vertices.Max(v=>v.y));
}
view.Sample(CurtainCall.HumanCannon.CannonBarrelPhase.Active, 0);
return string.Join("\n",lines);
