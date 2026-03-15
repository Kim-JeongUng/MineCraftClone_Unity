using System;
using System.Collections;
using UnityEngine;
using System.Collections.Generic;

namespace ToaruUnity.UI
{
    /// <summary>
    /// 페이지LIFO컬렉션, 페이지. 
    /// </summary>
    public class ViewStack : IEnumerable<AbstractView>
    {
        private readonly struct Element
        {
            public readonly object ViewKey;
            public readonly AbstractView View;

            public Element(object viewKey, AbstractView view)
            {
                ViewKey = viewKey;
                View = view;
            }
        }

        public struct Enumerator : IEnumerator<AbstractView>
        {
            private readonly ViewStack m_Stack;
            private readonly int m_Version;
            private int m_Index; // -1 = not started, -2 = ended/disposed
            private AbstractView m_Current;

            public AbstractView Current
            {
                get
                {
                    switch (m_Index)
                    {
                        case -1:
                            throw new InvalidOperationException("반복이 시작되지 않았습니다");
                        case -2:
                            throw new InvalidOperationException("반복이 종료되었습니다");
                        default:
                            return m_Current;
                    }
                }
            }

            internal Enumerator(ViewStack stack)
            {
                m_Stack = stack;
                m_Version = stack.m_Version;
                m_Index = -1;
                m_Current = default;
            }

            public void Dispose()
            {
                m_Index = -2;
                m_Current = default;
            }

            public bool MoveNext()
            {
                if (m_Version != m_Stack.m_Version)
                    throw new InvalidOperationException("반복 중 컬렉션이 수정되었습니다");

                if (m_Index == -2)
                    return false;

                m_Index++;

                if (m_Index == m_Stack.Count)
                {
                    m_Index = -2;
                    m_Current = default;
                    return false;
                }

                m_Current = m_Stack[m_Index];
                return true;
            }

            public void Reset()
            {
                if (m_Version != m_Stack.m_Version)
                    throw new InvalidOperationException("반복 중 컬렉션이 수정되었습니다");

                m_Index = -1;
                m_Current = default;
            }

            object IEnumerator.Current => Current;
        }


        private int m_Version;
        private int m_TopIndex;
        private Element[] m_Stack;
        private readonly int m_MinGrow;
        private readonly int m_MaxGrow;
        private readonly IEqualityComparer<object> m_KeyComparer;

        /// <summary>
        /// 가져오기요소개수 
        /// </summary>
        public int Count => m_TopIndex + 1;

        /// <summary>
        /// 가져오기요소 
        /// </summary>
        /// <param name="index">요소(스택 상단요소0, 1)</param> 
        /// <returns>만약요소, 반환요소; 반환null</returns> 
        public AbstractView this[int index] => Peek(index, out _);


        /// <summary>
        /// 생성ViewStack객체 
        /// </summary>
        /// <param name="minGrow">길이않, 길이최소, 0</param> 
        /// <param name="maxGrow">길이않, 길이최대, 0</param> 
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="minGrow"/>1<paramref name="maxGrow"/></exception> 
        public ViewStack(int minGrow, int maxGrow) : this(minGrow, maxGrow, EqualityComparer<object>.Default) { }

        /// <summary>
        /// 생성ViewStack객체 
        /// </summary>
        /// <param name="minGrow">길이않, 길이최소, 0</param> 
        /// <param name="maxGrow">길이않, 길이최대, 0</param> 
        /// <param name="objKeyComparer">요소Key</param> 
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="minGrow"/>1<paramref name="maxGrow"/></exception> 
        /// <exception cref="ArgumentNullException"><paramref name="objKeyComparer"/>null</exception> 
        public ViewStack(int minGrow, int maxGrow, IEqualityComparer<object> objKeyComparer)
        {
            if (minGrow < 1 || maxGrow < minGrow)
            {
                throw new ArgumentOutOfRangeException(nameof(minGrow));
            }

            m_Version = int.MinValue;
            m_TopIndex = -1;
            m_Stack = Array.Empty<Element>();
            m_MinGrow = minGrow;
            m_MaxGrow = maxGrow;
            m_KeyComparer = objKeyComparer ?? throw new ArgumentNullException(nameof(objKeyComparer));
        }


        /// <summary>
        /// 스택 상단요소 
        /// </summary>
        /// <param name="viewKey">요소Key</param> 
        /// <param name="view">요소, 않null</param> 
        /// <param name="openViewParam">페이지인자</param> 
        /// <param name="suspendViewParam">일시중지페이지인자</param> 
        /// <exception cref="ArgumentNullException"><paramref name="view"/>null</exception> 
        public void Push(object viewKey, AbstractView view, object openViewParam, object suspendViewParam)
        {
            if (view == null)
            {
                throw new ArgumentNullException(nameof(view));
            }

            if (m_TopIndex > -1)
            {
                AbstractView last = m_Stack[m_TopIndex].View;
                last.SetState(ViewState.Suspended, suspendViewParam); // 일시중지페이지 
            }

            m_TopIndex++;

            if (m_Stack.Length == m_TopIndex)
            {
                Grow();
            }

            m_Stack[m_TopIndex] = new Element(viewKey, view); // 스택 상단 

            view.OnBeforeTransition += OnBeforeViewTransition;
            view.OnAfterTransition += OnAfterViewTransition;
            view.SetState(ViewState.Active, openViewParam);

            m_Version++;
        }

        /// <summary>
        /// <paramref name="viewKey"/>**스택 상단요소요소시작**, 요소스택 상단 
        /// </summary>
        /// <param name="viewKey">요소Key</param> 
        /// <param name="navigatedView">페이지</param> 
        /// <param name="navigateViewParam">페이지인자</param> 
        /// <param name="suspendViewParam">일시중지페이지인자</param> 
        /// <returns>만약요소스택 상단, true 반환; false 반환</returns> 
        public bool TryMoveToTop(object viewKey, out AbstractView navigatedView, object navigateViewParam, object suspendViewParam)
        {
            // 않검사페이지 
            for (int i = m_TopIndex - 1; i > -1; i--)
            {
                Element element = m_Stack[i];

                if (m_KeyComparer.Equals(viewKey, element.ViewKey))
                {
                    for (int j = i; j < m_TopIndex; j++)
                    {
                        m_Stack[j] = m_Stack[j + 1];
                    }

                    // 일시중지페이지 
                    AbstractView last = m_Stack[m_TopIndex - 1].View;
                    last.SetState(ViewState.Suspended, suspendViewParam);

                    m_Stack[m_TopIndex] = element; // 스택 상단 

                    navigatedView = element.View;
                    navigatedView.SetState(ViewState.Active, navigateViewParam);

                    m_Version++;
                    return true;
                }
            }

            navigatedView = default;
            return false;
        }

        /// <summary>
        /// 만약요소, 스택 상단요소 
        /// </summary>
        /// <param name="closeViewParam">닫기페이지인자</param> 
        /// <param name="resumeViewParam">복원페이지인자</param> 
        /// <param name="removedViewKey">요소Key</param> 
        /// <param name="removedView">요소</param> 
        /// <returns>만약요소, true 반환; false 반환</returns> 
        public bool TryPop(object closeViewParam, object resumeViewParam, out object removedViewKey, out AbstractView removedView)
        {
            if (m_TopIndex < 0)
            {
                removedViewKey = default;
                removedView = default;
                return false;
            }

            Element element = m_Stack[m_TopIndex];
            m_Stack[m_TopIndex--] = default;

            removedViewKey = element.ViewKey;
            removedView = element.View;
            removedView.SetState(ViewState.Closed, closeViewParam);

            if (m_TopIndex > -1)
            {
                AbstractView last = m_Stack[m_TopIndex].View;
                last.SetState(ViewState.Active, resumeViewParam); // 복원페이지 
            }

            m_Version++;
            return true;
        }

        /// <summary>
        /// 가져오기스택 상단요소 
        /// </summary>
        /// <param name="viewKey">요소Key</param> 
        /// <returns>만약요소, 반환스택 상단요소; 반환null</returns> 
        public AbstractView Peek(out object viewKey)
        {
            return Peek(0, out viewKey);
        }

        /// <summary>
        /// 가져오기요소 
        /// </summary>
        /// <param name="index">요소(스택 상단요소0, 1)</param> 
        /// <param name="viewKey">요소Key</param> 
        /// <returns>만약요소, 반환요소; 반환null</returns> 
        public AbstractView Peek(int index, out object viewKey)
        {
            int i = m_TopIndex - index;

            if (i > -1 && i <= m_TopIndex)
            {
                Element element = m_Stack[i];
                viewKey = element.ViewKey;
                return element.View;
            }

            viewKey = default;
            return default;
        }

        /// <summary>
        /// 요소Key여부<paramref name="viewKey"/> 
        /// </summary>
        /// <param name="viewKey">Key</param> 
        /// <returns>만약요소Key<paramref name="viewKey"/>, true 반환; false 반환</returns> 
        public bool MatchTopKey(object viewKey)
        {
            if (m_TopIndex < 0)
            {
                return false;
            }

            object key = m_Stack[m_TopIndex].ViewKey;
            return m_KeyComparer.Equals(viewKey, key);
        }

        /// <summary>
        /// 가져오기여부포함Key 
        /// </summary>
        /// <param name="viewKey">조회Key</param> 
        /// <returns>만약포함Key, true 반환; false 반환</returns> 
        public bool ContainsKey(object viewKey)
        {
            for (int i = m_TopIndex; i > -1; i--)
            {
                if (m_KeyComparer.Equals(viewKey, m_Stack[i].ViewKey))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 가져오기여부포함요소 
        /// </summary>
        /// <param name="view">조회요소</param> 
        /// <returns>만약포함요소, true 반환; false 반환</returns> 
        public bool ContainsView(AbstractView view)
        {
            if (view != null)
            {
                for (int i = m_TopIndex; i > -1; i--)
                {
                    if (m_Stack[i].View == view)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// 가져오기반복.반복스택 상단시작. 
        /// </summary>
        /// <returns>반복</returns> 
        public Enumerator GetEnumerator()
        {
            return new Enumerator(this);
        }


        private void OnBeforeViewTransition(AbstractView view, ViewState nextState)
        {
            switch (nextState)
            {
                case ViewState.Closed:
                    view.OnBeforeTransition -= OnBeforeViewTransition; // 페이지닫기, 
                    break;

                case ViewState.Active:
                    if (view.State == ViewState.Closed)
                    {
                        view.gameObject.SetActive(true);                        view.Actions?.Reset(); // 페이지, 
                    }
                    else // if (view.State == ViewState.Suspended)
                    {
                        view.enabled = true;
                    }

                    view.Transform.SetAsLastSibling();
                    break;
            }
        }

        private void OnAfterViewTransition(AbstractView view)
        {
            switch (view.State)
            {
                case ViewState.Closed:
                    //view.enabled = false;
                    view.gameObject.SetActive(false);                    view.OnAfterTransition -= OnAfterViewTransition; // 페이지닫기, 
                    break;

                case ViewState.Suspended:
                    view.enabled = false;
                    break;
            }
        }

        private void Grow()
        {
            int newCapacity = Mathf.Clamp(m_Stack.Length << 1, m_Stack.Length + m_MinGrow, m_Stack.Length + m_MaxGrow);
            Element[] array = new Element[newCapacity];

            Array.Copy(m_Stack, 0, array, 0, m_Stack.Length);
            m_Stack = array;
        }


        IEnumerator<AbstractView> IEnumerable<AbstractView>.GetEnumerator()
        {
            return GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}