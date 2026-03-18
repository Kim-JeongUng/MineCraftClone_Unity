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

        private bool m_HasBoundLocalWorldReferences;
        private float m_LastSyncedMoveAnimationSpeed = -1f;
        private bool m_LastSyncedDigAnimationState;

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

            blockInteraction.AssignHandBlockUI(uiCanvasManager.CurrentHandBlockText, uiCanvasManager.HandBlockInput);
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

            if (Mathf.Abs(m_LastSyncedMoveAnimationSpeed - moveSpeed) < MoveAnimationSyncThreshold)
            {
                return;
            }

            m_LastSyncedMoveAnimationSpeed = moveSpeed;

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
        private void CmdSetMoveAnimation(float moveSpeed)
        {
            RpcSetMoveAnimation(moveSpeed);
        }

        [ClientRpc(includeOwner = false)]
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
