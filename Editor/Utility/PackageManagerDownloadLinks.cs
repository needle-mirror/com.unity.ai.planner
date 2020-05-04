using UnityEditor.PackageManager.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityEditor.AI.Planner.Editors
{
    [InitializeOnLoad]
    class PackageManagerDownloadLinks : IPackageManagerExtension
    {
        static string s_PackageName = "com.unity.ai.planner";
        static (string, string)[] s_DownloadLinks = { ("Samples", "https://github.com/Unity-Technologies/ai-planner-samples") };

        static PackageManagerDownloadLinks()
        {
            PackageManagerExtensions.RegisterExtension(new PackageManagerDownloadLinks());
        }

        bool m_PackageSelected;
        VisualElement m_DownloadLinkElement;

        public VisualElement CreateExtensionUI()
        {
            m_DownloadLinkElement = new VisualElement()
            {
                style =
                {
                    alignSelf = Align.FlexStart,
                    flexDirection = FlexDirection.Column
                }
            };

            var title = new Label()
            {
                text = "Downloads",
            };
            title.AddToClassList("containerTitle");
            m_DownloadLinkElement.Add(title);

            foreach (var (name, url) in s_DownloadLinks)
            {
                var link = CreateLink(name, url);
                m_DownloadLinkElement.Add(link);
            }

            return m_DownloadLinkElement;
        }

        static Button CreateLink(string name, string url)
        {
            var link = new Button()
            {
                text = name
            };
            link.AddToClassList("category");
            link.AddToClassList("link");
            link.clicked += () => { Application.OpenURL(url); };
            return link;
        }

        public void OnPackageSelectionChange(PackageManager.PackageInfo packageInfo)
        {
            m_PackageSelected = (packageInfo != null && packageInfo.name == s_PackageName);
            m_DownloadLinkElement.style.display = m_PackageSelected ? DisplayStyle.Flex:DisplayStyle.None;
        }

        public void OnPackageAddedOrUpdated(PackageManager.PackageInfo packageInfo) { }

        public void OnPackageRemoved(PackageManager.PackageInfo packageInfo) { }
    }
}
