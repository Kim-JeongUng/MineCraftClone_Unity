namespace ToaruUnity.UI
{
    /// <summary>
    /// 페이지 
    /// </summary>
    public enum SwitchViewResult
    {
        /// <summary>
        /// 페이지 
        /// </summary>
        Navigated,
        /// <summary>
        /// 페이지 
        /// </summary>
        NewViewOpened,
        /// <summary>
        /// , Keynull 
        /// </summary>
        Failed_BecauseKeyIsNull,
        /// <summary>
        /// , Mode<see cref="SwitchViewMode.None"/> 
        /// </summary>
        Failed_BecauseModeIsNone,
        /// <summary>
        /// , 페이지이미 
        /// </summary>
        Failed_BecauseNavigationIsUnnecessary
    }
}
