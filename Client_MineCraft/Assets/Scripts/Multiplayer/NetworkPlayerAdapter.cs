using System.Collections;
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
            while (!(World.Active is World world))
            {
                yield return null;
            }

            world.OverrideLocalPlayerReferences(transform, m_PlayerCamera);

            GameObject scenePlayer = GameObject.FindGameObjectWithTag("Player");
            if (scenePlayer != null && scenePlayer != gameObject)
            {
                scenePlayer.SetActive(false);
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
