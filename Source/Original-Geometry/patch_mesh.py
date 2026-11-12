import UnityPy,numpy as np,hashlib,json,struct
from pathlib import Path
from UnityPy.helpers.MeshHelper import MeshHandler
src=Path('game/Untitled Goose Game (2019)/Untitled_Data/sharedassets2.assets');data=bytearray(src.read_bytes());e=UnityPy.load(str(src));a=np.load('work/rex.npz');v=a['v'];count=len(v)
def pad16(b):return b+b'\0'*((-len(b))%16)
s0=np.c_[v,a['n'],np.tile([1.,0.,0.,-1.],(count,1))].astype('<f4').tobytes()
s1=b''.join(bytes(c)+struct.pack('<ff',0.,0.) for c in a['c'])
s2=b''.join(struct.pack('<4f4I',*w,*bi) for w,bi in zip(a['bw'],a['bi']))
vertex=pad16(s0)+pad16(s1)+pad16(s2)
center=(v.max(0)+v.min(0))/2;extent=(v.max(0)-v.min(0))/2
bounds={'m_Center':dict(zip('xyz',center)),'m_Extent':dict(zip('xyz',extent))}
patches=[];out=Path('Tiny-Rex-Mod');out.mkdir(exist_ok=True)
for pid in [288,323]:
 o=next(o for o in e.objects if o.path_id==pid);t=o.read_typetree()
 t['m_VertexData']['m_VertexCount']=count;t['m_VertexData']['m_DataSize']=vertex
 t['m_IndexBuffer']=a['f'].astype('<u2').tobytes();t['m_LocalAABB']=bounds
 t['m_SubMeshes']=[dict(firstByte=0,indexCount=a['f'].size,topology=0,baseVertex=0,firstVertex=0,vertexCount=count,localAABB=bounds)]
 raw=o.save_typetree(t);assert len(raw)<=o.byte_size
 payload=raw+b'\0'*(o.byte_size-len(raw));name=f'rex-{pid}.bin';(out/name).write_bytes(payload)
 # update the object table byte count to the actual new serialized size
 entry=struct.pack('<qII',pid,o.byte_start-o.assets_file.header.data_offset,o.byte_size)
 at=data.find(entry,0,o.assets_file.header.data_offset);assert at>=0 and data.find(entry,at+1,o.assets_file.header.data_offset)==-1
 meta=struct.pack('<I',len(raw));metaname=f'rex-{pid}-size.bin';(out/metaname).write_bytes(meta);data[at+12:at+16]=meta
 patches.append(dict(offset=at+12,size=4,file=metaname,sha256=hashlib.sha256(meta).hexdigest()))
 patches.append(dict(offset=o.byte_start,size=o.byte_size,file=name,sha256=hashlib.sha256(payload).hexdigest()))
 data[o.byte_start:o.byte_start+o.byte_size]=payload
# reparse entire patched asset and compare decoded mesh to source
Path('work/patched.assets').write_bytes(data);check=UnityPy.load('work/patched.assets')
for pid in [288,323]:
 ob=next(o for o in check.objects if o.path_id==pid);h=MeshHandler(ob.read());h.process()
 assert np.allclose(h.m_Vertices,v,atol=1e-5);assert np.allclose(h.m_BoneWeights,a['bw']);assert np.array_equal(h.m_BoneIndices,a['bi']);assert np.array_equal(np.array(h.get_triangles()).reshape(-1,3),a['f']);print('Verified',pid,len(h.m_Vertices),'vertices')
manifest=dict(target='Untitled_Data/sharedassets2.assets',original_sha256=hashlib.sha256(src.read_bytes()).hexdigest(),patched_sha256=hashlib.sha256(data).hexdigest(),patches=patches)
(out/'patch.json').write_text(json.dumps(manifest,indent=2))
