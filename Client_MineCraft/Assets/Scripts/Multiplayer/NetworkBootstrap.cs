using System.Collections;
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
        [SerializeField] private bool m_AutoJoinExistingHostAsClient = true;
        [SerializeField] [Min(0.1f)] private float m_HostProbeTimeoutSeconds = 3f;
        [SerializeField] private string m_NetworkAddress = "127.0.0.1";
        [SerializeField] private ushort m_KcpPort = 7777;
        [SerializeField] private MyNetworkManager m_NetworkManager;

        private Coroutine m_AutoStartRoutine;

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

            RuntimeGameMode initialMode = GetInitialRuntimeMode();
            GameModeContext.SetMode(initialMode);
            m_NetworkManager.PreconfigureWorldForCurrentMode();
            Debug.Log($"[MP] Bootstrap mode={m_Mode}, initialRuntimeMode={initialMode}, multiplayer={GameModeContext.IsMultiplayer}, server={GameModeContext.IsServer}, client={GameModeContext.IsClient}, address={m_NetworkAddress}:{m_KcpPort}, autoJoinExistingHostAsClient={m_AutoJoinExistingHostAsClient}");

        }

        private void Start()
        {
            if (!m_AutoStart)
            {
                return;
            }

            StartByMode();
        }

        [ContextMenu("Start By Mode")]
        public void StartByMode()
        {
            if (m_NetworkManager == null || m_NetworkManager.isNetworkActive || m_AutoStartRoutine != null)
            {
                return;
            }

            if (ShouldAutoJoinExistingHost())
            {
                m_AutoStartRoutine = StartCoroutine(AutoStartHostOrClient());
                return;
            }

            StartDirectByMode();
        }

        private void StartDirectByMode()
        {
            GameModeContext.SetMode(ToRuntimeMode(m_Mode));
            m_NetworkManager.PreconfigureWorldForCurrentMode();

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

        private IEnumerator AutoStartHostOrClient()
        {
            GameModeContext.SetMode(RuntimeGameMode.Client);
            m_NetworkManager.PreconfigureWorldForCurrentMode();
            m_NetworkManager.StartClient();
            Debug.Log($"[MP] Host probe started. Trying client connect first for {m_HostProbeTimeoutSeconds:0.0}s.");

            float deadline = Time.unscaledTime + m_HostProbeTimeoutSeconds;
            while (Time.unscaledTime < deadline)
            {
                if (NetworkClient.isConnected)
                {
                    Debug.Log("[MP] Existing host detected. Staying in client mode.");
                    m_AutoStartRoutine = null;
                    yield break;
                }

                if (!NetworkClient.active)
                {
                    break;
                }

                yield return null;
            }

            if (NetworkClient.active)
            {
                m_NetworkManager.StopClient();
                while (NetworkClient.active)
                {
                    yield return null;
                }
            }

            GameModeContext.SetMode(RuntimeGameMode.Host);
            m_NetworkManager.PreconfigureWorldForCurrentMode();
            m_NetworkManager.StartHost();
            Debug.Log("[MP] No existing host detected. Started local host.");
            m_AutoStartRoutine = null;
        }

        private bool ShouldAutoJoinExistingHost()
        {
            return m_Mode == StartMode.Host && m_AutoJoinExistingHostAsClient;
        }

        private RuntimeGameMode GetInitialRuntimeMode()
        {
            if (ShouldAutoJoinExistingHost())
            {
                return RuntimeGameMode.Client;
            }

            return ToRuntimeMode(m_Mode);
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
