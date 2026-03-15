using System.Runtime.InteropServices;
using Minecraft.Lua;
using UnityEngine;
using UnityEngine.Rendering;

namespace Minecraft.Rendering
{
    [XLua.GCOptimize]
    [StructLayout(LayoutKind.Sequential)]
    public struct BlockMeshVertexData : ILuaCallCSharp
    {
        public static readonly VertexAttributeDescriptor[] VertexAttributes = new[]
        {
            new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.SInt32, 3),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord2, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord3, VertexAttributeFormat.Float32, 3),
        };

        /// <summary>
        /// 좌표(Object Space). 
        /// </summary>
        public Vector3 PositionOS;
        /// <summary>
        /// UV 좌표. 
        /// </summary>
        public Vector2 UV;
        /// <summary>
        /// , xyz Albedo, Normal, MER. 
        /// </summary>
        public Vector3Int TexIndices;
        /// <summary>
        /// 광원 Emission SkyLight Ambient. 
        /// </summary>
        public Vector3 Lights;
        /// <summary>
        /// 블록월드좌표. 
        /// </summary>
        public Vector3 BlockPositionWS;
    }
}
