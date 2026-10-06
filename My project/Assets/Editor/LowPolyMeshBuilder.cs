using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 면마다 정점을 따로 두는(각진) 로우폴리 메시 조립기. 색은 버텍스 색으로 넣는다 (KUCA/VertexColorLit).
/// 모든 도형은 행렬 m 의 로컬 좌표로 만들고, 감는 방향은 도형 안쪽 점을 기준으로 자동으로 맞춘다.
/// </summary>
public class LowPolyMeshBuilder
{
    readonly List<Vector3> v = new List<Vector3>();
    readonly List<Vector3> n = new List<Vector3>();
    readonly List<Color> c = new List<Color>();

    public int VertexCount => v.Count;

    /// <summary>inside(도형 안쪽 점)의 반대쪽을 바라보도록 감는 방향을 맞춘다.</summary>
    public void Tri(Matrix4x4 m, Vector3 a, Vector3 b, Vector3 d, Color col, Vector3 inside)
    {
        Vector3 centroid = (a + b + d) / 3f;
        if (Vector3.Dot(Vector3.Cross(b - a, d - a), centroid - inside) < 0f)
            (b, d) = (d, b);
        a = m.MultiplyPoint3x4(a); b = m.MultiplyPoint3x4(b); d = m.MultiplyPoint3x4(d);
        Vector3 nn = Vector3.Cross(b - a, d - a).normalized;
        v.Add(a); v.Add(b); v.Add(d);
        n.Add(nn); n.Add(nn); n.Add(nn);
        // 버텍스 색은 감마 보정을 거치지 않으므로 리니어 색 공간 기준으로 바꿔 넣는다.
        Color lin = QualitySettings.activeColorSpace == ColorSpace.Linear ? col.linear : col;
        c.Add(lin); c.Add(lin); c.Add(lin);
    }

    void Quad(Matrix4x4 m, Vector3 a, Vector3 b, Vector3 d, Vector3 e, Color col, Vector3 inside)
    {
        Tri(m, a, b, d, col, inside);
        Tri(m, a, d, e, col, inside);
    }

    /// <summary>로컬 중심 center, 크기 size 인 상자 (윗면은 조금 밝게)</summary>
    public void Box(Matrix4x4 m, Vector3 center, Vector3 size, Color col)
    {
        Vector3 h = size / 2f;
        Vector3 P(float x, float y, float z) => center + new Vector3(x * h.x, y * h.y, z * h.z);
        Color top = Shade(col, 1.05f), side = col, bottom = Shade(col, 0.8f);
        Quad(m, P(-1, 1, -1), P(1, 1, -1), P(1, 1, 1), P(-1, 1, 1), top, center);
        Quad(m, P(-1, -1, -1), P(1, -1, -1), P(1, -1, 1), P(-1, -1, 1), bottom, center);
        Quad(m, P(-1, -1, -1), P(1, -1, -1), P(1, 1, -1), P(-1, 1, -1), side, center);
        Quad(m, P(-1, -1, 1), P(1, -1, 1), P(1, 1, 1), P(-1, 1, 1), Shade(col, 0.97f), center);
        Quad(m, P(-1, -1, -1), P(-1, -1, 1), P(-1, 1, 1), P(-1, 1, -1), Shade(col, 0.93f), center);
        Quad(m, P(1, -1, -1), P(1, -1, 1), P(1, 1, 1), P(1, 1, -1), Shade(col, 0.95f), center);
    }

    /// <summary>세로 기둥 (각기둥). 윗면 포함</summary>
    public void Prism(Matrix4x4 m, Vector3 baseCenter, float r, float height, int sides, Color col, bool caps = false)
    {
        Vector3 axis = baseCenter + Vector3.up * height / 2f;
        for (int i = 0; i < sides; i++)
        {
            float a0 = i * Mathf.PI * 2 / sides, a1 = (i + 1) * Mathf.PI * 2 / sides;
            var p0 = baseCenter + new Vector3(Mathf.Cos(a0) * r, 0f, Mathf.Sin(a0) * r);
            var p1 = baseCenter + new Vector3(Mathf.Cos(a1) * r, 0f, Mathf.Sin(a1) * r);
            var q0 = p0 + Vector3.up * height;
            var q1 = p1 + Vector3.up * height;
            Quad(m, p0, q0, q1, p1, Shade(col, 0.92f + 0.12f * ((i * 3) % sides) / sides), axis);
            if (caps)
                Tri(m, q0, q1, baseCenter + Vector3.up * height, Shade(col, 1.05f), axis);
        }
    }

    /// <summary>원뿔 (sides=4 면 피라미드). rotationDeg 로 모서리 방향을 돌린다.</summary>
    public void Cone(Matrix4x4 m, Vector3 baseCenter, float r, float height, int sides, Color col, float rotationDeg = 0f)
    {
        var tip = baseCenter + Vector3.up * height;
        var inner = baseCenter + Vector3.up * height * 0.3f;
        float rot = rotationDeg * Mathf.Deg2Rad;
        for (int i = 0; i < sides; i++)
        {
            float a0 = rot + i * Mathf.PI * 2 / sides, a1 = rot + (i + 1) * Mathf.PI * 2 / sides;
            var p0 = baseCenter + new Vector3(Mathf.Cos(a0) * r, 0f, Mathf.Sin(a0) * r);
            var p1 = baseCenter + new Vector3(Mathf.Cos(a1) * r, 0f, Mathf.Sin(a1) * r);
            Tri(m, p0, tip, p1, Shade(col, 0.92f + 0.16f * ((i * 37) % 7) / 6f), inner);
            Tri(m, p0, p1, baseCenter, Shade(col, 0.8f), inner);
        }
    }

    /// <summary>위로 갈수록 좁아지는 사각 기둥 (오벨리스크 몸통)</summary>
    public void Frustum4(Matrix4x4 m, Vector3 baseCenter, float baseHalf, float topHalf, float height, Color col)
    {
        Vector3 inside = baseCenter + Vector3.up * height / 2f;
        Vector3 B(float x, float z) => baseCenter + new Vector3(x * baseHalf, 0f, z * baseHalf);
        Vector3 T(float x, float z) => baseCenter + new Vector3(x * topHalf, height, z * topHalf);
        float[,] s = { { -1, -1 }, { 1, -1 }, { 1, 1 }, { -1, 1 } };
        for (int i = 0; i < 4; i++)
        {
            int j = (i + 1) % 4;
            Quad(m, B(s[i, 0], s[i, 1]), B(s[j, 0], s[j, 1]), T(s[j, 0], s[j, 1]), T(s[i, 0], s[i, 1]), Shade(col, 0.9f + 0.05f * i), inside);
        }
    }

    /// <summary>찌그러뜨릴 수 있는 정이십면체 (나뭇잎 덩어리)</summary>
    public void Ico(Matrix4x4 m, Vector3 center, Vector3 radius, Color col)
    {
        for (int f = 0; f < IcoF.Length; f += 3)
        {
            Vector3 a = center + Vector3.Scale(IcoV[IcoF[f]], radius);
            Vector3 b = center + Vector3.Scale(IcoV[IcoF[f + 1]], radius);
            Vector3 d = center + Vector3.Scale(IcoV[IcoF[f + 2]], radius);
            Tri(m, a, d, b, Shade(col, 0.94f + 0.12f * ((f * 13) % 5) / 4f), center);
        }
    }

    /// <summary>반구 돔 (천문대 지붕)</summary>
    public void Hemisphere(Matrix4x4 m, Vector3 baseCenter, float r, int segments, int rings, Color col)
    {
        for (int ring = 0; ring < rings; ring++)
        {
            float t0 = ring * Mathf.PI / 2 / rings, t1 = (ring + 1) * Mathf.PI / 2 / rings;
            for (int s = 0; s < segments; s++)
            {
                float p0 = s * Mathf.PI * 2 / segments, p1 = (s + 1) * Mathf.PI * 2 / segments;
                Vector3 P(float th, float ph) => baseCenter + new Vector3(Mathf.Cos(th) * Mathf.Cos(ph), Mathf.Sin(th), Mathf.Cos(th) * Mathf.Sin(ph)) * r;
                Color sh = Shade(col, 0.95f + 0.05f * ((s + ring) % 2));
                Tri(m, P(t0, p0), P(t1, p0), P(t1, p1), sh, baseCenter);
                Tri(m, P(t0, p0), P(t1, p1), P(t0, p1), sh, baseCenter);
            }
        }
    }

    /// <summary>
    /// 부채꼴 계단식 좌석 한 단: 중심 center 기준 반지름 r0~r1, 각도 a0~a1(라디안, 로컬 XZ), 높이 0~h
    /// </summary>
    public void ArcTier(Matrix4x4 m, Vector3 center, float r0, float r1, float a0, float a1, float h, int segments, Color col)
    {
        Vector3 inside = center + Vector3.up * h * 0.5f + new Vector3(Mathf.Cos((a0 + a1) / 2), 0f, Mathf.Sin((a0 + a1) / 2)) * (r0 + r1) / 2;
        Vector3 At(float r, float a, float y) => center + new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
        for (int s = 0; s < segments; s++)
        {
            float b0 = Mathf.Lerp(a0, a1, (float)s / segments), b1 = Mathf.Lerp(a0, a1, (float)(s + 1) / segments);
            Vector3 segInside = center + Vector3.up * h * 0.5f + new Vector3(Mathf.Cos((b0 + b1) / 2), 0f, Mathf.Sin((b0 + b1) / 2)) * (r0 + r1) / 2;
            Quad(m, At(r0, b0, h), At(r1, b0, h), At(r1, b1, h), At(r0, b1, h), Shade(col, 1.04f), segInside);   // 앉는 면
            Quad(m, At(r0, b0, 0), At(r0, b0, h), At(r0, b1, h), At(r0, b1, 0), Shade(col, 0.88f), segInside);   // 앞 단 높이
            Quad(m, At(r1, b0, 0), At(r1, b0, h), At(r1, b1, h), At(r1, b1, 0), Shade(col, 0.9f), segInside);    // 뒤쪽
        }
        Quad(m, At(r0, a0, 0), At(r1, a0, 0), At(r1, a0, h), At(r0, a0, h), Shade(col, 0.92f), inside);
        Quad(m, At(r0, a1, 0), At(r1, a1, 0), At(r1, a1, h), At(r0, a1, h), Shade(col, 0.92f), inside);
    }

    /// <summary>삼각 지붕 (박공 / 페디먼트). 로컬 X 방향이 폭, Z 방향이 깊이, 꼭대기 선은 Z 방향</summary>
    public void Gable(Matrix4x4 m, Vector3 baseCenter, float width, float depth, float height, Color col, bool ridgeAlongZ = true)
    {
        float w = width / 2f, d = depth / 2f;
        Vector3 inside = baseCenter + Vector3.up * height * 0.3f;
        Vector3 P(float x, float y, float z) => baseCenter + new Vector3(x, y, z);
        if (ridgeAlongZ)
        {
            Quad(m, P(-w, 0, -d), P(0, height, -d), P(0, height, d), P(-w, 0, d), Shade(col, 0.95f), inside);
            Quad(m, P(w, 0, -d), P(0, height, -d), P(0, height, d), P(w, 0, d), Shade(col, 1.03f), inside);
            Tri(m, P(-w, 0, -d), P(w, 0, -d), P(0, height, -d), Shade(col, 0.9f), inside);
            Tri(m, P(-w, 0, d), P(w, 0, d), P(0, height, d), Shade(col, 0.92f), inside);
        }
        else
        {
            Quad(m, P(-w, 0, -d), P(-w, height, 0), P(w, height, 0), P(w, 0, -d), Shade(col, 0.95f), inside);
            Quad(m, P(-w, 0, d), P(-w, height, 0), P(w, height, 0), P(w, 0, d), Shade(col, 1.03f), inside);
            Tri(m, P(-w, 0, -d), P(-w, 0, d), P(-w, height, 0), Shade(col, 0.9f), inside);
            Tri(m, P(w, 0, -d), P(w, 0, d), P(w, height, 0), Shade(col, 0.92f), inside);
        }
    }

    /// <summary>얇은 원판 (시계 판 등). 로컬 Z 방향을 바라본다</summary>
    public void Disc(Matrix4x4 m, Vector3 center, float r, float thickness, int sides, Color col)
    {
        Vector3 back = center - Vector3.forward * thickness;
        for (int i = 0; i < sides; i++)
        {
            float a0 = i * Mathf.PI * 2 / sides, a1 = (i + 1) * Mathf.PI * 2 / sides;
            var p0 = center + new Vector3(Mathf.Cos(a0) * r, Mathf.Sin(a0) * r, 0f);
            var p1 = center + new Vector3(Mathf.Cos(a1) * r, Mathf.Sin(a1) * r, 0f);
            Tri(m, center, p0, p1, col, back);
        }
    }

    static Color Shade(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, 1f);

    static readonly Vector3[] IcoV;
    static readonly int[] IcoF =
    {
        0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11, 1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
        3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9, 4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1,
    };

    static LowPolyMeshBuilder()
    {
        float t = (1f + Mathf.Sqrt(5f)) / 2f;
        IcoV = new[]
        {
            new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
            new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
            new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
        };
        for (int i = 0; i < IcoV.Length; i++) IcoV[i] = IcoV[i].normalized;
    }

    public Mesh ToMesh(string name)
    {
        var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
        mesh.SetVertices(v);
        mesh.SetNormals(n);
        mesh.SetColors(c);
        var idx = new int[v.Count];
        for (int i = 0; i < idx.Length; i++) idx[i] = i;
        mesh.SetTriangles(idx, 0);
        mesh.RecalculateBounds();
        return mesh;
    }
}
