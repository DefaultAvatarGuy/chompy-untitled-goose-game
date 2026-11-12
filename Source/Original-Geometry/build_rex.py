import numpy as np,json,math
V=[];F=[];C=[];B=[];W=[]
green=(98,153,83,255); light=(172,191,117,255); dark=(62,109,62,255);black=(28,35,30,255);ivory=(242,232,201,255)
def add(v,f,c,b):
 off=len(V);V.extend(v);F.extend([[off+i for i in t] for t in f]);C.extend([c]*len(v))
 for p in v:
  w=b(p) if callable(b) else {b:1};ids=list(w);ws=list(w.values());B.append((ids+[0]*4)[:4]);W.append((ws+[0]*4)[:4])
def ell(center,scale,color,bone,n=16,r=10,power=1):
 v=[];f=[]
 def sp(x):return math.copysign(abs(x)**power,x)
 for j in range(r+1):
  lat=-math.pi/2+math.pi*j/r
  for i in range(n):
   a=2*math.pi*i/n
   v.append([center[0]+scale[0]*sp(math.cos(lat))*sp(math.cos(a)),center[1]+scale[1]*sp(math.cos(lat))*sp(math.sin(a)),center[2]+scale[2]*sp(math.sin(lat))])
 for j in range(r):
  for i in range(n):
   a=j*n+i;b=j*n+(i+1)%n;c=(j+1)*n+(i+1)%n;d=(j+1)*n+i
   if j>0:f.append([a,b,c])
   if j<r-1:f.append([a,c,d])
 add(v,f,color,bone)
def tube(points,radii,bones,color,n=14):
 v=[];f=[]
 for j,p in enumerate(points):
  tangent=np.array(points[min(j+1,len(points)-1)])-np.array(points[max(j-1,0)])
  tangent=tangent/np.linalg.norm(tangent);u=np.array([1.,0,0]);q=np.cross(tangent,u)
  for i in range(n):v.append((np.array(p)+radii[j]*(math.cos(i*2*math.pi/n)*u+math.sin(i*2*math.pi/n)*q)).tolist())
 for j in range(len(points)-1):
  for i in range(n):
   a=j*n+i;b=j*n+(i+1)%n;c=(j+1)*n+(i+1)%n;d=(j+1)*n+i;f.extend([[a,b,c],[a,c,d]])
 # compute outward orientation
 for i in range(1,n-1):f.append([0,i+1,i]);a=(len(points)-1)*n;f.append([a,a+i,a+i+1])
 off=len(V);add(v,f,color,0)
 for j,weights in enumerate(bones):
  for i in range(n):
   idx=off+j*n+i;B[idx]=(list(weights)+[0]*4)[:4];W[idx]=(list(weights.values())+[0]*4)[:4]
# body and tummy
ell((0,-1.4,6.0),(2.65,2.75,3.6),green,0,n=20,r=14)
ell((0,.65,6.1),(1.85,.72,2.8),light,0,n=18,r=12)
# neck following all six live neck joints
pts=[(0,.0,7.1),(0,.6,8.3),(0,.8,9.1),(0,.6,10),(0,.15,10.8),(0,-.15,11.7),(0,-.2,12.5),(0,.1,13)]
tube(pts,[1.8,1.5,1.3,1.15,1.08,1.04,1.1,1.2],[{1:1},{2:1},{3:1},{4:1},{5:1},{6:1},{7:1},{8:1}],green)
# rounded boxy skull, muzzle, lower jaw; mouth ends at original grasp point
ell((0,.35,13.2),(2.15,1.8,1.9),green,8,n=20,r=12,power=.58)
ell((0,1.9,13.8),(1.95,1.03,.9),green,9,n=20,r=10,power=.50)
ell((0,1.77,13.05),(1.80,1.01,.36),light,10,n=20,r=8,power=.55)
# small dot eyes on sides, nostrils, mouth corners
for s in [-1,1]:
 ell((s*2.08,.20,13.9),(.16,.30,.31),black,8,n=12,r=8)
 ell((s*1.91,2.24,14.02),(.06,.15,.105),dark,9,n=10,r=6)
 ell((s*1.89,1.8,13.4),(.035,.84,.045),dark,9,n=12,r=6)
 # powerful thighs, short shins and three toes
 thigh=19 if s==-1 else 23;calf=20 if s==-1 else 24;ankle=21 if s==-1 else 25;toe=22 if s==-1 else 26
 ell((s*1.9,-.8,3.6),(1.15,1.35,1.95),green,thigh,n=16,r=10)
 ell((s*1.8,-.84,1.53),(.57,.60,1.35),green,calf,n=14,r=10)
 ell((s*1.85,-.30,.37),(.80,.95,.38),green,ankle,n=14,r=8)
 for x in [-.47,0,.47]:
  ell((s*1.85+x,.56,.25),(.24,1.05,.25),green,toe,n=12,r=8)
  ell((s*1.85+x,1.40,.24),(.16,.30,.15),ivory,toe,n=10,r=6)
 # tiny wing-driven arms near shoulder; fingers ride upper wing only
 arm=12 if s==-1 else 16
 tube([(s*.8,.15,7.3),(s*2.,-.7,7.8),(s*3.,-.15,8.1),(s*3.45,.35,7.85)],[.48,.42,.30,.23],[{11 if s==-1 else 15:1},{arm:1},{arm:1},{arm:1}],green,n=10)
 for z in [7.66,8.0]:ell((s*3.55,.62,z),(.25,.50,.12),green,arm,n=10,r=6)
# long tapered tail
pts=[(0,-2.8,5.5),(0,-4.1,5.5),(0,-5.7,5.2),(0,-7.2,4.9),(0,-8.7,4.6),(0,-10.0,4.4)]
tube(pts,[1.6,1.3,1.,.7,.37,.025],[{27:1},{27:.6,28:.4},{28:1},{28:1},{28:1},{28:1}],green,n=16)
# subtle back ridges, no aggressive spikes
for y,z,sc,b in [(-3.5,8.1,.6,0),(-4.5,6.8,.5,27),(-5.8,6.2,.4,28),(-7.2,5.6,.3,28)]:ell((0,y,z),(.24,sc,sc*.7),dark,b,n=10,r=6)
v=np.array(V);f=np.array(F);normal=np.zeros_like(v)
for t in f:
 a,b,c=v[t];n=np.cross(b-a,c-a)
 for i in t:normal[i]+=n
normal/=np.maximum(np.linalg.norm(normal,axis=1)[:,None],1e-10)
np.savez('work/rex.npz',v=v,f=f,n=normal,c=np.array(C,dtype='uint8'),bi=np.array(B),bw=np.array(W))
json.dump({'v':V,'f':F,'c':C,'bi':B,'bw':W},open('work/rex.json','w'))
print('Rex vertices',len(V),'triangles',len(F),'bounds',v.min(0),v.max(0))
