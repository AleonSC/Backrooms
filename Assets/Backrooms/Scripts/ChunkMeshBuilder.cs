using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Backrooms
{
    /// <summary>
    /// Builds the combined mesh for one chunk. Submeshes: walls, floor, ceiling,
    /// lit fixtures and dead fixtures. Flickering fixtures are returned separately
    /// so they can be animated individually.
    /// </summary>
    public static class ChunkMeshBuilder
    {
        public const int SubWall = 0;
        public const int SubFloor = 1;
        public const int SubCeiling = 2;
        public const int SubFixtureOn = 3;
        public const int SubFixtureOff = 4;
        public const int SubmeshCount = 5;

        public static readonly Vector3 FixtureSize = new Vector3(0.6f, 0.03f, 1.2f);

        public struct Fixture
        {
            public int Cx, Cz;
            public Vector3 Position;
            public LightState State;
        }

        sealed class Builder
        {
            public readonly List<Vector3> Vertices = new List<Vector3>();
            public readonly List<Vector3> Normals = new List<Vector3>();
            public readonly List<Vector2> Uvs = new List<Vector2>();
            public readonly List<int>[] Triangles = new List<int>[SubmeshCount];

            public Builder()
            {
                for (int i = 0; i < SubmeshCount; i++) Triangles[i] = new List<int>();
            }

            /// <summary>
            /// Adds a quad given its bottom-left corner and the viewer's right/up edge
            /// vectors as seen from the front. Unity treats clockwise triangles as front-facing.
            /// </summary>
            public void Face(int sub, Vector3 bl, Vector3 right, Vector3 up, System.Func<Vector3, Vector2> uv)
            {
                Vector3 normal = Vector3.Cross(up, right).normalized;
                int i = Vertices.Count;
                Vector3 a = bl, b = bl + up, c = bl + up + right, d = bl + right;
                Vertices.Add(a); Vertices.Add(b); Vertices.Add(c); Vertices.Add(d);
                for (int k = 0; k < 4; k++) Normals.Add(normal);
                Uvs.Add(uv(a)); Uvs.Add(uv(b)); Uvs.Add(uv(c)); Uvs.Add(uv(d));
                var t = Triangles[sub];
                t.Add(i); t.Add(i + 1); t.Add(i + 2);
                t.Add(i); t.Add(i + 2); t.Add(i + 3);
            }

            /// <summary>Vertical sides of an axis-aligned box with wallpaper mapped in world space.</summary>
            public void Sides(int sub, Vector3 min, Vector3 max, bool xFaces, bool zFaces)
            {
                Vector3 size = max - min;
                Vector3 up = new Vector3(0, size.y, 0);
                if (zFaces)
                {
                    Face(sub, new Vector3(max.x, min.y, max.z), new Vector3(-size.x, 0, 0), up, p => WallUv(-p.x, p.y)); // +Z
                    Face(sub, new Vector3(min.x, min.y, min.z), new Vector3(size.x, 0, 0), up, p => WallUv(p.x, p.y));   // -Z
                }
                if (xFaces)
                {
                    Face(sub, new Vector3(max.x, min.y, min.z), new Vector3(0, 0, size.z), up, p => WallUv(p.z, p.y));   // +X
                    Face(sub, new Vector3(min.x, min.y, max.z), new Vector3(0, 0, -size.z), up, p => WallUv(-p.z, p.y)); // -X
                }
            }

            static Vector2 WallUv(float along, float y)
            {
                return new Vector2(along / ProceduralTextures.WallpaperWidth, y / BackroomsGenerator.WallHeight);
            }

            /// <summary>A thin panel hanging just below the ceiling, facing down, with a bevel rim.</summary>
            public void FixturePanel(int sub, Vector3 centre)
            {
                Vector3 h = FixtureSize * 0.5f;
                float y = centre.y - FixtureSize.y;
                Vector3 min = new Vector3(centre.x - h.x, y, centre.z - h.z);
                Vector3 max = new Vector3(centre.x + h.x, centre.y, centre.z + h.z);
                System.Func<Vector3, Vector2> flat = p => Vector2.zero;
                Face(sub, new Vector3(max.x, y, min.z), new Vector3(-FixtureSize.x, 0, 0), new Vector3(0, 0, FixtureSize.z), flat); // facing down
                Vector3 up = new Vector3(0, FixtureSize.y, 0);
                Face(sub, new Vector3(max.x, y, max.z), new Vector3(-FixtureSize.x, 0, 0), up, flat);
                Face(sub, new Vector3(min.x, y, min.z), new Vector3(FixtureSize.x, 0, 0), up, flat);
                Face(sub, new Vector3(max.x, y, min.z), new Vector3(0, 0, FixtureSize.z), up, flat);
                Face(sub, new Vector3(min.x, y, max.z), new Vector3(0, 0, -FixtureSize.z), up, flat);
            }
        }

        public static Mesh Build(BackroomsGenerator gen, int kx, int kz, List<Fixture> fixtures)
        {
            const float C = BackroomsGenerator.Cell;
            const float H = BackroomsGenerator.WallHeight;
            const float T = BackroomsGenerator.WallThickness;
            const float P = BackroomsGenerator.PillarSize;
            const int N = BackroomsGenerator.ChunkCells;

            var b = new Builder();
            int cx0 = kx * N, cz0 = kz * N;
            float x0 = cx0 * C, z0 = cz0 * C, size = N * C;

            // Floor and ceiling for the whole chunk.
            b.Face(SubFloor, new Vector3(x0, 0, z0), new Vector3(size, 0, 0), new Vector3(0, 0, size),
                p => new Vector2(p.x, p.z) / ProceduralTextures.CarpetSize);
            b.Face(SubCeiling, new Vector3(x0 + size, H, z0), new Vector3(-size, 0, 0), new Vector3(0, 0, size),
                p => new Vector2(-(p.x + 0.3f), p.z + 0.3f) / ProceduralTextures.CeilingSize);

            for (int cx = cx0; cx < cx0 + N; cx++)
            {
                for (int cz = cz0; cz < cz0 + N; cz++)
                {
                    // Wall segments stop T/2 short of each corner; corner posts fill the joints
                    // so faces never overlap and z-fight.
                    if (gen.WallEast(cx, cz))
                    {
                        float x = (cx + 1) * C;
                        b.Sides(SubWall, new Vector3(x - T / 2, 0, cz * C + T / 2), new Vector3(x + T / 2, H, (cz + 1) * C - T / 2), true, false);
                    }
                    if (gen.WallSouth(cx, cz))
                    {
                        float z = (cz + 1) * C;
                        b.Sides(SubWall, new Vector3(cx * C + T / 2, 0, z - T / 2), new Vector3((cx + 1) * C - T / 2, H, z + T / 2), false, true);
                    }

                    Vector3 corner = new Vector3(cx * C, 0, cz * C);
                    if (gen.Pillar(cx, cz))
                    {
                        b.Sides(SubWall, corner + new Vector3(-P / 2, 0, -P / 2), corner + new Vector3(P / 2, H, P / 2), true, true);
                    }
                    else if (gen.CornerHasWall(cx, cz))
                    {
                        b.Sides(SubWall, corner + new Vector3(-T / 2, 0, -T / 2), corner + new Vector3(T / 2, H, T / 2), true, true);
                    }

                    var state = gen.GetLight(cx, cz);
                    if (state == LightState.None) continue;
                    var pos = BackroomsGenerator.FixturePosition(cx, cz);
                    fixtures.Add(new Fixture { Cx = cx, Cz = cz, Position = pos, State = state });
                    if (state == LightState.On) b.FixturePanel(SubFixtureOn, pos);
                    else if (state == LightState.Off) b.FixturePanel(SubFixtureOff, pos);
                }
            }

            var mesh = new Mesh { name = "Backrooms Chunk " + kx + "," + kz };
            mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(b.Vertices);
            mesh.SetNormals(b.Normals);
            mesh.SetUVs(0, b.Uvs);
            mesh.subMeshCount = SubmeshCount;
            for (int i = 0; i < SubmeshCount; i++) mesh.SetTriangles(b.Triangles[i], i, false);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Single fixture mesh used for individually animated (flickering) lights.</summary>
        public static Mesh BuildFixtureMesh()
        {
            var b = new Builder();
            b.FixturePanel(0, Vector3.zero);
            var mesh = new Mesh { name = "Backrooms Fixture" };
            mesh.SetVertices(b.Vertices);
            mesh.SetNormals(b.Normals);
            mesh.SetUVs(0, b.Uvs);
            mesh.SetTriangles(b.Triangles[0], 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
