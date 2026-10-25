using System;
using System.IO;
using UnityEngine;
using Memoria.Accessibility.Navigation;
namespace Memoria.Accessibility
{
    public class WorldMapAccessibilityManager : PersistenSingleton<WorldMapAccessibilityManager>
    {
        private bool _enabled;private readonly WorldTargetCatalog _catalog=new WorldTargetCatalog();
        private static readonly WorldReachabilityCache _reachability=new WorldReachabilityCache();
        private int _traceCount=-1,_traceHash;private string _traceUnavailable;
        public static readonly RegionObservations Regions=new RegionObservations(Path.Combine(Application.persistentDataPath,"FF9Access/observations"));
        public static event Action<NavigationContext,CatalogResult> Frame;
        static WorldMapAccessibilityManager() { NavigationSaveIdentity.Loaded+=Regions.SwitchSave;NavigationSaveIdentity.Saved+=Regions.CommitSave;NavigationLifecycleBridge.Releasing+=delegate{_reachability.Cancel();}; }
        public void Initialize() { _enabled=true; }
        public void Shutdown() { _enabled=false;NavigationLifecycleBridge.Release(StopReason.NativeClear); }
        public CatalogResult ReadTargets(NavigationContext context)
        {
            int id;string label;
            if(context.CanControl && NativeRegionReader.TryRead(out id,out label))Regions.Observe(context.SaveIdentity,context.LogicalMapKey,id,label,context.Position,context.Transport);
            var catalog=_catalog.Build(context,NativeWorldTargets.Read(context),Regions);
            var geometry=NativeWorldGeometry.Read(context,null);WorldReachabilityIndex index;
            if(geometry!=null && geometry.Rules!=null && geometry.Rules.Flying){TraceCatalog(context,catalog,catalog);return catalog;}
            _reachability.TryRead(geometry,context.CanControl && context.Foreground,out index);
            var result=WorldTargetCatalog.Filter(context,catalog,geometry,index);
            if(index==null && _reachability.Failure!=null)result.UnavailableReason="World destinations are unavailable: "+_reachability.Failure+".";
            TraceCatalog(context,catalog,result);
            return result;
        }
        private void TraceCatalog(NavigationContext context,CatalogResult source,CatalogResult result)
        {
            int hash=17;foreach(var target in result.Targets)hash=unchecked(hash*31+target.Id.GetHashCode());
            if(_traceCount==result.Targets.Count && _traceHash==hash && _traceUnavailable==result.UnavailableReason)return;
            _traceCount=result.Targets.Count;_traceHash=hash;_traceUnavailable=result.UnavailableReason;
            var names=new System.Text.StringBuilder();foreach(var target in result.Targets){if(names.Length!=0)names.Append(", ");names.Append(target.Label);}
            NavigationRuntime.Trace("World destination reachability; transport="+context.Transport.Kind+"; offered="+result.Targets.Count+"; available="+source.Targets.Count+"; reason="+result.UnavailableReason+"; destinations="+names);
        }
        public void Update()
        {
            if(!_enabled)return;NativeNavigationInput.EnsureUpdated();var context=NativeNavigationContext.Read();
            PresentFrame(context);
        }
        public void UpdateWorldMovement()
        {
            if(!_enabled)return;NativeNavigationInput.EnsureUpdated();var context=NativeNavigationContext.Read();
            // Only the completed native update may observe motion, never Unity Update.
            NativeWorldFootsteps.Tick(context);
            PresentFrame(context);
        }
        private void PresentFrame(NavigationContext context)
        {
            if(context.LogicalMapKey==null || !context.LogicalMapKey.StartsWith("world:"))return;
            NavigationRuntime.Tick();
            if(Frame!=null)Frame(context,ReadTargets(context));
        }
    }
}
