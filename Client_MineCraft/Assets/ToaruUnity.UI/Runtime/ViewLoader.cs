using System;
using UnityEngine;

namespace ToaruUnity.UI
{
    [DisallowMultipleComponent]
    public abstract class ViewLoader : MonoBehaviour
    {
        /// <summary>
        /// 로드. 
        /// </summary>
        /// <param name="key">key</param> 
        /// <param name="callback">로드</param> 
        public abstract void LoadViewPrefab(object key, Action<AbstractView> callback);

        /// <summary>
        /// 해제. 
        /// </summary>
        /// <param name="key">key</param> 
        /// <param name="prefab">해제</param> 
        public abstract void ReleaseViewPrefab(object key, AbstractView prefab);
    }
}