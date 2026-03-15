using XLua;

namespace Minecraft.Configurations
{
    /// <summary>
    /// 블록 
    /// </summary>
    [GCOptimize]
    [LuaCallCSharp]
    public enum BlockFace
    {
        /// <summary>
        /// 방향 (1, 0, 0) 
        /// </summary>
        PositiveX = 0,
        /// <summary>
        /// 방향 (0, 1, 0) 
        /// </summary>
        PositiveY = 1,
        /// <summary>
        /// 방향 (0, 0, 1) 
        /// </summary>
        PositiveZ = 2,
        /// <summary>
        /// 방향 (-1, 0, 0) 
        /// </summary>
        NegativeX = 3,
        /// <summary>
        /// 방향 (0, -1, 0) 
        /// </summary>
        NegativeY = 4,
        /// <summary>
        /// 방향 (0, 0, -1) 
        /// </summary>
        NegativeZ = 5
    }
}