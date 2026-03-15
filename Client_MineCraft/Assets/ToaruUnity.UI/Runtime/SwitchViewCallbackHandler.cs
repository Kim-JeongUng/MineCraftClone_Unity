namespace ToaruUnity.UI
{
    /// <summary>
    /// 페이지 
    /// </summary>
    /// <param name="result">페이지</param> 
    /// <param name="switchedViewKey">페이지Key</param> 
    /// <param name="switchedView">페이지객체</param> 
    public delegate void SwitchViewCallbackHandler(SwitchViewResult result, object switchedViewKey, AbstractView switchedView);
}