namespace Minecraft.Configurations
{
    /// <summary>
    /// 블록 
    /// </summary>
    [XLua.GCOptimize]
    [XLua.LuaCallCSharp]
    public enum BlockFaceCorner
    {
        /// <summary>
        /        /// </summary>
        LeftBottom = 0,
        /// <summary>
        /        /// </summary>
        RightBottom = 1,
        /// <summary>
        /        /// </summary>
        LeftTop = 2,
        /// <summary>
        /        /// </summary>
        RightTop = 3
    }
}
