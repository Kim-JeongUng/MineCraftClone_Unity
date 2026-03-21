using Minecraft.Multiplayer;
using Mirror;
using UnityEngine;
using UnityEngine.UI;

namespace Minecraft.UI
{
    [DisallowMultipleComponent]
    public class UICanvasManager : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] private GameObject m_GamePlayRoot;
        [SerializeField] private GameObject m_LoadingPageRoot;

        [Header("Loading UI")]
        [SerializeField] private Image m_ProgressBar;
        [SerializeField] private Text m_LoadingText;
        [SerializeField] private Text m_CurrentHandBlockText;
        [SerializeField] private InputField m_HandBlockInput;
        [SerializeField] private Transform m_HotbarRoot;
        [SerializeField] private GameObject m_BlockInventoryRoot;
        [SerializeField] private Transform m_BlockInventoryGridRoot;
        [SerializeField] [Min(0.1f)] private float m_MinChunkLoadingSeconds = 1.5f;
        [SerializeField] [Min(0.05f)] private float m_CloseDelayAfterFullProgress = 2.5f;
        [SerializeField] [Min(0.1f)] private float m_DefaultProgressSpeed = 0.6f;
        [SerializeField] [Min(0.1f)] private float m_CompletionProgressSpeed = 1.5f;

        public Text CurrentHandBlockText => m_CurrentHandBlockText;
        public InputField HandBlockInput => m_HandBlockInput;
        public Transform HotbarRoot => m_HotbarRoot;
        public GameObject BlockInventoryRoot => m_BlockInventoryRoot;
        public Transform BlockInventoryGridRoot => m_BlockInventoryGridRoot;

        private float m_CurrentProgress;
        private float m_ChunkLoadingElapsed;
        private float m_CompletionElapsed;
        private bool m_IsReady;
        private bool m_IsCompleting;
        private bool m_HasStartedChunkLoading;

        private void Awake()
        {
            SetReadyState(false);
            ApplyProgressImmediate(0f);
            SetLoadingMessage("네트워크 연결 대기 중...");
        }

        private void Update()
        {
            if (m_IsReady)
            {
                return;
            }

            if (m_IsCompleting)
            {
                UpdateCompletionState();
                return;
            }

            float targetProgress = 0.1f;
            string message = "네트워크 연결 대기 중...";
            float progressSpeed = m_DefaultProgressSpeed;

            if (IsNetworkReady())
            {
                targetProgress = 0.4f;
                message = "월드 초기화 중...";

                World world = World.Active as World;
                if (world != null && world.Initialized)
                {
                    if (!m_HasStartedChunkLoading)
                    {
                        m_HasStartedChunkLoading = true;
                        m_ChunkLoadingElapsed = 0f;
                    }

                    m_ChunkLoadingElapsed += Time.unscaledDeltaTime;
                    float chunkPhaseProgress = Mathf.Clamp01(m_ChunkLoadingElapsed / m_MinChunkLoadingSeconds);
                    targetProgress = Mathf.Lerp(0.4f, 0.9f, chunkPhaseProgress);
                    message = "지형 생성 중...";

                    if (IsGroundChunkReady(world) && m_ChunkLoadingElapsed >= m_MinChunkLoadingSeconds)
                    {
                        BeginCompletion();
                        UpdateCompletionState();
                        return;
                    }
                }
            }
            else
            {
                m_HasStartedChunkLoading = false;
                m_ChunkLoadingElapsed = 0f;
            }

            m_CurrentProgress = Mathf.MoveTowards(m_CurrentProgress, targetProgress, Time.unscaledDeltaTime * progressSpeed);
            ApplyProgressImmediate(m_CurrentProgress);
            SetLoadingMessage(message);
        }

        private void BeginCompletion()
        {
            if (m_IsCompleting)
            {
                return;
            }

            m_IsCompleting = true;
            m_CompletionElapsed = 0f;
            SetLoadingMessage("로딩 완료");
        }

        private void UpdateCompletionState()
        {
            m_CurrentProgress = Mathf.MoveTowards(m_CurrentProgress, 1f, Time.unscaledDeltaTime * m_CompletionProgressSpeed);
            ApplyProgressImmediate(m_CurrentProgress);
            SetLoadingMessage("로딩 완료");

            if (m_CurrentProgress < 1f)
            {
                return;
            }

            m_CompletionElapsed += Time.unscaledDeltaTime;
            if (m_CompletionElapsed >= m_CloseDelayAfterFullProgress)
            {
                SetReadyState(true);
            }
        }

        private bool IsNetworkReady()
        {
            if (!GameModeContext.IsMultiplayer)
            {
                return true;
            }

            if (NetworkClient.isConnected)
            {
                return true;
            }

            return NetworkServer.active;
        }

        private bool IsGroundChunkReady(World world)
        {
            if (world == null || !world.Initialized || world.ChunkManager == null)
            {
                return false;
            }

            Vector3 referencePosition = world.PlayerTransform != null ? world.PlayerTransform.position : world.DefaultSpawnPosition;
            ChunkPos center = ChunkPos.GetFromAny(referencePosition.x, referencePosition.z);

            if (!world.ChunkManager.GetChunk(center, false, out Chunk chunk) || chunk == null)
            {
                world.ChunkManager.GetChunk(center, true, out _);
                return false;
            }

            int localX = Mathf.FloorToInt(referencePosition.x - center.X);
            int localZ = Mathf.FloorToInt(referencePosition.z - center.Z);
            localX = Mathf.Clamp(localX, 0, WorldConsts.ChunkWidth - 1);
            localZ = Mathf.Clamp(localZ, 0, WorldConsts.ChunkWidth - 1);

            return chunk.GetTopVisibleBlockY(localX, localZ, int.MinValue) != int.MinValue;
        }

        private void SetReadyState(bool ready)
        {
            m_IsReady = ready;

            if (m_GamePlayRoot != null)
            {
                m_GamePlayRoot.SetActive(ready);
            }

            if (m_LoadingPageRoot != null)
            {
                m_LoadingPageRoot.SetActive(!ready);
            }
        }

        private void ApplyProgressImmediate(float progress)
        {
            if (m_ProgressBar != null)
            {
                m_ProgressBar.fillAmount = Mathf.Clamp01(progress);
            }
        }

        private void SetLoadingMessage(string message)
        {
            if (m_LoadingText != null)
            {
                m_LoadingText.text = message;
            }
        }
    }
}