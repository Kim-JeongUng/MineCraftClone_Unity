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

        public override void OnStartClient()
        {
            base.OnStartClient();
            ApplyLocalState(isLocalPlayer);
        }

        public override void OnStartLocalPlayer()
        {
            base.OnStartLocalPlayer();
            ApplyLocalState(true);
            StartCoroutine(BindWorldAndDisableScenePlayer());
        }

        private IEnumerator BindWorldAndDisableScenePlayer()
        {
            World world = null;
            while (world == null)
            {
                world = World.Active as World;
                yield return null;
            }

            Transform previousPlayerTransform = world.PlayerTransform;
            world.OverrideLocalPlayerReferences(transform, m_PlayerCamera);

            if (previousPlayerTransform != null && previousPlayerTransform != transform)
            {
                previousPlayerTransform.gameObject.SetActive(false);
            }

            Debug.Log("[MP] Local network player entered world and references were rebound.");
        }

        private void ApplyLocalState(bool local)
        {
            if (m_PlayerEntity != null)
            {
                m_PlayerEntity.enabled = local;
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
    }
}
