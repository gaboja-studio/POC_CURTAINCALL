#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using System.Linq;
using CurtainCall.Cameras;
using CurtainCall.HumanCannon;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.Settings;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>Opt-in review input only. The original pilot, ownership, transport and bounce systems judge every action.</summary>
    public sealed class RehearsalCannonReviewBrain
    {
        readonly NetworkPlayer player;
        readonly int carrierSlot;
        readonly Dictionary<ulong, Vector3> positions = new(), velocities = new();
        double nextTap;
        public string Status { get; private set; }
        public RehearsalCannonReviewBrain(NetworkPlayer player, int carrierSlot) { this.player = player; this.carrierSlot = carrierSlot; }

        public PlayerInputFrame BuildInput()
        {
            var review = Object.FindFirstObjectByType<WaitingRoomCannonRehearsal>();
            var course = review != null ? review.Course : null;
            if (course == null || !WaitingRoomCannonRehearsal.IsActive) { Status = "Waiting for cannon lesson"; return default; }
            var cannon = course.Cannon;
            var trampoline = course.Trampoline;
            SamplePlayers();
            if (player.State != PlayerState.Normal) { Status = "Waiting for checkpoint recovery"; return default; }
            ulong id = player.OwnerClientId;
            if (WaitingRoomCannonRehearsal.HasArrived(id)) { Status = "Arrived; waiting for teammates"; return default; }
            bool middle = WaitingRoomCannonRehearsal.Checkpoint(id) > 0;
            if (cannon.IsSeated(id))
            { Status = "Fresh F when boarding is ready"; return new PlayerInputFrame { InteractTapped = Tap() }; }
            if (cannon.TryGetFlight(id, out var flight))
            {
                if (flight.StationIndex < 0 || flight.Segment > 0)
                { Status = "Steering original rebound toward finish"; return Walk(course.FinishFloor.bounds.center); }
                Status = middle ? "Original second cannon flight; waiting for catch" : "Original first cannon flight toward middle";
                return default;
            }
            if (!middle) return Board(course, Mathf.Max(0, player.Slot) % 2);
            if (player.Slot == carrierSlot) return Carry(course);
            if (!RehearsalCannonCourse.Contains(course.MiddleFloor, player.transform.position))
            {
                // A successful stationary self-bounce uses the same native ground-bounce path as the last carrier.
                if (trampoline.Carrier == CannonSeatState.Nobody) return SelfBounce(course);
                Status = "Waiting for original catch/recovery"; return default;
            }
            var carrier = NetworkPlayer.All.FirstOrDefault(p => p != null && p.Slot == carrierSlot);
            if (carrier == null || trampoline.Carrier != carrier.OwnerClientId || course.Route.IsRiding(carrier.OwnerClientId)
                || carrier.transform.position.y > course.LowerFloor.bounds.max.y + .5f)
            { Status = "Waiting for carrier on lower floor"; return default; }
            var flyers = NetworkPlayer.All.Where(p => p != null && p.IsSpawned && p.Slot != carrierSlot && p.State == PlayerState.Normal
                && WaitingRoomCannonRehearsal.Checkpoint(p.OwnerClientId) > 0 && !WaitingRoomCannonRehearsal.HasArrived(p.OwnerClientId)).OrderBy(p => p.Slot).ToArray();
            var busy = flyers.FirstOrDefault(p => cannon.IsFlying(p.OwnerClientId) || cannon.IsSeated(p.OwnerClientId));
            if ((busy != null && busy != player) || (busy == null && flyers.FirstOrDefault() != player))
            { Status = "Waiting for previous teammate to land"; return default; }
            return Board(course, 2 + Mathf.Max(0, player.Slot) % 2);
        }

        PlayerInputFrame Board(RehearsalCannonCourse course, int index)
        {
            var station = course.Stations[index];
            if (course.Cannon.Occupant(index) != CannonSeatState.Nobody)
            { Status = "Waiting for original cannon seat"; return default; }
            var input = Walk(station.BoardingPoint.position);
            if (Vector3.ProjectOnPlane(player.transform.position - station.BoardingPoint.position, Vector3.up).magnitude < .25f)
            { input = default; input.InteractTapped = Tap(); }
            Status = "Walking to original cannon " + index;
            return input;
        }

        PlayerInputFrame Carry(RehearsalCannonCourse course)
        {
            var trampoline = course.Trampoline;
            ulong id = player.OwnerClientId;
            bool othersArrived = NetworkPlayer.All.Where(p => p != null && p.IsSpawned && p != player && p.State != PlayerState.Retired)
                .All(p => WaitingRoomCannonRehearsal.HasArrived(p.OwnerClientId));
            if (othersArrived)
            {
                if (trampoline.Carrier == id)
                {
                    // A self jump needs open headroom. Do not release below the raised finish deck.
                    var finish = course.FinishFloor.bounds;
                    if (trampoline.Surface.z > finish.min.z - 2.5f)
                    {
                        var safeSurface = course.LowerFloor.bounds.center;
                        safeSurface.z = course.LowerFloor.bounds.min.z + 2.8f;
                        Status = "Moving fabric clear of overhead finish deck";
                        return Walk(safeSurface - Vector3.ProjectOnPlane(trampoline.Surface - player.transform.position, Vector3.up));
                    }
                    Status = "Last carrier releases prop";
                    return new PlayerInputFrame { InteractLongPressed = true, InteractHoldProgress = 1 };
                }
                return SelfBounce(course);
            }
            if (trampoline.Carrier != id)
            {
                if (trampoline.Carrier != CannonSeatState.Nobody) { Status = "Waiting for trampoline handle"; return default; }
                var handle = trampoline.Handle.position;
                // Use the rear aisle rather than walking straight through the two original cannon bodies.
                var approach = handle;
                if (player.transform.position.x < handle.x - .3f)
                    approach.z = course.MiddleFloor.bounds.max.z - 1f;
                var input = Walk(approach);
                if (Vector3.ProjectOnPlane(player.transform.position - trampoline.Handle.position, Vector3.up).magnitude < .25f && trampoline.CanGrab(player))
                { input = default; input.InteractTapped = Tap(); }
                Status = "Walking to original trampoline handle"; return input;
            }
            if (course.Route.IsRiding(id)) { Status = "Original transport in motion"; return default; }
            Vector3 offset = Vector3.ProjectOnPlane(trampoline.Surface - player.transform.position, Vector3.up);
            if (!course.Route.TransportState(0).Delivered)
            {
                if (course.Route.CanStart(player, 0))
                { Status = "Waiting for native boarding countdown"; return default; }
                Status = "Boarding original transport as sole carrier";
                return Walk(course.Transport.StartPosition);
            }
            var flyer = NetworkPlayer.All.FirstOrDefault(p => p != null && p != player && course.Cannon.TryGetFlight(p.OwnerClientId, out var incoming)
                && incoming.StationIndex >= 2 && incoming.Segment == 0
                && WaitingRoomCannonRehearsal.Checkpoint(p.OwnerClientId) > 0 && !WaitingRoomCannonRehearsal.HasArrived(p.OwnerClientId));
            var b = course.LowerFloor.bounds;
            Vector3 surface = new Vector3(b.center.x, trampoline.Surface.y, b.min.z + 2.8f);
            if (flyer != null)
                surface = HumanCannonBotSteering.PredictCatch(flyer.transform.position, velocities.GetValueOrDefault(flyer.OwnerClientId),
                    trampoline.Surface.y, GameSettings.Base.Character.Gravity, 4f);
            surface.x = Mathf.Clamp(surface.x, b.min.x + 2.6f, b.max.x - 2.6f);
            surface.z = Mathf.Clamp(surface.z, b.min.z + 2.6f, b.max.z - 2.6f);
            var target = surface - offset; target.y = b.max.y;
            target.x = Mathf.Clamp(target.x, b.min.x + .4f, b.max.x - .4f);
            target.z = Mathf.Clamp(target.z, b.min.z + .4f, b.max.z - .4f);
            Status = flyer != null ? "Tracking native falling teammate" : "Waiting at catch area";
            return Walk(target);
        }

        PlayerInputFrame SelfBounce(RehearsalCannonCourse course)
        {
            var surface = course.Trampoline.Surface;
            var input = Walk(surface);
            if (Vector3.ProjectOnPlane(surface - player.transform.position, Vector3.up).magnitude < .2f)
            { input = default; input.ActionPressed = Tap(); }
            Status = "Jumping onto original fabric for self rebound";
            return input;
        }

        PlayerInputFrame Walk(Vector3 target)
        {
            Vector3 forward = CameraViewDirector.Current != null ? CameraViewDirector.Current.ViewForward : Vector3.forward;
            Vector2 axis = HumanCannonBotSteering.PlanarInput(player.transform.position, target, forward, .12f);
            return new PlayerInputFrame { Move = axis.y, Posture = axis.x };
        }
        bool Tap()
        {
            if (Time.timeAsDouble < nextTap) return false;
            nextTap = Time.timeAsDouble + .8d; return true;
        }
        void SamplePlayers()
        {
            foreach (var p in NetworkPlayer.All)
            {
                if (p == null) continue;
                if (positions.TryGetValue(p.OwnerClientId, out var previous) && Time.deltaTime > 0)
                    velocities[p.OwnerClientId] = Vector3.Lerp(velocities.GetValueOrDefault(p.OwnerClientId),
                        Vector3.ClampMagnitude((p.transform.position - previous) / Time.deltaTime, 35f), .35f);
                positions[p.OwnerClientId] = p.transform.position;
            }
        }
    }
}
#endif
