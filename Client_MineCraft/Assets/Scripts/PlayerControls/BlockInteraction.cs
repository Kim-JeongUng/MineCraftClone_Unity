using System;
using System.Collections.Generic;
using System.IO;
using Minecraft.Configurations;
using Minecraft.Entities;
using Minecraft.Lua;
using Minecraft.Multiplayer;
using Minecraft.PhysicSystem;
using Minecraft.Rendering;
using UnityEngine;
using UnityEngine.UI;
using Physics = Minecraft.PhysicSystem.Physics;

namespace Minecraft.PlayerControls
{
    [DisallowMultipleComponent]
    public class BlockInteraction : MonoBehaviour, ILuaCallCSharp
    {
        [Serializable]
        private class InventoryButtonBinding
        {
            public string BlockName;
            public Button Button;
            public Image Icon;
        }

        [Range(3, 12)] public float RaycastMaxDistance = 8;
        [Min(0.1f)] public float MaxClickSpacing = 0.4f;

        [Header("Hand Block UI")]
        [SerializeField] private Text m_CurrentHandBlockText;
        [SerializeField] private InputField m_HandBlockInput;
        [SerializeField] private MonoBehaviour[] m_DisableWhenEditHandBlock;

        [Header("Hotbar UI")]
        [SerializeField] private Transform m_HotbarRoot;
        [SerializeField] private Image[] m_HotbarSlotImages;
        [SerializeField] private Image[] m_HotbarIconImages;
        [SerializeField] private string[] m_HotbarBlockNames =
        {
            "dirt",
            "grass",
            "stone",
            "sand",
            "gravel",
            "log_oak",
            "leaves_oak",
            "tnt",
            "water",
            "lava"
        };

        [Header("Block Inventory UI")]
        [SerializeField] private GameObject m_BlockInventoryRoot;
        [SerializeField] private Transform m_BlockInventoryGridRoot;
        [SerializeField] private InventoryButtonBinding[] m_BlockInventoryButtons;
        [SerializeField] private KeyCode m_BlockInventoryToggleKey = KeyCode.I;

        [Header("Selection Visuals")]
        [SerializeField] [Range(0, 9)] private int m_SelectedHotbarIndex;
        [SerializeField] private bool m_InvertMouseWheelHotbar = false;
        [SerializeField] private Color m_SelectedSlotColor = new Color(1f, 1f, 1f, 0.95f);
        [SerializeField] private Color m_UnselectedSlotColor = new Color(1f, 1f, 1f, 0.2f);
        [SerializeField] private Color m_SelectedInventoryButtonColor = new Color(1f, 0.95f, 0.55f, 0.28f);
        [SerializeField] private Color m_UnselectedInventoryButtonColor = new Color(1f, 1f, 1f, 0.14f);

        [NonSerialized] private Camera m_Camera;
        [NonSerialized] private IAABBEntity m_PlayerEntity;
        [NonSerialized] private PlayerEntity m_PlayerAnimationEntity;
        [NonSerialized] private Func<BlockData, bool> m_DestroyRaycastSelector;
        [NonSerialized] private Func<BlockData, bool> m_PlaceRaycastSelector;
        [NonSerialized] private NetworkPlayerAdapter m_NetworkPlayerAdapter;

        [NonSerialized] private bool m_IsDigging;
        [NonSerialized] private float m_DiggingDamage;
        [NonSerialized] private Vector3Int m_FirstDigPos;
        [NonSerialized] private Vector3Int m_ClickedPos;
        [NonSerialized] private float m_ClickTime;
        [NonSerialized] private GameObject m_HandBlockInputGO;
        [NonSerialized] private bool m_IsBlockInventoryOpen;
        [NonSerialized] private bool m_InventoryButtonsBound;

        private readonly Dictionary<string, Sprite> m_BlockSpriteCache = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
        private const string BlockSpriteResourcesPath = "Block Sprites";

        private static readonly Dictionary<string, string[]> s_BlockSpriteCandidates = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            { "grass", new[] { "grass_top.png", "grass_side.png" } },
            { "water", new[] { "water_still.png" } },
            { "lava", new[] { "lava_still.png" } },
            { "leaves_oak", new[] { "leaves_oak.tga", "leaves_oak_carried.tga" } },
            { "quartz_block_half", new[] { "quartz_block_top.png", "quartz_block_side.png" } },
            { "glass_block", new[] { "glass.tga" } },
            { "torch", new[] { "torch_on.tga" } },
            { "tnt", new[] { "tnt_side.png" } },
            { "wall_torch", new[] { "torch_on.tga" } },
            { "cactus_DO_NOT_USE", new[] { "cactus_side.tga", "cactus_top.tga" } }
        };

        private static readonly KeyCode[] s_HotbarKeyCodes =
        {
            KeyCode.Alpha1,
            KeyCode.Alpha2,
            KeyCode.Alpha3,
            KeyCode.Alpha4,
            KeyCode.Alpha5,
            KeyCode.Alpha6,
            KeyCode.Alpha7,
            KeyCode.Alpha8,
            KeyCode.Alpha9,
            KeyCode.Alpha0
        };

        public void AssignHandBlockUI(Text currentHandBlockText, InputField handBlockInput, Transform hotbarRoot = null, GameObject blockInventoryRoot = null, Transform blockInventoryGridRoot = null)
        {
            m_CurrentHandBlockText = currentHandBlockText;
            m_HandBlockInput = handBlockInput;
            if (hotbarRoot != null)
            {
                m_HotbarRoot = hotbarRoot;
            }

            if (blockInventoryRoot != null)
            {
                m_BlockInventoryRoot = blockInventoryRoot;
            }

            if (blockInventoryGridRoot != null)
            {
                m_BlockInventoryGridRoot = blockInventoryGridRoot;
            }

            InitializeHandBlockInput();
            InitializeHotbarUI();
            InitializeInventoryUI();
            ApplySelectedHotbar(true);
        }

        public void Initialize(Camera camera, IAABBEntity playerEntity)
        {
            m_Camera = camera;
            m_PlayerEntity = playerEntity;
            m_PlayerAnimationEntity = playerEntity as PlayerEntity;
            m_DestroyRaycastSelector = DestroyRaycastSelect;
            m_PlaceRaycastSelector = PlaceRaycastSelect;
        }

        private void OnEnable()
        {
            m_NetworkPlayerAdapter = GetComponentInParent<NetworkPlayerAdapter>();
            m_IsDigging = false;
            m_DiggingDamage = 0;
            m_FirstDigPos = Vector3Int.down;
            m_ClickedPos = Vector3Int.down;
            m_ClickTime = 0;
            SetDigProgress(0);

            InitializeHandBlockInput();
            InitializeHotbarUI();
            InitializeInventoryUI();
            ApplySelectedHotbar(true);
        }

        private void OnDisable()
        {
            StopDigAnimationLoop();
            SetDigProgress(0);
            ShaderUtility.TargetedBlockPosition = Vector3.down;
            SetBlockInventoryOpen(false);
        }

        private void Update()
        {
            if (ChangeHandBlock())
            {
                return;
            }

            if (m_IsBlockInventoryOpen || !m_Camera || m_PlayerEntity == null)
            {
                return;
            }

            IWorld world = m_PlayerEntity.World;
            if (world?.RWAccessor == null || world.BlockDataTable == null || world.RenderingManager == null)
            {
                ShaderUtility.TargetedBlockPosition = Vector3.down;
                StopDigAnimationLoop();
                SetDigProgress(0);
                return;
            }

            Ray ray = GetRay();
            DigBlock(ray, world);
            PlaceBlock(ray, world);
        }

        private void InitializeHandBlockInput()
        {
            m_HandBlockInputGO = m_HandBlockInput != null ? m_HandBlockInput.gameObject : null;
            if (m_HandBlockInputGO != null)
            {
                m_HandBlockInputGO.SetActive(false);
            }

            if (m_CurrentHandBlockText != null)
            {
                m_CurrentHandBlockText.text = string.Empty;
                m_CurrentHandBlockText.gameObject.SetActive(false);
            }
        }

        private bool ChangeHandBlock()
        {
            if (Input.GetKeyDown(m_BlockInventoryToggleKey))
            {
                SetBlockInventoryOpen(!m_IsBlockInventoryOpen);
                return m_IsBlockInventoryOpen;
            }

            if (m_IsBlockInventoryOpen)
            {
                return true;
            }
            EnsureHotbarBlockNameSize();

            float mouseWheelInput = Input.mouseScrollDelta.y;
            if (!Mathf.Approximately(mouseWheelInput, 0f))
            {
                int direction = mouseWheelInput > 0f ? -1 : 1;
                if (m_InvertMouseWheelHotbar)
                {
                    direction = -direction;
                }

                int nextIndex = (m_SelectedHotbarIndex + direction + m_HotbarBlockNames.Length) % m_HotbarBlockNames.Length;
                SetSelectedHotbarIndex(nextIndex);
                return false;
            }

            for (int i = 0; i < s_HotbarKeyCodes.Length; i++)
            {
                if (!Input.GetKeyDown(s_HotbarKeyCodes[i]))
                {
                    continue;
                }

                SetSelectedHotbarIndex(i);
                return false;
            }

            return false;
        }

        private void InitializeHotbarUI()
        {
            EnsureHotbarBlockNameSize();
            EnsureHotbarSlotReferences();
            EnsureHotbarIconReferences();
            RefreshHotbarIcons();
        }

        private void EnsureHotbarBlockNameSize()
        {
            if (m_HotbarBlockNames != null && m_HotbarBlockNames.Length == 10)
            {
                return;
            }

            string[] resized = new string[10];
            for (int i = 0; i < resized.Length; i++)
            {
                resized[i] = m_HotbarBlockNames != null && i < m_HotbarBlockNames.Length ? m_HotbarBlockNames[i] : string.Empty;
            }

            m_HotbarBlockNames = resized;
        }

        private void EnsureHotbarSlotReferences()
        {
            if (m_HotbarSlotImages == null || m_HotbarSlotImages.Length != 10)
            {
                Array.Resize(ref m_HotbarSlotImages, 10);
            }

            if (m_HotbarRoot == null)
            {
                return;
            }

            for (int i = 0; i < 10 && i < m_HotbarRoot.childCount; i++)
            {
                if (m_HotbarSlotImages[i] == null)
                {
                    m_HotbarSlotImages[i] = m_HotbarRoot.GetChild(i).GetComponent<Image>();
                }
            }
        }

        private void EnsureHotbarIconReferences()
        {
            if (m_HotbarIconImages == null || m_HotbarIconImages.Length != 10)
            {
                Array.Resize(ref m_HotbarIconImages, 10);
            }

            for (int i = 0; i < m_HotbarIconImages.Length; i++)
            {
                if (m_HotbarIconImages[i] != null)
                {
                    continue;
                }

                Transform slot = m_HotbarRoot != null && i < m_HotbarRoot.childCount ? m_HotbarRoot.GetChild(i) : null;
                if (slot == null)
                {
                    continue;
                }

                Transform iconTransform = slot.childCount > 0 ? slot.GetChild(0) : null;
                if (iconTransform != null)
                {
                    m_HotbarIconImages[i] = iconTransform.GetComponent<Image>();
                }

                if (m_HotbarIconImages[i] != null)
                {
                    continue;
                }

                GameObject iconGO = new GameObject("Block Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                iconGO.transform.SetParent(slot, false);
                RectTransform iconRect = iconGO.GetComponent<RectTransform>();
                iconRect.anchorMin = Vector2.zero;
                iconRect.anchorMax = Vector2.one;
                iconRect.offsetMin = new Vector2(8f, 8f);
                iconRect.offsetMax = new Vector2(-8f, -8f);

                Image iconImage = iconGO.GetComponent<Image>();
                iconImage.raycastTarget = false;
                iconImage.preserveAspect = true;
                m_HotbarIconImages[i] = iconImage;
            }
        }

        private void RefreshHotbarIcons()
        {
            if (m_HotbarIconImages == null)
            {
                return;
            }

            for (int i = 0; i < m_HotbarIconImages.Length && i < m_HotbarBlockNames.Length; i++)
            {
                if (m_HotbarIconImages[i] == null)
                {
                    continue;
                }

                string blockName = m_HotbarBlockNames[i];
                m_HotbarIconImages[i].sprite = GetBlockSprite(blockName);
                m_HotbarIconImages[i].enabled = m_HotbarIconImages[i].sprite != null;
                m_HotbarIconImages[i].color = Color.white;
            }
        }

        private void InitializeInventoryUI()
        {
            if (m_BlockInventoryRoot != null)
            {
                m_BlockInventoryRoot.SetActive(false);
            }

            if (m_InventoryButtonsBound && m_BlockInventoryButtons != null && m_BlockInventoryButtons.Length > 0)
            {
                RefreshInventoryIcons();
                return;
            }

            m_InventoryButtonsBound = false;

            if ((m_BlockInventoryButtons == null || m_BlockInventoryButtons.Length == 0) && m_BlockInventoryGridRoot != null)
            {
                List<InventoryButtonBinding> bindings = new List<InventoryButtonBinding>();
                for (int i = 0; i < m_BlockInventoryGridRoot.childCount; i++)
                {
                    Transform child = m_BlockInventoryGridRoot.GetChild(i);
                    Button button = child.GetComponent<Button>();
                    if (button == null)
                    {
                        continue;
                    }

                    Image icon = null;
                    if (child.childCount > 0)
                    {
                        Transform firstChild = child.GetChild(0);
                        if (firstChild != null && string.Equals(firstChild.name, "Icon", StringComparison.OrdinalIgnoreCase))
                        {
                            icon = firstChild.GetComponent<Image>();
                        }
                    }

                    if (icon == null)
                    {
                        Transform iconTransform = child.Find("Icon");
                        if (iconTransform != null)
                        {
                            icon = iconTransform.GetComponent<Image>();
                        }
                    }

                    bindings.Add(new InventoryButtonBinding
                    {
                        BlockName = child.name,
                        Button = button,
                        Icon = icon
                    });
                }

                m_BlockInventoryButtons = bindings.ToArray();
            }

            if (m_BlockInventoryButtons == null)
            {
                return;
            }

            for (int i = 0; i < m_BlockInventoryButtons.Length; i++)
            {
                InventoryButtonBinding binding = m_BlockInventoryButtons[i];
                if (binding?.Button == null || string.IsNullOrWhiteSpace(binding.BlockName))
                {
                    continue;
                }

                string capturedBlockName = binding.BlockName;
                binding.Button.onClick.RemoveAllListeners();
                binding.Button.onClick.AddListener(() => AssignBlockToSelectedSlot(capturedBlockName));
            }

            m_InventoryButtonsBound = true;
            RefreshInventoryIcons();
        }

        private void RefreshInventoryIcons()
        {
            if (m_BlockInventoryButtons == null)
            {
                return;
            }

            for (int i = 0; i < m_BlockInventoryButtons.Length; i++)
            {
                InventoryButtonBinding binding = m_BlockInventoryButtons[i];
                if (binding == null)
                {
                    continue;
                }

                if (binding.Icon != null)
                {
                    binding.Icon.sprite = GetBlockSprite(binding.BlockName);
                    binding.Icon.enabled = binding.Icon.sprite != null;
                    binding.Icon.color = Color.white;
                }

                Image background = binding.Button != null ? binding.Button.GetComponent<Image>() : null;
                if (background != null)
                {
                    bool isSelected = string.Equals(binding.BlockName, GetSelectedBlockName(), StringComparison.OrdinalIgnoreCase);
                    background.color = isSelected ? m_SelectedInventoryButtonColor : m_UnselectedInventoryButtonColor;
                }
            }
        }

        private void AssignBlockToSelectedSlot(string blockName)
        {
            EnsureHotbarBlockNameSize();
            m_HotbarBlockNames[m_SelectedHotbarIndex] = blockName;
            ApplySelectedHotbar(true);
            SetBlockInventoryOpen(false);
        }

        private void SetBlockInventoryOpen(bool open)
        {
            if (m_BlockInventoryRoot == null)
            {
                m_IsBlockInventoryOpen = false;
                SetGameplayInputEnabled(true);
                return;
            }

            m_IsBlockInventoryOpen = open;
            m_BlockInventoryRoot.SetActive(open);
            RefreshInventoryIcons();
            SetGameplayInputEnabled(!open);
            Cursor.visible = open;
            Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
        }

        private void SetGameplayInputEnabled(bool enabled)
        {
            if (m_DisableWhenEditHandBlock == null)
            {
                return;
            }

            for (int i = 0; i < m_DisableWhenEditHandBlock.Length; i++)
            {
                if (m_DisableWhenEditHandBlock[i] != null)
                {
                    m_DisableWhenEditHandBlock[i].enabled = enabled;
                }
            }
        }

        private void SetSelectedHotbarIndex(int index)
        {
            m_SelectedHotbarIndex = Mathf.Clamp(index, 0, 9);
            ApplySelectedHotbar(false);
        }

        private void ApplySelectedHotbar(bool forceRefreshVisuals)
        {
            EnsureHotbarBlockNameSize();

            if (forceRefreshVisuals)
            {
                RefreshHotbarIcons();
            }

            if (m_HotbarSlotImages != null)
            {
                for (int i = 0; i < m_HotbarSlotImages.Length; i++)
                {
                    if (m_HotbarSlotImages[i] == null)
                    {
                        continue;
                    }

                    bool isSelected = i == m_SelectedHotbarIndex;
                    m_HotbarSlotImages[i].color = isSelected ? m_SelectedSlotColor : m_UnselectedSlotColor;
                }
            }

            RefreshInventoryIcons();
        }

        private string GetSelectedBlockName()
        {
            EnsureHotbarBlockNameSize();
            return m_HotbarBlockNames[m_SelectedHotbarIndex] ?? string.Empty;
        }

        private Sprite GetBlockSprite(string blockName)
        {
            if (string.IsNullOrWhiteSpace(blockName))
            {
                return null;
            }

            if (m_BlockSpriteCache.TryGetValue(blockName, out Sprite cachedSprite))
            {
                return cachedSprite;
            }

            foreach (string fileName in GetSpriteCandidateFileNames(blockName))
            {
                Sprite sprite = LoadSpriteFromAssetFolder(fileName);
                if (sprite != null)
                {
                    m_BlockSpriteCache[blockName] = sprite;
                    return sprite;
                }
            }

            m_BlockSpriteCache[blockName] = null;
            return null;
        }

        private static IEnumerable<string> GetSpriteCandidateFileNames(string blockName)
        {
            if (s_BlockSpriteCandidates.TryGetValue(blockName, out string[] specificCandidates))
            {
                foreach (string candidate in specificCandidates)
                {
                    yield return candidate;
                }
            }

            yield return $"{blockName}.png";
            yield return $"{blockName}.tga";
        }

        private static Sprite LoadSpriteFromAssetFolder(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return null;
            }

            string resourceName = Path.GetFileNameWithoutExtension(fileName);
            if (string.IsNullOrWhiteSpace(resourceName))
            {
                return null;
            }

            return Resources.Load<Sprite>($"{BlockSpriteResourcesPath}/{resourceName}");
        }

        private void DigBlock(Ray ray, IWorld world)
        {
            if (Physics.RaycastBlock(ray, RaycastMaxDistance, world, m_DestroyRaycastSelector, out BlockRaycastHit hit))
            {
                ShaderUtility.TargetedBlockPosition = hit.Position;

                if (Input.GetMouseButton(0))
                {
                    if (m_IsDigging)
                    {
                        if (hit.Position == m_FirstDigPos)
                        {
                            m_DiggingDamage += Time.deltaTime * 5;
                            SetDigProgress(m_DiggingDamage / hit.Block.Hardness);

                            if (m_DiggingDamage >= hit.Block.Hardness)
                            {
                                SetDigProgress(0);
                                m_IsDigging = false;
                                StopDigAnimationLoop();
                                BlockData airBlock = world.BlockDataTable.GetBlock(0);
                                if (GameModeContext.IsMultiplayer)
                                {
                                    if (m_NetworkPlayerAdapter == null || !m_NetworkPlayerAdapter.RequestSetBlock(hit.Position, airBlock, Quaternion.identity))
                                    {
                                        Debug.LogWarning($"[MP] Failed to send block removal request for {hit.Position}.");
                                    }
                                }
                                else
                                {
                                    world.RWAccessor.SetBlock(hit.Position.x, hit.Position.y, hit.Position.z, airBlock, Quaternion.identity, ModificationSource.PlayerAction);
                                }
                            }
                        }
                        else
                        {
                            SetDigProgress(0);
                            m_IsDigging = false;
                            StopDigAnimationLoop();
                        }
                    }
                    else
                    {
                        m_IsDigging = true;
                        m_DiggingDamage = 0;
                        m_FirstDigPos = hit.Position;
                        StartDigAnimationLoop();
                    }
                }
                else
                {
                    StopDigAnimationLoop();
                }

                if (Input.GetMouseButtonDown(0))
                {
                    m_ClickedPos = hit.Position;
                    m_ClickTime = Time.time;
                }

                if (Input.GetMouseButtonUp(0))
                {
                    SetDigProgress(0);
                    m_IsDigging = false;
                    StopDigAnimationLoop();

                    BlockData block = world.RWAccessor.GetBlock(hit.Position.x, hit.Position.y, hit.Position.z);
                    if ((hit.Position == m_ClickedPos) && (Time.time - m_ClickTime <= MaxClickSpacing))
                    {
                        if (GameModeContext.IsMultiplayer)
                        {
                            Debug.Log($"[TNT TRACE] client request click {hit.Position} block={block?.InternalName}");
                            if (m_NetworkPlayerAdapter == null || !m_NetworkPlayerAdapter.RequestClickBlock(hit.Position))
                            {
                                Debug.LogWarning($"[MP] Failed to send block click request for {hit.Position}.");
                            }
                        }
                        else
                        {
                            block.Click(world, hit.Position.x, hit.Position.y, hit.Position.z);
                        }
                    }

                    m_ClickedPos = Vector3Int.down;
                    m_ClickTime = 0;
                }
            }
            else
            {
                StopDigAnimationLoop();
                ShaderUtility.TargetedBlockPosition = Vector3.down;
            }
        }

        private void PlaceBlock(Ray ray, IWorld world)
        {
            if (Input.GetMouseButtonDown(1))
            {
                string currentHandBlockName = GetSelectedBlockName();
                BlockData block = world.BlockDataTable.GetBlock(currentHandBlockName);
                if (block == null)
                {
                    return;
                }

                if (Physics.RaycastBlock(ray, RaycastMaxDistance, world, m_PlaceRaycastSelector, out BlockRaycastHit hit))
                {
                    Vector3Int pos = (hit.Position + hit.Normal).FloorToInt();
                    AABB playerBB = m_PlayerEntity.BoundingBox + m_PlayerEntity.Position;
                    AABB blockBB = hit.Block.GetBoundingBox(pos, world, false).Value;

                    if (!playerBB.Intersects(blockBB))
                    {
                        Quaternion rotation = Quaternion.identity;

                        if ((block.RotationAxes & BlockRotationAxes.AroundXOrZAxis) == BlockRotationAxes.AroundXOrZAxis)
                        {
                            rotation *= Quaternion.FromToRotation(Vector3.up, hit.Normal);
                        }

                        if ((block.RotationAxes & BlockRotationAxes.AroundYAxis) == BlockRotationAxes.AroundYAxis)
                        {
                            Vector3 forward = m_PlayerEntity.Forward;
                            forward = Mathf.Abs(forward.x) > Mathf.Abs(forward.z)
                                ? new Vector3(forward.x, 0, 0)
                                : new Vector3(0, 0, forward.z);
                            rotation *= Quaternion.LookRotation(-forward.normalized, Vector3.up);
                        }

                        if (GameModeContext.IsMultiplayer)
                        {
                            if (m_NetworkPlayerAdapter == null || !m_NetworkPlayerAdapter.RequestSetBlock(pos, block, rotation))
                            {
                                Debug.LogWarning($"[MP] Failed to send block placement request for {pos} ({block.InternalName}).");
                            }
                            else
                            {
                                PlayDigAnimationOnce();
                            }
                        }
                        else
                        {
                            world.RWAccessor.SetBlock(pos.x, pos.y, pos.z, block, rotation, ModificationSource.PlayerAction);
                            PlayDigAnimationOnce();
                        }
                    }
                }
            }
        }

        private void StartDigAnimationLoop()
        {
            if (m_PlayerAnimationEntity != null)
            {
                m_PlayerAnimationEntity.StartDigAnimationLoop();
            }
        }

        private void StopDigAnimationLoop()
        {
            if (m_PlayerAnimationEntity != null)
            {
                m_PlayerAnimationEntity.StopDigAnimationLoop();
            }
        }

        private void PlayDigAnimationOnce()
        {
            if (m_PlayerAnimationEntity != null)
            {
                m_PlayerAnimationEntity.PlayDigAnimationOnce();
            }
        }

        private bool DestroyRaycastSelect(BlockData block)
        {
            return block != null
                   && !block.HasFlag(BlockFlags.IgnoreDestroyBlockRaycast)
                   && block.PhysicState == PhysicState.Solid;
        }

        private bool PlaceRaycastSelect(BlockData block)
        {
            return block != null
                   && !block.HasFlag(BlockFlags.IgnorePlaceBlockRaycast)
                   && block.PhysicState == PhysicState.Solid;
        }

        private Ray GetRay()
        {
            return m_Camera.ScreenPointToRay(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f));
        }

        private void SetDigProgress(float progress)
        {
            if (m_PlayerEntity?.World?.RenderingManager == null)
            {
                ShaderUtility.DigProgress = -1;
                return;
            }

            ShaderUtility.DigProgress = (int)(progress * m_PlayerEntity.World.RenderingManager.DigProgressTextureCount) - 1;
        }
    }
}
