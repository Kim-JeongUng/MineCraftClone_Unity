using System;

namespace ToaruUnity.UI
{
    /// <summary>
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
    public sealed class ActionAttribute : Attribute
    {
        /// <summary>
        /// 가져오기, 만약null, 기본 
        /// </summary>
        public string ActionName { get; }

        /// <summary>
        /// , 
        /// </summary>
        public ActionAttribute() { }

        /// <summary>
         /// </summary>
        /// <param name="actionName">, 만약null, 기본</param> 
        public ActionAttribute(string actionName)
        {
            ActionName = actionName;
        }
    }
}