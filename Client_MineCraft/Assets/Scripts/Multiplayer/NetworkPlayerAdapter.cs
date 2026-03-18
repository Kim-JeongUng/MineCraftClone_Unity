using System.Collections;
using Minecraft;
using Minecraft.Entities;
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

        private bool m_HasBoundLocalWorldReferences;

        private bool IsOwnedLocally => isLocalPlayer || isOwned;

        public bool RequestRemoveBlock(Vector3Int position)
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
                return TryRemoveBlockOnServer(position.x, position.y, position.z);
            }

            CmdRemoveBlock(position.x, position.y, position.z);
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

            if (previousPlayerTransform != null && previousPlayerTransform != transform)
            {
                previousPlayerTransform.gameObject.SetActive(false);
            }

            Debug.Log("[MP] Local network player entered world and references were rebound.");
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

        [Command]
        private void CmdRemoveBlock(int x, int y, int z)
        {
            TryRemoveBlockOnServer(x, y, z);
        }

        [Server]
        private bool TryRemoveBlockOnServer(int x, int y, int z)
        {
            if (y < 0 || y >= WorldConsts.ChunkHeight)
            {
                return false;
            }

            MyNetworkManager manager = NetworkManager.singleton as MyNetworkManager;
            return manager != null && manager.TryRemoveBlockOnServer(x, y, z);
        }
    }
}
