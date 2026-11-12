from pathlib import Path
import struct,json,hashlib,subprocess,re,sys,wave,numpy as np
import fsb5
from fsb5.vorbis import get_header_info
sys.path.insert(0,str(Path('work').resolve()))
from ogg_packets import packets
root=Path('Chompy-Roar');source=Path('game/Untitled Goose Game (2019)/Untitled_Data/StreamingAssets/Master Bank.bank');original=source.read_bytes();start=original.index(b'FSB5');fsb=fsb5.FSB5(original[start:]);data_start=start+fsb.header.size+fsb.header.sampleHeadersSize+fsb.header.nameTableSize
with wave.open(str(root/'Chompy-Roar-Preview.wav'),'rb') as wav:
 assert wav.getframerate()==48000 and wav.getnchannels()==1 and wav.getsampwidth()==2
 frames=wav.getnframes();pcm=np.frombuffer(wav.readframes(frames),'<i2');assert frames==96000
# Preserve the supplied recording's pitch/timbre; encode with the game's existing Vorbis setup.
ogg=subprocess.run(['ffmpeg','-v','error','-i',str(root/'Chompy-Roar-Preview.wav'),'-c:a','libvorbis','-q:a','3','-f','ogg','pipe:1'],capture_output=True,check=True).stdout
pk=packets(ogg);assert pk[2]==get_header_info(37,1,48000)[2]
encoded=b''.join(struct.pack('<H',len(p))+p for p in pk[3:])+b'\0\0';encoded=encoded.ljust((len(encoded)+15)//16*16,b'\0')
indices={i for i,s in enumerate(fsb.samples) if re.fullmatch(r'sfx_goose(?:B)?_honk_.*',s.name)}
old_headers=original[start+fsb.header.size:start+fsb.header.size+fsb.header.sampleHeadersSize]
header=bytearray(original[start:start+fsb.header.size]);sample_headers=bytearray();audio_body=bytearray();relative_copies=[];relative_patches=[];at=0;payload=bytearray(encoded)
for i,sample in enumerate(fsb.samples):
 raw=struct.unpack_from('<Q',old_headers,at)[0];at+=8;more=raw&1;chunks=bytearray()
 while more:
  code=struct.unpack_from('<I',old_headers,at)[0];at+=4;more=code&1;size=(code>>1)&0xffffff;kind=code>>25
  data=old_headers[at:at+size];at+=size
  if i in indices and kind==11:
   assert struct.unpack_from('<I',data)[0]==894604746
   data=data[:4]+b'\0'*4 # discard old seek-table entries, which refer to the old recording
  if i in indices and kind==13 and size==4:data=struct.pack('<f',float(np.max(np.abs(pcm.astype(float))))/32768)
  code=(code&~(0xffffff<<1))|(len(data)<<1);chunks.extend(struct.pack('<I',code)+data)
 new_offset=len(audio_body)
 raw=(raw&~(((1<<28)-1)<<6))|((new_offset//16)<<6)
 if i in indices:
  assert sample.frequency==48000 and sample.channels==1
  raw=(raw&((1<<34)-1))|(frames<<34);audio=encoded
  relative_patches.append(dict(offset=new_offset,size=len(encoded),payload_offset=0))
 else:
  audio=sample.data;relative_copies.append(dict(offset=new_offset,source_offset=data_start+sample.dataOffset,size=len(audio)))
 sample_headers.extend(struct.pack('<Q',raw)+chunks);assert len(audio)%16==0;audio_body.extend(audio)
assert at==len(old_headers)
struct.pack_into('<I',header,12,len(sample_headers));struct.pack_into('<I',header,20,len(audio_body))
old_names_start=start+fsb.header.size+fsb.header.sampleHeadersSize
names=original[old_names_start:old_names_start+fsb.header.nameTableSize]
new_names_start=start+len(header)+len(sample_headers);new_data_start=new_names_start+len(names)
output=bytearray(original[:start]+header+sample_headers+names+audio_body)
copies=[dict(offset=0,source_offset=0,size=start),dict(offset=new_names_start,source_offset=old_names_start,size=len(names))]
for cp in relative_copies:cp['offset']+=new_data_start;copies.append(cp)
patches=[]
for pp in relative_patches:pp['offset']+=new_data_start;patches.append(pp)
def patch(offset,data):
 patches.append(dict(offset=offset,size=len(data),payload_offset=len(payload)));payload.extend(data);output[offset:offset+len(data)]=data
patch(start,header+sample_headers);patch(4,struct.pack('<I',len(output)-8));patch(start-4,struct.pack('<I',len(output)-start));patch(899672,struct.pack('<I',len(output)-start))
check=fsb5.FSB5(bytes(output[start:]));assert len(output)==start+check.raw_size
for i,s in enumerate(check.samples):
 if i in indices:
  assert s.samples==frames
  rebuilt=bytes(check.rebuild_sample(s));decoded=subprocess.run(['ffmpeg','-v','error','-i','pipe:0','-f','s16le','pipe:1'],input=rebuilt,capture_output=True,check=True).stdout
  a=np.frombuffer(decoded,'<i2')[:frames].astype(float);assert len(a)==frames;assert np.sqrt(np.mean(a*a))>1000
 else:assert s.data==fsb.samples[i].data and s.samples==fsb.samples[i].samples and s.metadata==fsb.samples[i].metadata
(root/'Payload/roar.bin').write_bytes(payload)
manifest=dict(target='Untitled_Data/StreamingAssets/Master Bank.bank',original_sha256=hashlib.sha256(original).hexdigest(),patched_sha256=hashlib.sha256(output).hexdigest(),output_size=len(output),payload_sha256=hashlib.sha256(payload).hexdigest(),copies=copies,patches=patches,replaced_calls=len(indices),sample_frames=frames,sample_rate=48000)
(root/'patch.json').write_text(json.dumps(manifest,indent=2));Path('work/Chompy-Roar.bank').write_bytes(output)
print('Verified',len(indices),'two-second dinosaur calls; all other samples retained. Payload bytes:',len(payload))
