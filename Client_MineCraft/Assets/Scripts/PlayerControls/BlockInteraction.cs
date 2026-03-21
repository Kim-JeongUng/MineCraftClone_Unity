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
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Minecraft.PlayerControls
{
    [DisallowMultipleComponent]
    public class BlockInteraction : MonoBehaviour, ILuaCallCSharp
    {
        [Range(3, 12)] public float RaycastMaxDistance = 8;
        [Min(0.1f)] public float MaxClickSpacing = 0.4f;

        [Header("Hand Block UI")]
        [SerializeField] private Text m_CurrentHandBlockText;
        [SerializeField] private InputField m_HandBlockInput;
        [SerializeField] private MonoBehaviour[] m_DisableWhenEditHandBlock;
        [SerializeField] private Transform m_HotbarRoot;
        [SerializeField] private Image[] m_HotbarSlotImages;
        [SerializeField] private Text[] m_HotbarSlotTexts;
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
        [SerializeField] [Range(0, 9)] private int m_SelectedHotbarIndex;
        [SerializeField] private Color m_SelectedSlotColor = new Color(1f, 1f, 1f, 0.95f);
        [SerializeField] private Color m_UnselectedSlotColor = new Color(1f, 1f, 1f, 0.2f);
        [SerializeField] private KeyCode m_BlockInventoryToggleKey = KeyCode.I;

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
        [NonSerialized] private Canvas m_UICanvas;
        [NonSerialized] private GameObject m_BlockInventoryRoot;
        [NonSerialized] private GridLayoutGroup m_BlockInventoryGrid;
        [NonSerialized] private bool m_IsBlockInventoryOpen;
        [NonSerialized] private string[] m_AvailableBlockNames = Array.Empty<string>();

        private readonly Dictionary<string, Sprite> m_BlockSpriteCache = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<int, Image> m_HotbarIconImages = new Dictionary<int, Image>();

        private const string BlockSpriteFolderAssetPath = "Assets/Minecraft Default PBR Resources/Block Sprites";
        private static readonly Color s_InventoryPanelColor = new Color(0f, 0f, 0f, 0.8f);
        private static readonly Color s_InventoryButtonColor = new Color(1f, 1f, 1f, 0.14f);
        private static readonly Color s_InventoryButtonHighlightColor = new Color(1f, 0.95f, 0.55f, 0.28f);

        private static readonly Dictionary<string, string[]> s_BlockSpriteCandidates = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            { "grass", new[] { "grass_top.png", "grass_side.png" } },
            { "water", new[] { "water_still.png" } },
            { "lava", new[] { "lava_still.png" } },
            { "leaves_oak", new[] { "leaves_oak.tga", "leaves_oak_carried.tga" } },
            { "quartz_block_half", new[] { "quartz_block_top.png", "quartz_block_side.png" } },
            { "glass_block", new[] { "glass.tga" } },
            { "torch", new[] { "torch_on.tga" } },
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

        public void AssignHandBlockUI(Text currentHandBlockText, InputField handBlockInput)
        {
            m_CurrentHandBlockText = currentHandBlockText;
            m_HandBlockInput = handBlockInput;
            m_HandBlockInputGO = m_HandBlockInput != null ? m_HandBlockInput.gameObject : null;
            if (m_HandBlockInputGO != null)
            {
                m_HandBlockInputGO.SetActive(false);
            }

            InitializeHotbarUI();
            EnsureInventoryUI();
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

            m_HandBlockInputGO = m_HandBlockInput != null ? m_HandBlockInput.gameObject : null;
            if (m_HandBlockInputGO != null)
            {
                m_HandBlockInputGO.SetActive(false);
            }

            InitializeHotbarUI();
            EnsureInventoryUI();
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
            CacheHotbarSlotsFromRoot();
            ClearHotbarLabels();
            EnsureHotbarIcons();
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

        private void CacheHotbarSlotsFromRoot()
        {
            if (m_HotbarRoot == null && m_CurrentHandBlockText != null && m_CurrentHandBlockText.transform.parent != null)
            {
                Transform found = m_CurrentHandBlockText.transform.parent.Find("Item Bar");
                if (found != null)
                {
                    m_HotbarRoot = found;
                }
            }

            if (m_HotbarRoot == null)
            {
                return;
            }

            if (m_HotbarSlotImages == null || m_HotbarSlotImages.Length != 10)
            {
                m_HotbarSlotImages = new Image[10];
            }

            if (m_HotbarSlotTexts == null || m_HotbarSlotTexts.Length != 10)
            {
                m_HotbarSlotTexts = new Text[10];
            }

            m_UICanvas = m_HotbarRoot.GetComponentInParent<Canvas>(true);

            for (int i = 0; i < 10; i++)
            {
                Transform child = m_HotbarRoot.Find(i == 0 ? "Item" : $"Item ({i})");
                if (child == null)
                {
                    continue;
                }

                m_HotbarSlotImages[i] = child.GetComponent<Image>();
                if (m_HotbarSlotTexts[i] == null)
                {
                    m_HotbarSlotTexts[i] = child.GetComponentInChildren<Text>(true);
                }
            }
        }

        private void ClearHotbarLabels()
        {
            if (m_CurrentHandBlockText != null)
            {
                m_CurrentHandBlockText.text = string.Empty;
                m_CurrentHandBlockText.gameObject.SetActive(false);
            }

            if (m_HotbarSlotTexts == null)
            {
                return;
            }

            for (int i = 0; i < m_HotbarSlotTexts.Length; i++)
            {
                if (m_HotbarSlotTexts[i] == null)
                {
                    continue;
                }

                m_HotbarSlotTexts[i].text = string.Empty;
                m_HotbarSlotTexts[i].gameObject.SetActive(false);
            }
        }

        private void EnsureHotbarIcons()
        {
            if (m_HotbarRoot == null)
            {
                return;
            }

            for (int i = 0; i < 10; i++)
            {
                Transform slot = m_HotbarRoot.Find(i == 0 ? "Item" : $"Item ({i})");
                if (slot == null)
                {
                    continue;
                }

                if (m_HotbarIconImages.ContainsKey(i) && m_HotbarIconImages[i] != null)
                {
                    continue;
                }

                Transform iconTransform = slot.Find("Block Icon");
                Image iconImage;
                if (iconTransform != null)
                {
                    iconImage = iconTransform.GetComponent<Image>();
                }
                else
                {
                    GameObject iconGO = new GameObject("Block Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    iconGO.transform.SetParent(slot, false);
                    RectTransform iconRect = iconGO.GetComponent<RectTransform>();
                    iconRect.anchorMin = Vector2.zero;
                    iconRect.anchorMax = Vector2.one;
                    iconRect.offsetMin = new Vector2(8f, 8f);
                    iconRect.offsetMax = new Vector2(-8f, -8f);
                    iconImage = iconGO.GetComponent<Image>();
                    iconImage.raycastTarget = false;
                    iconImage.preserveAspect = true;
                }

                m_HotbarIconImages[i] = iconImage;
            }
        }

        private void RefreshHotbarIcons()
        {
            for (int i = 0; i < 10; i++)
            {
                if (!m_HotbarIconImages.TryGetValue(i, out Image iconImage) || iconImage == null)
                {
                    continue;
                }

                string blockName = i < m_HotbarBlockNames.Length ? m_HotbarBlockNames[i] : string.Empty;
                iconImage.sprite = GetBlockSprite(blockName);
                iconImage.enabled = iconImage.sprite != null;
                iconImage.color = Color.white;
            }
        }

        private void EnsureInventoryUI()
        {
            CacheHotbarSlotsFromRoot();
            if (m_UICanvas == null || m_BlockInventoryRoot != null)
            {
                RefreshInventoryChoices();
                return;
            }

            GameObject panelGO = new GameObject("Block Inventory", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panelGO.transform.SetParent(m_UICanvas.transform, false);
            RectTransform panelRect = panelGO.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(720f, 420f);
            panelRect.anchoredPosition = new Vector2(0f, 20f);

            Image panelImage = panelGO.GetComponent<Image>();
            panelImage.color = s_InventoryPanelColor;
            panelImage.raycastTarget = true;

            GameObject gridGO = new GameObject("Grid", typeof(RectTransform), typeof(CanvasRenderer), typeof(GridLayoutGroup));
            gridGO.transform.SetParent(panelGO.transform, false);
            RectTransform gridRect = gridGO.GetComponent<RectTransform>();
            gridRect.anchorMin = Vector2.zero;
            gridRect.anchorMax = Vector2.one;
            gridRect.offsetMin = new Vector2(20f, 20f);
            gridRect.offsetMax = new Vector2(-20f, -20f);

            m_BlockInventoryGrid = gridGO.GetComponent<GridLayoutGroup>();
            m_BlockInventoryGrid.cellSize = new Vector2(64f, 64f);
            m_BlockInventoryGrid.spacing = new Vector2(12f, 12f);
            m_BlockInventoryGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            m_BlockInventoryGrid.constraintCount = 8;
            m_BlockInventoryGrid.childAlignment = TextAnchor.UpperLeft;
            m_BlockInventoryGrid.startAxis = GridLayoutGroup.Axis.Horizontal;
            m_BlockInventoryGrid.startCorner = GridLayoutGroup.Corner.UpperLeft;

            m_BlockInventoryRoot = panelGO;
            m_BlockInventoryRoot.SetActive(false);
            RefreshInventoryChoices();
        }

        private void RefreshInventoryChoices()
        {
            if (m_BlockInventoryGrid == null)
            {
                return;
            }

            string[] blockNames = GetAvailableBlocks();
            if (m_AvailableBlockNames.Length == blockNames.Length)
            {
                bool same = true;
                for (int i = 0; i < blockNames.Length; i++)
                {
                    if (!string.Equals(m_AvailableBlockNames[i], blockNames[i], StringComparison.Ordinal))
                    {
                        same = false;
                        break;
                    }
                }

                if (same && m_BlockInventoryGrid.transform.childCount == blockNames.Length)
                {
                    return;
                }
            }

            m_AvailableBlockNames = blockNames;
            for (int i = m_BlockInventoryGrid.transform.childCount - 1; i >= 0; i--)
            {
                Destroy(m_BlockInventoryGrid.transform.GetChild(i).gameObject);
            }

            for (int i = 0; i < m_AvailableBlockNames.Length; i++)
            {
                CreateInventoryButton(m_AvailableBlockNames[i]);
            }
        }

        private string[] GetAvailableBlocks()
        {
            if (m_PlayerEntity?.World?.BlockDataTable == null)
            {
                return GetFallbackBlockList();
            }

            List<string> names = new List<string>();
            BlockTable table = m_PlayerEntity.World.BlockDataTable;
            for (int i = 0; i < table.BlockCount; i++)
            {
                BlockData block = table.GetBlock(i);
                if (!IsSelectableBlock(block))
                {
                    continue;
                }

                names.Add(block.InternalName);
            }

            return names.ToArray();
        }

        private string[] GetFallbackBlockList()
        {
            List<string> names = new List<string>();
            foreach (string blockName in m_HotbarBlockNames)
            {
                if (string.IsNullOrWhiteSpace(blockName) || names.Contains(blockName))
                {
                    continue;
                }

                names.Add(blockName);
            }

            return names.ToArray();
        }

        private static bool IsSelectableBlock(BlockData block)
        {
            return block != null
                   && !string.IsNullOrWhiteSpace(block.InternalName)
                   && !string.Equals(block.InternalName, "air", StringComparison.OrdinalIgnoreCase)
                   && block.InternalName.IndexOf("DO_NOT_USE", StringComparison.OrdinalIgnoreCase) < 0;
        }

        private void CreateInventoryButton(string blockName)
        {
            GameObject buttonGO = new GameObject(blockName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            buttonGO.transform.SetParent(m_BlockInventoryGrid.transform, false);

            Image background = buttonGO.GetComponent<Image>();
            background.color = string.Equals(blockName, GetSelectedBlockName(), StringComparison.OrdinalIgnoreCase)
                ? s_InventoryButtonHighlightColor
                : s_InventoryButtonColor;
            background.raycastTarget = true;

            Button button = buttonGO.GetComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = background.color;
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.24f);
            colors.pressedColor = new Color(1f, 1f, 1f, 0.32f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(1f, 1f, 1f, 0.08f);
            button.colors = colors;
            button.onClick.AddListener(() => AssignBlockToSelectedSlot(blockName));

            GameObject iconGO = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            iconGO.transform.SetParent(buttonGO.transform, false);
            RectTransform iconRect = iconGO.GetComponent<RectTransform>();
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.one;
            iconRect.offsetMin = new Vector2(8f, 8f);
            iconRect.offsetMax = new Vector2(-8f, -8f);

            Image iconImage = iconGO.GetComponent<Image>();
            iconImage.raycastTarget = false;
            iconImage.preserveAspect = true;
            iconImage.sprite = GetBlockSprite(blockName);
            iconImage.enabled = iconImage.sprite != null;
        }

        private void AssignBlockToSelectedSlot(string blockName)
        {
            EnsureHotbarBlockNameSize();
            m_HotbarBlockNames[m_SelectedHotbarIndex] = blockName;
            ApplySelectedHotbar(true);
            RefreshInventorySelectionHighlight();
            SetBlockInventoryOpen(false);
        }

        private void RefreshInventorySelectionHighlight()
        {
            if (m_BlockInventoryGrid == null)
            {
                return;
            }

            string selectedBlock = GetSelectedBlockName();
            for (int i = 0; i < m_BlockInventoryGrid.transform.childCount; i++)
            {
                Transform child = m_BlockInventoryGrid.transform.GetChild(i);
                Image image = child.GetComponent<Image>();
                if (image == null)
                {
                    continue;
                }

                bool isSelected = string.Equals(child.name, selectedBlock, StringComparison.OrdinalIgnoreCase);
                image.color = isSelected ? s_InventoryButtonHighlightColor : s_InventoryButtonColor;

                Button button = child.GetComponent<Button>();
                if (button == null)
                {
                    continue;
                }

                ColorBlock colors = button.colors;
                colors.normalColor = image.color;
                button.colors = colors;
            }
        }

        private void SetBlockInventoryOpen(bool open)
        {
            m_IsBlockInventoryOpen = open;
            if (m_BlockInventoryRoot != null)
            {
                if (open)
                {
                    RefreshInventoryChoices();
                    RefreshInventorySelectionHighlight();
                }

                m_BlockInventoryRoot.SetActive(open);
            }

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
                ClearHotbarLabels();
                EnsureHotbarIcons();
                RefreshHotbarIcons();
            }

            if (m_CurrentHandBlockText != null)
            {
                m_CurrentHandBlockText.text = string.Empty;
            }

            for (int i = 0; i < 10; i++)
            {
                bool isSelected = i == m_SelectedHotbarIndex;
                if (m_HotbarSlotImages != null && i < m_HotbarSlotImages.Length && m_HotbarSlotImages[i] != null)
                {
                    m_HotbarSlotImages[i].color = isSelected ? m_SelectedSlotColor : m_UnselectedSlotColor;
                }
            }

            RefreshInventorySelectionHighlight();
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
#if UNITY_EDITOR
            string assetPath = Path.Combine(BlockSpriteFolderAssetPath, fileName).Replace('\\', '/');
            return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
#else
            return null;
#endif
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
                        block.Click(world, hit.Position.x, hit.Position.y, hit.Position.z);
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
                            forward = Mathf.Abs(forward.x) > Mathf.Abs(forward.z) ? new Vector3(forward.x, 0, 0) : new Vector3(0, 0, forward.z);
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
