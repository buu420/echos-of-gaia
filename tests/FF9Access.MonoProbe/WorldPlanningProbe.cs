using System;
using System.Reflection;
using System.Threading;

// Load the staged game assembly into the original Mono without a Unity scene.
// Only pure navigation types are created; no game input or state is changed.
internal static class WorldPlanningProbe
{
    private static Assembly _game;
    private static Type Type(string name) { return _game.GetType("Memoria.Accessibility.Navigation."+name,true); }
    private static object Create(string name,params object[] args) { return Activator.CreateInstance(Type(name),args); }
    private static object Point(float x,float z) { return Create("NavigationPoint",x,0f,z); }
    private static void Set(object value,string field,object data) { value.GetType().GetField(field).SetValue(value,data); }
    private static object Get(object value,string field) { return value.GetType().GetField(field).GetValue(value); }
    private static void Require(bool condition,string message) { if(!condition)throw new InvalidOperationException(message); }
    private static bool Start(object worker,long request,object context,object target,object geometry)
    {
        return (bool)worker.GetType().GetMethod("TryStart").Invoke(worker,new object[]{request,context,target,geometry});
    }
    private static bool Take(object worker,long request,out object result)
    {
        object[] args=new object[]{request,null};bool taken=(bool)worker.GetType().GetMethod("TryTake").Invoke(worker,args);result=args[1];return taken;
    }
    private static bool Same(object a,object b) {return (float)Get(a,"X")== (float)Get(b,"X") && (float)Get(a,"Y")== (float)Get(b,"Y") && (float)Get(a,"Z")== (float)Get(b,"Z");}
    private static void CheckCountedGuidance()
    {
        object context=Create("NavigationContext"),target=Create("NavigationTarget"),route=Create("RoutePlan"),movement=Create("MovementObservation"),decision=Create("RouteDecision"),update=Create("NavigationUpdate"),speech=Create("SpeechState"),announcer=Create("NavigationAnnouncements");
        Set(context,"LogicalMapKey","field:100");Set(context,"CanControl",true);Set(context,"Foreground",true);Set(context,"Position",Point(0,0));
        Set(target,"Id",Create("TargetIdentity",Enum.Parse(Type("TargetKind"),"FieldExit"),"field:100",10));Set(target,"Label","Exit");Set(target,"Position",Point(-480,1200));
        Array points=Array.CreateInstance(Type("NavigationPoint"),2);points.SetValue(Point(-480,0),0);points.SetValue(Point(-480,1200),1);
        Set(route,"Success",true);Set(route,"Waypoints",points);Set(movement,"PlannedRoute",route);Set(movement,"RouteAvailable",true);Set(movement,"RoutePoint",Point(-480,0));
        Set(decision,"Mode",Enum.Parse(Type("RouteMode"),"Guiding"));Set(update,"Context",context);Set(update,"Selected",target);Set(update,"Movement",movement);Set(update,"Session",decision);Set(update,"GuidanceEnabled",true);
        MethodInfo format=announcer.GetType().GetMethod("Format");
        System.Collections.IList output=(System.Collections.IList)format.Invoke(announcer,new object[]{update,5000,speech});
        Require(output.Count==1 && (string)Get(output[0],"Text")=="left 2.","Mono counted guidance uses final-target distance or native units");
        MethodInfo acknowledge=announcer.GetType().GetMethod("Acknowledge");acknowledge.Invoke(announcer,new object[]{output[0],true});
        Set(context,"Position",Point(-480,0));Set(movement,"RoutePoint",Point(-480,1200));Set(speech,"NowMilliseconds",100d);
        output=(System.Collections.IList)format.Invoke(announcer,new object[]{update,5000,speech});
        Require(output.Count==1 && (string)Get(output[0],"Text")=="up 5.","Mono turn guidance waited for the reminder interval");
        acknowledge.Invoke(announcer,new object[]{output[0],false});Set(speech,"NowMilliseconds",1200d);
        output=(System.Collections.IList)format.Invoke(announcer,new object[]{update,5000,speech});
        Require(output.Count==1 && (string)Get(output[0],"Text")=="up 5.","Mono lost a rejected turn before reminder time");
        Set(speech,"NarrationActive",true);Set(speech,"LastAnnouncement","left 2.");
        output=(System.Collections.IList)format.Invoke(announcer,new object[]{update,5000,speech});Require(output.Count==0,"Mono guidance talked over recorded narration");
        Console.WriteLine("PASS Mono counted guidance: left 2 to corner, up 5 immediately, rejected-turn retry and narration deferral; no Unity/input access");
    }
    private static void CheckFieldExitsAndInteractions()
    {
        object geometry=Create("FieldGeometrySnapshot"),triangle=Create("FieldTriangle");
        Array vertices=Array.CreateInstance(Type("NavigationPoint"),3);
        vertices.SetValue(Create("NavigationPoint",0f,0f,0f),0);vertices.SetValue(Create("NavigationPoint",1000f,1000f,0f),1);vertices.SetValue(Create("NavigationPoint",0f,0f,1000f),2);
        Set(triangle,"Vertices",vertices);Array triangles=Array.CreateInstance(Type("FieldTriangle"),1);triangles.SetValue(triangle,0);Set(geometry,"Triangles",triangles);Set(geometry,"Radius",80f);
        Array region=Array.CreateInstance(Type("NavigationPoint"),4);region.SetValue(Point(0,0),0);region.SetValue(Point(59,0),1);region.SetValue(Point(59,82),2);region.SetValue(Point(0,82),3);
        Array candidates=(Array)Type("FieldRegionApproaches").GetMethod("Interaction").Invoke(null,new object[]{region,geometry});
        Require(candidates.Length>0,"Mono omitted the radius-clear sloped trigger corner");
        foreach(object point in candidates)Require((bool)geometry.GetType().GetMethod("IsSegmentClear").Invoke(geometry,new object[]{point,point}),"Mono trigger candidate lacks native clearance");
        var records=(System.Collections.IDictionary)Type("FieldExitEvidence").GetField("_surfaces",BindingFlags.NonPublic|BindingFlags.Static).GetValue(null);
        int count=0;foreach(System.Collections.IList list in records.Values)count+=list.Count;
        Require(count==8,"Mono did not load all eight reviewed trigger surfaces");
        object gates=Create("FieldInteractionGates");Set(gates,"HasTalkHandler",true);Set(gates,"ReviewedListener",true);Set(gates,"MapTalkAllowed",true);Set(gates,"Level",7);
        object empty=Enum.Parse(Type("FieldInteractionState"),"Unavailable");
        Require(Type("FieldInteractionRules").GetMethod("Talk").Invoke(null,new object[]{gates,empty}).ToString()=="Unknown","Mono disabled the reviewed player-owned conversation");
        object binding=Type("FieldInteractionBindings").GetMethod("FindActor").Invoke(null,new object[]{806,4,"a764a6e4badfbdd1aee0970c9147841370c4e49ceedb7cd20ae07a36353d12ec",217,4});
        var can=Type("FieldInteractionBindings").GetMethod("CanRequest");
        Require(!(bool)can.Invoke(null,new object[]{binding,6}) && (bool)can.Invoke(null,new object[]{binding,7}),"Mono lost requested-speaker priority");
        var volume=Type("FieldExitProximityTracker").GetMethod("VolumeGain");
        Require((float)volume.Invoke(null,new object[]{true,100,true,100})==0f && (float)volume.Invoke(null,new object[]{true,200,false,50})==1f,"Mono exit cue mute/volume mismatch");
        object ops=Create("FieldDoorOpcodes");Set(ops,"MapJump",43);Set(ops,"WorldJump",182);Set(ops,"JumpPosition",164);Set(ops,"MoveQuad",158);
        var instruction=Type("ScriptInstruction").GetConstructor(new[]{typeof(int),typeof(int),typeof(int),typeof(byte[])});
        Array path=Array.CreateInstance(Type("ScriptInstruction"),6);
        path.SetValue(instruction.Invoke(new object[]{0,9,5,new byte[]{120,250,1,125,80,251,24,127}}),0);
        object branch=instruction.Invoke(new object[]{9,12,2,null});Set(branch,"Argument",10);path.SetValue(branch,1);
        path.SetValue(instruction.Invoke(new object[]{12,13,164,null}),2);path.SetValue(instruction.Invoke(new object[]{13,14,158,null}),3);
        path.SetValue(instruction.Invoke(new object[]{14,18,43,null}),4);path.SetValue(instruction.Invoke(new object[]{22,23,4,null}),5);
        var current=Type("FieldDoorEvidence").GetMethod("CurrentPlainWalkOut");
        Func<int,int,int?> height=delegate(int uid,int property){return uid==250 && property==1?(int?)-1301:null;};
        object snapshot=Create("ScriptSnapshot",new byte[16],new byte[16],new byte[16]);
        Require((bool)current.Invoke(null,new object[]{path,snapshot,ops,height}) && !(bool)current.Invoke(null,new object[]{path,snapshot,ops,null}),"Mono current plain path lost the scoped native height guard");
        Console.WriteLine("PASS Mono field exits/interactions: 3D radius corner, eight embedded surface reviews, player-owned listener, speaker request priority and native sound mute/volume; no Unity/input access");
    }
    private static void CheckStory()
    {
        var timer=System.Diagnostics.Stopwatch.StartNew();Type native=Type("NativeStoryTargets");native.GetMethod("Initialize").Invoke(null,null);timer.Stop();
        var fields=(System.Collections.IDictionary)native.GetField("_fields",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
        Require(fields.Count==804,"Mono Story metadata did not load every gameplay field");
        object context=Create("NavigationContext");Set(context,"Generation",1L);Set(context,"LogicalMapKey","field:104");Set(context,"SaveIdentity","story-probe");Set(context,"PlayerIdentity",7);Set(context,"StoryProgress",1000);
        const string seller="cd021ee4b7483b7b783e7477c5c9ffac67b67e5de29476232ff3f0bab7cf3c9c";
        const string player="802456aa29a6aabecc4894a2c1273e1658b02b2b3db2358f67ceda9fe6925733";
        Require((bool)native.GetMethod("Matches").Invoke(null,new object[]{context,4,seller}),"Mono native seller provenance failed");
        object observation=Create("FieldTargetObservation");Set(observation,"Id",Create("TargetIdentity",Enum.Parse(Type("TargetKind"),"FieldActor"),"field:104",4));Set(observation,"Visible",true);Set(observation,"IsPerson",true);Set(observation,"HasInteraction",false);
        Func<int,string,bool> matches=delegate(int sid,string hash){return sid==7 && hash==player;};
        var annotate=native.GetMethod("Annotate");annotate.Invoke(null,new object[]{context,4,seller,observation,null,matches});
        Require(((int[])Get(observation,"StoryMilestones")).Length==1 && ((int[])Get(observation,"StoryMilestones"))[0]==1150,"Mono lost the reviewed player-listener objective");
        Require((bool)Get(observation,"HasInteraction"),"Mono trivial native Talk listener was mistaken for no interaction");
        Require(((string)Get(observation,"StoryAction")).IndexOf("ticket booth")>=0,"Mono Story lost the required action wording");
        annotate.Invoke(null,new object[]{context,4,"modified",observation,null,matches});Require(((int[])Get(observation,"StoryMilestones")).Length==0,"Mono modified script inherited a Story objective");
        Set(context,"StoryProgress",1150);annotate.Invoke(null,new object[]{context,4,seller,observation,null,matches});Require(((int[])Get(observation,"StoryMilestones")).Length==0,"Mono completed ticket visit remained a required objective");
        byte[] buffer={99,99,99,99,99,99,99,99,1,0};byte[] instance=(byte[])Type("ScriptSnapshot").GetMethod("InstanceBytes").Invoke(null,new object[]{buffer,2});Require(instance.Length==2 && instance[0]==1,"Mono native word offset was read as bytes");
        Console.WriteLine("PASS Mono Story metadata: 804 fields, native seller/player hashes, current listener checkpoint, modified/completed rejection and word-offset snapshot; warm="+timer.ElapsedMilliseconds+" ms; no Unity/input access");
    }
    private static void CheckStoryWorker(object context,object physical,object geometry)
    {
        object story=Type("StoryTargetCatalog").GetMethod("Wrap").Invoke(null,new object[]{context,10500,physical,"Story worker fixture"});
        object worker=Create("OwnedWorldPlanner");Require(Start(worker,9,context,story,geometry),"Mono Story worker did not start");
        object result;DateTime deadline=DateTime.UtcNow.AddSeconds(5);while(!Take(worker,9,out result) && DateTime.UtcNow<deadline)Thread.Sleep(1);
        Require(result!=null,"Mono Story worker did not publish");object route=result.GetType().GetProperty("Route").GetValue(result,null),identity=route.GetType().GetProperty("PhysicalIdentity").GetValue(route,null);
        Require(identity.Equals(Get(physical,"Id")),"Mono world worker lost the physical identity");
        Require((bool)Type("GuidanceRouteValidation").GetMethod("Matches").Invoke(null,new object[]{route,context,story}),"Mono Story route failed freshness validation");
        Console.WriteLine("PASS Mono Story world worker: separate selection/physical identity survives the real ThreadPool and guidance validation; no Unity/input access");
    }
    private static void CheckHotCold()
    {
        object reader=Create("ChocoboHotColdReader"),frame=Create("HotColdFrame");
        Set(frame,"Generation",1L);Set(frame,"MapKey","field:2950");Set(frame,"Active",true);Set(frame,"RoundActive",true);Set(frame,"CanSpeak",true);
        Set(frame,"TimerVisible",true);Set(frame,"TimerRunning",true);Set(frame,"TimerSeconds",59.99d);Set(frame,"Depth",80);Set(frame,"Found",2);
        MethodInfo update=reader.GetType().GetMethod("Update"),cue=reader.GetType().GetMethod("ObserveCue"),take=reader.GetType().GetMethod("TakeSpeech");
        update.Invoke(reader,new object[]{frame});cue.Invoke(reader,new object[]{Enum.Parse(Type("HotColdCue"),"Located")});
        object speech=Create("SpeechState");object output=take.Invoke(reader,new object[]{speech,true,false});
        Require((string)Get(output,"Text")=="Chocobo Hot and Cold. 59 seconds remaining. Treasure located; keep digging. Depth 80. Found 2.","Mono minigame status lost displayed values");
        MethodInfo acknowledge=reader.GetType().GetMethod("Acknowledge");acknowledge.Invoke(reader,new object[]{output,true});
        reader.GetType().GetMethod("ObserveResult").Invoke(reader,new object[]{"Found Potion but you cannot carry any more."});
        output=take.Invoke(reader,new object[]{speech,false,false});Require((string)Get(output,"Text")=="Found Potion but you cannot carry any more.","Mono minigame lost visible result");
        acknowledge.Invoke(reader,new object[]{output,false});Require((bool)reader.GetType().GetProperty("HasPendingResults").GetValue(reader,null),"Mono rejected result was discarded");
        Set(speech,"NowMilliseconds",500d);output=take.Invoke(reader,new object[]{speech,false,false});acknowledge.Invoke(reader,new object[]{output,true});
        Require(!(bool)reader.GetType().GetProperty("HasPendingResults").GetValue(reader,null),"Mono accepted result remained queued");
        reader.GetType().GetMethod("Reset").Invoke(reader,null);Require(take.Invoke(reader,new object[]{speech,true,false})==null,"Mono minigame retained an ended presentation");
        object[] binding=new object[]{"JP",282,"[IMME]ク、クェーッ!!","ク、クェーッ!!",null};
        Require(Type("HotColdPresentationBindings").GetMethod("Classify").Invoke(null,binding).ToString()=="Cry" && binding[4].ToString()=="Located","Mono localized cry binding failed");
        object context=Create("NavigationContext");Set(context,"Foreground",true);Set(context,"CanInspect",true);Set(context,"MinigameInspection",true);
        object physical=Create("PhysicalInputSnapshot");Set(physical,"ControllerConnected",true);Set(physical,"Buttons",Enum.Parse(Type("NavigationButton"),"R3, Automatic, DpadUp"));Set(physical,"MoveX",1f);
        object parse=Type("NavigationBindings").GetMethod("Parse").Invoke(null,new object[]{null});object broker=Create("NavigationInputBroker",Get(parse,"Bindings"));
        Array commands=(Array)broker.GetType().GetMethod("Update").Invoke(broker,new object[]{context,physical});
        Require(commands.Length==1 && commands.GetValue(0).ToString()=="Status","Mono minigame stick click did not read only status");
        Require(!(bool)broker.GetType().GetProperty("MenuOpen").GetValue(broker,null) && (bool)broker.GetType().GetMethod("FilterButton").Invoke(broker,new object[]{Enum.Parse(Type("NavigationButton"),"Automatic"),true}),"Mono minigame captured digging");
        Require((float)broker.GetType().GetMethod("FilterAxis").Invoke(broker,new object[]{Enum.Parse(Type("NavigationAxis"),"MoveX"),1f})==1f,"Mono minigame captured movement");
        Console.WriteLine("PASS Mono Hot and Cold: shown status, UTF-8 cry meaning, visible result, reset and stick-click inspection with dig/movement pass-through; no Unity/input access");
    }
    private static void CheckFieldLandmarks()
    {
        object context=Create("NavigationContext");Set(context,"Generation",1L);Set(context,"LogicalMapKey","field:100");Set(context,"SaveIdentity","landmark-probe");Set(context,"PlayerIdentity",7);
        object observation=Create("FieldTargetObservation");object id=Create("TargetIdentity",Enum.Parse(Type("TargetKind"),"FieldActor"),"field:100",9);
        Set(observation,"Id",id);Set(observation,"Visible",true);Set(observation,"IsPerson",true);Set(observation,"HasInteraction",true);Set(observation,"Position",Point(9,0));
        Array approaches=Array.CreateInstance(Type("NavigationPoint"),1);approaches.SetValue(Point(5,2),0);Set(observation,"ApproachCandidates",approaches);
        Array observations=Array.CreateInstance(Type("FieldTargetObservation"),1);observations.SetValue(observation,0);
        object catalog=Create("FieldTargetCatalog");MethodInfo build=catalog.GetType().GetMethod("Build");build.Invoke(catalog,new object[]{context,observations,null});
        Set(observation,"Visible",false);Set(observation,"OutOfView",true);Set(observation,"Position",Point(900,900));approaches.SetValue(Point(900,900),0);
        object result=build.Invoke(catalog,new object[]{context,observations,null});var choices=(System.Collections.IList)Get(result,"Targets");
        Require(choices.Count==1 && Same(Get(choices[0],"Position"),Point(9,0)),"Mono scrolled landmark lost its observed position");
        Require(Same(((Array)Get(choices[0],"ApproachCandidates")).GetValue(0),Point(5,2)),"Mono landmark copied an unseen approach update");
        Require(((string)Get(choices[0],"Availability")).IndexOf("last seen")>=0,"Mono landmark did not disclose last-seen evidence");
        Set(observation,"OutOfView",false);result=build.Invoke(catalog,new object[]{context,observations,null});
        Require(((System.Collections.IList)Get(result,"Targets")).Count==0,"Mono hidden landmark remained selectable");
        Set(observation,"Visible",true);build.Invoke(catalog,new object[]{context,observations,null});
        Set(observation,"Visible",false);Set(observation,"OutOfView",true);Set(observation,"NativePresentationIdentity",42);
        result=build.Invoke(catalog,new object[]{context,observations,null});
        Require(((System.Collections.IList)Get(result,"Targets")).Count==0,"Mono reused actor ID inherited an unseen landmark");
        Console.WriteLine("PASS Mono field landmarks: camera-scroll retention, observed position and approach snapshots, last-seen disclosure and native hide removal; no Unity/input access");
    }
    private static void CheckReachability()
    {
        Array surfaces=Array.CreateInstance(Type("WorldSurface"),6);int next=0;
        for(int x=0;x<3;x++)for(int half=0;half<2;half++) {
            object surface=Create("WorldSurface");Set(surface,"Id",next);Set(surface,"BlockId",0);Set(surface,"NormalY",1f);Set(surface,"TerrainId",x==1?57*4:0);
            Array vertices=Array.CreateInstance(Type("NavigationPoint"),3);
            vertices.SetValue(Point(half==0?x*16:(x+1)*16,0),0);
            vertices.SetValue(Point((x+1)*16,half==0?0:-16),1);vertices.SetValue(Point(x*16,-16),2);
            Set(surface,"Vertices",vertices);surfaces.SetValue(surface,next++);
        }
        object rules=Create("TransportRules");Set(rules,"LimitLow",1u);
        object geometry=Create("WorldGeometrySnapshot",1L,"Mono connectivity",surfaces,rules,true,null);
        object context=Create("NavigationContext");Set(context,"Generation",1L);Set(context,"LogicalMapKey","world:1");Set(context,"Position",Point(4,-4));
        object catalog=Create("CatalogResult");var targets=(System.Collections.IList)Get(catalog,"Targets");
        for(int i=0;i<2;i++) {
            object target=Create("NavigationTarget");Set(target,"Id",Create("TargetIdentity",Enum.Parse(Type("TargetKind"),"WorldLocation"),"world:1",i));Set(target,"Generation",1L);Set(target,"Position",Point(i==0?4:36,-4));targets.Add(target);
        }
        object cache=Create("WorldReachabilityCache");MethodInfo read=cache.GetType().GetMethod("TryRead");object[] arguments=new object[]{geometry,true,null};object index=null;
        for(int i=0;i<300;i++){if((bool)read.Invoke(cache,arguments)){index=arguments[2];break;}Thread.Sleep(10);}
        Require(index!=null,"Mono reachability worker did not publish");
        MethodInfo filter=Type("WorldTargetCatalog").GetMethod("Filter");
        object selected=filter.Invoke(null,new object[]{context,catalog,geometry,index});
        var choices=(System.Collections.IList)Get(selected,"Targets");Require(choices.Count==1 && (int)Get(Get(choices[0],"Id"),"NativeId")==0,"Mono foot catalog crossed water");
        Set(context,"Position",Point(36,-4));selected=filter.Invoke(null,new object[]{context,catalog,geometry,index});choices=(System.Collections.IList)Get(selected,"Targets");
        Require(choices.Count==1 && (int)Get(Get(choices[0],"Id"),"NativeId")==1,"Mono area exit retained the old destinations");
        cache.GetType().GetMethod("Cancel").Invoke(cache,null);
        object returned=geometry.GetType().GetMethod("ForContext").Invoke(geometry,new object[]{9L,"battle return",rules,null});arguments=new object[]{returned,false,null};
        Require((bool)read.Invoke(cache,arguments) && Object.ReferenceEquals(index,arguments[2]),"Mono battle return rebuilt the area index");
        Console.WriteLine("PASS Mono world reachability: detached ThreadPool index, separated walking areas, fresh-position filtering and battle-return cache reuse; no Unity/input access");
    }
    private static void CheckFootsteps()
    {
        object cadence=Create("WorldFootstepCadence"),context=Create("NavigationContext");
        Set(context,"Generation",1L);Set(context,"LogicalMapKey","world:0");Set(context,"SaveIdentity","footsteps");Set(context,"PlayerIdentity",1);
        Set(context,"CanControl",true);Set(context,"Foreground",true);Set(context,"Position",Point(1535,-1279));
        Set(context,"Transport",Create("TransportIdentity",Enum.Parse(Type("TransportKind"),"Foot"),1,1));
        MethodInfo observe=cadence.GetType().GetMethod("Observe");
        Require(!(bool)observe.Invoke(cadence,new object[]{context,true,0d}),"Mono footsteps sounded while priming");
        Set(context,"Position",Point(1,-1));Require((bool)observe.Invoke(cadence,new object[]{context,true,300d}),"Mono wrapped footstep was not accepted");
        Require(!(bool)observe.Invoke(cadence,new object[]{context,true,600d}) && !(bool)cadence.GetType().GetProperty("IsMoving").GetValue(cadence,null),"Mono blocked footsteps did not stop");
        Set(context,"GateReason",Enum.Parse(Type("GateReason"),"Battle"));Set(context,"Position",Point(2,-1));
        Require(!(bool)observe.Invoke(cadence,new object[]{context,true,900d}),"Mono battle created footsteps");
        int loaded=0,played=0,stopped=0;
        Func<int,int> load=delegate(int index){loaded++;return 100+index;};
        Func<int,float,int> play=delegate(int bank,float gain){played++;return 1000+played;};
        Action<int> stop=delegate(int voice){stopped++;};Action<string> report=delegate(string message){throw new Exception(message);};
        object cache=Create("CachedFootstepPlayer",2,load,play,stop,report);
        for(int i=0;i<5;i++)cache.GetType().GetMethod("WarmOne").Invoke(cache,null);
        Require(loaded==2,"Mono footsteps reload cached samples");
        for(int i=0;i<6;i++)Require((bool)cache.GetType().GetMethod("Play").Invoke(cache,new object[]{1f}),"Mono cached step not played");
        Require(loaded==2 && stopped==2,"Mono step playback loads files or exceeds owned voice slots");
        cache.GetType().GetMethod("Stop").Invoke(cache,null);Require(stopped==6,"Mono suspension left owned voices active");
        int rejected=0,reports=0;
        Func<int,float,int> reject=delegate(int bank,float gain){rejected++;return 0;};
        Action<string> reportRejected=delegate(string message){reports++;};
        object failedCache=Create("CachedFootstepPlayer",1,load,reject,stop,reportRejected);
        failedCache.GetType().GetMethod("WarmOne").Invoke(failedCache,null);
        for(int i=0;i<3;i++)Require(!(bool)failedCache.GetType().GetMethod("Play").Invoke(failedCache,new object[]{1f}),"Mono rejected native voice was accepted");
        Require(rejected==1 && reports==1,"Mono native voice rejection did not report once and disable retries");
        Console.WriteLine("PASS Mono world footsteps: wrapped accepted movement, silent stationary/battle gates, bounded cached playback, owned cancellation and native rejection reporting; no Unity/input access");
    }
    private static void CheckCaptureBoundary()
    {
        object preparation=Create("WorldGeometryPreparation");
        Require((bool)preparation.GetType().GetMethod("TryBegin").Invoke(preparation,new object[]{21L}),"Mono capture-boundary worker did not begin");
        Thread.Sleep(10);int reports=0;
        Action<Exception> report=delegate(Exception exception){reports++;throw new InvalidOperationException("diagnostic failed");};
        ConstructorInfo constructor=Type("WorldCaptureBoundary").GetConstructors(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)[0];
        object boundary=constructor.Invoke(new object[]{preparation,report});
        Action capture=delegate{throw new OutOfMemoryException("injected native capture failure");};
        Require(!(bool)boundary.GetType().GetMethod("TryCapture").Invoke(boundary,new object[]{capture}),"Mono capture failure did not stop publication");
        Require((bool)boundary.GetType().GetProperty("Unavailable").GetValue(boundary,null) && reports==1,"Mono capture failure was not bounded");
        object[] args=new object[]{21L,1L,"failed",Create("TransportRules"),new int[0],null};
        Require(!(bool)preparation.GetType().GetMethod("TryRead").Invoke(preparation,args),"Mono failed capture published partial topology");
        DateTime deadline=DateTime.UtcNow.AddSeconds(10);bool replaced=false;
        while(!(replaced=(bool)preparation.GetType().GetMethod("TryBegin").Invoke(preparation,new object[]{22L})) && DateTime.UtcNow<deadline)Thread.Sleep(1);
        Require(replaced,"Mono failed capture left the worker waiting");preparation.GetType().GetMethod("Invalidate").Invoke(preparation,null);
        Console.WriteLine("PASS Mono native capture fault: exception and failing diagnostic contained, cancelled batch waiter released, no partial topology; no Unity/input access");
    }
    private static void CheckPreparation(Array surfaces,object rules)
    {
        object preparation=Create("WorldGeometryPreparation");MethodInfo start=preparation.GetType().GetMethod("TryStartOwned"),read=preparation.GetType().GetMethod("TryRead");
        Require((bool)start.Invoke(preparation,new object[]{7L,surfaces}),"Mono preparation did not queue detached geometry");
        object[] args=new object[]{7L,10L,"fresh-rules",rules,new int[]{0},null};DateTime deadline=DateTime.UtcNow.AddSeconds(10);bool ready=false;
        while(!(ready=(bool)read.Invoke(preparation,args)) && DateTime.UtcNow<deadline)Thread.Sleep(1);
        Require(ready && args[5]!=null,"Mono preparation did not publish");object geometry=args[5];
        Require((long)Get(geometry,"Generation")==10L && (string)Get(geometry,"Fingerprint")=="fresh-rules","Mono topology retained obsolete context state");
        Set(Get(geometry,"Rules"),"LimitLow",0u);((int[])Get(geometry,"NativeCache"))[0]=-1;
        args=new object[]{7L,11L,"fresh-again",rules,new int[]{0},null};Require((bool)read.Invoke(preparation,args),"Mono topology was not reusable");
        object fresh=args[5];Require((long)Get(fresh,"Generation")==11L && (uint)Get(Get(fresh,"Rules"),"LimitLow")==1u && ((int[])Get(fresh,"NativeCache"))[0]==0,"Mono request state was shared with an earlier request");
        Require(Object.ReferenceEquals(Get(geometry,"Surfaces"),Get(fresh,"Surfaces")),"Mono request rebuilt unchanged topology");
        object retained=preparation.GetType().GetMethod("RetainReady").Invoke(preparation,null);
        object seamHit=fresh.GetType().GetMethod("Probe").Invoke(fresh,new object[]{Create("NavigationPoint",1f,0f,0.00001f),0,Create("PrivateWorldCache",new int[0])});
        Require(seamHit!=null && (int)Get(seamHit,"Id")==0,"Mono periodic cell rounding rejected native seam support");
        preparation.GetType().GetMethod("Invalidate").Invoke(preparation,null);Require(!(bool)read.Invoke(preparation,args),"Mono invalidated topology remained available");
        MethodInfo begin=preparation.GetType().GetMethod("TryBegin"),append=preparation.GetType().GetMethod("TryAppendOwned"),complete=preparation.GetType().GetMethod("Complete");
        Require((bool)begin.Invoke(preparation,new object[]{8L}),"Mono incremental preparation did not begin");
        Require((bool)append.Invoke(preparation,new object[]{8L,surfaces}),"Mono incremental preparation rejected a detached batch");
        args=new object[]{8L,12L,"streamed",rules,new int[]{0},null};Thread.Sleep(10);
        Require(!(bool)read.Invoke(preparation,args),"Mono incremental preparation published before native loading completed");
        Require((bool)complete.Invoke(preparation,new object[]{8L}),"Mono incremental loading completion rejected");
        deadline=DateTime.UtcNow.AddSeconds(10);while(!(ready=(bool)read.Invoke(preparation,args)) && DateTime.UtcNow<deadline)Thread.Sleep(1);
        Require(ready && args[5]!=null,"Mono incremental preparation failed to finish");
        preparation.GetType().GetMethod("Invalidate").Invoke(preparation,null);
        Require((bool)begin.Invoke(preparation,new object[]{9L}),"Mono waiting preparation did not begin");Thread.Sleep(10);
        preparation.GetType().GetMethod("Invalidate").Invoke(preparation,null);
        deadline=DateTime.UtcNow.AddSeconds(10);bool rebound=false;MethodInfo bind=preparation.GetType().GetMethod("TryBindReady");
        while(!(rebound=(bool)bind.Invoke(preparation,new object[]{10L,retained})) && DateTime.UtcNow<deadline)Thread.Sleep(1);
        Require(rebound,"Mono cancelled batch waiter did not release ownership");
        args=new object[]{10L,13L,"rebound",rules,new int[]{0},null};Require((bool)read.Invoke(preparation,args),"Mono retained topology could not be rebound");
        Require((long)Get(args[5],"Generation")==13L && Object.ReferenceEquals(Get(retained,"Surfaces"),Get(args[5],"Surfaces")),"Mono rebound topology rebuilt or retained old context");
        object content=Create("WorldGeometryContent",retained);
        Require((bool)content.GetType().GetMethod("MatchesBlock").Invoke(content,new object[]{0,surfaces}),"Mono exact native block content failed to match");
        object budget=Create("WorldCaptureBudget");MethodInfo take=budget.GetType().GetMethod("Take");
        Require((int)take.Invoke(budget,new object[]{1,24,480})==24 && (int)take.Invoke(budget,new object[]{1,24,480})==0 && (int)take.Invoke(budget,new object[]{2,24,480})==24,"Mono native readers exceeded the shared frame copy budget");
        Console.WriteLine("PASS Mono world preparation: incremental ThreadPool topology, complete-only publication, cancellation wakes waiting worker, exact retained reuse, fresh private generation/rules/cache, seam support and shared per-frame copy budget; no Unity/input access");
    }
    private static void CheckFieldPlanner(object context,object target,object geometry)
    {
        Array triangles=Array.CreateInstance(Type("FieldTriangle"),12);int id=0;
        for(int z=0;z<2;z++)for(int x=0;x<3;x++)for(int half=0;half<2;half++) {
            object tri=Create("FieldTriangle");Set(tri,"Id",id);
            Array points=Array.CreateInstance(Type("NavigationPoint"),3);
            points.SetValue(Point(half==0?x*20:(x+1)*20,z*20),0);
            points.SetValue(Point((x+1)*20,half==0?z*20:(z+1)*20),1);
            points.SetValue(Point(x*20,(z+1)*20),2);Set(tri,"Vertices",points);triangles.SetValue(tri,id++);
        }
        foreach(object tri in triangles) {
            Array points=(Array)Get(tri,"Vertices");int[] neighbors=new int[]{-1,-1,-1};
            for(int edge=0;edge<3;edge++)foreach(object next in triangles)if(next!=tri) {
                Array other=(Array)Get(next,"Vertices");
                for(int j=0;j<3;j++)if((Same(points.GetValue(edge),other.GetValue(j))&&Same(points.GetValue((edge+1)%3),other.GetValue((j+1)%3))) || (Same(points.GetValue(edge),other.GetValue((j+1)%3))&&Same(points.GetValue((edge+1)%3),other.GetValue(j))))neighbors[edge]=(int)Get(next,"Id");
            }Set(tri,"Neighbors",neighbors);
        }
        Set(geometry,"Triangles",triangles);Set(geometry,"ActiveTriangle",1);
        Set(context,"LogicalMapKey","field:mono-fixture");Set(context,"Position",Point(15,20));
        Set(target,"Id",Create("TargetIdentity",Enum.Parse(Type("TargetKind"),"FieldActor"),"field:mono-fixture",16));Set(target,"Position",Point(45,20));
        object planner=Create("FieldRoutePlanner"),route=planner.GetType().GetMethod("Plan").Invoke(planner,new object[]{context,target,geometry});
        Require((bool)Get(route,"Success"),"Mono field planner rejected a safe short interior portal");
        Set(geometry,"Radius",1f);
        Require((bool)geometry.GetType().GetMethod("IsSegmentClear").Invoke(geometry,new object[]{Point(10,10),Point(10,10)}),"Mono field planner lost stationary active support");
        Console.WriteLine("PASS Mono field planner: safe interior portals, native surface trace and stationary shared-edge support; no Unity/input access");
        Array region=Array.CreateInstance(Type("NavigationPoint"),4);
        region.SetValue(Point(35,10),0);region.SetValue(Point(55,10),1);region.SetValue(Point(55,30),2);region.SetValue(Point(35,30),3);
        Set(target,"InteractionUid",19);Set(target,"IsInteractive",true);Set(target,"InteractionRegion",region);Set(target,"Position",Create("NavigationPoint",45f,1000f,20f));
        route=planner.GetType().GetMethod("Plan").Invoke(planner,new object[]{context,target,geometry});
        Require((bool)Get(route,"Success") && (float)Get(Get(route,"ApproachPoint"),"Y")==0,"Mono ground interaction region failed");
        Set(target,"InteractionUid",-1);Set(target,"InteractionRegion",null);Set(target,"UseViewingPoint",true);Set(target,"IsInteractive",false);Set(target,"Position",Create("NavigationPoint",90f,1000f,20f));
        route=planner.GetType().GetMethod("Plan").Invoke(planner,new object[]{context,target,geometry});
        Require((bool)Get(route,"Success") && (bool)Get(route,"ViewingPoint") && (float)Get(Get(route,"ApproachPoint"),"X")>40,"Mono connected viewing approach failed");
        Console.WriteLine("PASS Mono Forest approaches: raised actor uses ground interaction region; noninteractive observed object uses connected viewing surface");
        Set(geometry,"Radius",10f);Set(context,"Position",Point(20,5));
        Require(!(bool)geometry.GetType().GetMethod("IsSegmentClear").Invoke(geometry,new object[]{Point(20,5),Point(20,5)}),"Mono boundary overlap was incorrectly stationary-clear");
        route=planner.GetType().GetMethod("Plan").Invoke(planner,new object[]{context,target,geometry});
        Require((bool)Get(route,"Success") && (bool)Get(route,"ViewingPoint"),"Mono departure from native wall clearance failed");
        Console.WriteLine("PASS Mono field departure: existing wall overlap recedes to full-radius clearance; ordinary viewing route remains available");
    }
    internal static void Run(string gamePath)
    {
        Require(GC.CollectionCount(0)>=0,"Original Mono cannot report the passive collection counter");
        _game=Assembly.LoadFrom(gamePath);
        CheckCountedGuidance();
        CheckStory();
        CheckFieldExitsAndInteractions();
        CheckHotCold();
        CheckFootsteps();
        CheckReachability();
        CheckFieldLandmarks();
        CheckCaptureBoundary();
        object context=Create("NavigationContext"),target=Create("NavigationTarget"),rules=Create("TransportRules");
        Set(context,"Generation",1L);Set(context,"LogicalMapKey","world:0");Set(context,"SaveIdentity","mono-fixture");Set(context,"PlayerIdentity",12);
        Set(context,"CanControl",true);Set(context,"Foreground",true);Set(context,"Position",Point(1,-1));
        Set(context,"Transport",Create("TransportIdentity",Enum.Parse(Type("TransportKind"),"Foot"),12,0));
        Set(target,"Generation",1L);Set(target,"Position",Point(2,-2));
        Set(target,"Id",Create("TargetIdentity",Enum.Parse(Type("TargetKind"),"RegionPoint"),"world:0",1));
        Set(rules,"LimitLow",1u);Set(rules,"Step",0.05f);
        object surface=Create("WorldSurface");Set(surface,"Id",0);Set(surface,"TerrainId",0);Set(surface,"NormalY",1f);
        Array vertices=Array.CreateInstance(Type("NavigationPoint"),3);vertices.SetValue(Point(0,0),0);vertices.SetValue(Point(10,0),1);vertices.SetValue(Point(0,-10),2);Set(surface,"Vertices",vertices);
        Array surfaces=Array.CreateInstance(Type("WorldSurface"),1);surfaces.SetValue(surface,0);
        CheckPreparation(surfaces,rules);
        object geometry=Create("WorldGeometrySnapshot",1L,"mono-floor",surfaces,rules,true,new int[]{0});
        CheckStoryWorker(context,target,geometry);
        object worker=Create("OwnedWorldPlanner");Require(Start(worker,1,context,target,geometry),"Mono worker did not queue");
        object result=null;DateTime deadline=DateTime.UtcNow.AddSeconds(10);
        while(!Take(worker,1,out result) && DateTime.UtcNow<deadline)Thread.Sleep(1);
        Require(result!=null,"Mono worker did not publish completion");
        object route=result.GetType().GetProperty("Route").GetValue(result,null);Require(route!=null && (bool)Get(route,"Success"),"Mono worker did not compute the route");
        MethodInfo current=result.GetType().GetMethod("IsCurrent");Require((bool)current.Invoke(result,new object[]{context,target,"mono-floor"}),"Fresh Mono result rejected");
        Set(context,"Position",Create("NavigationPoint",1f,1f,-1f));Require(!(bool)current.Invoke(result,new object[]{context,target,"mono-floor"}),"Mono accepted an obsolete route height");Set(context,"Position",Point(1,-1));
        Require(Start(worker,2,context,target,geometry),"Mono second request did not queue");worker.GetType().GetMethod("Cancel").Invoke(worker,null);
        Require(!Take(worker,2,out result),"Mono published a cancelled request");
        deadline=DateTime.UtcNow.AddSeconds(10);bool started=false;
        while(!(started=Start(worker,3,context,target,geometry)) && DateTime.UtcNow<deadline)Thread.Sleep(1);
        Require(started,"Mono cancellation did not release the worker");
        while(!Take(worker,3,out result) && DateTime.UtcNow<deadline)Thread.Sleep(1);
        Require(result!=null,"Mono replacement request did not complete");route=result.GetType().GetProperty("Route").GetValue(result,null);
        Require(route!=null && (bool)Get(route,"Success"),"Mono replacement route failed");
        Console.WriteLine("PASS Mono world worker: ThreadPool completion, height freshness, cancellation and replacement; no Unity/input access");
        object field=Create("FieldGeometrySnapshot");Set(field,"Generation",1L);Set(field,"Radius",12f);
        object triangle=Create("FieldTriangle");Set(triangle,"Id",9);Set(triangle,"Floor",3);Set(triangle,"Vertices",vertices);Set(triangle,"Neighbors",new int[]{8,7,-1});
        Array triangles=Array.CreateInstance(Type("FieldTriangle"),1);triangles.SetValue(triangle,0);Set(field,"Triangles",triangles);
        Set(context,"CurrentLocation","Room \"one\"\nsecond line");
        string capture=(string)Type("FieldRouteDiagnostics").GetMethod("Serialize").Invoke(null,new object[]{context,target,field,route});
        Require(capture.IndexOf("\"Radius\":12",StringComparison.Ordinal)>=0 && capture.IndexOf("Room \\\"one\\\"\\u000asecond line",StringComparison.Ordinal)>=0,"Mono field snapshot values were not preserved");
        Require(capture.IndexOf("\"Neighbors\":[8,7,-1]",StringComparison.Ordinal)>=0 && capture.IndexOf("\"Vertices\":[{\"X\":0,\"Y\":0,\"Z\":0}",StringComparison.Ordinal)>=0,"Mono field snapshot did not retain the nested triangle graph");
        Console.WriteLine("PASS Mono field diagnostic: detached JSON values without System.Data or Unity/input access");
        CheckFieldPlanner(context,target,field);
    }
}
