from pathlib import Path
import numpy as np,json,struct,hashlib
import UnityPy
from UnityPy.helpers.MeshHelper import MeshHandler
# Reuse the original geometric primitives without changing the original Chompy source.
source=Path('work/build_rex.py').read_text();exec(source[:source.index('# body and tummy')])
old=np.load('work/rex.npz');S=np.load('work/skin.npy');F=np.linalg.inv(S[0])@S
bp=json.loads(Path('work/mesh288.json').read_text())['m_BindPose'];bp=[np.array([[d[f'e{x}{y}'] for y in range(4)] for x in range(4)]) for d in bp]
# Relative wing orientation in the source folded pose; remove the hidden-wing scale.
chest=np.linalg.inv(bp[1]);chest_now=S[1]@chest
from scipy.spatial.transform import Rotation
scene=UnityPy.load('game/Untitled Goose Game (2019)/Untitled_Data/level2');objects={o.path_id:o for o in scene.objects}
transforms={o.path_id:o.read_typetree() for o in scene.objects if o.type.name=='Transform'}
refs=objects[57400].read_typetree()['m_Bones'];world_quats={}
def world_rotation(tid):
 if tid==0:return Rotation.identity()
 if tid not in world_quats:
  t=transforms[tid];q=t['m_LocalRotation'];world_quats[tid]=world_rotation(t['m_Father']['m_PathID'])*Rotation.from_quat([q[k] for k in 'xyzw'])
 return world_quats[tid]
specs=[];raws=[]
for side,bi,bone_name in [(-1,12,'upperwing_r'),(1,16,'upperwing_l')]:
 V.clear();FACES=F;F=[];C.clear();B.clear();W.clear()
 pivot=np.array([side*1.8,.25,7.65])
 tube([tuple(pivot),(side*2.55,1.0,7.35),(side*2.65,1.65,6.95)],[.33,.28,.23],[{1:1}]*3,green,n=12)
 ell((side*2.58,1.84,6.92),(.32,.45,.29),green,1,n=12,r=8)
 for xx,zz in [(-.17,6.92),(.17,6.78)]:
  ell((side*2.58+xx,2.17,zz),(.13,.43,.13),green,1,n=10,r=6)
  ell((side*2.58+xx,2.52,zz),(.08,.21,.08),ivory,1,n=10,r=6)
 v=np.array(V);faces=np.array(F);colors=np.array(C,dtype='uint8');raws.append((v.copy(),faces.copy(),colors.copy()))
 local=(bp[1]@np.c_[v,np.ones(len(v))].T).T[:,:3];p=(bp[1]@np.r_[pivot,1])[:3];local-=p
 normals=np.zeros_like(local)
 for tri in faces:
  a,b,c=local[tri];n=np.cross(b-a,c-a)
  for idx in tri:normals[idx]+=n
 normals/=np.maximum(np.linalg.norm(normals,axis=1)[:,None],1e-8)
 q=(world_rotation(refs[1]['m_PathID']).inv()*world_rotation(refs[bi]['m_PathID'])).as_quat()
 axis=bp[1][:3,:3]@np.array([side,0,0]);axis/=np.linalg.norm(axis)
 specs.append(dict(name='Left' if side==1 else 'Right',bone=bone_name,pivot=p.tolist(),vertices=local.reshape(-1).tolist(),normals=normals.reshape(-1).tolist(),triangles=faces.reshape(-1).tolist(),colors=colors.reshape(-1).astype(int).tolist(),foldedWing=q.tolist(),flapAxis=axis.tolist()))
 F=FACES
out=Path('Chompy-Playtime/Payload/BepInEx/plugins/TinyRexPlaytime/Chompy-Arms.json');out.write_text(json.dumps(dict(arms=specs),separators=(',',':')))
# Remove only the hidden old arm triangles from the original working mesh.
original=Path('game/Untitled Goose Game (2019)/Untitled_Data/sharedassets2.assets').read_bytes();old_manifest=json.loads(Path('work/arms06/OriginalModel/patch.json').read_text());old_data=bytearray(original)
for p in old_manifest['patches']:old_data[p['offset']:p['offset']+p['size']]=(Path('work/arms06/OriginalModel')/p['file']).read_bytes()
Path('work/arms06/v01.assets').write_bytes(old_data);eold=UnityPy.load('work/arms06/v01.assets');ebase=UnityPy.load(original);base_objs={o.path_id:o for o in ebase.objects};output=bytearray(original);patches=[];new_tri=None
for pid in [288,323]:
 oo=next(o for o in eold.objects if o.path_id==pid);t=oo.read_typetree();h=MeshHandler(oo.read());h.process();indices=np.array(h.get_triangles()).reshape(-1,3);weights=np.array(h.m_BoneWeights);bones=np.array(h.m_BoneIndices)
 is_arm=(np.where(np.isin(bones,[11,12,15,16]),weights,0).sum(1)>.99);tri_mask=is_arm[indices];assert not np.any(tri_mask.any(1)^tri_mask.all(1))
 kept=indices[~tri_mask.all(1)];new_tri=kept
 t['m_IndexBuffer']=kept.astype('<u2').tobytes();t['m_SubMeshes'][0]['indexCount']=kept.size
 o=base_objs[pid];raw=o.save_typetree(t);assert len(raw)<=o.byte_size;payload=raw.ljust(o.byte_size,b'\0');file=f'rex-{pid}.bin';Path('Chompy-Playtime/Model',file).write_bytes(payload)
 entry=struct.pack('<qII',pid,o.byte_start-o.assets_file.header.data_offset,o.byte_size);at=output.find(entry,0,o.assets_file.header.data_offset);assert at>=0
 meta=struct.pack('<I',len(raw));mf=f'rex-{pid}-size.bin';Path('Chompy-Playtime/Model',mf).write_bytes(meta)
 for offset,name,blob in [(at+12,mf,meta),(o.byte_start,file,payload)]:
  output[offset:offset+len(blob)]=blob;patches.append(dict(offset=offset,size=len(blob),file=name,sha256=hashlib.sha256(blob).hexdigest()))
 print(pid,'hidden arm triangles removed:',len(indices)-len(kept),'other triangles retained:',len(kept))
Path('work/arms06/new.assets').write_bytes(output);check=UnityPy.load('work/arms06/new.assets')
for pid in [288,323]:
 oldo=next(o for o in eold.objects if o.path_id==pid);newo=next(o for o in check.objects if o.path_id==pid);a=oldo.read_typetree();b=newo.read_typetree()
 for key in a:
  if key not in ['m_IndexBuffer','m_SubMeshes']:assert a[key]==b[key],key
 print(pid,'all original vertex data, colors, weights, bind poses, and shapes preserved')
m=dict(target=old_manifest['target'],original_sha256=old_manifest['original_sha256'],patched_sha256=hashlib.sha256(output).hexdigest(),patches=patches);Path('Chompy-Playtime/Model/patch.json').write_text(json.dumps(m,indent=2))
# Exact folded-pose preview of the body and new chest-mounted arms.
Fold=np.linalg.inv(S[0])@S;v=np.c_[old['v'],np.ones(len(old['v']))];body=np.zeros_like(v)
for j in range(4):body+=np.einsum('nij,nj->ni',Fold[old['bi'][:,j]],v)*old['bw'][:,j,None]
vv=body[:,:3].tolist();ff=new_tri.tolist();cc=old['c'].tolist()
for v,f,c in raws:
 placed=(Fold[1]@np.c_[v,np.ones(len(v))].T).T[:,:3];offset=len(vv);vv+=placed.tolist();ff+=(f+offset).tolist();cc+=c.tolist()
Path('work/arms06/preview.json').write_text(json.dumps(dict(v=vv,f=ff,c=cc)))
print('New arm vertices:',[len(s['vertices'])//3 for s in specs])
