using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

#pragma warning disable CS0649
#pragma warning disable IDE0032
#pragma warning disable IDE1006
#pragma warning disable IDE0044

namespace ToaruUnity.UI
{
    /// <summary>
    /// 모든페이지 
    /// </summary>
    [DisallowMultipleComponent]
    public abstract class AbstractView : MonoBehaviour
    {
        private ViewState m_State;
        private bool m_IsTransiting;
        private TransitionQueue m_TransitionQueue;
        private Transform m_Transform; // may be null

        private ActionCenter m_ActionCenter; // may be null

        [SerializeField]
        [Tooltip("화면 상태 전환 전에 호출")]
        private BeforeTransitionEvent m_OnBeforeTransition;

        [SerializeField]
        [Tooltip("화면 상태 전환 후에 호출")]
        private AfterTransitionEvent m_OnAfterTransition;


        /// <summary>
        /// 가져오기객체 
        /// </summary>
        public ViewState State => m_State;

        /// <summary>
        /// 가져오기여부 
        /// </summary>
        public bool IsTransiting => m_IsTransiting;

        /// <summary>
        /// 화면 상태 전환 전에 호출
        /// </summary>
        public event UnityAction<AbstractView, ViewState> OnBeforeTransition
        {
            add => m_OnBeforeTransition.AddListener(value);
            remove => m_OnBeforeTransition.RemoveListener(value);
        }

        /// <summary>
        /// 화면 상태 전환 후에 호출
        /// </summary>
        public event UnityAction<AbstractView> OnAfterTransition
        {
            add => m_OnAfterTransition.AddListener(value);
            remove => m_OnAfterTransition.RemoveListener(value);
        }


        /// <summary>
        /// 가져오기개수 
        /// </summary>
        public int RemainingTransitionCount => m_TransitionQueue.Count;


        /// <summary>
        /// 가져오기객체<see cref="UnityEngine.Transform"/> 
        /// </summary>
        public Transform Transform => m_Transform ?? (m_Transform = GetComponent<Transform>());



        /// <summary>
        /// 가져오기<see cref="ActionCenter"/>객체. 
        /// 만약, 반환null. 
        /// </summary>
        protected internal ActionCenter Actions => m_ActionCenter;


        protected AbstractView() { }


        internal void Create(ActionCenter actionCenter)
        {
            m_State = ViewState.Closed;
            m_IsTransiting = false;
            m_TransitionQueue = new TransitionQueue();
            m_Transform = null;

            m_ActionCenter = actionCenter;
            m_ActionCenter?.RegisterStateChangeHandler(OnRefreshView);

            OnCreate();
        }

        internal void SetState(ViewState nextState, object param)
        {
            Transition transition = new Transition(nextState, param);

            if (IsTransiting)
            {
                m_TransitionQueue.Enqueue(in transition);
            }
            else
            {
                IEnumerator routine = CreateTransitionRoutine(in transition);

                if (routine == null)
                {
                    SetStateAfterTransition(nextState);
                }
                else
                {
                    StartCoroutine(DoTransitions(nextState, routine));
                }
            }
        }

        private IEnumerator DoTransitions(ViewState nextState, IEnumerator routine)
        {
            m_IsTransiting = true;

            do
            {
                yield return routine; // null, 

                SetStateAfterTransition(nextState);
            }
            while (TryGetNextTransition(out nextState, out routine));

            m_IsTransiting = false;
        }

        private bool TryGetNextTransition(out ViewState nextState, out IEnumerator routine)
        {
            if (m_TransitionQueue.TryDequeue(out Transition transition))
            {
                nextState = transition.NextState;
                routine = CreateTransitionRoutine(in transition);
                return true;
            }

            nextState = default;
            routine = default;
            return false;
        }

        private IEnumerator CreateTransitionRoutine(in Transition transition)
        {
            m_OnBeforeTransition.Invoke(this, transition.NextState);

            switch (transition.NextState)
            {
                case ViewState.Closed when State == ViewState.Active:
                    return OnClose(transition.Param);

                case ViewState.Suspended when State == ViewState.Active:
                    return OnSuspend(transition.Param);

                case ViewState.Active when State == ViewState.Closed:
                    return OnOpen(transition.Param);

                case ViewState.Active when State == ViewState.Suspended:
                    return OnResume(transition.Param);

                default:
                    throw new InvalidOperationException($"상태에서 전환할 수 없음: {State}{transition.NextState}");
            }
        }

        private void SetStateAfterTransition(ViewState value)
        {
            m_State = value;
            m_OnAfterTransition.Invoke(this);
        }



        protected virtual void OnCreate() { }

        protected virtual void OnDestroy() { }

        protected virtual void OnRefreshView(IActionState state) { }

        protected virtual void OnUpdate(float deltaTime) { }

        protected virtual IEnumerator OnOpen(object param) { return null; }

        protected virtual IEnumerator OnClose(object param) { return null; }

        protected virtual IEnumerator OnResume(object param) { return null; }

        protected virtual IEnumerator OnSuspend(object param) { return null; }



        private void Update()
        {
            Actions?.UpdateCoroutines();
            OnUpdate(Time.deltaTime);
        }


        // (AbstractView view, ViewState nextState)
        [Serializable]
        private sealed class BeforeTransitionEvent : UnityEvent<AbstractView, ViewState> { }

        // (AbstractView view)
        [Serializable]
        private sealed class AfterTransitionEvent : UnityEvent<AbstractView> { }

        private readonly struct Transition
        {
            public readonly ViewState NextState;
            public readonly object Param;

            public Transition(ViewState nextState, object param)
            {
                NextState = nextState;
                Param = param;
            }
        }

        private struct TransitionQueue
        {
            private Queue<Transition> m_Queue;

            public int Count => m_Queue == null ? 0 : m_Queue.Count;

            public void Enqueue(in Transition transition)
            {
                if (m_Queue == null)
                {
                    m_Queue = new Queue<Transition>();
                }

                m_Queue.Enqueue(transition);
            }

            public bool TryDequeue(out Transition transition)
            {
                if (Count == 0)
                {
                    transition = default;
                    return false;
                }

                transition = m_Queue.Dequeue();
                return true;
            }
        }
    }
}