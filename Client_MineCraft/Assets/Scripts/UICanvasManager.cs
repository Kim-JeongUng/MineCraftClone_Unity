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
        [SerializeField] [Range(0.01f, 1f)] private float m_ProgressLerpSpeed = 0.2f;

        private float m_CurrentProgress;
        private bool m_IsReady;

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

            float targetProgress = 0.05f;
            string message = "네트워크 연결 대기 중...";

            if (IsNetworkReady())
            {
                targetProgress = 0.4f;
                message = "월드 초기화 중...";

                World world = World.Active as World;
                if (world != null && world.Initialized)
                {
                    targetProgress = 0.75f;
                    message = "지형 생성 중...";

                    if (IsGroundChunkReady(world))
                    {
                        SetReadyState(true);
                        ApplyProgressImmediate(1f);
                        SetLoadingMessage("로딩 완료");
                        return;
                    }
                }
            }

            m_CurrentProgress = Mathf.MoveTowards(m_CurrentProgress, targetProgress, Time.unscaledDeltaTime * m_ProgressLerpSpeed);
            ApplyProgressImmediate(m_CurrentProgress);
            SetLoadingMessage(message);
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
