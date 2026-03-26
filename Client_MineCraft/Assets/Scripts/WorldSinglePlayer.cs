using System;
using System.Collections;
using System.Collections.Generic;
using Minecraft.Configurations;
using Minecraft.Entities;
using Minecraft.PhysicSystem;
using UnityEngine;
using static Minecraft.Rendering.LightingUtility;
using static Minecraft.WorldConsts;

namespace Minecraft
{
    internal class WorldSinglePlayer : World
    {
        private const int MaxFluidLevel = 7;
        private const float FluidHeightUnit = 1f / 8f;

        [NonSerialized] private Stack<Vector3Int> m_BlocksToLightQueue; // 않, 메인 스레드 
        [NonSerialized] private Stack<Vector3Int> m_ImportantBlocksToLightQueue;
        [NonSerialized] private Queue<Vector3Int> m_BlocksToTickQueue; // 않, 메인 스레드 

        protected override IEnumerator OnInitialize()
        {
            yield return null;

            m_BlocksToLightQueue = new Stack<Vector3Int>();
            m_ImportantBlocksToLightQueue = new Stack<Vector3Int>();
            m_BlocksToTickQueue = new Queue<Vector3Int>();
            StartCoroutine(EnablePlayer());
        }

        protected override void OnUpdate()
        {
            LightBlocks();
            TickBlocks();
        }

        private void LightBlocks()
        {
            // TODO: 최적화 

            int limit = MaxLightBlockCountPerFrame; // 방지멈춤 
            LightBlocks(m_ImportantBlocksToLightQueue, ref limit, ModificationSource.PlayerAction);
            LightBlocks(m_BlocksToLightQueue, ref limit, ModificationSource.InternalOrSystem);
        }

        private void LightBlocks(Stack<Vector3Int> queue, ref int limit, ModificationSource source)
        {
            while (limit-- > 0 && queue.Count > 0)
            {
                Vector3Int blockPos = queue.Pop();

                if (blockPos.y < 0 || blockPos.y >= ChunkHeight)
                {
                    continue;
                }

                if (!ChunkManager.GetChunk(ChunkPos.GetFromAny(blockPos.x, blockPos.z), false, out _))
                {
                    // 않, 만약！ 
                    // m_BlocksToLightQueue.Push(blockPos);
                    // break;
                    continue;
                }

                int x = blockPos.x;
                int y = blockPos.y;
                int z = blockPos.z;

                BlockData block = RWAccessor.GetBlock(x, y, z);
                int opacity = Mathf.Max(block.LightOpacity, 1);
                int finalLight = 0;

                if (opacity < MaxLight || block.LightValue > 0) // 않0 
                {
                    int max = RWAccessor.GetAmbientLight(x + 1, y, z);
                    int temp;

                    if ((temp = RWAccessor.GetAmbientLight(x - 1, y, z)) > max)
                        max = temp;

                    if ((temp = RWAccessor.GetAmbientLight(x, y + 1, z)) > max)
                        max = temp;

                    if ((temp = RWAccessor.GetAmbientLight(x, y - 1, z)) > max)
                        max = temp;

                    if ((temp = RWAccessor.GetAmbientLight(x, y, z + 1)) > max)
                        max = temp;

                    if ((temp = RWAccessor.GetAmbientLight(x, y, z - 1)) > max)
                        max = temp;

                    finalLight = max - opacity;

                    if (block.LightValue > finalLight)
                    {
                        finalLight = block.LightValue; // (않) 
                    }
                    else if (finalLight < 0)
                    {
                        finalLight = 0;
                    }
                    //else if (finalLight > MaxLight)
                    //{
                    //  finalLight = MaxLight;
                    //}
                }

                if (RWAccessor.SetAmbientLightLevel(x, y, z, finalLight, source))
                {
                    queue.Push(new Vector3Int(x - 1, y, z));
                    queue.Push(new Vector3Int(x, y - 1, z));
                    queue.Push(new Vector3Int(x, y, z - 1));
                    queue.Push(new Vector3Int(x + 1, y, z));
                    queue.Push(new Vector3Int(x, y + 1, z));
                    queue.Push(new Vector3Int(x, y, z + 1));
                }
            }
        }

        private void TickBlocks()
        {
            int count = m_BlocksToTickQueue.Count;

            if (count > MaxTickBlockCountPerFrame)
            {
                count = MaxTickBlockCountPerFrame; // 방지멈춤 
            }

            while (count-- > 0)
            {
                Vector3Int blockPos = m_BlocksToTickQueue.Dequeue();
                int x = blockPos.x;
                int y = blockPos.y;
                int z = blockPos.z;

                BlockData block = RWAccessor.GetBlock(x, y, z);
                if (block == null)
                {
                    continue;
                }

                if (TrySimulateFluid(block, x, y, z))
                {
                    continue;
                }

                block.Tick(this, x, y, z);
            }
        }

        private bool TrySimulateFluid(BlockData block, int x, int y, int z)
        {
            if (block.PhysicState != PhysicState.Fluid)
            {
                return false;
            }

            BlockData air = BlockDataTable.GetBlock("air");
            Quaternion currentRotation = RWAccessor.GetBlockRotation(x, y, z, Quaternion.identity);
            int currentLevel = DecodeFluidLevel(currentRotation);
            bool hasSourceAbove = IsSameFluid(block, RWAccessor.GetBlock(x, y + 1, z));

            if (TryFlowDownward(block, x, y, z))
            {
                return true;
            }

            int nextLevel = Mathf.Min(MaxFluidLevel, currentLevel + 1);
            if (nextLevel <= MaxFluidLevel && TryFlowHorizontal(block, x, y, z, nextLevel))
            {
                return true;
            }

            if (hasSourceAbove)
            {
                if (currentLevel == 0)
                {
                    return true;
                }

                RWAccessor.SetBlock(x, y, z, block, EncodeFluidLevel(0), ModificationSource.InternalOrSystem);
                return true;
            }

            int minAdjacentLevel = GetMinAdjacentFluidLevel(block, x, y, z);
            if (minAdjacentLevel > MaxFluidLevel)
            {
                if (currentLevel > 0)
                {
                    RWAccessor.SetBlock(x, y, z, air, Quaternion.identity, ModificationSource.InternalOrSystem);
                    return true;
                }

                return true;
            }

            int stabilizedLevel = Mathf.Min(MaxFluidLevel, minAdjacentLevel + 1);
            if (stabilizedLevel != currentLevel)
            {
                RWAccessor.SetBlock(x, y, z, block, EncodeFluidLevel(stabilizedLevel), ModificationSource.InternalOrSystem);
                return true;
            }

            return true;
        }

        private bool TryFlowDownward(BlockData fluid, int x, int y, int z)
        {
            if (y <= 0)
            {
                return false;
            }

            int targetY = y - 1;
            BlockData target = RWAccessor.GetBlock(x, targetY, z);
            if (!CanFlowInto(target))
            {
                return false;
            }

            RWAccessor.SetBlock(x, targetY, z, fluid, EncodeFluidLevel(0), ModificationSource.InternalOrSystem);
            return true;
        }

        private bool TryFlowHorizontal(BlockData fluid, int x, int y, int z, int nextLevel)
        {
            if (nextLevel > MaxFluidLevel)
            {
                return false;
            }

            bool changed = false;
            changed |= TryFlowInto(fluid, x - 1, y, z, nextLevel);
            changed |= TryFlowInto(fluid, x + 1, y, z, nextLevel);
            changed |= TryFlowInto(fluid, x, y, z - 1, nextLevel);
            changed |= TryFlowInto(fluid, x, y, z + 1, nextLevel);
            return changed;
        }

        private bool TryFlowInto(BlockData fluid, int x, int y, int z, int level)
        {
            if (y < 0 || y >= ChunkHeight)
            {
                return false;
            }

            BlockData target = RWAccessor.GetBlock(x, y, z);
            if (!CanFlowInto(target))
            {
                if (IsSameFluid(fluid, target))
                {
                    int existingLevel = DecodeFluidLevel(RWAccessor.GetBlockRotation(x, y, z, Quaternion.identity));
                    if (existingLevel > level)
                    {
                        RWAccessor.SetBlock(x, y, z, fluid, EncodeFluidLevel(level), ModificationSource.InternalOrSystem);
                        return true;
                    }
                }

                return false;
            }

            RWAccessor.SetBlock(x, y, z, fluid, EncodeFluidLevel(level), ModificationSource.InternalOrSystem);
            return true;
        }

        private int GetMinAdjacentFluidLevel(BlockData fluid, int x, int y, int z)
        {
            int minLevel = int.MaxValue;
            ReadAdjacentFluidLevel(fluid, x - 1, y, z, ref minLevel);
            ReadAdjacentFluidLevel(fluid, x + 1, y, z, ref minLevel);
            ReadAdjacentFluidLevel(fluid, x, y, z - 1, ref minLevel);
            ReadAdjacentFluidLevel(fluid, x, y, z + 1, ref minLevel);
            return minLevel;
        }

        private void ReadAdjacentFluidLevel(BlockData fluid, int x, int y, int z, ref int minLevel)
        {
            BlockData adjacent = RWAccessor.GetBlock(x, y, z);
            if (!IsSameFluid(fluid, adjacent))
            {
                return;
            }

            int adjacentLevel = DecodeFluidLevel(RWAccessor.GetBlockRotation(x, y, z, Quaternion.identity));
            if (adjacentLevel < minLevel)
            {
                minLevel = adjacentLevel;
            }
        }

        private static bool IsSameFluid(BlockData current, BlockData other)
        {
            return current != null && other != null && current.ID == other.ID;
        }

        private static bool CanFlowInto(BlockData target)
        {
            if (target == null)
            {
                return false;
            }

            if (target.InternalName == "air")
            {
                return true;
            }

            return false;
        }

        private static int DecodeFluidLevel(Quaternion data)
        {
            int level = Mathf.RoundToInt(data.x / FluidHeightUnit);
            return Mathf.Clamp(level, 0, MaxFluidLevel);
        }

        private static Quaternion EncodeFluidLevel(int level)
        {
            int clampedLevel = Mathf.Clamp(level, 0, MaxFluidLevel);
            return new Quaternion(clampedLevel * FluidHeightUnit, 0f, 0f, 1f);
        }

        private IEnumerator EnablePlayer()
        {
            while (PlayerTransform == null)
            {
                yield return null;
            }

            PlayerEntity playerEntity = null;

            while (!PlayerTransform.TryGetComponent(out playerEntity))
            {
                yield return null;
            }

            playerEntity.enabled = true;
        }

        public override void LightBlock(int x, int y, int z, ModificationSource source)
        {
            if (source == ModificationSource.InternalOrSystem)
            {
                m_BlocksToLightQueue.Push(new Vector3Int(x, y, z));
            }
            else
            {
                m_ImportantBlocksToLightQueue.Push(new Vector3Int(x, y, z));
            }
        }

        public override void TickBlock(int x, int y, int z)
        {
            m_BlocksToTickQueue.Enqueue(new Vector3Int(x, y, z));
            m_BlocksToTickQueue.Enqueue(new Vector3Int(x - 1, y, z));
            m_BlocksToTickQueue.Enqueue(new Vector3Int(x + 1, y, z));
            m_BlocksToTickQueue.Enqueue(new Vector3Int(x, y - 1, z));
            m_BlocksToTickQueue.Enqueue(new Vector3Int(x, y + 1, z));
            m_BlocksToTickQueue.Enqueue(new Vector3Int(x, y, z - 1));
            m_BlocksToTickQueue.Enqueue(new Vector3Int(x, y, z + 1));
        }
    }
}
