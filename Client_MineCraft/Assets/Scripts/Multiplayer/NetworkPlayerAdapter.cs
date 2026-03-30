using System.Collections;
using Minecraft;
using Minecraft.Configurations;
using Minecraft.Entities;
using Minecraft.PlayerControls;
using Minecraft.UI;
using Mirror;
using UnityEngine;

namespace Minecraft.Multiplayer
{
    public class NetworkPlayerAdapter : NetworkBehaviour
    {
        [SerializeField] private PlayerEntity m_PlayerEntity;
        [SerializeField] private Camera m_PlayerCamera;
        [SerializeField] private Behaviour[] m_LocalOnlyBehaviours;

        private const int RemotePlayerLayer = 9;
        private const int LocalPlayerLayer = 10;
        private const float MoveAnimationSyncThreshold = 0.05f;
        private const float MoveAnimationSyncIntervalSeconds = 0.08f;
        private const float MoveAnimationQuantizationStep = 0.02f;

        private bool m_HasBoundLocalWorldReferences;
        private float m_LastSyncedMoveAnimationSpeed = -1f;
        private bool m_LastSyncedDigAnimationState;
        private float m_LastMoveAnimationSyncTime = float.NegativeInfinity;
        private bool m_HasPendingMoveAnimationSync;
        private float m_PendingMoveAnimationSpeed;

        private bool IsOwnedLocally => isLocalPlayer || isOwned;

        public bool RequestSetBlock(Vector3Int position, BlockData block, Quaternion rotation)
        {
            if (!GameModeContext.IsMultiplayer || block == null)
            {
                return false;
            }

            if (!isOwned && !isLocalPlayer)
            {
                return false;
            }

            if (isServer)
            {
                return TrySetBlockOnServer(position.x, position.y, position.z, block.ID, rotation);
            }

            CmdSetBlock(position.x, position.y, position.z, block.ID, rotation);
            return true;
        }


        public bool RequestClickBlock(Vector3Int position)
        {
            if (!GameModeContext.IsMultiplayer)
            {
                return false;
            }

            if (!isOwned && !isLocalPlayer)
            {
                return false;
            }

            if (isServer)
            {
                Debug.Log($"[TNT TRACE] request click on server directly ({position.x},{position.y},{position.z})");
                return TryClickBlockOnServer(position.x, position.y, position.z);
            }

            Debug.Log($"[TNT TRACE] send CmdClickBlock ({position.x},{position.y},{position.z})");
            CmdClickBlock(position.x, position.y, position.z);
            return true;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            ApplyLocalState(IsOwnedLocally);
        }

        public override void OnStartAuthority()
        {
            base.OnStartAuthority();
            ApplyLocalState(true);
            StartCoroutine(BindWorldAndDisableScenePlayer());
        }

        public override void OnStartLocalPlayer()
        {
            base.OnStartLocalPlayer();
            ApplyLocalState(true);
            StartCoroutine(BindWorldAndDisableScenePlayer());
        }

        private IEnumerator BindWorldAndDisableScenePlayer()
        {
            if (m_HasBoundLocalWorldReferences)
            {
                yield break;
            }

            World world = null;
            while (world == null)
            {
                world = World.Active as World;
                yield return null;
            }

            Transform previousPlayerTransform = world.PlayerTransform;
            world.OverrideLocalPlayerReferences(transform, m_PlayerCamera);
            m_HasBoundLocalWorldReferences = true;
            BindCanvasHandBlockUI();

            if (previousPlayerTransform != null && previousPlayerTransform != transform)
            {
                previousPlayerTransform.gameObject.SetActive(false);
            }

            Debug.Log("[MP] Local network player entered world and references were rebound.");
        }

        private void BindCanvasHandBlockUI()
        {
            BlockInteraction blockInteraction = GetComponentInChildren<BlockInteraction>(true);
            if (blockInteraction == null)
            {
                return;
            }

            UICanvasManager uiCanvasManager = FindObjectOfType<UICanvasManager>(true);
            if (uiCanvasManager == null)
            {
                return;
            }

            blockInteraction.AssignHandBlockUI(
                uiCanvasManager.CurrentHandBlockText,
                uiCanvasManager.HandBlockInput,
                uiCanvasManager.HotbarRoot,
                uiCanvasManager.BlockInventoryRoot,
                uiCanvasManager.BlockInventoryGridRoot);
        }

        private void ApplyLocalState(bool local)
        {
            SetLayerRecursively(gameObject, local ? LocalPlayerLayer : RemotePlayerLayer);

            if (m_PlayerEntity != null)
            {
                m_PlayerEntity.enabled = local;
            }

            if (m_LocalOnlyBehaviours != null)
            {
                for (int i = 0; i < m_LocalOnlyBehaviours.Length; i++)
                {
                    if (m_LocalOnlyBehaviours[i] != null)
                    {
                        m_LocalOnlyBehaviours[i].enabled = local;
                    }
                }
            }

            if (m_PlayerCamera != null)
            {
                m_PlayerCamera.enabled = local;

                AudioListener listener = m_PlayerCamera.GetComponent<AudioListener>();
                if (listener != null)
                {
                    listener.enabled = local;
                }
            }
        }

        private static void SetLayerRecursively(GameObject target, int layer)
        {
            if (target == null)
            {
                return;
            }

            Transform[] children = target.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                children[i].gameObject.layer = layer;
            }
        }

        public void SyncMoveAnimation(float moveSpeed)
        {
            if (!GameModeContext.IsMultiplayer || !IsOwnedLocally)
            {
                return;
            }

            float quantizedMoveSpeed = QuantizeMoveSpeed(moveSpeed);
            if (Mathf.Abs(m_LastSyncedMoveAnimationSpeed - quantizedMoveSpeed) < MoveAnimationSyncThreshold)
            {
                return;
            }

            bool forceImmediateSend = quantizedMoveSpeed <= 0.001f || m_LastSyncedMoveAnimationSpeed <= 0.001f;
            float elapsed = Time.unscaledTime - m_LastMoveAnimationSyncTime;
            if (!forceImmediateSend && elapsed < MoveAnimationSyncIntervalSeconds)
            {
                m_HasPendingMoveAnimationSync = true;
                m_PendingMoveAnimationSpeed = quantizedMoveSpeed;
                return;
            }

            SendMoveAnimationSync(quantizedMoveSpeed);
        }

        private void Update()
        {
            if (!m_HasPendingMoveAnimationSync || !GameModeContext.IsMultiplayer || !IsOwnedLocally)
            {
                return;
            }

            if (Time.unscaledTime - m_LastMoveAnimationSyncTime < MoveAnimationSyncIntervalSeconds)
            {
                return;
            }

            SendMoveAnimationSync(m_PendingMoveAnimationSpeed);
            m_HasPendingMoveAnimationSync = false;
        }

        private void SendMoveAnimationSync(float moveSpeed)
        {
            m_LastSyncedMoveAnimationSpeed = moveSpeed;
            m_LastMoveAnimationSyncTime = Time.unscaledTime;

            if (isServer)
            {
                RpcSetMoveAnimation(moveSpeed);
                return;
            }

            CmdSetMoveAnimation(moveSpeed);
        }

        public void SyncDigAnimationState(bool active)
        {
            if (!GameModeContext.IsMultiplayer || !IsOwnedLocally)
            {
                return;
            }

            if (m_LastSyncedDigAnimationState == active)
            {
                return;
            }

            m_LastSyncedDigAnimationState = active;

            if (isServer)
            {
                RpcSetDigAnimationState(active);
                return;
            }

            CmdSetDigAnimationState(active);
        }

        [Command]
        private void CmdSetBlock(int x, int y, int z, int blockId, Quaternion rotation)
        {
            TrySetBlockOnServer(x, y, z, blockId, rotation);
        }


        [Command]
        private void CmdClickBlock(int x, int y, int z)
        {
            Debug.Log($"[TNT TRACE] server received CmdClickBlock ({x},{y},{z})");
            TryClickBlockOnServer(x, y, z);
        }

        private static float QuantizeMoveSpeed(float moveSpeed)
        {
            float clampedSpeed = Mathf.Clamp01(moveSpeed);
            return Mathf.Round(clampedSpeed / MoveAnimationQuantizationStep) * MoveAnimationQuantizationStep;
        }

        [Command(channel = Channels.Unreliable)]
        private void CmdSetMoveAnimation(float moveSpeed)
        {
            RpcSetMoveAnimation(moveSpeed);
        }

        [ClientRpc(includeOwner = false, channel = Channels.Unreliable)]
        private void RpcSetMoveAnimation(float moveSpeed)
        {
            if (m_PlayerEntity != null)
            {
                m_PlayerEntity.ApplyRemoteMoveAnimation(moveSpeed);
            }
        }

        [Command]
        private void CmdSetDigAnimationState(bool active)
        {
            RpcSetDigAnimationState(active);
        }

        [ClientRpc(includeOwner = false)]
        private void RpcSetDigAnimationState(bool active)
        {
            if (m_PlayerEntity != null)
            {
                m_PlayerEntity.ApplyRemoteDigAnimationState(active);
            }
        }


        [Server]
        private bool TryClickBlockOnServer(int x, int y, int z)
        {
            if (y < 0 || y >= WorldConsts.ChunkHeight)
            {
                return false;
            }

            World world = World.Active as World;
            if (world?.RWAccessor == null)
            {
                return false;
            }

            MyNetworkManager manager = NetworkManager.singleton as MyNetworkManager;
            if (manager == null || !manager.TryClickBlockOnServer(x, y, z))
            {
                Debug.LogWarning($"[TNT TRACE] TryClickBlockOnServer rejected ({x},{y},{z})");
                return false;
            }

            Debug.Log($"[TNT TRACE] TryClickBlockOnServer accepted ({x},{y},{z})");
            return true;
        }

        [Server]
        private bool TrySetBlockOnServer(int x, int y, int z, int blockId, Quaternion rotation)
        {
            if (y < 0 || y >= WorldConsts.ChunkHeight)
            {
                return false;
            }

            MyNetworkManager manager = NetworkManager.singleton as MyNetworkManager;
            return manager != null && manager.TrySetBlockOnServer(x, y, z, blockId, rotation);
        }
    }
}
