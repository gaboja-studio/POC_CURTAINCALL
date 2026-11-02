using UnityEngine;

namespace CurtainCall.Cameras
{
    /// <summary>Camera-only, two-sided swept sphere against a static decorative mesh.
    /// World-space triangles preserve openings and support non-uniform/negative scale.
    /// No physics colliders, mesh copies, or per-frame allocations.</summary>
    internal sealed class CameraMeshCollision
    {
        struct Triangle
        {
            public Vector3 A, B, C, Normal;
            public Bounds Bounds;
        }

        Mesh cachedMesh;
        Matrix4x4 cachedMatrix;
        Triangle[] triangles;
        Bounds bounds;

        public bool Intersect(Mesh mesh, Matrix4x4 matrix, Vector3 origin, Vector3 direction,
            float radius, float maximum, out float distance)
        {
            distance = maximum;
            if (mesh == null || !mesh.isReadable || direction.sqrMagnitude < .000001f) return false;
            if (triangles == null || cachedMesh != mesh || cachedMatrix != matrix) Build(mesh, matrix);
            direction.Normalize();
            var ray = new Ray(origin, direction);
            if (!MayHit(bounds, ray, radius, distance)) return false;
            bool found = false;
            foreach (var triangle in triangles) {
                if (!MayHit(triangle.Bounds, ray, radius, distance)) continue;
                var a = triangle.A; var b = triangle.B; var c = triangle.C;
                var normal = triangle.Normal;
                float plane = Vector3.Dot(origin - a, normal);
                Vector3 projection = origin - normal * plane;
                if (Mathf.Abs(plane) <= radius && Inside(projection, triangle)) {
                    distance = 0; return true;
                }
                float speed = Vector3.Dot(direction, normal);
                if (Mathf.Abs(speed) > .000001f) {
                    // Both sides: the scene's visible material need not share mesh winding.
                    for (int side = -1; side <= 1; side += 2) {
                        float t = (side * radius - plane) / speed;
                        if (t >= 0 && t <= distance && Inside(origin + direction * t - normal * (side * radius), triangle)) {
                            distance = t; found = true;
                        }
                    }
                }
                found |= Edge(a, b, origin, direction, radius, ref distance);
                found |= Edge(b, c, origin, direction, radius, ref distance);
                found |= Edge(c, a, origin, direction, radius, ref distance);
            }
            return found;
        }

        void Build(Mesh mesh, Matrix4x4 matrix)
        {
            var vertices = mesh.vertices;
            for (int i = 0; i < vertices.Length; i++) vertices[i] = matrix.MultiplyPoint3x4(vertices[i]);
            var indices = mesh.triangles;
            triangles = new Triangle[indices.Length / 3];
            bounds = new Bounds(matrix.MultiplyPoint3x4(mesh.bounds.center), Vector3.zero);
            for (int i = 0; i < triangles.Length; i++) {
                var a = vertices[indices[i * 3]]; var b = vertices[indices[i * 3 + 1]]; var c = vertices[indices[i * 3 + 2]];
                var box = new Bounds(a, Vector3.zero); box.Encapsulate(b); box.Encapsulate(c);
                triangles[i] = new Triangle { A = a, B = b, C = c, Normal = Vector3.Cross(b - a, c - a).normalized, Bounds = box };
                bounds.Encapsulate(box);
            }
            cachedMesh = mesh; cachedMatrix = matrix;
        }

        static bool MayHit(Bounds box, Ray ray, float radius, float maximum)
        {
            box.Expand(radius * 2);
            return box.Contains(ray.origin) || (box.IntersectRay(ray, out float hit) && hit <= maximum);
        }

        static bool Inside(Vector3 point, Triangle t) => t.Normal.sqrMagnitude > .5f
            && Vector3.Dot(Vector3.Cross(t.B - t.A, point - t.A), t.Normal) >= -.00001f
            && Vector3.Dot(Vector3.Cross(t.C - t.B, point - t.B), t.Normal) >= -.00001f
            && Vector3.Dot(Vector3.Cross(t.A - t.C, point - t.C), t.Normal) >= -.00001f;

        // A triangle's rounded boundary is three capsules: cylinder plus endpoint spheres.
        static bool Edge(Vector3 a, Vector3 b, Vector3 origin, Vector3 direction, float radius, ref float distance)
        {
            Vector3 edge = b - a;
            float length = edge.magnitude;
            bool found = Sphere(a, origin, direction, radius, ref distance);
            found |= Sphere(b, origin, direction, radius, ref distance);
            if (length < .000001f) return found;
            Vector3 axis = edge / length;
            Vector3 offset = origin - a;
            float along = Vector3.Dot(offset, axis), velocity = Vector3.Dot(direction, axis);
            Vector3 radial = offset - axis * along, radialVelocity = direction - axis * velocity;
            float c = radial.sqrMagnitude - radius * radius;
            if (along >= 0 && along <= length && c <= 0) { distance = 0; return true; }
            float aa = radialVelocity.sqrMagnitude, bb = Vector3.Dot(radial, radialVelocity);
            float discriminant = bb * bb - aa * c;
            if (aa < .000001f || discriminant < 0) return found;
            float t = (-bb - Mathf.Sqrt(discriminant)) / aa;
            float at = along + velocity * t;
            if (t >= 0 && t <= distance && at >= 0 && at <= length) { distance = t; return true; }
            return found;
        }

        static bool Sphere(Vector3 center, Vector3 origin, Vector3 direction, float radius, ref float distance)
        {
            Vector3 offset = origin - center;
            float c = offset.sqrMagnitude - radius * radius;
            if (c <= 0) { distance = 0; return true; }
            float b = Vector3.Dot(offset, direction), discriminant = b * b - c;
            if (discriminant < 0) return false;
            float t = -b - Mathf.Sqrt(discriminant);
            if (t < 0 || t > distance) return false;
            distance = t; return true;
        }
    }
}
