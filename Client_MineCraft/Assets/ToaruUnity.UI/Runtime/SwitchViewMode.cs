using System;

namespace ToaruUnity.UI
{
    /// <summary>
    /// 페이지 
    /// </summary>
    [Flags]
    public enum SwitchViewMode
    {
        /// <summary>
        /// 않 
        /// </summary>
        None = 0,
        /// <summary>
        /// 페이지 
        /// </summary>
        OpenNew = 1 << 0,
        /// <summary>
        /// 이미페이지 
        /// </summary>
        Navigate = 1 << 1,
        /// <summary>
        /// 이미페이지, 만약, 페이지 
        /// </summary>
        NavigateOrOpenNew = Navigate | OpenNew
    }
}