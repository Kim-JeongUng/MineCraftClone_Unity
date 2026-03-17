using Mirror;
using kcp2k;
using UnityEngine;

namespace Minecraft.Multiplayer
{
    public class NetworkBootstrap : MonoBehaviour
    {
        public enum StartMode
        {
            SinglePlayer,
            DedicatedServer,
            Client,
            Host,
        }

        [SerializeField] private StartMode m_Mode = StartMode.SinglePlayer;
        [SerializeField] private bool m_AutoStart = true;
        [SerializeField] private string m_NetworkAddress = "127.0.0.1";
        [SerializeField] private ushort m_KcpPort = 7777;
        [SerializeField] private MyNetworkManager m_NetworkManager;

        private void Awake()
        {
            if (m_NetworkManager == null)
            {
                m_NetworkManager = FindObjectOfType<MyNetworkManager>();
            }

            if (m_NetworkManager == null)
            {
                Debug.LogError("[MP] NetworkBootstrap could not find MyNetworkManager.");
                return;
            }

            m_NetworkManager.networkAddress = m_NetworkAddress;

            KcpTransport kcp = m_NetworkManager.GetComponent<KcpTransport>();
            if (kcp != null)
            {
                kcp.Port = m_KcpPort;
            }

            GameModeContext.SetMode(ToRuntimeMode(m_Mode));
            Debug.Log($"[MP] Bootstrap mode={m_Mode}, multiplayer={GameModeContext.IsMultiplayer}, server={GameModeContext.IsServer}, client={GameModeContext.IsClient}, address={m_NetworkAddress}:{m_KcpPort}");

            if (!m_AutoStart)
            {
                return;
            }

            StartByMode();
        }

        [ContextMenu("Start By Mode")]
        public void StartByMode()
        {
            if (m_NetworkManager == null || m_NetworkManager.isNetworkActive)
            {
                return;
            }

            switch (m_Mode)
            {
                case StartMode.DedicatedServer:
                    m_NetworkManager.StartServer();
                    break;
                case StartMode.Client:
                    m_NetworkManager.StartClient();
                    break;
                case StartMode.Host:
                    m_NetworkManager.StartHost();
                    break;
            }
        }

        private static RuntimeGameMode ToRuntimeMode(StartMode mode)
        {
            return mode switch
            {
                StartMode.DedicatedServer => RuntimeGameMode.DedicatedServer,
                StartMode.Client => RuntimeGameMode.Client,
                StartMode.Host => RuntimeGameMode.Host,
                _ => RuntimeGameMode.SinglePlayer,
            };
        }
    }
}
