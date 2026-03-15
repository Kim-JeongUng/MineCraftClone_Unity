using System;
using XLua;

namespace Minecraft.Configurations
{
    [Flags]
    [GCOptimize]
    [LuaCallCSharp]
    public enum BlockRotationAxes
    {
        /// <summary>
        /// 않 
        /// </summary>
        None = 0,
        /// <summary>
        /// Y 
        /// </summary>
        AroundYAxis = 1 << 0,
        /// <summary>
        /// X Z 
        /// </summary>
        AroundXOrZAxis = 1 << 1
    }
}
