var director = UnityEngine.Object.FindFirstObjectByType<CurtainCall.WaitingRoom.Pursuit.PursuitDirector>();
UnityEngine.AI.NavMesh.SamplePosition(new UnityEngine.Vector3(-6, 0, 77.4f), out var lobby, 3, UnityEngine.AI.NavMesh.AllAreas);
var vertices = UnityEngine.AI.NavMesh.CalculateTriangulation().vertices;
var path = new UnityEngine.AI.NavMeshPath();
bool Connected(UnityEngine.Vector3 a, UnityEngine.Vector3 b) => UnityEngine.AI.NavMesh.CalculatePath(a, b, UnityEngine.AI.NavMesh.AllAreas, path) && path.status == UnityEngine.AI.NavMeshPathStatus.PathComplete;
var report = new System.Collections.Generic.List<string>();
foreach (var room in director.Rooms.Where(r => r.Count > 0)) {
    var spawn = room.SpawnPoints[0];
    UnityEngine.AI.NavMesh.SamplePosition(spawn.position, out var snapped, 2, UnityEngine.AI.NavMesh.AllAreas);
    if (Connected(snapped.position, lobby.position)) spawn.position = snapped.position;
    else {
        var candidates = vertices.Where(p => room.Contains(p)).OrderBy(p => (p - spawn.position).sqrMagnitude).Take(500).ToArray();
        foreach (var candidate in candidates) {
            if (!Connected(candidate, lobby.position)) continue;
            // Move slightly off polygon boundaries into the mesh.
            if (UnityEngine.AI.NavMesh.SamplePosition(candidate, out var sample, .5f, UnityEngine.AI.NavMesh.AllAreas)) spawn.position = sample.position;
            break;
        }
    }
    var patrol = new System.Collections.Generic.List<UnityEngine.Transform>();
    foreach (var point in room.PatrolPoints) {
        if (point == null || !UnityEngine.AI.NavMesh.SamplePosition(point.position, out var hit, 2, UnityEngine.AI.NavMesh.AllAreas) || !Connected(spawn.position, hit.position)) continue;
        point.position = hit.position; patrol.Add(point);
    }
    room.PatrolPoints = patrol.Count > 0 ? patrol.ToArray() : new[] { spawn };
    report.Add(room.name + ": lobby path=" + Connected(spawn.position, lobby.position) + ", patrol=" + room.PatrolPoints.Length);
}
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(director.gameObject.scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(director.gameObject.scene);
return report.ToArray();
