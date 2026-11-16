using System;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.Cameras
{
    public sealed partial class CameraViewDirector
    {
        // Search below an overhead obstruction first, then follow either side of the corridor.
        static readonly Vector2[] FramingAngles =
        {
            new(0f, -20f), new(0f, -40f), new(0f, -58f),
            new(-22f, 0f), new(22f, 0f), new(-45f, 0f), new(45f, 0f),
            new(-72f, 0f), new(72f, 0f), new(-85f, 0f), new(85f, 0f),
            new(-28f, -30f), new(28f, -30f), new(-62f, -32f), new(62f, -32f)
        };
        Vector3 lastFramingArm, lastFramingPosition;
        bool reframing;
        readonly List<CameraCollisionSurface> sightSurfaces = new();
        const float FramingDamping = .36f;
        const float MinFramingDistance = 1.55f;
        const float ClearanceTolerance = .012f;

        Vector3 KeepSubjectVisible(Vector3 feet, float bodyHeight, Vector3 pivot,
            Vector3 intended, Vector3 radial, float radialLimit, bool reset, float step)
        {
            Vector3 arm = intended - pivot;
            float distance = arm.magnitude;
            CameraCollisionSurface.CollectSightSurfaces(SceneManager.GetActiveScene(),
                new Bounds(pivot, Vector3.one * (2f * (distance + bodyHeight))), sightSurfaces);
            Vector3 torso = feet + Vector3.up * Mathf.Max(.1f, bodyHeight * .58f);
            Vector3 head = feet + Vector3.up * Mathf.Max(.15f, bodyHeight * .94f);
            Vector3 previous = pivot + lastFramingArm;
            float anchorMotion = (previous - lastFramingPosition).sqrMagnitude;
            if (!reset && reframing && anchorMotion > .0001f && anchorMotion < 1f
                && !CameraArmReaches(pivot, previous) && CameraArmReaches(pivot, lastFramingPosition))
                previous = lastFramingPosition;
            Vector3 target = radial;
            float radialDistance = Vector3.Distance(radial, pivot);
            int visible = radialDistance < 1.2f ? 0 : VisibleParts(radial, torso, head);
            bool chooseAlternate = distance >= MinFramingDistance
                && (visible < 2 || Mathf.Min(radialLimit, radialDistance) < distance * .8f
                    || reframing && radialLimit < distance * .88f);
            if (chooseAlternate)
            {
                float best = FramingScore(radial, visible, pivot, intended, reset ? radial : previous) + .14f;
                void Consider(Vector3 direction)
                {
                    float reach = WallDistance(pivot, direction, distance);
                    if (reach < MinFramingDistance) return;
                    Vector3 point = pivot + direction * reach;
                    float maximumScore = FramingScore(point, 2, pivot, intended, reset ? radial : previous);
                    if (maximumScore <= best) return;
                    int sight = VisibleParts(point, torso, head);
                    if (sight < visible) return;
                    float score = maximumScore - 5f * (2 - sight);
                    if (score <= best) return;
                    best = score;
                    target = point;
                }

                if (!reset && (previous - pivot).sqrMagnitude > .01f)
                    Consider((previous - pivot).normalized);

                Vector3 direction = arm / distance;
                Vector3 horizontal = Horizontal(direction);
                if (horizontal != Vector3.zero)
                {
                    float pitchAngle = Mathf.Asin(Mathf.Clamp(direction.y, -1f, 1f)) * Mathf.Rad2Deg;
                    foreach (var adjustment in FramingAngles)
                        Consider(ArmAtElevation(Quaternion.AngleAxis(adjustment.x, Vector3.up) * horizontal,
                            pitchAngle + adjustment.y));
                }
            }

            bool diverted = (target - radial).sqrMagnitude > .001f;
            if (reset || !diverted && !reframing)
            {
                reframing = diverted;
                return target;
            }

            // Keep the previous shoulder/height while the intended arm turns through a wall.
            Vector3 motion = target - previous;
            Vector3 advance = Damper.Damp(motion, Vector3.one * FramingDamping, step);
            Vector3 position = previous + advance;
            if (CameraArmReaches(pivot, previous))
            {
                float length = advance.magnitude;
                if (length > ClearanceTolerance)
                    position = previous + advance * (WallDistance(previous, advance / length, length) / length);
                if (!CameraArmReaches(pivot, position)) position = previous;
            }
            else if (!CameraArmReaches(pivot, position))
            {
                // Geometry can move into the prior camera between updates. Start from a valid place.
                position = target;
            }
            reframing = diverted || (position - radial).sqrMagnitude > .0025f;
            return position;
        }

        bool CameraArmReaches(Vector3 pivot, Vector3 position)
        {
            Vector3 arm = position - pivot;
            float distance = arm.magnitude;
            return distance <= ClearanceTolerance || WallDistance(pivot, arm / distance, distance) >= distance - ClearanceTolerance;
        }

        static float FramingScore(Vector3 point, int visible, Vector3 pivot, Vector3 requested, Vector3 previous)
        {
            float reach = Vector3.Distance(point, pivot);
            return 5f * visible + Mathf.Min(reach, MinFramingDistance) + reach
                - .36f * Vector3.Distance(point, requested) - .4f * Vector3.Distance(point, previous);
        }

        int VisibleParts(Vector3 camera, Vector3 torso, Vector3 head)
            => (HasSight(camera, torso) ? 1 : 0) + (HasSight(camera, head) ? 1 : 0);

        bool HasSight(Vector3 camera, Vector3 bodyPart)
        {
            Vector3 line = bodyPart - camera;
            float distance = line.magnitude - .045f;
            if (distance <= 0f) return true;
            Vector3 direction = line.normalized;
            int count;
            while ((count = Physics.RaycastNonAlloc(camera, direction, wallHits, distance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) == wallHits.Length)
                Array.Resize(ref wallHits, wallHits.Length * 2);
            for (int i = 0; i < count; i++)
                if (BlocksCamera(wallHits[i].collider)) return false;
            return CameraCollisionSurface.HasSight(sightSurfaces, camera, direction, distance);
        }
    }
}
