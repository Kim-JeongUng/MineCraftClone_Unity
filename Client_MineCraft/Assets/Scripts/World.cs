using System;
using System.Collections;
using System.Diagnostics;
using Minecraft.Assets;
using Minecraft.Audio;
using Minecraft.Configurations;
using Minecraft.Entities;
using Minecraft.Lua;
using Minecraft.Multiplayer;
using Minecraft.Rendering;
using Minecraft.ScriptableWorldGeneration;
using UnityEngine;
using UnityEngine.Events;

namespace Minecraft
{
    [DisallowMultipleComponent]
    public abstract class World : MonoBehaviour, IWorld
    {
        public readonly struct BlockChangedInfo
        {
            public BlockChangedInfo(int x, int y, int z, BlockData block, Quaternion rotation, ModificationSource source)
            {
                X = x;
                Y = y;
                Z = z;
                Block = block;
                Rotation = rotation;
                Source = source;
            }

            public int X { get; }

            public int Y { get; }

            public int Z { get; }

            public BlockData Block { get; }

            public Quaternion Rotation { get; }

            public ModificationSource Source { get; }
        }

        public static IWorld Active { get; private set; }
        public static WorldSetting ActiveSetting { get; set; }


        [Header("Asset Names")]
        [SerializeField] private string m_BlockTableAssetName;
        [SerializeField] private string m_ItemTableAssetName;
        [SerializeField] private string m_BiomeTableAssetName;
        [SerializeField] private string m_WorldGenPipelineAssetName;

        [Space]

        [Header("Local Player References")]
        [SerializeField] private Transform m_Player;
        [SerializeField] private Camera m_MainCamera;

        [Space]

        [Header("Manager References")]
        [SerializeField] private AudioManager m_AudioManager;
        [SerializeField] private LuaManager m_LuaManager;
        [SerializeField] private ChunkManager m_ChunkManager;
        [SerializeField] private SectionRenderingManager m_RenderingManager;
        [SerializeField] private EntityManager m_EntityManager;

        [Header("Others")]
        [SerializeField] private int m_Seed = 0;
        [SerializeField] private int m_MaxTickBlockCountPerFrame = 500;
        [SerializeField] private int m_MaxLightBlockCountPerFrame = 500;

        [NonSerialized] private IWorldRWAccessor m_RWAccessor;
        [NonSerialized] private BlockTable m_BlockTable;
        [NonSerialized] private BiomeTable m_BiomeTable;
        [NonSerialized] private WorldGeneratePipeline m_WorldGenPipeline;
        [NonSerialized] private bool m_Initialized;


        public bool Initialized => m_Initialized;

        public virtual IWorldRWAccessor RWAccessor => m_RWAccessor;

        public Transform PlayerTransform => m_Player;

        public Vector3 DefaultSpawnPosition => m_Player != null ? m_Player.position : transform.position;

        public Quaternion DefaultSpawnRotation => m_Player != null ? m_Player.rotation : transform.rotation;

        public int ConfiguredSeed => m_Seed;

        public Camera MainCamera => m_MainCamera;

        public AudioManager AudioManager => m_AudioManager;

        public LuaManager LuaManager => m_LuaManager;

        public ChunkManager ChunkManager => m_ChunkManager;

        public SectionRenderingManager RenderingManager => m_RenderingManager;

        public BlockTable BlockDataTable => m_BlockTable;

        public BiomeTable BiomeDataTable => m_BiomeTable;

        public WorldGeneratePipeline WorldGenPipeline => m_WorldGenPipeline;

        public EntityManager EntityManager => m_EntityManager;

        public event Action<Transform, Camera> LocalPlayerReferencesChanged;
        public event Action<BlockChangedInfo> BlockChanged;

        public void OverrideLocalPlayerReferences(Transform player, Camera mainCamera)
        {
            bool changed = false;

            if (player != null && m_Player != player)
            {
                m_Player = player;
                changed = true;
            }

            if (mainCamera != null && m_MainCamera != mainCamera)
            {
                m_MainCamera = mainCamera;
                m_EntityManager?.SetMainCamera(mainCamera);
                changed = true;
            }

            if (changed)
            {
                LocalPlayerReferencesChanged?.Invoke(m_Player, m_MainCamera);
            }
        }

        public int MaxTickBlockCountPerFrame
        {
            get => m_MaxTickBlockCountPerFrame;
            set => m_MaxTickBlockCountPerFrame = value;
        }
        public int MaxLightBlockCountPerFrame
        {
            get => m_MaxLightBlockCountPerFrame;
            set => m_MaxLightBlockCountPerFrame = value;
        }



        private IEnumerator Start()
        {
            if (Active != null)
            {
                throw new InvalidOperationException("There is already a world!");
            }

            while (GameModeContext.IsClient && !GameModeContext.IsServer && !GameModeContext.HasAuthoritativeWorldSettings)
            {
                yield return null;
            }

            int resolvedSeed = ResolveInitialSeed();

            if (ActiveSetting == null)
            {
                ActiveSetting = new WorldSetting
                {
                    Seed = resolvedSeed
                };
            }
            else if (ActiveSetting.Seed == 0)
            {
                ActiveSetting.Seed = resolvedSeed;
            }

            if (ActiveSetting == null)
            {
                throw new InvalidOperationException("Cannot create a world without World.ActiveSetting!");
            }

            AsyncAsset blockTable = AssetManager.Instance.LoadAsset<BlockTable>(m_BlockTableAssetName);
            AsyncAsset biomeTable = AssetManager.Instance.LoadAsset<BiomeTable>(m_BiomeTableAssetName);
            AsyncAsset worldGenPipeline = AssetManager.Instance.LoadAsset<WorldGeneratePipeline>(m_WorldGenPipelineAssetName);

            yield return blockTable;
            m_BlockTable = blockTable.GetAssetAs<BlockTable>();
            yield return BlockDataTable.Initialize();

            yield return biomeTable;
            m_BiomeTable = biomeTable.GetAssetAs<BiomeTable>();
            yield return BiomeDataTable.Initialize();

            yield return worldGenPipeline;
            m_WorldGenPipeline = worldGenPipeline.GetAssetAs<WorldGeneratePipeline>();
            WorldGenPipeline.Initialize(this, ActiveSetting.Seed);

            yield return LuaManager.Initialize();
            ChunkManager.Initialize(this);
            RenderingManager.Initialize(this);
            m_EntityManager.Initialize();
            m_EntityManager.SetMainCamera(m_MainCamera);

            yield return OnInitialize();

            m_RWAccessor = new DefaultWorldRWAccessor(this);
            m_Initialized = true;
            Active = this;

            LuaManager.ExecuteLuaScripts();
            BlockDataTable.LoadBlockBehavioursInLua(this);
        }

        private void OnDestroy()
        {
            Active = null;
            ActiveSetting = null;
            m_Initialized = false;
            BlockDataTable.Dispose();

            // AssetManager.Instance.UnloadAsset(m_BlockTableAssetName);
            // AssetManager.Instance.UnloadAsset(m_BiomeTableAssetName);
            // AssetManager.Instance.UnloadAsset(m_WorldGenPipelineAssetName);
            AssetManager.Instance.UnloadAll();

            OnDispose();
        }

        private void Update()
        {
            if (!m_Initialized)
            {
                return;
            }

            OnUpdate();
        }

        public void MarkBlockMeshDirty(int x, int y, int z, ModificationSource source)
        {
            RenderingManager.MarkBlockMeshDirty(x, y, z, source);
        }


        protected virtual IEnumerator OnInitialize()
        {
            yield return null;
        }

        private int ResolveInitialSeed()
        {
            if (GameModeContext.IsMultiplayer && GameModeContext.HasAuthoritativeWorldSettings && GameModeContext.AuthoritativeWorldSeed != 0)
            {
                return GameModeContext.AuthoritativeWorldSeed;
            }

            return m_Seed == 0 ? (Process.GetCurrentProcess().Id + DateTime.Now.GetHashCode()) : m_Seed;
        }

        protected virtual void OnUpdate() { }

        protected virtual void OnDispose() { }

        public abstract void LightBlock(int x, int y, int z, ModificationSource source);

        public abstract void TickBlock(int x, int y, int z);

        internal void NotifyBlockChanged(int x, int y, int z, BlockData block, Quaternion rotation, ModificationSource source)
        {
            BlockChanged?.Invoke(new BlockChangedInfo(x, y, z, block, rotation, source));
        }


        [Serializable] private class OnInitializedEvent : UnityEvent<IWorld> { }


        private class DefaultWorldRWAccessor : IWorldRWAccessor
        {
            private readonly World m_World;

            public bool Accessible => true;

            public Vector3Int WorldSpaceOrigin => Vector3Int.zero;

            public IWorld World { get; }

            public DefaultWorldRWAccessor(World world)
            {
                m_World = world;
                World = world;
            }

            public int GetAmbientLight(int x, int y, int z, int defaultValue = 0)
            {
                ChunkPos pos = ChunkPos.GetFromAny(x, z);

                if (World.ChunkManager.GetChunk(pos, false, out Chunk chunk))
                {
                    this.AccessorSpaceToAccessorSpacePosition(chunk, ref x, ref y, ref z);
                    return chunk.GetAmbientLight(x, y, z, defaultValue);
                }

                return defaultValue;
            }

            public BlockData GetBlock(int x, int y, int z, BlockData defaultValue = null)
            {
                ChunkPos pos = ChunkPos.GetFromAny(x, z);

                if (World.ChunkManager.GetChunk(pos, false, out Chunk chunk))
                {
                    this.AccessorSpaceToAccessorSpacePosition(chunk, ref x, ref y, ref z);
                    return chunk.GetBlock(x, y, z, defaultValue);
                }

                return defaultValue;
            }

            public Quaternion GetBlockRotation(int x, int y, int z, Quaternion defaultValue = default)
            {
                ChunkPos pos = ChunkPos.GetFromAny(x, z);

                if (World.ChunkManager.GetChunk(pos, false, out Chunk chunk))
                {
                    this.AccessorSpaceToAccessorSpacePosition(chunk, ref x, ref y, ref z);
                    return chunk.GetBlockRotation(x, y, z, defaultValue);
                }

                return defaultValue;
            }

            public int GetMixedLightLevel(int x, int y, int z, int defaultValue = 0)
            {
                ChunkPos pos = ChunkPos.GetFromAny(x, z);

                if (World.ChunkManager.GetChunk(pos, false, out Chunk chunk))
                {
                    this.AccessorSpaceToAccessorSpacePosition(chunk, ref x, ref y, ref z);
                    return chunk.GetMixedLightLevel(x, y, z, defaultValue);
                }

                return defaultValue;
            }

            public int GetSkyLight(int x, int y, int z, int defaultValue = 0)
            {
                ChunkPos pos = ChunkPos.GetFromAny(x, z);

                if (World.ChunkManager.GetChunk(pos, false, out Chunk chunk))
                {
                    this.AccessorSpaceToAccessorSpacePosition(chunk, ref x, ref y, ref z);
                    return chunk.GetSkyLight(x, y, z, defaultValue);
                }

                return defaultValue;
            }

            public int GetTopVisibleBlockY(int x, int z, int defaultValue = 0)
            {
                ChunkPos pos = ChunkPos.GetFromAny(x, z);

                if (World.ChunkManager.GetChunk(pos, false, out Chunk chunk))
                {
                    int y = 0;
                    this.AccessorSpaceToAccessorSpacePosition(chunk, ref x, ref y, ref z);
                    return chunk.GetTopVisibleBlockY(x, z, defaultValue);
                }

                return defaultValue;
            }

            public bool SetAmbientLightLevel(int x, int y, int z, int value, ModificationSource source)
            {
                ChunkPos pos = ChunkPos.GetFromAny(x, z);

                if (World.ChunkManager.GetChunk(pos, false, out Chunk chunk))
                {
                    this.AccessorSpaceToAccessorSpacePosition(chunk, ref x, ref y, ref z);
                    return chunk.SetAmbientLightLevel(x, y, z, value, source);
                }

                return false;
            }

            public bool SetBlock(int x, int y, int z, BlockData value, Quaternion rotation, ModificationSource source)
            {
                int worldX = x;
                int worldY = y;
                int worldZ = z;
                ChunkPos pos = ChunkPos.GetFromAny(x, z);

                if (World.ChunkManager.GetChunk(pos, false, out Chunk chunk))
                {
                    this.AccessorSpaceToAccessorSpacePosition(chunk, ref x, ref y, ref z);
                    bool changed = chunk.SetBlock(x, y, z, value, rotation, source);

                    if (changed)
                    {
                        m_World.NotifyBlockChanged(worldX, worldY, worldZ, value, rotation, source);
                    }

                    return changed;
                }

                return false;
            }
        }
    }
}
