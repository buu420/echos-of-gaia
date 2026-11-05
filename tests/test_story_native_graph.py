"""Runtime UID ownership and causal request paths from actual decoded events."""
import importlib.util
import json
from pathlib import Path
import unittest

ROOT=Path(__file__).resolve().parents[1]

def module():
    spec=importlib.util.spec_from_file_location('story_native_graph',ROOT/'tools/story_native_graph.py')
    value=importlib.util.module_from_spec(spec);spec.loader.exec_module(value);return value

def op(name,args,off=1):return {'op':name,'args':args,'off':off,'next':off+1}
def entry(functions,kind=2):return {'type':kind,'functions':{str(k):v for k,v in functions.items()}}

class NativeStoryRequestGraphTests(unittest.TestCase):
    def test_pinnacle_request_129_runs_script_2_on_instance_129(self):
        native=json.loads((ROOT/'tests/Fixtures/story-request-alias.json').read_text())
        graph=module().NativeEventGraph(native['entries'])
        calls=graph.calls((8,130,3))
        self.assertIn((2,129,46),[call.target for call in calls if call.offset==1113])
        self.assertNotIn((129,129,46),[call.target for call in calls])
        self.assertIn((2,128,46),graph.functions)

    def test_self_requests_preserve_shared_script_instance(self):
        entries={'0':entry({0:[op('NEW3',[2,128]),op('NEW3',[2,129],2)]},0),
                 '2':entry({3:[op('REQSW',[6,255,46],10)],46:[op('rsv04',[],20)]})}
        graph=module().NativeEventGraph(entries)
        self.assertEqual([(2,129,46)],[call.target for call in graph.calls((2,129,3))])
        self.assertEqual([(2,128,46)],[call.target for call in graph.calls((2,128,3))])

    def test_default_uid_zero_means_the_created_script_id(self):
        entries={'0':entry({0:[op('NEW2',[8,0])]},0),'8':entry({2:[op('rsv04',[])]},1)}
        graph=module().NativeEventGraph(entries)
        self.assertIn((8,8,2),graph.functions)
        self.assertNotIn((8,0,2),graph.functions)

    def test_reused_uid_keeps_each_possible_script_owner(self):
        entries={'0':entry({0:[op('NEW3',[2,129]),op('NEW3',[3,129],2)],3:[op('REQSW',[6,129,46],5)]},0),
                 '2':entry({46:[op('rsv04',[])]}),'3':entry({46:[op('rsv04',[])]})}
        graph=module().NativeEventGraph(entries)
        self.assertEqual({(2,129,46),(3,129,46)},{call.target for call in graph.calls((0,0,3))})

    def test_request_paths_are_not_silently_cut_off_after_four_calls(self):
        entries={'0':entry({0:[op('NEW3',[sid,0],sid) for sid in range(1,8)]},0)}
        entries.update({str(sid):entry({3 if sid==1 else 10:[op('REQSW',[6,sid+1,10],10*sid)]}) for sid in range(1,7)})
        entries['7']=entry({10:[op('rsv04',[],70)]})
        paths=module().NativeEventGraph(entries).player_paths((7,7,10))
        self.assertEqual(1,len(paths));self.assertEqual((1,1,3),paths[0].root)
        self.assertEqual(6,len(paths[0].edges))

    def test_dynamic_creation_is_recorded_without_inventing_a_default_instance(self):
        entries={'0':entry({0:[op('NEW3',[2,{'expr':[[0x7a,'01'],[0x7f,'']]}])]},0),'2':entry({3:[op('rsv04',[])]})}
        graph=module().NativeEventGraph(entries)
        self.assertNotIn((2,2,3),graph.functions)
        self.assertTrue(graph.unresolved)

    def test_player_alias_tracks_control_assignments_outside_init(self):
        entries={'0':entry({0:[op('NEW3',[2,128])],3:[op('REQSW',[6,250,10],10)]},0),
                 '2':entry({1:[op('CC',[],20)],10:[op('rsv04',[],30)]})}
        calls=module().NativeEventGraph(entries).calls((0,0,3))
        self.assertEqual([(2,128,10)],[call.target for call in calls])

if __name__=='__main__':unittest.main()
