namespace Minecraft.Multiplayer
{
    public enum RuntimeGameMode
    {
        SinglePlayer,
        DedicatedServer,
        Client,
        Host,
    }

    public static class GameModeContext
    {
        public static RuntimeGameMode Mode { get; private set; } = RuntimeGameMode.SinglePlayer;

        public static bool IsMultiplayer => Mode != RuntimeGameMode.SinglePlayer;

        public static bool IsServer => Mode == RuntimeGameMode.DedicatedServer || Mode == RuntimeGameMode.Host;

        public static bool IsClient => Mode == RuntimeGameMode.Client || Mode == RuntimeGameMode.Host;

        public static void SetMode(RuntimeGameMode mode)
        {
            Mode = mode;
        }
    }
}
