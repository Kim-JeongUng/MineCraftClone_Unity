using Minecraft.Configurations;

namespace Minecraft.Multiplayer
{
    /// <summary>
    /// Marks a TNT click invocation as "visual-only" when dispatched from client fuse-start messages.
    /// This prevents re-entering authoritative server fuse/explosion logic on host/client shared runtime.
    /// </summary>
    public static class TntVisualDispatchContext
    {
        public static bool IsVisualOnlyDispatch { get; private set; }

        public static void InvokeVisualOnlyClick(BlockData block, IWorld world, int x, int y, int z)
        {
            if (block == null || world == null)
            {
                return;
            }

            bool previous = IsVisualOnlyDispatch;
            IsVisualOnlyDispatch = true;
            try
            {
                block.Click(world, x, y, z);
            }
            finally
            {
                IsVisualOnlyDispatch = previous;
            }
        }
    }
}
