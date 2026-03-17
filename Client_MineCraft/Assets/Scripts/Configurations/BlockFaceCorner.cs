namespace Minecraft.Configurations
{
    /// <summary>
    /// 블록 
    /// </summary>
    [XLua.GCOptimize]
    [XLua.LuaCallCSharp]
    public enum BlockFaceCorner
    {
        LeftBottom = 0,
        RightBottom = 1,
        LeftTop = 2,
        RightTop = 3
    }
}
