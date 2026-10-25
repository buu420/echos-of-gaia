using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;

namespace Memoria.Launcher
{
    public static class UiAccessibility
    {
        public static void Name(DependencyObject control, string name, string id)
        {
            if (!String.IsNullOrWhiteSpace(name)) AutomationProperties.SetName(control, name);
            if (!String.IsNullOrWhiteSpace(id)) AutomationProperties.SetAutomationId(control, id);
        }

        public static string Resource(string key, string fallback)
        {
            return Lang.Res[key] as string ?? fallback;
        }

        public static void Apply(DependencyObject root)
        {
            var control = root as Control;
            if (control != null && String.IsNullOrEmpty(AutomationProperties.GetName(control))) {
                // Headered containers announce their visible header, not a field inside.
                string text = ReadText((control as HeaderedContentControl)?.Header);
                if (String.IsNullOrWhiteSpace(text)) text = ReadText((control as ContentControl)?.Content);
                if (String.IsNullOrWhiteSpace(text)) text = ReadText(control.ToolTip);
                if (!String.IsNullOrWhiteSpace(text)) Name(control, text, control.Name);
            }
            int children = VisualTreeHelper.GetChildrenCount(root);
            for (int index = 0; index < children; index++) Apply(VisualTreeHelper.GetChild(root, index));
        }

        private static string ReadText(object value)
        {
            var text = value as string; if (text != null) return text;
            var block = value as TextBlock; if (block != null) return block.Text;
            var content = value as ContentControl; if (content != null) return ReadText(content.Content);
            var panel = value as Panel;
            if (panel != null) foreach (UIElement child in panel.Children) {
                text = ReadText(child); if (!String.IsNullOrWhiteSpace(text)) return text;
            }
            return null;
        }
    }
}
