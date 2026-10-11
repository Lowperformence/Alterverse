namespace Alterverse.Characters
{
    /// <summary>Upcoming heroes are catalogued without changing the live Season 1 roster.</summary>
    public static class Season2Content
    {
        public const bool Released = false;
#if UNITY_EDITOR
        public static bool EditorPreviewEnabled { get; set; }
        public static bool TimmyAvailable => Released || EditorPreviewEnabled;
#else
        public static bool TimmyAvailable => Released;
#endif
    }
}
