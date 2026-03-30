using Minecraft.Multiplayer;
using Mirror;
using UnityEngine;
using UnityEngine.UI;

namespace Minecraft.UI
{
    [DisallowMultipleComponent]
    public class UICanvasManager : MonoBehaviour
    {
        private enum LoadingState
        {
            WaitingForNetwork,
            InitializingWorld,
            GeneratingTerrain,
            Completed
        }

        private const string WaitingNetworkMessage = "네트워크 연결 대기 중...";
        private const string InitializingWorldMessage = "월드 초기화 중...";
        private const string GeneratingTerrainMessage = "지형 생성 중...";
        private const string LoadingCompleteMessage = "로딩 완료";

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
        private LoadingState m_CurrentLoadingState;
        private string m_LastLoadingMessage = string.Empty;

        private void Awake()
        {
            SetReadyState(false);
            ApplyProgressImmediate(0f);
            SetLoadingState(LoadingState.WaitingForNetwork);
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
            LoadingState loadingState = LoadingState.WaitingForNetwork;
            float progressSpeed = m_DefaultProgressSpeed;

            if (IsNetworkReady())
            {
                targetProgress = 0.4f;
                loadingState = LoadingState.InitializingWorld;

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
                    loadingState = LoadingState.GeneratingTerrain;

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
            SetLoadingState(loadingState);
        }

        private void BeginCompletion()
        {
            if (m_IsCompleting)
            {
                return;
            }

            m_IsCompleting = true;
            m_CompletionElapsed = 0f;
            SetLoadingState(LoadingState.Completed);
        }

        private void UpdateCompletionState()
        {
            m_CurrentProgress = Mathf.MoveTowards(m_CurrentProgress, 1f, Time.unscaledDeltaTime * m_CompletionProgressSpeed);
            ApplyProgressImmediate(m_CurrentProgress);
            SetLoadingState(LoadingState.Completed);

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
            if (m_LoadingText != null && m_LastLoadingMessage != message)
            {
                m_LoadingText.text = message;
                m_LastLoadingMessage = message;
            }
        }

        private void SetLoadingState(LoadingState state)
        {
            if (m_CurrentLoadingState == state && !string.IsNullOrEmpty(m_LastLoadingMessage))
            {
                return;
            }

            m_CurrentLoadingState = state;
            SetLoadingMessage(GetMessageForState(state));
        }

        private static string GetMessageForState(LoadingState state)
        {
            return state switch
            {
                LoadingState.InitializingWorld => InitializingWorldMessage,
                LoadingState.GeneratingTerrain => GeneratingTerrainMessage,
                LoadingState.Completed => LoadingCompleteMessage,
                _ => WaitingNetworkMessage,
            };
        }
    }
}
