namespace BehindTheCurtain.Core
{
    // Provisional connection kinds and logical placement slots, independent of world coordinates.
    public enum ConnectionPointKind { Thread, Anchor, Signal }
    public enum ConnectionPointPosition { Front, Left, Right, Rear }

    public static class ConnectionPointCatalog
    {
        public static bool TryGetAllowedMethods(ConnectionPointKind kind, out InterferenceMethod methods)
        {
            switch (kind)
            {
                case ConnectionPointKind.Thread: methods = InterferenceMethod.Cut; return true;
                case ConnectionPointKind.Anchor: methods = InterferenceMethod.Shoot; return true;
                case ConnectionPointKind.Signal: methods = InterferenceMethod.Disrupt; return true;
                default: methods = 0; return false;
            }
        }
    }
}
