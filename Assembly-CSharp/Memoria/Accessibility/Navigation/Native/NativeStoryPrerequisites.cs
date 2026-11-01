using System;
using Memoria.Prime;
namespace Memoria.Accessibility.Navigation
{
    public static class NativeStoryPrerequisites
    {
        private static readonly StoryPrerequisiteCatalog Catalog=new StoryPrerequisiteCatalog(StoryPrerequisiteData.All);
        private static string _error;
        public static void Initialize(){Catalog.HasScope(-1,-1);}
        public static bool TryAppend(NavigationContext context,CatalogResult targets)
        {
            int field=FF9StateSystem.Common.FF9.fldMapNo;
            if(context==null || context.LogicalMapKey==null || !context.LogicalMapKey.StartsWith("field:"+field+":") || !Catalog.HasScope(field,context.StoryProgress))return false;
            var evidence=new StoryPrerequisiteEvidence{Field=field};
            if(!context.Foreground || (!context.CanInspect && !context.CanControl))return Catalog.TryAppend(context,targets,evidence);
            try {
                var engine=PersistenSingleton<EventEngine>.Instance;
                var player=engine==null?null:engine.GetControlChar() as Actor;
                if(engine==null || engine.gMode!=1 || player==null || player.uid!=context.PlayerIdentity || player.fieldMapActorController==null || !player.fieldMapActorController.isPlayer)return Catalog.TryAppend(context,targets,evidence);
                var director=engine.GetObjUID(0) as Obj;
                evidence.DirectorHash=director!=null && director.sid==0?Hash(director):null;
                evidence.PlayerSid=player.sid;evidence.PlayerHash=Hash(player);
                evidence.ActiveTriangle=player.fieldMapActorController.activeTri;
                var rule=Catalog.Field(field);
                if((rule.Kind==StoryPrerequisiteKind.FossilJunction || rule.Kind==StoryPrerequisiteKind.FossilBoarding) && engine.fieldmap!=null)evidence.BgiHash=NativeFieldTargets.CurrentBgiHash(engine.fieldmap);
                evidence.GlobalByte=index=>Byte(FF9StateSystem.EventState.gEventGlobal,index);
                evidence.MapByte=index=>Byte(engine.GetMapVar(),index);
                evidence.Object=id=> {
                    var obj=engine.GetObjUID(id.NativeId) as Obj;if(obj==null)return null;
                    var actor=obj as Actor;
                    return new StoryPrerequisiteObject{Sid=obj.sid,Hash=Hash(obj),Model=actor==null?-1:actor.model};
                };
                bool handled=Catalog.TryAppend(context,targets,evidence);_error=null;return handled;
            }catch(Exception exception) {
                if(_error!=exception.Message){_error=exception.Message;Log.Message("[FF9Access] Story prerequisite read unavailable: {0}",exception);}
                return Catalog.TryAppend(context,targets,new StoryPrerequisiteEvidence{Field=field});
            }
        }
        private static int? Byte(byte[] values,int index){return values!=null && index>=0 && index<values.Length?(int?)values[index]:null;}
        private static string Hash(Obj obj){return obj==null || obj.ebData==null?null:NativeFieldTargets.Fingerprint(obj.ebData);}
    }
}
