using Minecraft.Configurations;
using Minecraft.PlayerControls;
using Minecraft.Multiplayer;
using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace Minecraft.Entities
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BlockInteraction))]
    [RequireComponent(typeof(FluidInteractor))]
    public class PlayerEntity : Entity
    {
        // ...fields of entity class

        [Space]
        [Header("Input")]
        [SerializeField] private InputActionAsset m_InputActions;

        [Space]
        [Header("Config")]
        public float WalkSpeed;
        public float RunSpeed;
        public float FlyUpSpeed;
        public float JumpHeight;
        [SerializeField] private float m_StepInterval;
        [SerializeField] [Range(0, 1)] private float m_RunstepLengthen;

        [Space]

        [SerializeField] private FirstPersonLook m_FirstPersonLook;
        [SerializeField] private FOVKick m_FOVKick;
        [SerializeField] private CurveControlledBob m_HeadBob;
        [SerializeField] private LerpControlledBob m_JumpBob;

        [Space]
        [Header("Animation")]
        [SerializeField] private Animator m_CharacterAnimator;
        [SerializeField] [Min(0.01f)] private float m_DigAnimationPulseDuration = 0.45f;

        [Space]
        [Header("Events")]
        [SerializeField] private UnityEvent<BlockData> m_OnStepOnBlock;


        [NonSerialized] private InputAction m_MoveAction;
        // [NonSerialized] private InputAction m_RunAction;
        [NonSerialized] private InputAction m_LookAction;
        [NonSerialized] private InputAction m_JumpAction;
        [NonSerialized] private InputAction m_FlyAction;
        [NonSerialized] private InputAction m_FlyDownAction;
        [NonSerialized] private InputAction m_CursorStateAction;

        private Camera m_Camera;
        private Transform m_CameraTransform;
        private FluidInteractor m_FluidInteractor;

        private Vector3 m_OriginalCameraPosition;
        private bool m_Jump;
        private bool m_FlyDown;
        private bool m_IsRunning;
        private bool m_PreviouslyGrounded;
        private float m_StepCycle;
        private float m_NextStep;

        private float m_LastTimePressW;
        private bool m_IsDigAnimationLoopActive;
        private float m_LastMoveAnimationSpeed = -1f;
        private bool m_LastDigAnimationState;
        private Coroutine m_DigAnimationPulseCoroutine;

        private static readonly int s_MoveSpeedHash = Animator.StringToHash("Move Speed");
        private static readonly int s_DigHash = Animator.StringToHash("Dig");

        private NetworkPlayerAdapter m_NetworkPlayerAdapter;

        private void Awake()
        {
            if (m_CharacterAnimator == null)
            {
                m_CharacterAnimator = GetComponentInChildren<Animator>(true);
            }

            if (m_NetworkPlayerAdapter == null)
            {
                m_NetworkPlayerAdapter = GetComponent<NetworkPlayerAdapter>();
            }
        }


        protected override void Start()
        {
            base.Start();

            if (m_InputActions == null)
            {
                Debug.LogWarning($"[{nameof(PlayerEntity)}] Missing InputActionAsset on {name}.");
                enabled = false;
                return;
            }

            m_InputActions.Enable();
            m_MoveAction = m_InputActions["Player/Move"];
            // m_RunAction = m_InputActions["Player/Run"];
            m_LookAction = m_InputActions["Player/Look"];
            m_JumpAction = m_InputActions["Player/Jump"];
            m_FlyAction = m_InputActions["Player/Fly"];
            m_FlyDownAction = m_InputActions["Player/Fly Down"];
            m_CursorStateAction = m_InputActions["Player/Cursor State"];

            m_Camera = GetComponentInChildren<Camera>();
            m_CameraTransform = m_Camera != null ? m_Camera.transform : null;
            m_FluidInteractor = GetComponent<FluidInteractor>();
            m_NetworkPlayerAdapter ??= GetComponent<NetworkPlayerAdapter>();
            m_CharacterAnimator ??= GetComponentInChildren<Animator>(true);

            if (m_CameraTransform != null)
            {
                m_FirstPersonLook.Initialize(transform, m_CameraTransform, true);
                m_HeadBob.Initialize(m_CameraTransform);
                m_JumpBob.Initialize();
                m_OriginalCameraPosition = m_CameraTransform.localPosition;
            }
            m_StepCycle = 0f;
            m_NextStep = m_StepCycle * 0.5f;

            m_LastTimePressW = 0f;
            m_IsDigAnimationLoopActive = false;
            m_LastDigAnimationState = false;

            // m_RunAction.performed += SwitchRunMode;
            m_JumpAction.performed += SwitchJumpMode;
            m_FlyAction.performed += SwitchFlyMode;
            m_FlyDownAction.performed += SwitchFlyDownMode;
            m_CursorStateAction.performed += SwitchCursorState;

            BlockInteraction interaction = GetComponent<BlockInteraction>();
            if (interaction != null)
            {
                interaction.Initialize(m_Camera, this);
                interaction.enabled = m_Camera != null;
            }
        }

        private void SwitchJumpMode(InputAction.CallbackContext context)
        {
            m_Jump = context.ReadValueAsButton();
        }

        // private void SwitchRunMode(InputAction.CallbackContext context)
        // {
        //   m_IsRunning = true;
        // }

        private void SwitchFlyMode(InputAction.CallbackContext context)
        {
            UseGravity = !UseGravity;
        }

        private void SwitchFlyDownMode(InputAction.CallbackContext context)
        {
            m_FlyDown = context.ReadValueAsButton();
        }

        private void SwitchCursorState(InputAction.CallbackContext context)
        {
            bool value = context.ReadValueAsButton();
            m_FirstPersonLook.SetCursorLockMode(!value);
        }

        private void Update()
        {
            InitializeEntityIfNot();
            if (!Initialized || m_CameraTransform == null || m_FluidInteractor == null)
            {
                return;
            }

            // Update 입력. 
            // 만약 FixedUpdate 입력, 
            // FixedUpdate 않 
            SwitchWalkAndRunMode();

            m_FirstPersonLook.LookRotation(m_LookAction.ReadValue<Vector2>(), Time.deltaTime);

            bool isGrounded = GetIsGrounded(out BlockData groundBlock);

            if (!m_PreviouslyGrounded && isGrounded)
            {
                StartCoroutine(m_JumpBob.DoBobCycle());
                PlayBlockStepSound(groundBlock);

                m_NextStep = m_StepCycle + 0.5f;
            }

            m_PreviouslyGrounded = isGrounded;

        }

        protected override void FixedUpdate()
        {
            InitializeEntityIfNot();
            if (!Initialized || m_CameraTransform == null || m_FluidInteractor == null)
            {
                return;
            }

            float speed = GetInput(out Vector2 input);
            m_FluidInteractor.UpdateState(this, m_CameraTransform, out float vMultiplier);

            // always move along the camera forward as it is the direction that it being aimed at
            Vector3 velocity = m_Transform.forward * input.y + m_Transform.right * input.x;
            velocity = speed * vMultiplier * velocity.normalized;

            if (UseGravity)
            {
                velocity.y = Velocity.y; // 않 y 방향 

                bool isGrounded = GetIsGrounded(out BlockData groundBlock);

                if (isGrounded && m_Jump)
                {
                    AddInstantForce(new Vector3(0, JumpHeight * Mass / Time.fixedDeltaTime, 0));
                    PlayBlockStepSound(groundBlock);
                    // m_Jump = false;
                }

                ProgressStepCycle(input, speed, isGrounded, groundBlock);
                UpdateCameraPosition(speed, isGrounded);
            }
            else if (m_FlyDown)
            {
                velocity.y = -FlyUpSpeed;            }
            else if (m_Jump)
            {
                velocity.y = FlyUpSpeed;            }
            else
            {
                velocity.y = 0;            }

            AddInstantForce((velocity - Velocity) * Mass / Time.fixedDeltaTime);
            UpdateMoveAnimation(velocity);

            base.FixedUpdate(); // move
        }

        public void StartDigAnimationLoop()
        {
            m_IsDigAnimationLoopActive = true;
            StopDigAnimationPulseCoroutine();
            SetDigAnimation(true, true);
        }

        public void StopDigAnimationLoop()
        {
            m_IsDigAnimationLoopActive = false;
            StopDigAnimationPulseCoroutine();
            SetDigAnimation(false, true);
        }

        public void PlayDigAnimationOnce()
        {
            StopDigAnimationPulseCoroutine();
            m_DigAnimationPulseCoroutine = StartCoroutine(PlayDigAnimationPulse(true));
        }

        public void ApplyRemoteMoveAnimation(float moveSpeed)
        {
            SetMoveAnimation(moveSpeed, false);
        }

        public void ApplyRemoteDigAnimationState(bool active)
        {
            StopDigAnimationPulseCoroutine();
            m_IsDigAnimationLoopActive = active;
            SetDigAnimation(active, false);
        }

        private void UpdateMoveAnimation(Vector3 velocity)
        {
            float horizontalSpeed = new Vector2(velocity.x, velocity.z).magnitude;
            float referenceSpeed = Mathf.Max(0.01f, Mathf.Max(WalkSpeed, RunSpeed));
            float moveSpeed = Mathf.Clamp01(horizontalSpeed / referenceSpeed);
            SetMoveAnimation(moveSpeed, true);
        }

        private void SetMoveAnimation(float moveSpeed, bool synchronize)
        {
            if (m_CharacterAnimator != null)
            {
                m_CharacterAnimator.SetFloat(s_MoveSpeedHash, moveSpeed);
            }

            if (Mathf.Abs(m_LastMoveAnimationSpeed - moveSpeed) <= 0.01f)
            {
                return;
            }

            m_LastMoveAnimationSpeed = moveSpeed;

            if (synchronize && m_NetworkPlayerAdapter != null)
            {
                m_NetworkPlayerAdapter.SyncMoveAnimation(moveSpeed);
            }
        }

        private System.Collections.IEnumerator PlayDigAnimationPulse(bool synchronize)
        {
            SetDigAnimation(true, synchronize);
            yield return new WaitForSeconds(m_DigAnimationPulseDuration);

            if (!m_IsDigAnimationLoopActive)
            {
                SetDigAnimation(false, synchronize);
            }

            m_DigAnimationPulseCoroutine = null;
        }

        private void StopDigAnimationPulseCoroutine()
        {
            if (m_DigAnimationPulseCoroutine == null)
            {
                return;
            }

            StopCoroutine(m_DigAnimationPulseCoroutine);
            m_DigAnimationPulseCoroutine = null;
        }

        private void SetDigAnimation(bool active, bool synchronize)
        {
            if (m_CharacterAnimator != null)
            {
                m_CharacterAnimator.SetBool(s_DigHash, active);
            }

            if (m_LastDigAnimationState == active)
            {
                return;
            }

            m_LastDigAnimationState = active;

            if (synchronize && m_NetworkPlayerAdapter != null)
            {
                m_NetworkPlayerAdapter.SyncDigAnimationState(active);
            }
        }

        private void ProgressStepCycle(Vector2 input, float speed, bool isGrounded, BlockData blockUnderFeet)
        {
            Vector3 velocity = Velocity;

            if (velocity.sqrMagnitude > 0 && input != Vector2.zero)
            {
                m_StepCycle += (Velocity.magnitude + (speed * (m_IsRunning ? m_RunstepLengthen : 1f))) * Time.fixedDeltaTime;
            }

            if (m_StepCycle <= m_NextStep)
            {
                return;
            }

            m_NextStep = m_StepCycle + m_StepInterval;

            if (isGrounded)
            {
                PlayBlockStepSound(blockUnderFeet);
            }
        }

        private void UpdateCameraPosition(float speed, bool isGrounded)
        {
            Vector3 newCameraPosition;

            if (!m_HeadBob.Enabled)
            {
                return;
            }

            if (Velocity.sqrMagnitude > 0 && isGrounded)
            {
                m_CameraTransform.localPosition = m_HeadBob.DoHeadBob(Velocity.magnitude + (speed * (m_IsRunning ? m_RunstepLengthen : 1f)), m_StepInterval, Time.fixedDeltaTime);
                newCameraPosition = m_CameraTransform.localPosition;
                newCameraPosition.y = m_CameraTransform.localPosition.y - m_JumpBob.GetOffset();
            }
            else
            {
                newCameraPosition = m_CameraTransform.localPosition;
                newCameraPosition.y = m_OriginalCameraPosition.y - m_JumpBob.GetOffset();
            }

            m_CameraTransform.localPosition = newCameraPosition;
        }

        private void SwitchWalkAndRunMode()
        {
            // TODO: Input System ？ 
            if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))
            {
                float currentTime = Time.time;

                if (currentTime - m_LastTimePressW <= 0.2f)
                {
                    // 플레이어 W 
                    m_IsRunning = true;
                }

                m_LastTimePressW = currentTime;
            }
        }

        private float GetInput(out Vector2 input)
        {
            input = m_MoveAction.ReadValue<Vector2>();

            if (input == Vector2.zero)
            {
                // 플레이어, 
                m_IsRunning = false;
            }

            return m_IsRunning ? RunSpeed : WalkSpeed;
        }

        private void PlayBlockStepSound(BlockData block)
        {
            //block.PlayStepAutio(m_AudioSource);
        }
    }
}
