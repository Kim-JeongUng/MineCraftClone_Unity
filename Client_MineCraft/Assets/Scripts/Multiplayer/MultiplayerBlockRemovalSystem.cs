using System.Collections;
using System.Collections.Generic;
using Minecraft.Configurations;
using Mirror;
using UnityEngine;
using static Minecraft.WorldConsts;

namespace Minecraft.Multiplayer
{
    [DisallowMultipleComponent]
    public class MultiplayerBlockRemovalSystem : MonoBehaviour
    {
        public struct RequestChunkRemovalsMessage : NetworkMessage
        {
            public int ChunkX;
            public int ChunkZ;
        }

        public struct ChunkRemovalsSnapshotMessage : NetworkMessage
        {
            public int ChunkX;
            public int ChunkZ;
            public int[] RemovedBlockIndices;
        }

        public struct BlockRemovedDeltaMessage : NetworkMessage
        {
            public int X;
            public int Y;
            public int Z;
        }

        private readonly Dictionary<ChunkPos, HashSet<int>> m_ServerRemovedBlocksByChunk = new Dictionary<ChunkPos, HashSet<int>>();
        private readonly Dictionary<ChunkPos, HashSet<int>> m_ClientKnownRemovedBlocksByChunk = new Dictionary<ChunkPos, HashSet<int>>();
        private Coroutine m_WorldBindingRoutine;
        private World m_BoundWorld;

        public void StartServer()
        {
            NetworkServer.RegisterHandler<RequestChunkRemovalsMessage>(OnServerRequestChunkRemovals, false);
            BindWorldCallbacks();
        }

        public void StopServer()
        {
            m_ServerRemovedBlocksByChunk.Clear();
            UnbindWorldCallbacks();
        }

        public void StartClient()
        {
            NetworkClient.RegisterHandler<ChunkRemovalsSnapshotMessage>(OnClientChunkSnapshotReceived, false);
            NetworkClient.RegisterHandler<BlockRemovedDeltaMessage>(OnClientBlockRemovedDeltaReceived, false);
            BindWorldCallbacks();
        }

        public void StopClient()
        {
            m_ClientKnownRemovedBlocksByChunk.Clear();
            UnbindWorldCallbacks();
        }

        public bool TryRemoveBlockOnServer(int x, int y, int z)
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

            BlockData airBlock = world.BlockDataTable.GetBlock(0);
            if (airBlock == null)
            {
                return false;
            }

            if (!world.RWAccessor.SetBlock(x, y, z, airBlock, Quaternion.identity, ModificationSource.PlayerAction))
            {
                return false;
            }

            ChunkPos chunkPos = ChunkPos.GetFromAny(x, z);
            if (!m_ServerRemovedBlocksByChunk.TryGetValue(chunkPos, out HashSet<int> removedBlocks))
            {
                removedBlocks = new HashSet<int>();
                m_ServerRemovedBlocksByChunk.Add(chunkPos, removedBlocks);
            }

            removedBlocks.Add(ToLocalBlockIndex(x - chunkPos.X, y, z - chunkPos.Z));
            NetworkServer.SendToAll(new BlockRemovedDeltaMessage { X = x, Y = y, Z = z });
            return true;
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

            m_BoundWorld = null;
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
                NetworkClient.Send(new RequestChunkRemovalsMessage { ChunkX = chunkPos.X, ChunkZ = chunkPos.Z });
            }

            if (m_ClientKnownRemovedBlocksByChunk.TryGetValue(chunkPos, out HashSet<int> removedBlocks))
            {
                ApplyRemovalsToLoadedChunk(chunkPos, removedBlocks);
            }
        }

        private void OnServerRequestChunkRemovals(NetworkConnectionToClient conn, RequestChunkRemovalsMessage message)
        {
            if (conn == null)
            {
                return;
            }

            ChunkPos chunkPos = ChunkPos.Get(message.ChunkX, message.ChunkZ);
            int[] removedIndices = null;

            if (m_ServerRemovedBlocksByChunk.TryGetValue(chunkPos, out HashSet<int> removedBlocks) && removedBlocks.Count > 0)
            {
                removedIndices = new int[removedBlocks.Count];
                removedBlocks.CopyTo(removedIndices);
            }

            conn.Send(new ChunkRemovalsSnapshotMessage
            {
                ChunkX = chunkPos.X,
                ChunkZ = chunkPos.Z,
                RemovedBlockIndices = removedIndices
            });
        }

        private void OnClientChunkSnapshotReceived(ChunkRemovalsSnapshotMessage message)
        {
            ChunkPos chunkPos = ChunkPos.Get(message.ChunkX, message.ChunkZ);
            HashSet<int> removedBlocks = GetOrCreateClientChunkState(chunkPos, clearExisting: true);

            if (message.RemovedBlockIndices != null)
            {
                for (int i = 0; i < message.RemovedBlockIndices.Length; i++)
                {
                    removedBlocks.Add(message.RemovedBlockIndices[i]);
                }
            }

            ApplyRemovalsToLoadedChunk(chunkPos, removedBlocks);
        }

        private void OnClientBlockRemovedDeltaReceived(BlockRemovedDeltaMessage message)
        {
            ChunkPos chunkPos = ChunkPos.GetFromAny(message.X, message.Z);
            HashSet<int> removedBlocks = GetOrCreateClientChunkState(chunkPos, clearExisting: false);
            removedBlocks.Add(ToLocalBlockIndex(message.X - chunkPos.X, message.Y, message.Z - chunkPos.Z));
            ApplyRemovalIfChunkLoaded(message.X, message.Y, message.Z);
        }

        private HashSet<int> GetOrCreateClientChunkState(ChunkPos chunkPos, bool clearExisting)
        {
            if (!m_ClientKnownRemovedBlocksByChunk.TryGetValue(chunkPos, out HashSet<int> removedBlocks))
            {
                removedBlocks = new HashSet<int>();
                m_ClientKnownRemovedBlocksByChunk.Add(chunkPos, removedBlocks);
            }
            else if (clearExisting)
            {
                removedBlocks.Clear();
            }

            return removedBlocks;
        }

        private void ApplyRemovalsToLoadedChunk(ChunkPos chunkPos, HashSet<int> removedBlocks)
        {
            if (removedBlocks == null || removedBlocks.Count == 0)
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

            foreach (int localIndex in removedBlocks)
            {
                FromLocalBlockIndex(localIndex, out int localX, out int y, out int localZ);
                ApplyRemovalIfChunkLoaded(chunkPos.X + localX, y, chunkPos.Z + localZ);
            }
        }

        private void ApplyRemovalIfChunkLoaded(int x, int y, int z)
        {
            World world = World.Active as World;
            if (world == null || !world.Initialized || world.BlockDataTable == null)
            {
                return;
            }

            ChunkPos chunkPos = ChunkPos.GetFromAny(x, z);
            if (!world.ChunkManager.GetChunk(chunkPos, false, out _))
            {
                return;
            }

            BlockData airBlock = world.BlockDataTable.GetBlock(0);
            if (airBlock == null)
            {
                return;
            }

            world.RWAccessor.SetBlock(x, y, z, airBlock, Quaternion.identity, ModificationSource.InternalOrSystem);
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
