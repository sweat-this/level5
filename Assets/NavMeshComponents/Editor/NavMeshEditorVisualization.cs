namespace UnityEditor.AI
{
    static class NavMeshEditorVisualization
    {
        static int s_ShowNavigation;

        public static bool IsNavigationVisible
        {
            get { return s_ShowNavigation > 0; }
        }

        public static void ShowNavigation()
        {
            s_ShowNavigation++;
        }

        public static void HideNavigation()
        {
            if (s_ShowNavigation > 0)
                s_ShowNavigation--;
        }
    }
}
