using System;
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
        [SerializeField] private Color m_SelectedTextColor = new Color(1f, 0.95f, 0.55f, 1f);
        [SerializeField] private Color m_UnselectedTextColor = Color.white;

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
            ApplySelectedHotbar(true);
        }

        private void OnDisable()
        {
            StopDigAnimationLoop();
            SetDigProgress(0);
            ShaderUtility.TargetedBlockPosition = Vector3.down;
        }

        private void Update()
        {
            if (ChangeHandBlock())
            {
                return;
            }

            if (!m_Camera || m_PlayerEntity == null)
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
            EnsureHotbarSlotTexts();
            RefreshHotbarLabels();
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

        private void EnsureHotbarSlotTexts()
        {
            if (m_HotbarRoot == null || m_HotbarSlotTexts == null)
            {
                return;
            }

            //Font builtinFont = Resources.GetBuiltinResource<Font>("Arial.ttf");

            for (int i = 0; i < m_HotbarSlotTexts.Length; i++)
            {
                if (m_HotbarSlotTexts[i] != null || i >= m_HotbarRoot.childCount)
                {
                    continue;
                }

                Transform slot = m_HotbarRoot.GetChild(i);
                GameObject labelGO = new GameObject($"Slot Label {i + 1}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                labelGO.transform.SetParent(slot, false);

                RectTransform rectTransform = labelGO.GetComponent<RectTransform>();
                rectTransform.anchorMin = Vector2.zero;
                rectTransform.anchorMax = Vector2.one;
                rectTransform.offsetMin = new Vector2(4f, 4f);
                rectTransform.offsetMax = new Vector2(-4f, -4f);

                // Text label = labelGO.GetComponent<Text>();
                // label.font = builtinFont;
                // label.fontSize = 12;
                // label.alignment = TextAnchor.MiddleCenter;
                // label.horizontalOverflow = HorizontalWrapMode.Wrap;
                // label.verticalOverflow = VerticalWrapMode.Overflow;
                // label.supportRichText = false;
                // m_HotbarSlotTexts[i] = label;
            }
        }

        private void RefreshHotbarLabels()
        {
            if (m_HotbarSlotTexts == null)
            {
                return;
            }

            for (int i = 0; i < m_HotbarSlotTexts.Length && i < m_HotbarBlockNames.Length; i++)
            {
                if (m_HotbarSlotTexts[i] == null)
                {
                    continue;
                }

                m_HotbarSlotTexts[i].text = $"{GetHotbarDisplayKey(i)}\n{GetDisplayName(m_HotbarBlockNames[i])}";
            }
        }

        private void SetSelectedHotbarIndex(int index)
        {
            m_SelectedHotbarIndex = Mathf.Clamp(index, 0, 9);
            ApplySelectedHotbar(false);
        }

        private void ApplySelectedHotbar(bool forceRefreshLabels)
        {
            EnsureHotbarBlockNameSize();

            if (forceRefreshLabels)
            {
                RefreshHotbarLabels();
            }

            if (m_CurrentHandBlockText != null)
            {
                m_CurrentHandBlockText.text = GetSelectedBlockName();
            }

            for (int i = 0; i < 10; i++)
            {
                bool isSelected = i == m_SelectedHotbarIndex;
                if (m_HotbarSlotImages != null && i < m_HotbarSlotImages.Length && m_HotbarSlotImages[i] != null)
                {
                    m_HotbarSlotImages[i].color = isSelected ? m_SelectedSlotColor : m_UnselectedSlotColor;
                }

                if (m_HotbarSlotTexts != null && i < m_HotbarSlotTexts.Length && m_HotbarSlotTexts[i] != null)
                {
                    m_HotbarSlotTexts[i].color = isSelected ? m_SelectedTextColor : m_UnselectedTextColor;
                }
            }
        }

        private string GetSelectedBlockName()
        {
            EnsureHotbarBlockNameSize();
            return m_HotbarBlockNames[m_SelectedHotbarIndex] ?? string.Empty;
        }

        private static string GetHotbarDisplayKey(int index)
        {
            return index == 9 ? "0" : (index + 1).ToString();
        }

        private static string GetDisplayName(string blockName)
        {
            if (string.IsNullOrWhiteSpace(blockName))
            {
                return "EMPTY";
            }

            return blockName.Replace('_', ' ').ToUpperInvariant();
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

                                //block.PlayDigAudio(m_AudioSource);

                                // if (Setting.SettingManager.Active.RenderingSetting.EnableDestroyEffect)
                                // {
                                //   ParticleSystem effect = Instantiate(m_DestroyEffectPrefab, firstHitPos + new Vector3(0.5f, 0.5f, 0.5f), Quaternion.identity).GetComponent<ParticleSystem>();
                                //   ParticleSystem.MainModule main = effect.main;
                                //   main.startColor = block.DestoryEffectColor;
                                // }
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
                // 선택된 블록 없음
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

                        // 반드시 아래 순서대로 곱해 Y축 회전 후 XZ축 회전을 보장

                        if ((block.RotationAxes & BlockRotationAxes.AroundXOrZAxis) == BlockRotationAxes.AroundXOrZAxis)
                        {
                            rotation *= Quaternion.FromToRotation(Vector3.up, hit.Normal);                        }

                        if ((block.RotationAxes & BlockRotationAxes.AroundYAxis) == BlockRotationAxes.AroundYAxis)
                        {
                            Vector3 forward = m_PlayerEntity.Forward;
                            forward = Mathf.Abs(forward.x) > Mathf.Abs(forward.z) ? new Vector3(forward.x, 0, 0) : new Vector3(0, 0, forward.z);
                            rotation *= Quaternion.LookRotation(-forward.normalized, Vector3.up); // 플레이어 
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
