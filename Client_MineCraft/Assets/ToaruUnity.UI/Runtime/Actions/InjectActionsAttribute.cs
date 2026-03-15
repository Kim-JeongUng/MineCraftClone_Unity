using System;

namespace ToaruUnity.UI
{
    /// <summary>
    /// <see cref="ActionCenter"/> 
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class InjectActionsAttribute : Attribute
    {
        /// <summary>
        /// 가져오기<see cref="ActionCenter"/> 
        /// </summary>
        public Type ActionCenterType { get; }

        /// <summary>
        /// <see cref="ActionCenter"/> 
        /// </summary>
        /// <param name="actionCenterType"><see cref="ActionCenter"/></param> 
        public InjectActionsAttribute(Type actionCenterType)
        {
            ActionCenterType = actionCenterType;
        }
    }
}
