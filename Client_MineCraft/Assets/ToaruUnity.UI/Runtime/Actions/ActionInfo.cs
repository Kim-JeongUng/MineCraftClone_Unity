using System;
using System.Collections.Generic;
using System.Reflection;

namespace ToaruUnity.UI
{
    public readonly struct ActionInfo : IEquatable<ActionInfo>
    {
        private readonly ActionCenter m_Center;
        private readonly MethodInfo m_Method;

        /// <summary>
        /// 가져오기 
        /// </summary>
        public string ActionName { get; }

        /// <summary>
        /// 가져오기객체여부 
        /// </summary>
        public bool IsValid => m_Center != null && m_Method != null;

        /// <summary>
        /// 가져오기 
        /// </summary>
        public string MethodName => m_Method.Name;

        /// <summary>
        /// 가져오기인자개수 
        /// </summary>
        public int ParameterCount => m_Method.GetParameters().Length;

        /// <summary>
        /// 가져오기여부 
        /// </summary>
        public bool IsCoroutine => m_Method.ReturnType == typeof(IEnumerator<bool>);


        internal ActionInfo(ActionCenter center, string actionName, MethodInfo method)
        {
            m_Center = center;
            m_Method = method;
            ActionName = actionName;
        }


        public void Execute()
            => m_Center.Execute(ActionName);

        public void Execute<T0>(T0 arg0)
            => m_Center.Execute(ActionName, arg0);

        public void Execute<T0, T1>(T0 arg0, T1 arg1)
            => m_Center.Execute(ActionName, arg0, arg1);

        public void Execute<T0, T1, T2>(T0 arg0, T1 arg1, T2 arg2)
            => m_Center.Execute(ActionName, arg0, arg1, arg2);

        public void Execute<T0, T1, T2, T3>(T0 arg0, T1 arg1, T2 arg2, T3 arg3)
            => m_Center.Execute(ActionName, arg0, arg1, arg2, arg3);

        public bool Equals(ActionInfo other)
        {
            return ReferenceEquals(other.m_Center, m_Center) && other.ActionName == ActionName;
        }

        public override bool Equals(object obj)
        {
            return obj is ActionInfo info && Equals(info);
        }

        public override int GetHashCode()
        {
            var hashCode = 678288864;
            hashCode = hashCode * -1521134295 + m_Center.GetHashCode();
            hashCode = hashCode * -1521134295 + ActionName.GetHashCode();
            return hashCode;
        }

        public static bool operator ==(ActionInfo left, ActionInfo right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ActionInfo left, ActionInfo right)
        {
            return !(left == right);
        }
    }
}