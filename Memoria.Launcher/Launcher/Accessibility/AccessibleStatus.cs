using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace Memoria.Launcher
{
    // The screen reader observes the ordinary WPF control. Only the explicit
    // Test speech action uses Prism, so focus does not produce duplicate output.
    public sealed class AccessibleStatus : TextBox
    {
        public AccessibleStatus()
        {
            IsReadOnly = true;
            TextWrapping = System.Windows.TextWrapping.Wrap;
            AutomationProperties.SetName(this, "Accessibility status");
            AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Polite);
        }

        public void Report(string message, bool important)
        {
            Text = message ?? "";
            AutomationProperties.SetLiveSetting(this, important ? AutomationLiveSetting.Assertive : AutomationLiveSetting.Polite);
            var peer = UIElementAutomationPeer.FromElement(this) ?? UIElementAutomationPeer.CreatePeerForElement(this);
            if (peer != null) peer.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
        }
    }
}
