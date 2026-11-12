import struct
def packets(data):
 out=[];pending=bytearray();off=0
 while off<len(data):
  assert data[off:off+4]==b'OggS';n=data[off+26];sizes=data[off+27:off+27+n];at=off+27+n
  for sz in sizes:
   pending.extend(data[at:at+sz]);at+=sz
   if sz<255:out.append(bytes(pending));pending.clear()
  off=at
 assert not pending
 return out
if __name__=='__main__':
 from fsb5.vorbis import get_header_info
 from pathlib import Path
 p=packets(Path('work/voices/test.ogg').read_bytes());print('setup match',p[2]==get_header_info(37,1,48000)[2],len(p[2]));print([len(x) for x in p[3:]])
