using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace ToaruUnity.UI
{
    /// <summary>
    /// UI 
    /// </summary>
    public interface IUIManager
    {
        /// <summary>
        /// 가져오기페이지개수 
        /// </summary>
        int ViewCount { get; }

        /// <summary>
        /// 가져오기페이지 
        /// </summary>
        Transform ViewContainer { get; }

        /// <summary>
        /// 가져오기페이지Key 
        /// </summary>
        IEqualityComparer<object> ViewKeyComparer { get; }

        /// <summary>
        /// 페이지 
        /// </summary>
        event UnityAction<object, AbstractView> OnViewOpened;

        /// <summary>
        /// 페이지 
        /// </summary>
        event UnityAction<object, AbstractView> OnViewNavigated;

        /// <summary>
        /// 닫기페이지 
        /// </summary>
        event UnityAction<object> OnViewClosed;

        /// <summary>
        /// 페이지 
        /// </summary>
        event UnityAction OnActiveViewChanged;

        /// <summary>
        /// 가져오기페이지 
        /// </summary>
        AbstractView ActiveView { get; }


        /// <summary>
        /// 페이지 
        /// </summary>
        /// <param name="viewKey">페이지Key</param> 
        void OpenNewView(object viewKey);

        /// <summary>
        /// 페이지 
        /// </summary>
        /// <param name="viewKey">페이지Key</param> 
        void NavigateToView(object viewKey);

        /// <summary>
        /// 페이지 
        /// </summary>
        /// <param name="viewKey">페이지Key</param> 
        /// <param name="mode"></param> 
        void SwitchView(object viewKey, SwitchViewMode mode);

        /// <summary>
        /// 페이지 
        /// </summary>
        /// <param name="viewKey">페이지Key</param> 
        /// <param name="mode"></param> 
        /// <param name="parameters">페이지인자</param> 
        void SwitchView(object viewKey, SwitchViewMode mode, SwitchViewParameters parameters);

        /// <summary>
        /// 페이지 
        /// </summary>
        /// <param name="viewKey">페이지Key</param> 
        /// <param name="mode"></param> 
        /// <param name="callback"></param> 
        void SwitchView(object viewKey, SwitchViewMode mode, SwitchViewCallbackHandler callback);

        /// <summary>
        /// 페이지 
        /// </summary>
        /// <param name="viewKey">페이지Key</param> 
        /// <param name="mode"></param> 
        /// <param name="callback"></param> 
        /// <param name="parameters">페이지인자</param> 
        void SwitchView(object viewKey, SwitchViewMode mode, SwitchViewCallbackHandler callback, SwitchViewParameters parameters);


        /// <summary>
        /// 닫기페이지 
        /// </summary>
        /// <returns>만약페이지닫기, true 반환; false 반환</returns> 
        bool CloseActiveView();

        /// <summary>
        /// 닫기페이지 
        /// </summary>
        /// <param name="removedViewKey">닫기페이지Key</param> 
        /// <returns>만약페이지닫기, true 반환; false 반환</returns> 
        bool CloseActiveView(out object removedViewKey);

        /// <summary>
        /// 닫기페이지 
        /// </summary>
        /// <param name="parameters">닫기페이지인자</param> 
        /// <returns>만약페이지닫기, true 반환; false 반환</returns> 
        bool CloseActiveView(CloseViewParameters parameters);

        /// <summary>
        /// 닫기페이지 
        /// </summary>
        /// <param name="removedViewKey">닫기페이지Key</param> 
        /// <param name="parameters">닫기페이지인자</param> 
        /// <returns>만약페이지닫기, true 반환; false 반환</returns> 
        bool CloseActiveView(out object removedViewKey, CloseViewParameters parameters);
    }
}