//for debugging


using UnityEngine;
using System.Collections.Generic;

[ExecuteAlways]
public class OctahedronIndependentUVVisualizer : MonoBehaviour
{
    [Range(0, 8)]
    public int subdivisionPerTriangle = 3;  

    public bool drawInGameView = true;
    public bool drawInSceneView = true;
    public Vector2 uvOffset = new Vector2(100, 100);
    public Vector2 uvSize = new Vector2(512, 512);
    public bool showWireframe = true;
    public bool showFilled = false;

    private List<List<Vector2[]>> perTriangleUVTriangles = new List<List<Vector2[]>>();

    void OnEnable()
    {
        GenerateIndependentSubdivision();
    }


    void GenerateIndependentSubdivision()
    {
        perTriangleUVTriangles.Clear();

        Vector3[][] originalTriangles = new Vector3[][]
        {
            new Vector3[] { new Vector3(0, 1, 0), new Vector3(1, 0, 0), new Vector3(0, 0, 1) },   // 上 X+,Z+
            new Vector3[] { new Vector3(0, 1, 0), new Vector3(0, 0, 1), new Vector3(-1, 0, 0) },  // 上 Z+,X-
            new Vector3[] { new Vector3(0, 1, 0), new Vector3(-1, 0, 0), new Vector3(0, 0, -1) }, // 上 X-,Z-
            new Vector3[] { new Vector3(0, 1, 0), new Vector3(0, 0, -1), new Vector3(1, 0, 0) },  // 上 Z-,X+
            new Vector3[] { new Vector3(0.01f, -1, 0.01f), new Vector3(0.01f, -0.01f, 1.01f), new Vector3(1.01f, -0.01f, 0.01f) },  // 下 Z+,X+
            new Vector3[] { new Vector3(-0.01f, -1, 0.01f), new Vector3(-1.01f, -0.01f, 0.01f), new Vector3(0.0f, -0.01f, 1.01f) }, // 下 X-,Z+
             new Vector3[] { new Vector3(-0.01f, -1, -0.01f), new Vector3(-0.01f, -0.01f, -1.01f), new Vector3(-1.01f, -0.01f, -0.01f) },// 下 Z-,X-
             new Vector3[] { new Vector3(0.01f, -1, -0.01f), new Vector3(1.01f, -0.01f, -0.01f), new Vector3(0.01f, -0.01f, -1.01f) }  // 下 X+,Z-
        };

        for (int t = 0; t < originalTriangles.Length; t++)
        {
            Vector3[] triVerts = originalTriangles[t];
            List<Vector3[]> subTriangles = SubdivideTriangle(triVerts[0], triVerts[1], triVerts[2], subdivisionPerTriangle);
            
            List<Vector2[]> uvTriangles = new List<Vector2[]>();
            foreach (var subTri in subTriangles)
            {
                Vector2 uv0 = DirectionToOctahedronUV(subTri[0].normalized);
                Vector2 uv1 = DirectionToOctahedronUV(subTri[1].normalized);
                Vector2 uv2 = DirectionToOctahedronUV(subTri[2].normalized);
                uvTriangles.Add(new Vector2[] { uv0, uv1, uv2 });
            }
            perTriangleUVTriangles.Add(uvTriangles);
        }
    }

    List<Vector3[]> SubdivideTriangle(Vector3 v0, Vector3 v1, Vector3 v2, int level)
    {
        List<Vector3[]> result = new List<Vector3[]>();
        if (level == 0)
        {
            result.Add(new Vector3[] { v0, v1, v2 });
            return result;
        }

        Vector3 v01 = (v0 + v1) * 0.5f;
        Vector3 v12 = (v1 + v2) * 0.5f;
        Vector3 v20 = (v2 + v0) * 0.5f;

        var sub1 = SubdivideTriangle(v0, v01, v20, level - 1);
        var sub2 = SubdivideTriangle(v1, v12, v01, level - 1);
        var sub3 = SubdivideTriangle(v2, v20, v12, level - 1);
        var sub4 = SubdivideTriangle(v01, v12, v20, level - 1);

        result.AddRange(sub1);
        result.AddRange(sub2);
        result.AddRange(sub3);
        result.AddRange(sub4);
        return result;
    }

    Vector2 DirectionToOctahedronUV(Vector3 dir)
    {
        float sum = Mathf.Abs(dir.x) + Mathf.Abs(dir.y) + Mathf.Abs(dir.z);
        float nx = dir.x / sum;
        float nz = dir.z / sum;

        if (dir.y <= 0)
        {
            float flipX = nx >= 0 ? 1f : -1f;
            float flipZ = nz >= 0 ? 1f : -1f;
            float newX = (1f - Mathf.Abs(nz)) * flipX;
            float newZ = (1f - Mathf.Abs(nx)) * flipZ;
            nx = newX;
            nz = newZ;
        }

        return new Vector2(nx * 0.5f + 0.5f, nz * 0.5f + 0.5f);
    }

    Vector2 UVToScreenPos(Vector2 uv)
    {
        return new Vector2(uvOffset.x + uv.x * uvSize.x, uvOffset.y + uv.y * uvSize.y);
    }

    void OnRenderObject()
    {
        bool shouldDraw = (drawInGameView && Camera.current == Camera.main) ||
                          (drawInSceneView && Camera.current != null && Camera.current.name == "SceneCamera");
        if (!shouldDraw) return;

        Material mat = new Material(Shader.Find("Hidden/Internal-Colored"));
        mat.SetPass(0);
        GL.PushMatrix();
        GL.LoadPixelMatrix();

        DrawAxes();

        for (int t = 0; t < perTriangleUVTriangles.Count; t++)
        {
            Color col = Color.HSVToRGB(t / 8f, 1f, 0.8f);
            var uvTris = perTriangleUVTriangles[t];
            
            if (showWireframe)
            {
                GL.Begin(GL.LINES);
                GL.Color(col);
                foreach (var tri in uvTris)
                {
                    Vector2 p0 = UVToScreenPos(tri[0]);
                    Vector2 p1 = UVToScreenPos(tri[1]);
                    Vector2 p2 = UVToScreenPos(tri[2]);
                    DrawLine(p0, p1);
                    DrawLine(p1, p2);
                    DrawLine(p2, p0);
                }
                GL.End();
            }

            if (showFilled)
            {
                GL.Begin(GL.TRIANGLES);
                GL.Color(new Color(col.r, col.g, col.b, 0.3f));
                foreach (var tri in uvTris)
                {
                    Vector2 p0 = UVToScreenPos(tri[0]);
                    Vector2 p1 = UVToScreenPos(tri[1]);
                    Vector2 p2 = UVToScreenPos(tri[2]);
                    GL.Vertex3(p0.x, p0.y, 0);
                    GL.Vertex3(p1.x, p1.y, 0);
                    GL.Vertex3(p2.x, p2.y, 0);
                }
                GL.End();
            }
        }

        GL.PopMatrix();
    }

    void DrawAxes()
    {
        Material mat = new Material(Shader.Find("Hidden/Internal-Colored"));
        mat.SetPass(0);
        GL.PushMatrix();
        GL.LoadPixelMatrix();
        GL.Begin(GL.LINES);
        GL.Color(Color.gray);
        Vector2 bl = UVToScreenPos(new Vector2(0, 0));
        Vector2 br = UVToScreenPos(new Vector2(1, 0));
        Vector2 tl = UVToScreenPos(new Vector2(0, 1));
        Vector2 tr = UVToScreenPos(new Vector2(1, 1));
        DrawLine(bl, br);
        DrawLine(br, tr);
        DrawLine(tr, tl);
        DrawLine(tl, bl);
        GL.End();
        GL.PopMatrix();
    }

    void DrawLine(Vector2 a, Vector2 b)
    {
        GL.Vertex3(a.x, a.y, 0);
        GL.Vertex3(b.x, b.y, 0);
    }
}