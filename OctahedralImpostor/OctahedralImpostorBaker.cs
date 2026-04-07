using UnityEngine;
using UnityEditor;

public class OctahedralImpostorBaker : EditorWindow
{
    private GameObject targetObject;
    private int tilesPerSide = 32;
    private int tileResolution = 32;
    private float distanceScale = 2.0f;          // 相机距离 = 包围盒半径 * distanceScale
    private Shader normalReplacementShader;       

    [MenuItem("Tools/Octahedral Impostor Baker")]
    public static void ShowWindow()
    {
        GetWindow<OctahedralImpostorBaker>("Impostor Baker");
    }

    private void OnGUI()
    {
        targetObject = (GameObject)EditorGUILayout.ObjectField("Target", targetObject, typeof(GameObject), true);
        tilesPerSide = EditorGUILayout.IntField("Tiles Per Side", tilesPerSide);
        tileResolution = EditorGUILayout.IntField("Tile Resolution", tileResolution);
        distanceScale = EditorGUILayout.FloatField("Distance Scale", distanceScale);
        normalReplacementShader = (Shader)EditorGUILayout.ObjectField("Normal Replacement Shader", normalReplacementShader, typeof(Shader), false);

        if (GUILayout.Button("Bake Impostor"))
        {
            if (targetObject == null) { Debug.LogError("No target object selected."); return; }
            if (normalReplacementShader == null) { 
                //to do 
            }
            BakeImpostor();
        }
    }

    Vector2 DebugOctEnc(Vector3 vec)
    {
        vec = Vector3.Normalize(vec);
        // vec.xz /= dot( 1,  abs(vec) );
        //         if ( vec.y <= 0 )
        //         {
        //             half2 flip = vec.xz >= 0 ? half2(1,1) : half2(-1,-1);
        //             vec.xz = (1-abs(vec.zx)) * flip;
        //         }
        //         return vec.xz * 0.5 + 0.5;
        float sum = Mathf.Abs(vec.x) + Mathf.Abs(vec.y) + Mathf.Abs(vec.z);
        float nx = vec.x / sum;
        float nz = vec.z / sum;

        if (vec.y <= 0)
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

    private void BakeImpostor()
    {
        Renderer renderer = targetObject.GetComponent<Renderer>();
        if (renderer == null) { Debug.LogError("Target object has no Renderer."); return; }
        Bounds bounds = renderer.bounds;

        int atlasSize = tilesPerSide * tileResolution;

      
        RenderTexture atlasAlbedoRT = new RenderTexture(atlasSize, atlasSize, 0, RenderTextureFormat.ARGB32);
        atlasAlbedoRT.Create();
 

        GameObject tempCameraGo = new GameObject("TempBakeCamera");
        Camera cam = tempCameraGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0, 0, 0, 0);
        cam.orthographicSize = bounds.extents.magnitude;

        RenderTexture rtAlbedo = RenderTexture.GetTemporary(tileResolution, tileResolution, 24, RenderTextureFormat.ARGB32);

        int originalLayer = targetObject.layer;
        int tempLayer = 31; 
        targetObject.layer = tempLayer;
        cam.cullingMask = 1 << tempLayer;


        for (int i = 0; i < tilesPerSide; i++)
        {
            for (int j = 0; j < tilesPerSide; j++)
            {
     
                float u = (i + 0.5f) / tilesPerSide;
                float v = (j + 0.5f) / tilesPerSide;
      
                Vector3 localDir = OctahedronUVToDirection(new Vector2(u, v));

                Vector2 encUV = DebugOctEnc(localDir);

                if(!Mathf.Approximately(u,encUV.x) ||  !Mathf.Approximately(v,encUV.y))
                {  
                    Debug.LogError("fatal error");
                    Debug.Break();
                }

                Vector3 worldDir = targetObject.transform.TransformDirection(localDir);
                Vector3 cameraPos = bounds.center + worldDir * bounds.extents.magnitude * distanceScale;
                cam.transform.position = cameraPos;

                cam.targetTexture = rtAlbedo;
                cam.Render();

                // 将临时 RT 拷贝到图集 RT 的对应区域
                int dstX = i * tileResolution;
                int dstY = j * tileResolution;
                Graphics.CopyTexture(rtAlbedo, 0, 0, 0, 0, tileResolution, tileResolution, atlasAlbedoRT, 0, 0, dstX, dstY);
            
            }
        }

        targetObject.layer = originalLayer;

        string folderPath = "Assets/OctahedralImpostor/";
        if (!System.IO.Directory.Exists(folderPath))
            System.IO.Directory.CreateDirectory(folderPath);

        string albedoPath = folderPath + targetObject.name + "_ImpostorAlbedo.png";

        SaveRenderTextureToPNG(atlasAlbedoRT, albedoPath);
  
        OctahedralImpostorData data = ScriptableObject.CreateInstance<OctahedralImpostorData>();
        data.albedoAtlas = AssetDatabase.LoadAssetAtPath<Texture2D>(albedoPath);

        data.tilesPerSide = tilesPerSide;
        data.tileResolution = tileResolution;
        data.bounds = bounds;

        string dataPath = folderPath + targetObject.name + "_ImpostorData.asset";
        AssetDatabase.CreateAsset(data, dataPath);
        AssetDatabase.SaveAssets();

        RenderTexture.ReleaseTemporary(rtAlbedo);

        atlasAlbedoRT.Release();

        DestroyImmediate(tempCameraGo);

        Debug.Log("Baking completed.");

    }

    /// <summary>
    /// 将八面体 UV (u,v) 映射为球面方向（单位向量）
    /// 输入范围 [0,1]?，输出方向覆盖整个球面（包括下半球）
    /// 算法：先将 UV 展开到 [-1,1]?，然后应用八面体逆映射
    /// </summary>
    private Vector3 OctahedronUVToDirection(Vector2 uv)
    {
        Vector2 f = uv * 2 - Vector2.one;
        // float z = 1 - Mathf.Abs(p.x) - Mathf.Abs(p.y);
        // Vector3 dir = new Vector3(p.x, p.y, z);
        // if (z < 0)
        // {
        //     float x = dir.x, y = dir.y;
        //     dir.x = (1 - Mathf.Abs(y)) * (x >= 0 ? 1 : -1);
        //     dir.y = (1 - Mathf.Abs(x)) * (y >= 0 ? 1 : -1);
        //     Debug.LogError("sssss");
        // } 
        // return dir.normalized;

        var n = new Vector3(f.x, 1f - Mathf.Abs(f.x) - Mathf.Abs(f.y), f.y);
        var t = Mathf.Clamp01(-n.y);
        n.x += n.x >= 0f ? -t : t;
        n.z += n.z >= 0f ? -t : t;
        return n;

    }

    private void SaveRenderTextureToPNG(RenderTexture rt, string path)
    {
        RenderTexture.active = rt;
        Texture2D tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply();
        byte[] bytes = tex.EncodeToPNG();
        System.IO.File.WriteAllBytes(path, bytes);
        AssetDatabase.Refresh();
        RenderTexture.active = null;
        DestroyImmediate(tex);
    }
}