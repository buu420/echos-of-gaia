using System;
using Memoria.Accessibility.Navigation;
namespace Memoria.Accessibility
{
    public class AccessibleNavigationManager : PersistenSingleton<AccessibleNavigationManager>
    {
        private bool _enabled;
        private readonly FieldTargetCatalog _catalog = new FieldTargetCatalog();
        public static event Action<NavigationContext,CatalogResult> Frame;
        public void Initialize() { NativeStoryTargets.Initialize();_enabled = true; }
        public void Shutdown() { _enabled = false; NavigationLifecycleBridge.Release(StopReason.NativeClear); }
        public CatalogResult ReadTargets(NavigationContext context) { return _catalog.Build(context,NativeFieldTargets.Read(context),NavigationSaveIdentity.Identities); }
        public void Update()
        {
            if(!_enabled)return;
            NativeNavigationInput.EnsureUpdated();
            var context=NativeNavigationContext.Read(); if(context.LogicalMapKey==null || !context.LogicalMapKey.StartsWith("field:"))return;
            NavigationRuntime.Tick();if(Frame!=null && !context.MinigameInspection)Frame(context,ReadTargets(context));
        }
    }
}
