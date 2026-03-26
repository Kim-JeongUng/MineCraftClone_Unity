using System.Collections;
using System.Collections.Generic;
using System;
using System.Security.Cryptography;
using Minecraft.Configurations;
using Minecraft;
using Mirror;
using kcp2k;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Minecraft.Multiplayer
{
    public class MyNetworkManager : NetworkManager
    {
        private struct WorldSettingsMessage : NetworkMessage
        {
            public int Seed;
        }

        [Header("World Sync")]
        [SerializeField] private int m_MultiplayerSeed = 13579;

        [Header("Spawning")]
        [SerializeField] private Transform m_FallbackSpawnPoint;
        [SerializeField] private MultiplayerSpawnService m_SpawnService;
        [SerializeField] [Min(1f)] private float m_SpawnPreparationTimeoutSeconds = 10f;

        private MultiplayerBlockRemovalSystem m_BlockRemovalSystem;
        private readonly Dictionary<int, Coroutine> m_PendingSpawnCoroutines = new Dictionary<int, Coroutine>();
        private int m_RuntimeMultiplayerSeed;

        public void PreconfigureWorldForCurrentMode()
        {
            if (!GameModeContext.IsMultiplayer)
            {
                return;
            }

            if (GameModeContext.IsClient && !GameModeContext.IsServer)
            {
                GameModeContext.MarkWorldSettingsPending();
                Debug.Log($"[MP] Client waiting for authoritative world settings. mode={GameModeContext.Mode}");
                return;
            }

            EnsureAuthoritativeSeedInitialized();
            ApplyLocalWorldSetting(GetAuthoritativeSeed());
            Debug.Log($"[MP] Preconfigured local multiplayer world. mode={GameModeContext.Mode}, seed={GetAuthoritativeSeed()}");
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            EnsureBlockRemovalSystemReference();
            m_BlockRemovalSystem.StartServer();
            ApplyServerWorldSetting();
            EnsureSpawnServiceReference();

            ushort port = 0;
            if (Transport.active is KcpTransport kcp)
            {
                port = kcp.Port;
            }

            Debug.Log($"[MP] Server start complete. port={port}, seed={World.ActiveSetting?.Seed}, mode={GameModeContext.Mode}");
        }

        public override void OnServerConnect(NetworkConnectionToClient conn)
        {
            base.OnServerConnect(conn);
            SendWorldSettings(conn, "connect");
            Debug.Log($"[MP] Server accepted client. connId={conn.connectionId}, connected={NetworkServer.connections.Count}, sharedSeed={GetAuthoritativeSeed()}");
        }

        public override void OnServerAddPlayer(NetworkConnectionToClient conn)
        {
            if (conn.identity != null)
            {
                Debug.LogWarning($"[MP] Skip AddPlayer. connId={conn.connectionId} already has player.");
                return;
            }

            EnsureSpawnServiceReference();
            CleanupPendingSpawn(conn.connectionId);
            m_PendingSpawnCoroutines[conn.connectionId] = StartCoroutine(AddPlayerWhenSpawnReady(conn));
        }

        public override void OnServerDisconnect(NetworkConnectionToClient conn)
        {
            EnsureSpawnServiceReference();
            CleanupPendingSpawn(conn.connectionId);
            m_SpawnService.ReleaseSpawn(conn);
            Debug.Log($"[MP] Server disconnect. connId={conn.connectionId}, connected(before)={NetworkServer.connections.Count}");
            base.OnServerDisconnect(conn);

            int remainingConnections = NetworkServer.connections.Count;
            if (remainingConnections == 0 && NetworkServer.active)
            {
                Debug.Log("[MP] No clients remain in the room. Stopping server so the next join creates a fresh session.");
                StopServer();
            }
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            EnsureBlockRemovalSystemReference();
            m_BlockRemovalSystem.StartClient();
            NetworkClient.RegisterHandler<WorldSettingsMessage>(OnWorldSettingsReceived, false);
            PreconfigureWorldForCurrentMode();
            Debug.Log($"[MP] Client start complete. preconfiguredSeed={GetAuthoritativeSeed()}");
        }

        public override void OnClientConnect()
        {
            base.OnClientConnect();
            Debug.Log($"[MP] Client connected successfully. activeSeed={World.ActiveSetting?.Seed}");
        }

        public override void OnClientDisconnect()
        {
            Debug.Log("[MP] Client disconnected from server.");
            ResetClientWorldState();
            base.OnClientDisconnect();
        }

        public override void OnServerError(NetworkConnectionToClient conn, TransportError error, string reason)
        {
            EnsureSpawnServiceReference();
            if (conn != null)
            {
                CleanupPendingSpawn(conn.connectionId);
                m_SpawnService.ReleaseSpawn(conn);
            }

            Debug.LogWarning($"[MP] Server error. connId={conn?.connectionId}, error={error}, reason={reason}");
            base.OnServerError(conn, error, reason);
        }

        public override void OnStopServer()
        {
            foreach (var pair in m_PendingSpawnCoroutines)
            {
                if (pair.Value != null)
                {
                    StopCoroutine(pair.Value);
                }
            }

            m_PendingSpawnCoroutines.Clear();
            EnsureBlockRemovalSystemReference();
            m_BlockRemovalSystem.StopServer();
            EnsureSpawnServiceReference();
            m_SpawnService.ResetReservations();
            m_RuntimeMultiplayerSeed = 0;
            base.OnStopServer();
        }

        public override void OnStopClient()
        {
            EnsureBlockRemovalSystemReference();
            m_BlockRemovalSystem.StopClient();
            ResetClientWorldState();
            base.OnStopClient();
        }

        private IEnumerator AddPlayerWhenSpawnReady(NetworkConnectionToClient conn)
        {
            int connectionId = conn != null ? conn.connectionId : -1;
            World world = null;
            float deadline = Time.unscaledTime + m_SpawnPreparationTimeoutSeconds;

            while (Time.unscaledTime < deadline)
            {
                if (conn == null || !NetworkServer.connections.ContainsKey(connectionId) || conn.identity != null)
                {
                    RemovePendingSpawnRecord(connectionId);
                    ReleaseSpawnReservation(conn);
                    yield break;
                }

                world = World.Active as World;
                if (world != null && world.Initialized)
                {
                    break;
                }

                yield return null;
            }

            if (world == null || !world.Initialized)
            {
                Debug.LogWarning($"[MP] AddPlayer aborted because world was not ready in time. connId={connectionId}");
                RemovePendingSpawnRecord(connectionId);
                ReleaseSpawnReservation(conn);
                yield break;
            }

            MultiplayerSpawnService.SpawnReservation reservation = m_SpawnService.ReserveSpawn(conn, world);
            while (Time.unscaledTime < deadline)
            {
                if (conn == null || !NetworkServer.connections.ContainsKey(connectionId) || conn.identity != null)
                {
                    RemovePendingSpawnRecord(connectionId);
                    ReleaseSpawnReservation(conn);
                    yield break;
                }

                if (m_SpawnService.IsSpawnAreaReady(world, reservation.Position))
                {
                    break;
                }

                yield return null;
            }

            if (!m_SpawnService.IsSpawnAreaReady(world, reservation.Position))
            {
                Debug.LogWarning($"[MP] AddPlayer aborted because spawn chunks were not ready in time. connId={connectionId}, reserved={reservation.Position}");
                RemovePendingSpawnRecord(connectionId);
                ReleaseSpawnReservation(conn);
                yield break;
            }

            if (playerPrefab == null)
            {
                Debug.LogWarning($"[MP] AddPlayer aborted because playerPrefab is missing. connId={connectionId}");
                RemovePendingSpawnRecord(connectionId);
                ReleaseSpawnReservation(conn);
                yield break;
            }

            Vector3 spawnPosition = m_SpawnService.FinalizeSpawn(conn, world);
            Quaternion spawnRotation = m_SpawnService.SpawnRotation;
            GameObject player = Instantiate(playerPrefab, spawnPosition, spawnRotation);
            bool added = NetworkServer.AddPlayerForConnection(conn, player);

            if (added)
            {
                SendWorldSettings(conn, "add-player");
            }

            RemovePendingSpawnRecord(connectionId);

            if (!added)
            {
                Debug.LogWarning($"[MP] AddPlayer failed after spawn preparation. connId={connectionId}, reserved={reservation.Position}, final={spawnPosition}");
                Destroy(player);
                ReleaseSpawnReservation(conn);
                yield break;
            }

            Vector3 anchor = m_SpawnService.AnchorPosition;
            Vector3 delta = spawnPosition - anchor;
            Debug.Log($"[MP] AddPlayer complete. connId={connectionId}, player={player.name}, seed={GetAuthoritativeSeed()}, baseSpawn={m_SpawnService.BaseSpawnPosition}, anchor={anchor}, finalSpawn={spawnPosition}, deltaFromAnchor={delta}, mode={GameModeContext.Mode}");
        }

        private void SendWorldSettings(NetworkConnectionToClient conn, string reason)
        {
            if (conn == null)
            {
                return;
            }

            int seed = GetAuthoritativeSeed();
            conn.Send(new WorldSettingsMessage { Seed = seed });
            Debug.Log($"[MP] Sent authoritative world settings. connId={conn.connectionId}, seed={seed}, reason={reason}");
        }

        private void CleanupPendingSpawn(int connectionId)
        {
            if (m_PendingSpawnCoroutines.TryGetValue(connectionId, out Coroutine routine))
            {
                if (routine != null)
                {
                    StopCoroutine(routine);
                }

                m_PendingSpawnCoroutines.Remove(connectionId);
            }
        }

        public bool TrySetBlockOnServer(int x, int y, int z, int blockId, Quaternion rotation)
        {
            EnsureBlockRemovalSystemReference();
            return m_BlockRemovalSystem != null && m_BlockRemovalSystem.TrySetBlockOnServer(x, y, z, blockId, rotation);
        }

        public bool TryClickBlockOnServer(int x, int y, int z)
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

            BlockData block = world.RWAccessor.GetBlock(x, y, z);
            if (block == null)
            {
                return false;
            }

            block.Click(world, x, y, z);
            return true;
        }

        private void RemovePendingSpawnRecord(int connectionId)
        {
            if (connectionId >= 0)
            {
                m_PendingSpawnCoroutines.Remove(connectionId);
            }
        }

        private void ReleaseSpawnReservation(NetworkConnectionToClient conn)
        {
            if (conn == null)
            {
                return;
            }

            EnsureSpawnServiceReference();
            m_SpawnService.ReleaseSpawn(conn);
        }


        private void ResetClientWorldState()
        {
            if (GameModeContext.IsServer)
            {
                return;
            }

            if (World.ActiveSetting != null)
            {
                World.ActiveSetting.Seed = 0;
            }

            GameModeContext.MarkWorldSettingsPending();
        }

        private void EnsureBlockRemovalSystemReference()
        {
            if (m_BlockRemovalSystem == null)
            {
                m_BlockRemovalSystem = GetComponent<MultiplayerBlockRemovalSystem>();

                if (m_BlockRemovalSystem == null)
                {
                    m_BlockRemovalSystem = gameObject.AddComponent<MultiplayerBlockRemovalSystem>();
                }
            }
        }

        private void EnsureSpawnServiceReference()
        {
            if (m_SpawnService == null)
            {
                m_SpawnService = GetComponent<MultiplayerSpawnService>();
            }

            if (m_SpawnService == null)
            {
                m_SpawnService = gameObject.AddComponent<MultiplayerSpawnService>();
            }

            m_SpawnService.ConfigureFallbackSpawnPoint(m_FallbackSpawnPoint);
        }

        private void ApplyServerWorldSetting()
        {
            EnsureAuthoritativeSeedInitialized();
            ApplyLocalWorldSetting(GetAuthoritativeSeed());
            Debug.Log($"[MP] Server authoritative world seed prepared. seed={GetAuthoritativeSeed()}, scene={SceneManager.GetActiveScene().name}");
        }

        private void ApplyLocalWorldSetting(int seed)
        {
            if (!GameModeContext.IsMultiplayer)
            {
                return;
            }

            if (World.ActiveSetting == null)
            {
                World.ActiveSetting = new WorldSetting();
            }

            World.ActiveSetting.Seed = seed;
            GameModeContext.SetAuthoritativeWorldSeed(seed);
        }

        private int GetAuthoritativeSeed()
        {
            return m_RuntimeMultiplayerSeed != 0 ? m_RuntimeMultiplayerSeed : m_MultiplayerSeed;
        }

        private void EnsureAuthoritativeSeedInitialized()
        {
            if (!GameModeContext.IsServer || m_RuntimeMultiplayerSeed != 0)
            {
                return;
            }

            m_RuntimeMultiplayerSeed = GenerateTimeBasedSeed();
        }

        private static int GenerateTimeBasedSeed()
        {
            Span<byte> buffer = stackalloc byte[4];
            RandomNumberGenerator.Fill(buffer);
            int randomSeed = BitConverter.ToInt32(buffer);
            int timeSeed = unchecked((int)(DateTime.UtcNow.Ticks ^ (DateTime.UtcNow.Ticks >> 32)));
            int guidSeed = Guid.NewGuid().GetHashCode();
            int seed = randomSeed ^ timeSeed ^ guidSeed;
            return seed == 0 ? 13579 : seed;
        }

        private void OnWorldSettingsReceived(WorldSettingsMessage message)
        {
            ApplyLocalWorldSetting(message.Seed);
            Debug.Log($"[MP] Client received authoritative world settings. seed={message.Seed}");
        }
    }
}
