using System;
using System.Collections.Generic;
using UnityEngine;
namespace ValleyRail
{
    /// <summary>How a train part reflects light. Each finish is one material, so one submesh, of a car.</summary>
    public enum Finish { Paint, Metal, Glass, Matte }

    /// <summary>A colour and finish for one train part.</summary>
    public readonly struct Paint
    {
        public readonly Color color;
        public readonly Finish finish;
        public Paint(Color color, Finish finish = Finish.Paint)
        {
            this.color = color;
            this.finish = finish;
        }
        public static implicit operator Paint(Color color) => new Paint(color);
    }

    /// <summary>One cross-section of a lofted shell: the unit profile scaled by the half sizes around height <c>y</c>.</summary>
    public readonly struct Section
    {
        public readonly float y, halfWidth, halfHeight;
        public Section(float y, float halfWidth, float halfHeight)
        {
            this.y = y;
            this.halfWidth = halfWidth;
            this.halfHeight = halfHeight;
        }
        public static Section Span(float bottom, float top, float halfWidth) => new Section((bottom + top) * .5f, halfWidth, (top - bottom) * .5f);
    }

    /// <summary>
    /// Collects boxes, cylinders and lofted shells into one mesh per train car.
    /// Colours are texels of a shared palette texture, so a whole car draws with one material per finish.
    /// </summary>
    public sealed class TrainMeshBuilder
    {
        public const int FinishCount = 4;
        readonly Func<Color, Vector2> texel;
        readonly List<Vector3> vertices = new List<Vector3>(4096);
        readonly List<Vector3> normals = new List<Vector3>(4096);
        readonly List<Vector2> uvs = new List<Vector2>(4096);
        readonly List<int>[] triangles = new List<int>[FinishCount];

        public TrainMeshBuilder(Func<Color, Vector2> texel)
        {
            this.texel = texel ?? throw new ArgumentNullException(nameof(texel));
            for (int i = 0; i < FinishCount; i++)
                triangles[i] = new List<int>(4096);
        }

        public void Box(Vector3 center, Vector3 size, Paint paint) => Box(center, size, paint, Quaternion.identity);
        public void Box(Vector3 center, Vector3 size, Paint paint, Quaternion rotation)
        {
            var uv = texel(paint.color);
            var half = size * .5f;
            for (int axis = 0; axis < 3; axis++)
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    Vector3 n = Vector3.zero, u = Vector3.zero, v = Vector3.zero;
                    n[axis] = sign;
                    u[(axis + 1) % 3] = half[(axis + 1) % 3];
                    v[(axis + 2) % 3] = half[(axis + 2) % 3];
                    Vector3 face = Vector3.Scale(n, half), normal = rotation * n;
                    int a = Vertex(center + rotation * (face - u - v), normal, uv);
                    int b = Vertex(center + rotation * (face + u - v), normal, uv);
                    int c = Vertex(center + rotation * (face + u + v), normal, uv);
                    int d = Vertex(center + rotation * (face - u + v), normal, uv);
                    Quad(paint.finish, a, b, c, d, normal);
                }
        }

        /// <summary>A cylinder along the local z axis of <paramref name="rotation"/>, with smooth sides and flat caps.</summary>
        public void Cylinder(Vector3 center, float radius, float length, Paint paint, Quaternion rotation, int sides = 12, Paint? caps = null)
        {
            var side = texel(paint.color);
            var cap = caps ?? paint;
            var capUv = texel(cap.color);
            var axis = rotation * Vector3.forward * (length * .5f);
            var ring = new Vector3[sides];
            for (int i = 0; i < sides; i++)
            {
                float angle = i * 2 * Mathf.PI / sides;
                ring[i] = rotation * new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0);
            }
            for (int i = 0; i < sides; i++)
            {
                Vector3 r0 = ring[i], r1 = ring[(i + 1) % sides];
                int a = Vertex(center + r0 * radius - axis, r0, side);
                int b = Vertex(center + r1 * radius - axis, r1, side);
                int c = Vertex(center + r1 * radius + axis, r1, side);
                int d = Vertex(center + r0 * radius + axis, r0, side);
                Quad(paint.finish, a, b, c, d, r0 + r1);
            }
            for (int s = -1; s <= 1; s += 2)
            {
                var normal = axis.normalized * s;
                var middle = center + axis * s;
                int hub = Vertex(middle, normal, capUv);
                int first = vertices.Count;
                for (int i = 0; i < sides; i++)
                    Vertex(middle + ring[i] * radius, normal, capUv);
                for (int i = 0; i < sides; i++)
                    Triangle(cap.finish, hub, first + i, first + (i + 1) % sides, normal);
            }
        }

        /// <summary>
        /// Sweeps a profile along z through <paramref name="stations"/>. The profile is a set of smooth polylines
        /// in unit space (x and y in [-1, 1], clockwise seen from the front); the points they share are sharp edges.
        /// <paramref name="paint"/> colours each face from its z and its unit-profile position, so bands and windows
        /// line up with the stations and profile points.
        /// </summary>
        public void Loft(Vector2[][] profile, float[] stations, Func<float, Section> section, Func<float, Vector2, Paint> paint, Paint? backCap = null, Paint? frontCap = null)
        {
            int rings = stations.Length;
            var shapes = new Section[rings];
            for (int r = 0; r < rings; r++)
                shapes[r] = section(stations[r]);
            foreach (var strip in profile)
            {
                int count = strip.Length;
                var grid = new Vector3[rings, count];
                for (int r = 0; r < rings; r++)
                    for (int j = 0; j < count; j++)
                        grid[r, j] = Point(strip[j], shapes[r], stations[r]);
                var smooth = new Vector3[rings, count];
                for (int r = 0; r < rings; r++)
                    for (int j = 0; j < count; j++)
                    {
                        var along = grid[r, Mathf.Min(j + 1, count - 1)] - grid[r, Mathf.Max(j - 1, 0)];
                        var forward = grid[Mathf.Min(r + 1, rings - 1), j] - grid[Mathf.Max(r - 1, 0), j];
                        var n = Vector3.Cross(forward, along);
                        smooth[r, j] = n.sqrMagnitude > 1e-12f ? n.normalized : new Vector3(strip[j].x, strip[j].y, 0).normalized;
                    }
                for (int r = 0; r + 1 < rings; r++)
                    for (int j = 0; j + 1 < count; j++)
                    {
                        var p = paint((stations[r] + stations[r + 1]) * .5f, (strip[j] + strip[j + 1]) * .5f);
                        var uv = texel(p.color);
                        int a = Vertex(grid[r, j], smooth[r, j], uv);
                        int b = Vertex(grid[r, j + 1], smooth[r, j + 1], uv);
                        int c = Vertex(grid[r + 1, j + 1], smooth[r + 1, j + 1], uv);
                        int d = Vertex(grid[r + 1, j], smooth[r + 1, j], uv);
                        Quad(p.finish, a, b, c, d, smooth[r, j] + smooth[r, j + 1] + smooth[r + 1, j] + smooth[r + 1, j + 1]);
                    }
            }
            if (backCap.HasValue)
                Cap(profile, shapes[0], stations[0], -1, backCap.Value);
            if (frontCap.HasValue)
                Cap(profile, shapes[rings - 1], stations[rings - 1], 1, frontCap.Value);
        }

        static Vector3 Point(Vector2 unit, Section s, float z) => new Vector3(unit.x * s.halfWidth, s.y + unit.y * s.halfHeight, z);

        void Cap(Vector2[][] profile, Section s, float z, int side, Paint paint)
        {
            var uv = texel(paint.color);
            var normal = new Vector3(0, 0, side);
            int hub = Vertex(new Vector3(0, s.y, z), normal, uv);
            int first = vertices.Count;
            foreach (var strip in profile)
                for (int j = 0; j + 1 < strip.Length; j++)
                    Vertex(Point(strip[j], s, z), normal, uv);
            int count = vertices.Count - first;
            for (int i = 0; i < count; i++)
                Triangle(paint.finish, hub, first + i, first + (i + 1) % count, normal);
        }

        int Vertex(Vector3 position, Vector3 normal, Vector2 uv)
        {
            vertices.Add(position);
            normals.Add(normal);
            uvs.Add(uv);
            return vertices.Count - 1;
        }

        // Winds the triangle so it faces along `facing`, whatever order the corners come in.
        void Triangle(Finish finish, int a, int b, int c, Vector3 facing)
        {
            var list = triangles[(int)finish];
            var n = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
            list.Add(a);
            if (Vector3.Dot(n, facing) < 0)
            {
                list.Add(c);
                list.Add(b);
            }
            else
            {
                list.Add(b);
                list.Add(c);
            }
        }

        void Quad(Finish finish, int a, int b, int c, int d, Vector3 facing)
        {
            Triangle(finish, a, b, c, facing);
            Triangle(finish, a, c, d, facing);
        }

        /// <summary>
        /// Creates the mesh with one submesh per finish that was used, in the order returned by <paramref name="finishes"/>.
        /// A reversed car is turned half round, so a cab drawn facing forward ends up at the back of the train.
        /// </summary>
        public Mesh Build(string name, bool reversed, out Finish[] finishes)
        {
            if (reversed)
                for (int i = 0; i < vertices.Count; i++)
                {
                    vertices[i] = new Vector3(-vertices[i].x, vertices[i].y, -vertices[i].z);
                    normals[i] = new Vector3(-normals[i].x, normals[i].y, -normals[i].z);
                }
            var used = new List<Finish>();
            for (int i = 0; i < FinishCount; i++)
                if (triangles[i].Count > 0)
                    used.Add((Finish)i);
            var mesh = new Mesh { name = name };
            if (vertices.Count > 65535)
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = used.Count;
            for (int i = 0; i < used.Count; i++)
                mesh.SetTriangles(triangles[(int)used[i]], i);
            mesh.RecalculateBounds();
            mesh.UploadMeshData(false);
            finishes = used.ToArray();
            return mesh;
        }
    }
}
