"""Native FFIX request graph: script IDs and instantiated object UIDs are distinct.

Only native NEW/NEW2/NEW3 sites establish a literal object binding. Reused UIDs
retain each possible owner; live instance/hash/guard proof must select one.
Nodes are (script ID, object UID, tag), with UID -1 for an unresolved template.
"""
from collections import defaultdict, namedtuple

RequestEdge=namedtuple('RequestEdge','source target offset opcode')
RequestPath=namedtuple('RequestPath','root edges')
Creation=namedtuple('Creation','sid uid source offset opcode')

class NativeEventGraph:
    def __init__(self,entries):
        self.entries={int(sid):entry for sid,entry in entries.items()}
        self.templates={(sid,int(tag)):ops for sid,entry in self.entries.items() for tag,ops in entry['functions'].items() if ops}
        self.creations=[];self.unresolved=[];self.instances=defaultdict(set)
        self.instances[0].add(0)  # EventEngine starts the field's director.
        for source,ops in self.templates.items():
            for row in ops:
                if row['op'] not in ('NEW','NEW2','NEW3'):continue
                args=row['args']
                if len(args)<2 or not all(isinstance(v,int) for v in args[:2]) or args[0] not in self.entries or not 0<=args[0]<251 or not 0<=args[1]<=255:
                    self.unresolved.append({'sid':source[0],'tag':source[1],'offset':row['off'],'reason':'dynamic or party-slot creation'});continue
                sid,uid=args[:2];uid=uid or sid
                self.instances[sid].add(uid);self.creations.append(Creation(sid,uid,source,row['off'],row['op']))
        self.by_uid=defaultdict(set)
        for sid,uids in self.instances.items():
            for uid in uids:self.by_uid[uid].add(sid)
        self.players={(sid,uid) for sid,uids in self.instances.items() if any(row['op']=='CC' for (owner,tag),ops in self.templates.items() if owner==sid for row in ops) for uid in uids}
        self.functions={(sid,uid,tag):ops for (sid,tag),ops in self.templates.items() for uid in self.instances.get(sid,{-1})}
        self._calls=defaultdict(list);self._callers=defaultdict(list);self._paths={}
        for source,ops in self.functions.items():
            for row in ops:
                if row['op'] not in ('REQ','REQSW','REQEW'):continue
                args=row['args']
                if len(args)<3 or not isinstance(args[1],int) or not isinstance(args[2],int):
                    self.unresolved.append({'sid':source[0],'uid':source[1],'tag':source[2],'offset':row['off'],'reason':'dynamic request target or tag'});continue
                for target in self.resolve(source,args[1],args[2]):
                    edge=RequestEdge(source,target,row['off'],row['op']);self._calls[source].append(edge);self._callers[target].append(edge)

    def resolve(self,source,uid,tag):
        if uid==255:owners={(source[0],source[1])}
        elif uid==250:owners=self.players
        elif 251<=uid<=254:
            # Party slots are selected at runtime. They are not script IDs 251+.
            self.unresolved.append({'sid':source[0],'uid':source[1],'tag':source[2],'reason':'runtime party-slot request','recipient':uid,'requestedTag':tag});owners=set()
        else:owners={(sid,uid) for sid in self.by_uid.get(uid,())}
        return sorted((sid,instance,tag) for sid,instance in owners if (sid,instance,tag) in self.functions)

    def calls(self,node):return list(self._calls.get(node,()))

    def player_paths(self,node):
        if node in self._paths:return self._paths[node]
        result=[];pending=[(node,(),frozenset())];visited=0
        while pending:
            current,edges,seen=pending.pop();visited+=1
            if visited>8192:
                self.unresolved.append({'sid':node[0],'uid':node[1],'tag':node[2],'reason':'request-path budget exceeded'});break
            if current in seen:continue
            if current[2] in (2,3):
                result.append(RequestPath(current,edges));continue
            if len(edges)>=64:
                self.unresolved.append({'sid':node[0],'uid':node[1],'tag':node[2],'reason':'request path deeper than native analysis limit'});continue
            for edge in self._callers.get(current,()):pending.append((edge.source,(edge,)+edges,seen|{current}))
        self._paths[node]=result;return result
