using System;
using Unity.Behavior;
using Unity.Properties;

namespace CurtainCall.WaitingRoom.Pursuit
{
    // Short actions are reevaluated by the graph's repeating priority selector every frame.
    // This lets visual contact interrupt hearing/search immediately without parallel movement writers.
    [Serializable, GeneratePropertyBag]
    [NodeDescription(name: "Pursuit: Prepare attack", story: "If a new target is acquired, prepare the attack", category: "Pursuit", id: "a481d526b02a4aa39727bc775a100001")]
    public partial class PursuitPrepareAction : Unity.Behavior.Action
    { protected override Status OnStart() => GameObject.GetComponent<PursuitActor>().ActPrepare() ? Status.Success : Status.Failure; }
    [Serializable, GeneratePropertyBag]
    [NodeDescription(name: "Pursuit: Chase target", story: "Chase the last observed target position", category: "Pursuit", id: "a481d526b02a4aa39727bc775a100002")]
    public partial class PursuitChaseAction : Unity.Behavior.Action
    { protected override Status OnStart() => GameObject.GetComponent<PursuitActor>().ActChase() ? Status.Success : Status.Failure; }
    [Serializable, GeneratePropertyBag]
    [NodeDescription(name: "Pursuit: Investigate sound", story: "Visit the remembered nearby sound", category: "Pursuit", id: "a481d526b02a4aa39727bc775a100003")]
    public partial class PursuitInvestigateAction : Unity.Behavior.Action
    { protected override Status OnStart() => GameObject.GetComponent<PursuitActor>().ActInvestigate() ? Status.Success : Status.Failure; }
    [Serializable, GeneratePropertyBag]
    [NodeDescription(name: "Pursuit: Listen and look", story: "Pause and look toward a distant sound", category: "Pursuit", id: "a481d526b02a4aa39727bc775a100004")]
    public partial class PursuitListenAction : Unity.Behavior.Action
    { protected override Status OnStart() => GameObject.GetComponent<PursuitActor>().ActListen() ? Status.Success : Status.Failure; }
    [Serializable, GeneratePropertyBag]
    [NodeDescription(name: "Pursuit: Search lost target", story: "Search the last seen location, then resume patrol", category: "Pursuit", id: "a481d526b02a4aa39727bc775a100005")]
    public partial class PursuitSearchAction : Unity.Behavior.Action
    { protected override Status OnStart() => GameObject.GetComponent<PursuitActor>().ActSearch() ? Status.Success : Status.Failure; }
    [Serializable, GeneratePropertyBag]
    [NodeDescription(name: "Pursuit: Patrol route", story: "Patrol the placement waypoints in order", category: "Pursuit", id: "a481d526b02a4aa39727bc775a100006")]
    public partial class PursuitPatrolAction : Unity.Behavior.Action
    { protected override Status OnStart() => GameObject.GetComponent<PursuitActor>().ActPatrol() ? Status.Success : Status.Failure; }
}
