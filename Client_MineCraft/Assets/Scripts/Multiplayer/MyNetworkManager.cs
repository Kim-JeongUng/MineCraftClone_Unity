using Mirror;
using kcp2k;
using UnityEngine;

namespace Minecraft.Multiplayer
{
    public class MyNetworkManager : NetworkManager
    {
        [SerializeField] private Transform m_FallbackSpawnPoint;

        public override void OnStartServer()
        {
            base.OnStartServer();

            ushort port = 0;
            if (Transport.active is KcpTransport kcp)
            {
                port = kcp.Port;
            }

            Debug.Log($"[MP] Server start complete. port={port}");
        }

        public override void OnServerConnect(NetworkConnectionToClient conn)
        {
            base.OnServerConnect(conn);
            Debug.Log($"[MP] Server accepted client. connId={conn.connectionId}, connected={NetworkServer.connections.Count}");
        }

        public override void OnServerAddPlayer(NetworkConnectionToClient conn)
        {
            if (conn.identity != null)
            {
                Debug.LogWarning($"[MP] Skip AddPlayer. connId={conn.connectionId} already has player.");
                return;
            }

            Transform spawn = GetSpawnPoint();
            GameObject player = Instantiate(playerPrefab, spawn.position, spawn.rotation);
            NetworkServer.AddPlayerForConnection(conn, player);
            Debug.Log($"[MP] AddPlayer complete. connId={conn.connectionId}, player={player.name}, spawn={spawn.position}");
        }

        public override void OnServerDisconnect(NetworkConnectionToClient conn)
        {
            Debug.Log($"[MP] Server disconnect. connId={conn.connectionId}, connected(before)={NetworkServer.connections.Count}");
            base.OnServerDisconnect(conn);
        }

        public override void OnClientConnect()
        {
            base.OnClientConnect();
            Debug.Log("[MP] Client connected successfully.");
        }

        public override void OnClientDisconnect()
        {
            Debug.Log("[MP] Client disconnected from server.");
            base.OnClientDisconnect();
        }

        private Transform GetSpawnPoint()
        {
            if (m_FallbackSpawnPoint != null)
            {
                return m_FallbackSpawnPoint;
            }

            GameObject existingPlayer = GameObject.FindGameObjectWithTag("Player");
            if (existingPlayer != null)
            {
                return existingPlayer.transform;
            }

            return transform;
        }
    }
}
