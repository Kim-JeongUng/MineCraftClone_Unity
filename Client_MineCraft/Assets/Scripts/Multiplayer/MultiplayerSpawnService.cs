using System.Collections.Generic;
using Minecraft.Configurations;
using Minecraft.PhysicSystem;
using Mirror;
using UnityEngine;
using static Minecraft.WorldConsts;

namespace Minecraft.Multiplayer
{
    [DisallowMultipleComponent]
    public class MultiplayerSpawnService : MonoBehaviour
    {
        private static readonly Vector2Int[] SpawnOffsets =
        {
            new Vector2Int(0, 0),
            new Vector2Int(3, 0),
            new Vector2Int(-3, 0),
            new Vector2Int(0, 3),
            new Vector2Int(0, -3),
            new Vector2Int(3, 3),
            new Vector2Int(-3, 3),
            new Vector2Int(3, -3),
            new Vector2Int(-3, -3),
            new Vector2Int(5, 0),
            new Vector2Int(-5, 0),
            new Vector2Int(0, 5),
            new Vector2Int(0, -5),
        };

        [Header("Spawn Policy")]
        [SerializeField] private Transform m_FallbackSpawnPoint;
        [SerializeField] [Min(1)] private int m_ClearanceAboveGround = 2;
        [SerializeField] [Min(1)] private int m_MinSpawnHeight = 4;
        [SerializeField] [Range(0, 64)] private int m_HorizontalLandSearchRadius = 12;
        [SerializeField] [Range(16, 1024)] private int m_MaxHorizontalLandSearchRadius = 256;
        [SerializeField] private bool m_EnableVerboseSpawnDebug = true;
        [SerializeField] private bool m_UseFirstResolvedSpawnAsAnchor = true;

        private readonly Dictionary<int, SpawnReservation> m_Reservations = new Dictionary<int, SpawnReservation>();
        private Vector3 m_BaseSpawnPosition;
        private Vector3 m_AnchorPosition;
        private Quaternion m_SpawnRotation = Quaternion.identity;
        private bool m_Initialized;
        private bool m_HasAnchor;

        public Quaternion SpawnRotation => m_SpawnRotation;
        public Vector3 BaseSpawnPosition => m_BaseSpawnPosition;
        public Vector3 AnchorPosition => m_HasAnchor ? m_AnchorPosition : m_BaseSpawnPosition;

        public readonly struct SpawnReservation
        {
            public SpawnReservation(int connectionId, int spawnIndex, Vector2Int offset, Vector3 position)
            {
                ConnectionId = connectionId;
                SpawnIndex = spawnIndex;
                Offset = offset;
                Position = position;
            }

            public int ConnectionId { get; }
            public int SpawnIndex { get; }
            public Vector2Int Offset { get; }
            public Vector3 Position { get; }
        }

        public void ConfigureFallbackSpawnPoint(Transform fallbackSpawnPoint)
        {
            if (fallbackSpawnPoint != null)
            {
                m_FallbackSpawnPoint = fallbackSpawnPoint;
            }
        }

        [Server]
        public void InitializeFromWorld(World world)
        {
            if (m_Initialized)
            {
                return;
            }

            Transform sourceTransform = ResolveSourceTransform(world);
            m_BaseSpawnPosition = sourceTransform != null ? sourceTransform.position : transform.position;
            m_SpawnRotation = sourceTransform != null ? sourceTransform.rotation : transform.rotation;
            m_AnchorPosition = m_BaseSpawnPosition;
            m_HasAnchor = false;
            m_Initialized = true;

            Debug.Log($"[MP] SpawnService initialized. base={m_BaseSpawnPosition}, chunk={ChunkPos.GetFromAny(m_BaseSpawnPosition.x, m_BaseSpawnPosition.z)}, source={(sourceTransform != null ? sourceTransform.name : gameObject.name)}");
        }

        [Server]
        public SpawnReservation ReserveSpawn(NetworkConnectionToClient conn, World world)
        {
            if (!m_Initialized)
            {
                InitializeFromWorld(world);
            }

            if (m_Reservations.TryGetValue(conn.connectionId, out SpawnReservation existing))
            {
                return existing;
            }

            Vector3 reference = AnchorPosition;
            int spawnIndex = m_Reservations.Count;
            Vector2Int offset = SpawnOffsets[spawnIndex % SpawnOffsets.Length];
            Vector3 reservedPosition = new Vector3(reference.x + offset.x, reference.y, reference.z + offset.y);
            SpawnReservation reservation = new SpawnReservation(conn.connectionId, spawnIndex, offset, reservedPosition);
            m_Reservations[conn.connectionId] = reservation;

            Debug.Log($"[MP] SpawnService reserved spawn. connId={conn.connectionId}, index={spawnIndex}, reference={reference}, offsetXZ=({offset.x}, {offset.y}), reserved={reservedPosition}, chunk={ChunkPos.GetFromAny(reservedPosition.x, reservedPosition.z)}");
            return reservation;
        }

        [Server]
        public bool IsSpawnAreaReady(World world, Vector3 position)
        {
            if (world == null || !world.Initialized || world.ChunkManager == null)
            {
                return false;
            }

            ChunkPos center = ChunkPos.GetFromAny(position.x, position.z);
            bool ready = true;

            for (int x = -1; x <= 1; x++)
            {
                for (int z = -1; z <= 1; z++)
                {
                    if (!world.ChunkManager.GetChunk(center.AddOffset(x, z), true, out _))
                    {
                        ready = false;
                    }
                }
            }

            return ready;
        }

        [Server]
        public Vector3 FinalizeSpawn(NetworkConnectionToClient conn, World world)
        {
            if (!m_Reservations.TryGetValue(conn.connectionId, out SpawnReservation reservation))
            {
                reservation = ReserveSpawn(conn, world);
            }

            Vector3 finalSpawn = ResolveSafeSpawnHeight(reservation.Position, world);

            if (!m_HasAnchor && m_UseFirstResolvedSpawnAsAnchor)
            {
                SetAnchorPosition(finalSpawn);
            }

            Vector3 reference = AnchorPosition;
            Vector3 delta = finalSpawn - reference;
            Debug.Log($"[MP] SpawnService finalized spawn. connId={conn.connectionId}, index={reservation.SpawnIndex}, reference={reference}, offsetXZ=({reservation.Offset.x}, {reservation.Offset.y}), final={finalSpawn}, delta={delta}, chunk={ChunkPos.GetFromAny(finalSpawn.x, finalSpawn.z)}");
            return finalSpawn;
        }

        [Server]
        public bool TryFinalizeGrassSpawn(NetworkConnectionToClient conn, World world, out Vector3 finalSpawn)
        {
            if (!m_Reservations.TryGetValue(conn.connectionId, out SpawnReservation reservation))
            {
                reservation = ReserveSpawn(conn, world);
            }

            if (!TryResolveNearestGrassSpawn(reservation.Position, world, out finalSpawn))
            {
                return false;
            }

            if (!m_HasAnchor && m_UseFirstResolvedSpawnAsAnchor)
            {
                SetAnchorPosition(finalSpawn);
            }

            Vector3 reference = AnchorPosition;
            Vector3 delta = finalSpawn - reference;
            Debug.Log($"[MP] SpawnService finalized grass spawn. connId={conn.connectionId}, index={reservation.SpawnIndex}, reference={reference}, offsetXZ=({reservation.Offset.x}, {reservation.Offset.y}), final={finalSpawn}, delta={delta}, chunk={ChunkPos.GetFromAny(finalSpawn.x, finalSpawn.z)}");
            return true;
        }

        [Server]
        public void ResetReservations()
        {
            m_Reservations.Clear();
            m_HasAnchor = false;
            m_AnchorPosition = m_BaseSpawnPosition;
        }

        [Server]
        public void ReleaseSpawn(NetworkConnectionToClient conn)
        {
            if (conn == null)
            {
                return;
            }

            if (m_Reservations.Remove(conn.connectionId))
            {
                Debug.Log($"[MP] SpawnService released spawn reservation. connId={conn.connectionId}");
            }
        }

        [Server]
        public void SetAnchorPosition(Vector3 position)
        {
            m_AnchorPosition = position;
            m_HasAnchor = true;
            Debug.Log($"[MP] SpawnService anchor set. anchor={m_AnchorPosition}, chunk={ChunkPos.GetFromAny(position.x, position.z)}");
        }

        private Transform ResolveSourceTransform(World world)
        {
            if (world != null && world.PlayerTransform != null)
            {
                return world.PlayerTransform;
            }

            if (m_FallbackSpawnPoint != null)
            {
                return m_FallbackSpawnPoint;
            }

            return transform;
        }

        private Vector3 ResolveSafeSpawnHeight(Vector3 candidate, World world)
        {
            int sampleX = Mathf.RoundToInt(candidate.x);
            int sampleZ = Mathf.RoundToInt(candidate.z);
            float fallbackY = Mathf.Max(candidate.y, m_MinSpawnHeight) + m_ClearanceAboveGround;

            LogSpawnDebug($"ResolveSafeSpawnHeight start. candidate={candidate}, sample=({sampleX}, {sampleZ}), fallbackY={fallbackY}, radius={m_HorizontalLandSearchRadius}");

            if (world == null || !world.Initialized || world.RWAccessor == null)
            {
                LogSpawnDebug("World or RWAccessor is not ready. Returning fallback Y without terrain validation.");
                return new Vector3(candidate.x, fallbackY, candidate.z);
            }

            if (TryFindNearestSafeSpawnPosition(world, sampleX, sampleZ, out int safeX, out int nearestSafeY, out int safeZ))
            {
                BlockData resolvedGround = world.RWAccessor.GetBlock(safeX, nearestSafeY - 1, safeZ);
                LogSpawnDebug($"Resolved nearest grass spawn. resolved=({safeX}, {nearestSafeY}, {safeZ}), ground={resolvedGround?.InternalName ?? "null"}");
                return new Vector3(safeX, nearestSafeY, safeZ);
            }

            LogSpawnDebug("Nearest grass search failed. Falling back to legacy vertical safe search on original column.");
            if (TryFindHighestSafeSpawnY(world, sampleX, sampleZ, out int surfaceSafeY))
            {
                BlockData surfaceGround = world.RWAccessor.GetBlock(sampleX, surfaceSafeY - 1, sampleZ);
                LogSpawnDebug($"Legacy vertical safe search succeeded. resolved=({sampleX}, {surfaceSafeY}, {sampleZ}), ground={surfaceGround?.InternalName ?? "null"}");
                return new Vector3(candidate.x, surfaceSafeY, candidate.z);
            }

            int topVisibleY = world.RWAccessor.GetTopVisibleBlockY(sampleX, sampleZ, int.MinValue);
            if (topVisibleY != int.MinValue)
            {
                int preferredSpawnY = Mathf.Max(topVisibleY + 1, m_MinSpawnHeight);
                int maxSearchY = Mathf.Min(ChunkHeight - 4, preferredSpawnY + m_ClearanceAboveGround + 8);

                if (TryFindSafeSpawnY(world, sampleX, sampleZ, preferredSpawnY, maxSearchY, out int safeY))
                {
                    BlockData preferredGround = world.RWAccessor.GetBlock(sampleX, safeY - 1, sampleZ);
                    LogSpawnDebug($"TopVisible fallback succeeded. resolved=({sampleX}, {safeY}, {sampleZ}), ground={preferredGround?.InternalName ?? "null"}");
                    return new Vector3(candidate.x, safeY, candidate.z);
                }
            }

            int fallbackStartY = Mathf.RoundToInt(fallbackY);
            if (TryFindSafeSpawnY(world, sampleX, sampleZ, fallbackStartY, fallbackStartY + 6, out int fallbackSafeY))
            {
                Debug.LogWarning($"[MP] SpawnService used fallback spawn height search. sample=({sampleX}, {sampleZ}), fallbackStartY={fallbackStartY}, resolvedY={fallbackSafeY}");
                BlockData fallbackGround = world.RWAccessor.GetBlock(sampleX, fallbackSafeY - 1, sampleZ);
                LogSpawnDebug($"Fallback spawn height search picked ground={fallbackGround?.InternalName ?? "null"}");
                return new Vector3(candidate.x, fallbackSafeY, candidate.z);
            }

            if (TryFindDryHeadroomY(world, sampleX, sampleZ, fallbackStartY, ChunkHeight - 4, out int dryHeadroomY))
            {
                Debug.LogWarning($"[MP] SpawnService could not find grass ground. Using dry headroom fallback. sample=({sampleX}, {sampleZ}), fallbackStartY={fallbackStartY}, resolvedY={dryHeadroomY}");
                BlockData dryHeadroomGround = world.RWAccessor.GetBlock(sampleX, dryHeadroomY - 1, sampleZ);
                LogSpawnDebug($"Dry headroom fallback picked ground={dryHeadroomGround?.InternalName ?? "null"}");
                return new Vector3(candidate.x, dryHeadroomY, candidate.z);
            }

            if (TryFindDryHeadroomY(world, sampleX, sampleZ, m_MinSpawnHeight, ChunkHeight - 4, out int anyDryHeadroomY))
            {
                Debug.LogWarning($"[MP] SpawnService could not find nearby safe spawn. Using global dry headroom fallback. sample=({sampleX}, {sampleZ}), resolvedY={anyDryHeadroomY}");
                BlockData globalDryGround = world.RWAccessor.GetBlock(sampleX, anyDryHeadroomY - 1, sampleZ);
                LogSpawnDebug($"Global dry headroom fallback picked ground={globalDryGround?.InternalName ?? "null"}");
                return new Vector3(candidate.x, anyDryHeadroomY, candidate.z);
            }

            Debug.LogWarning($"[MP] SpawnService could not validate dry headroom. Falling back to raw Y. sample=({sampleX}, {sampleZ}), fallbackY={fallbackY}");
            LogSpawnDebug("All validation paths failed. Returning raw fallback Y.");
            return new Vector3(candidate.x, fallbackY, candidate.z);
        }

        private bool TryResolveNearestGrassSpawn(Vector3 candidate, World world, out Vector3 resolved)
        {
            int sampleX = Mathf.RoundToInt(candidate.x);
            int sampleZ = Mathf.RoundToInt(candidate.z);

            if (world == null || !world.Initialized || world.RWAccessor == null)
            {
                resolved = default;
                return false;
            }

            int searchRadius = Mathf.Max(m_HorizontalLandSearchRadius, m_MaxHorizontalLandSearchRadius);
            if (TryFindNearestSafeSpawnPosition(world, sampleX, sampleZ, searchRadius, out int safeX, out int safeY, out int safeZ))
            {
                resolved = new Vector3(safeX, safeY, safeZ);
                return true;
            }

            LogSpawnDebug($"No grass spawn found up to max radius. center=({sampleX}, {sampleZ}), maxRadius={searchRadius}");
            resolved = default;
            return false;
        }

        private bool TryFindNearestSafeSpawnPosition(World world, int centerX, int centerZ, out int safeX, out int safeY, out int safeZ)
        {
            return TryFindNearestSafeSpawnPosition(world, centerX, centerZ, m_HorizontalLandSearchRadius, out safeX, out safeY, out safeZ);
        }

        private bool TryFindNearestSafeSpawnPosition(World world, int centerX, int centerZ, int radius, out int safeX, out int safeY, out int safeZ)
        {
            radius = Mathf.Max(0, radius);
            int bestDistanceSq = int.MaxValue;
            safeX = default;
            safeY = default;
            safeZ = default;

            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dz = -radius; dz <= radius; dz++)
                {
                    int distanceSq = (dx * dx) + (dz * dz);
                    if (distanceSq > bestDistanceSq)
                    {
                        continue;
                    }

                    int x = centerX + dx;
                    int z = centerZ + dz;
                    if (!TryFindTopVisibleGrassSpawnY(world, x, z, out int candidateY))
                    {
                        continue;
                    }

                    if (distanceSq < bestDistanceSq)
                    {
                        bestDistanceSq = distanceSq;
                        safeX = x;
                        safeY = candidateY;
                        safeZ = z;
                    }
                }
            }

            if (bestDistanceSq != int.MaxValue)
            {
                if (bestDistanceSq > 0)
                {
                    Debug.Log($"[MP] SpawnService moved spawn to nearest land. center=({centerX}, {centerZ}), resolved=({safeX}, {safeY}, {safeZ}), radius={radius}, distance={Mathf.Sqrt(bestDistanceSq):F2}");
                }
                else
                {
                    LogSpawnDebug($"Center column already valid grass spawn. center=({centerX}, {centerZ}), y={safeY}");
                }

                return true;
            }

            LogSpawnDebug($"Could not find grass spawn in radius. center=({centerX}, {centerZ}), radius={radius}");
            return false;
        }

        private bool TryFindTopVisibleGrassSpawnY(World world, int x, int z, out int safeY)
        {
            int topVisibleY = world.RWAccessor.GetTopVisibleBlockY(x, z, int.MinValue);
            if (topVisibleY == int.MinValue)
            {
                safeY = default;
                return false;
            }

            BlockData topBlock = world.RWAccessor.GetBlock(x, topVisibleY, z);
            if (!IsGrassBlock(topBlock))
            {
                safeY = default;
                return false;
            }

            int spawnY = topVisibleY + 1;
            if (spawnY < 1 || spawnY > ChunkHeight - 4)
            {
                safeY = default;
                return false;
            }

            if (!HasStandingRoomOnGrass(world, x, z, spawnY))
            {
                safeY = default;
                return false;
            }

            safeY = spawnY;
            return true;
        }

        private void LogSpawnDebug(string message)
        {
            if (!m_EnableVerboseSpawnDebug)
            {
                return;
            }

            Debug.Log($"[MP][SpawnDebug] {message}");
        }

        private bool TryFindHighestSafeSpawnY(World world, int x, int z, out int safeY)
        {
            for (int y = ChunkHeight - 4; y >= Mathf.Max(1, m_MinSpawnHeight); y--)
            {
                if (HasStandingRoom(world, x, z, y))
                {
                    safeY = y;
                    return true;
                }
            }

            safeY = default;
            return false;
        }

        private bool TryFindSafeSpawnY(World world, int x, int z, int minY, int maxY, out int safeY)
        {
            for (int y = Mathf.Clamp(minY, 1, ChunkHeight - 4); y <= Mathf.Clamp(maxY, 1, ChunkHeight - 4); y++)
            {
                if (HasStandingRoom(world, x, z, y))
                {
                    safeY = y;
                    return true;
                }
            }

            safeY = default;
            return false;
        }


        private bool TryFindDryHeadroomY(World world, int x, int z, int minY, int maxY, out int safeY)
        {
            for (int y = Mathf.Clamp(minY, 1, ChunkHeight - 4); y <= Mathf.Clamp(maxY, 1, ChunkHeight - 4); y++)
            {
                if (HasDryHeadroom(world, x, z, y))
                {
                    safeY = y;
                    return true;
                }
            }

            safeY = default;
            return false;
        }

        private bool HasStandingRoom(World world, int x, int z, int spawnY)
        {
            BlockData groundBlock = world.RWAccessor.GetBlock(x, spawnY - 1, z);
            if (!IsGroundBlock(groundBlock))
            {
                return false;
            }

            return IsEmptyForSpawn(world.RWAccessor.GetBlock(x, spawnY, z))
                   && IsEmptyForSpawn(world.RWAccessor.GetBlock(x, spawnY + 1, z))
                   && IsEmptyForSpawn(world.RWAccessor.GetBlock(x, spawnY + 2, z))
                   && IsExposedToSky(world, x, z, spawnY + 2);
        }

        private bool HasStandingRoomOnGrass(World world, int x, int z, int spawnY)
        {
            BlockData groundBlock = world.RWAccessor.GetBlock(x, spawnY - 1, z);
            if (!IsGrassBlock(groundBlock))
            {
                return false;
            }

            return IsEmptyForSpawn(world.RWAccessor.GetBlock(x, spawnY, z))
                   && IsEmptyForSpawn(world.RWAccessor.GetBlock(x, spawnY + 1, z))
                   && IsEmptyForSpawn(world.RWAccessor.GetBlock(x, spawnY + 2, z))
                   && IsExposedToSky(world, x, z, spawnY + 2);
        }

        private bool HasDryHeadroom(World world, int x, int z, int spawnY)
        {
            return IsEmptyForSpawn(world.RWAccessor.GetBlock(x, spawnY, z))
                   && IsEmptyForSpawn(world.RWAccessor.GetBlock(x, spawnY + 1, z))
                   && IsEmptyForSpawn(world.RWAccessor.GetBlock(x, spawnY + 2, z));
        }

        private static bool IsGroundBlock(BlockData block)
        {
            return block != null
                   && block.PhysicState == PhysicState.Solid
                   && !block.Flags.HasFlag(BlockFlags.IgnoreCollisions)
                   && !block.Flags.HasFlag(BlockFlags.AlwaysInvisible);
        }

        private static bool IsGrassBlock(BlockData block)
        {
            return IsGroundBlock(block)
                   && string.Equals(block.InternalName, "grass", System.StringComparison.OrdinalIgnoreCase);
        }

        private bool IsExposedToSky(World world, int x, int z, int startY)
        {
            for (int y = Mathf.Clamp(startY, 1, ChunkHeight - 1); y < ChunkHeight; y++)
            {
                if (!IsSkyPassable(world.RWAccessor.GetBlock(x, y, z)))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsSkyPassable(BlockData block)
        {
            return block == null
                   || (block.Flags.HasFlag(BlockFlags.IgnoreCollisions) && block.PhysicState != PhysicState.Fluid)
                   || block.Flags.HasFlag(BlockFlags.AlwaysInvisible) && block.PhysicState != PhysicState.Fluid;
        }

        private static bool IsEmptyForSpawn(BlockData block)
        {
            return block == null
                   || (block.Flags.HasFlag(BlockFlags.IgnoreCollisions) && block.PhysicState != PhysicState.Fluid)
                   || block.Flags.HasFlag(BlockFlags.AlwaysInvisible) && block.PhysicState != PhysicState.Fluid;
        }
    }
}
