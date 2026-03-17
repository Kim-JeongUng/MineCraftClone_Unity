using System;
using System.Collections;
using System.Collections.Generic;
using Minecraft.Configurations;
using Minecraft.Entities;
using Minecraft.Multiplayer;
using UnityEngine;
using static Minecraft.Rendering.LightingUtility;
using static Minecraft.WorldConsts;

namespace Minecraft
{
    internal class WorldSinglePlayer : World
    {
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
                RWAccessor.GetBlock(x, y, z)?.Tick(this, x, y, z);
            }
        }

        private IEnumerator EnablePlayer()
        {
            if (GameModeContext.IsMultiplayer)
            {
                yield break;
            }

            yield return new WaitForSeconds(5);

            if (PlayerTransform == null)
            {
                Debug.LogWarning("[WorldSinglePlayer] PlayerTransform is not assigned. Skipping PlayerEntity enable.");
                yield break;
            }

            if (!PlayerTransform.TryGetComponent(out PlayerEntity playerEntity))
            {
                Debug.LogWarning("[WorldSinglePlayer] PlayerTransform has no PlayerEntity. Skipping enable.");
                yield break;
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
