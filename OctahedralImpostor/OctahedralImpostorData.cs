// OctahedralImpostorData.cs
using UnityEngine;

[CreateAssetMenu(fileName = "OctahedralImpostorData", menuName = "Impostors/OctahedralImpostorData")]
public class OctahedralImpostorData : ScriptableObject
{
    public Texture2D albedoAtlas;
    public int tilesPerSide;       // 每边 tile 数量（例如 32）
    public int tileResolution;     // 每个 tile 的像素尺寸（正方形）
    public Bounds bounds;          // 物体的包围盒（烘焙时自动计算）
}