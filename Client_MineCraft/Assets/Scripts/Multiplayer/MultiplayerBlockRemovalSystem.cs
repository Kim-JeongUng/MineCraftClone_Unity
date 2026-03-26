using System.Collections;
using System.Collections.Generic;
using Minecraft.Configurations;
using Mirror;
using UnityEngine;

namespace Minecraft.Multiplayer
{
    [DisallowMultipleComponent]
    public class MultiplayerBlockRemovalSystem : MonoBehaviour
    {
        public struct RequestChunkBlockChangesMessage : NetworkMessage
        {
            public int ChunkX;
            public int ChunkZ;
        }

        public struct ChunkBlockChangesSnapshotMessage : NetworkMessage
        {
            public int ChunkX;
            public int ChunkZ;
            public int ChunkVersion;
            public int[] LocalIndices;
            public int[] BlockIds;
            public Quaternion[] Rotations;
        }

        public struct BlockChangedDeltaMessage : NetworkMessage
        {
            public int X;
            public int Y;
            public int Z;
            public int ChunkVersion;
            public int BlockId;
            public Quaternion Rotation;
        }

        private struct BlockChangeState
        {
            public int BlockId;
            public Quaternion Rotation;
        }

        private readonly Dictionary<ChunkPos, Dictionary<int, BlockChangeState>> m_ServerBlockChangesByChunk = new Dictionary<ChunkPos, Dictionary<int, BlockChangeState>>();
        private readonly Dictionary<ChunkPos, Dictionary<int, BlockChangeState>> m_ClientBlockChangesByChunk = new Dictionary<ChunkPos, Dictionary<int, BlockChangeState>>();
        private readonly Dictionary<ChunkPos, int> m_ServerChunkVersions = new Dictionary<ChunkPos, int>();
        private readonly Dictionary<ChunkPos, int> m_ClientChunkVersions = new Dictionary<ChunkPos, int>();
        private readonly Dictionary<long, BlockChangedDeltaMessage> m_ServerPendingDeltas = new Dictionary<long, BlockChangedDeltaMessage>();
        private readonly Queue<long> m_ServerPendingDeltaOrder = new Queue<long>();

        private const int MaxServerDeltaSendsPerFrame = 256;

        private Coroutine m_WorldBindingRoutine;
        private World m_BoundWorld;

        public void StartServer()
        {
            NetworkServer.RegisterHandler<RequestChunkBlockChangesMessage>(OnServerRequestChunkBlockChanges, false);
            BindWorldCallbacks();
        }

        public void StopServer()
        {
            m_ServerBlockChangesByChunk.Clear();
            m_ServerChunkVersions.Clear();
            m_ServerPendingDeltas.Clear();
            m_ServerPendingDeltaOrder.Clear();
            UnbindWorldCallbacks();
        }

        public void StartClient()
        {
            NetworkClient.RegisterHandler<ChunkBlockChangesSnapshotMessage>(OnClientChunkSnapshotReceived, false);
            NetworkClient.RegisterHandler<BlockChangedDeltaMessage>(OnClientBlockChangedDeltaReceived, false);
            BindWorldCallbacks();
        }

        public void StopClient()
        {
            m_ClientBlockChangesByChunk.Clear();
            m_ClientChunkVersions.Clear();
            UnbindWorldCallbacks();
        }

        public bool TrySetBlockOnServer(int x, int y, int z, int blockId, Quaternion rotation)
        {
            if (!NetworkServer.active)
            {
                return false;
            }

            World world = World.Active as World;
            if (world == null || !world.Initialized || world.BlockDataTable == null)
            {
                return false;
            }

            if (y < 0 || y >= WorldConsts.ChunkHeight || blockId < 0 || blockId >= world.BlockDataTable.BlockCount)
            {
                return false;
            }

            BlockData targetBlock = world.BlockDataTable.GetBlock(blockId);
            if (targetBlock == null)
            {
                return false;
            }

            return world.RWAccessor.SetBlock(x, y, z, targetBlock, rotation, ModificationSource.PlayerAction);
        }

        private void BindWorldCallbacks()
        {
            if (m_WorldBindingRoutine == null)
            {
                m_WorldBindingRoutine = StartCoroutine(BindWorldWhenReady());
            }
        }

        private IEnumerator BindWorldWhenReady()
        {
            while (enabled)
            {
                World activeWorld = World.Active as World;
                if (activeWorld != null && activeWorld.Initialized && activeWorld.ChunkManager != null)
                {
                    if (m_BoundWorld != activeWorld)
                    {
                        DetachWorldCallbacks();
                        m_BoundWorld = activeWorld;
                        m_BoundWorld.ChunkManager.OnChunkLoaded += OnChunkLoaded;
                        m_BoundWorld.BlockChanged += OnWorldBlockChanged;
                    }

                    m_WorldBindingRoutine = null;
                    yield break;
                }

                yield return null;
            }

            m_WorldBindingRoutine = null;
        }

        private void UnbindWorldCallbacks()
        {
            if (m_WorldBindingRoutine != null)
            {
                StopCoroutine(m_WorldBindingRoutine);
                m_WorldBindingRoutine = null;
            }

            DetachWorldCallbacks();
        }

        private void DetachWorldCallbacks()
        {
            if (m_BoundWorld != null && m_BoundWorld.ChunkManager != null)
            {
                m_BoundWorld.ChunkManager.OnChunkLoaded -= OnChunkLoaded;
            }

            if (m_BoundWorld != null)
            {
                m_BoundWorld.BlockChanged -= OnWorldBlockChanged;
            }

            m_BoundWorld = null;
        }

        private void OnWorldBlockChanged(World.BlockChangedInfo blockChange)
        {
            if (!NetworkServer.active || blockChange.Block == null)
            {
                return;
            }

            ChunkPos chunkPos = ChunkPos.GetFromAny(blockChange.X, blockChange.Z);
            int localIndex = ToLocalBlockIndex(blockChange.X - chunkPos.X, blockChange.Y, blockChange.Z - chunkPos.Z);
            Dictionary<int, BlockChangeState> chunkChanges = GetOrCreateServerChunkChanges(chunkPos);
            BlockChangeState nextState = new BlockChangeState
            {
                BlockId = blockChange.Block.ID,
                Rotation = blockChange.Rotation
            };

            if (chunkChanges.TryGetValue(localIndex, out BlockChangeState previousState) &&
                previousState.BlockId == nextState.BlockId &&
                previousState.Rotation.Equals(nextState.Rotation))
            {
                return;
            }

            chunkChanges[localIndex] = nextState;
            int nextVersion = GetNextServerChunkVersion(chunkPos);
            EnqueueServerDelta(new BlockChangedDeltaMessage
            {
                X = blockChange.X,
                Y = blockChange.Y,
                Z = blockChange.Z,
                ChunkVersion = nextVersion,
                BlockId = blockChange.Block.ID,
                Rotation = blockChange.Rotation
            });
        }

        private void LateUpdate()
        {
            if (!NetworkServer.active || m_ServerPendingDeltaOrder.Count == 0)
            {
                return;
            }

            int sendBudget = MaxServerDeltaSendsPerFrame;
            while (sendBudget > 0 && m_ServerPendingDeltaOrder.Count > 0)
            {
                long key = m_ServerPendingDeltaOrder.Dequeue();
                if (!m_ServerPendingDeltas.TryGetValue(key, out BlockChangedDeltaMessage delta))
                {
                    continue;
                }

                m_ServerPendingDeltas.Remove(key);
                NetworkServer.SendToAll(delta);
                sendBudget--;
            }
        }

        private void OnChunkLoaded(Chunk chunk)
        {
            if (chunk == null)
            {
                return;
            }

            ChunkPos chunkPos = chunk.Position;

            if (NetworkClient.isConnected)
            {
                NetworkClient.Send(new RequestChunkBlockChangesMessage
                {
                    ChunkX = chunkPos.X,
                    ChunkZ = chunkPos.Z
                });
            }

            if (m_ClientBlockChangesByChunk.TryGetValue(chunkPos, out Dictionary<int, BlockChangeState> chunkChanges))
            {
                ApplyChunkChangesIfLoaded(chunkPos, chunkChanges);
            }
        }

        private void OnServerRequestChunkBlockChanges(NetworkConnectionToClient conn, RequestChunkBlockChangesMessage message)
        {
            if (conn == null)
            {
                return;
            }

            ChunkPos chunkPos = ChunkPos.Get(message.ChunkX, message.ChunkZ);
            ChunkBlockChangesSnapshotMessage snapshot = new ChunkBlockChangesSnapshotMessage
            {
                ChunkX = chunkPos.X,
                ChunkZ = chunkPos.Z,
                ChunkVersion = GetServerChunkVersion(chunkPos)
            };

            if (m_ServerBlockChangesByChunk.TryGetValue(chunkPos, out Dictionary<int, BlockChangeState> chunkChanges) && chunkChanges.Count > 0)
            {
                int count = chunkChanges.Count;
                snapshot.LocalIndices = new int[count];
                snapshot.BlockIds = new int[count];
                snapshot.Rotations = new Quaternion[count];

                int i = 0;
                foreach (KeyValuePair<int, BlockChangeState> pair in chunkChanges)
                {
                    snapshot.LocalIndices[i] = pair.Key;
                    snapshot.BlockIds[i] = pair.Value.BlockId;
                    snapshot.Rotations[i] = pair.Value.Rotation;
                    i++;
                }
            }

            conn.Send(snapshot);
        }

        private void OnClientChunkSnapshotReceived(ChunkBlockChangesSnapshotMessage message)
        {
            ChunkPos chunkPos = ChunkPos.Get(message.ChunkX, message.ChunkZ);
            if (message.ChunkVersion < GetClientChunkVersion(chunkPos))
            {
                return;
            }

            m_ClientChunkVersions[chunkPos] = message.ChunkVersion;
            Dictionary<int, BlockChangeState> chunkChanges = GetOrCreateClientChunkChanges(chunkPos, clearExisting: true);

            int count = GetSafeSnapshotCount(message);
            for (int i = 0; i < count; i++)
            {
                chunkChanges[message.LocalIndices[i]] = new BlockChangeState
                {
                    BlockId = message.BlockIds[i],
                    Rotation = message.Rotations[i]
                };
            }

            ApplyChunkChangesIfLoaded(chunkPos, chunkChanges);
        }

        private void OnClientBlockChangedDeltaReceived(BlockChangedDeltaMessage message)
        {
            if (!IsValidBlockCoordinates(message.X, message.Y, message.Z))
            {
                return;
            }

            ChunkPos chunkPos = ChunkPos.GetFromAny(message.X, message.Z);
            if (message.ChunkVersion < GetClientChunkVersion(chunkPos))
            {
                return;
            }

            m_ClientChunkVersions[chunkPos] = message.ChunkVersion;
            int localIndex = ToLocalBlockIndex(message.X - chunkPos.X, message.Y, message.Z - chunkPos.Z);

            Dictionary<int, BlockChangeState> chunkChanges = GetOrCreateClientChunkChanges(chunkPos, clearExisting: false);
            chunkChanges[localIndex] = new BlockChangeState
            {
                BlockId = message.BlockId,
                Rotation = message.Rotation
            };

            ApplyBlockChangeIfLoaded(message.X, message.Y, message.Z, message.BlockId, message.Rotation);
        }

        private Dictionary<int, BlockChangeState> GetOrCreateServerChunkChanges(ChunkPos chunkPos)
        {
            if (!m_ServerBlockChangesByChunk.TryGetValue(chunkPos, out Dictionary<int, BlockChangeState> chunkChanges))
            {
                chunkChanges = new Dictionary<int, BlockChangeState>();
                m_ServerBlockChangesByChunk.Add(chunkPos, chunkChanges);
            }

            return chunkChanges;
        }

        private Dictionary<int, BlockChangeState> GetOrCreateClientChunkChanges(ChunkPos chunkPos, bool clearExisting)
        {
            if (!m_ClientBlockChangesByChunk.TryGetValue(chunkPos, out Dictionary<int, BlockChangeState> chunkChanges))
            {
                chunkChanges = new Dictionary<int, BlockChangeState>();
                m_ClientBlockChangesByChunk.Add(chunkPos, chunkChanges);
            }
            else if (clearExisting)
            {
                chunkChanges.Clear();
            }

            return chunkChanges;
        }

        private void ApplyChunkChangesIfLoaded(ChunkPos chunkPos, Dictionary<int, BlockChangeState> chunkChanges)
        {
            if (chunkChanges == null || chunkChanges.Count == 0)
            {
                return;
            }

            World world = World.Active as World;
            if (world == null || !world.Initialized || world.BlockDataTable == null || world.ChunkManager == null)
            {
                return;
            }

            if (!world.ChunkManager.GetChunk(chunkPos, false, out _))
            {
                return;
            }

            foreach (KeyValuePair<int, BlockChangeState> pair in chunkChanges)
            {
                FromLocalBlockIndex(pair.Key, out int localX, out int y, out int localZ);
                ApplyBlockChangeIfLoaded(chunkPos.X + localX, y, chunkPos.Z + localZ, pair.Value.BlockId, pair.Value.Rotation);
            }
        }

        private void ApplyBlockChangeIfLoaded(int x, int y, int z, int blockId, Quaternion rotation)
        {
            World world = World.Active as World;
            if (world == null || !world.Initialized || world.BlockDataTable == null || world.ChunkManager == null)
            {
                return;
            }

            if (!IsValidBlockCoordinates(x, y, z))
            {
                return;
            }

            if (blockId < 0 || blockId >= world.BlockDataTable.BlockCount)
            {
                return;
            }

            ChunkPos chunkPos = ChunkPos.GetFromAny(x, z);
            if (!world.ChunkManager.GetChunk(chunkPos, false, out _))
            {
                return;
            }

            BlockData block = world.BlockDataTable.GetBlock(blockId);
            if (block == null)
            {
                return;
            }

            world.RWAccessor.SetBlock(x, y, z, block, rotation, ModificationSource.InternalOrSystem);
        }

        private static int GetSafeSnapshotCount(ChunkBlockChangesSnapshotMessage message)
        {
            if (message.LocalIndices == null || message.BlockIds == null || message.Rotations == null)
            {
                return 0;
            }

            return Mathf.Min(message.LocalIndices.Length, message.BlockIds.Length, message.Rotations.Length);
        }

        private int GetNextServerChunkVersion(ChunkPos chunkPos)
        {
            int nextVersion = GetServerChunkVersion(chunkPos) + 1;
            m_ServerChunkVersions[chunkPos] = nextVersion;
            return nextVersion;
        }

        private int GetServerChunkVersion(ChunkPos chunkPos)
        {
            return m_ServerChunkVersions.TryGetValue(chunkPos, out int version) ? version : 0;
        }

        private int GetClientChunkVersion(ChunkPos chunkPos)
        {
            return m_ClientChunkVersions.TryGetValue(chunkPos, out int version) ? version : 0;
        }

        private void EnqueueServerDelta(BlockChangedDeltaMessage message)
        {
            long key = ToWorldBlockKey(message.X, message.Y, message.Z);
            if (m_ServerPendingDeltas.ContainsKey(key))
            {
                m_ServerPendingDeltas[key] = message;
                return;
            }

            m_ServerPendingDeltas.Add(key, message);
            m_ServerPendingDeltaOrder.Enqueue(key);
        }

        private static long ToWorldBlockKey(int x, int y, int z)
        {
            unchecked
            {
                long key = (uint)x;
                key = (key << 20) ^ (uint)z;
                key = (key << 10) ^ (uint)y;
                return key;
            }
        }

        private static bool IsValidBlockCoordinates(int x, int y, int z)
        {
            return y >= 0 && y < WorldConsts.ChunkHeight;
        }

        private static int ToLocalBlockIndex(int localX, int y, int localZ)
        {
            return (localX << 12) | (y << 4) | localZ;
        }

        private static void FromLocalBlockIndex(int localIndex, out int localX, out int y, out int localZ)
        {
            localX = (localIndex >> 12) & 0xF;
            y = (localIndex >> 4) & 0xFF;
            localZ = localIndex & 0xF;
        }
    }
}
