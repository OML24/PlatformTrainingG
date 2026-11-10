import cv2, numpy as np, sys
# usage (from repo root): python tools/make-login-illustration.py out.png [source.jpg]
# save out.png as src/ZuLms.Web/wwwroot/img/login-campus.jpg (2400px) + a 1200px copy as login-campus-sm.jpg
SRC=sys.argv[2] if len(sys.argv)>2 else 'WhatsApp Image 2026-10-10 at 2.01.48 PM.jpeg'
img=cv2.imread(SRC)
img=cv2.fastNlMeansDenoisingColored(img,None,3,3,7,21)
S=1.5
img=cv2.resize(img,None,fx=S,fy=S,interpolation=cv2.INTER_LANCZOS4)
H,W=img.shape[:2]
hsv=cv2.cvtColor(img,cv2.COLOR_BGR2HSV).astype(np.float32)
h,s,v=hsv[...,0],hsv[...,1],hsv[...,2]
gray=cv2.cvtColor(img,cv2.COLOR_BGR2GRAY)
# local contrast so facades/arches keep their shading
clahe=cv2.createCLAHE(clipLimit=2.2,tileGridSize=(8,8)); g=clahe.apply(gray)
smooth=cv2.bilateralFilter(g,7,30,5)
sm=smooth.astype(np.float32)/255
yy=np.arange(H)[:,None]/H; xx=np.arange(W)[None,:]/W

# --- sky: everything reachable from the top edge without crossing a strong edge ---
bar=cv2.Canny(cv2.GaussianBlur(gray,(0,0),2.0),28,70)
bar=cv2.dilate(bar,np.ones((3,3),np.uint8))>0
warm=(h>5)&(h<35)&(s>38)            # beige stone facades never count as sky
cand=((yy<0.72)&(~bar)&(v>85)&(~warm)).astype(np.uint8)
n,lab=cv2.connectedComponents(cand,connectivity=4)
top=set(np.unique(lab[:4,:]))-{0}
sky=np.isin(lab,list(top)).astype(np.uint8)
sky=cv2.morphologyEx(sky,cv2.MORPH_CLOSE,np.ones((9,9),np.uint8))
sky=cv2.dilate(sky,np.ones((3,3),np.uint8))
sky=cv2.GaussianBlur(sky.astype(np.float32),(0,0),2.5)

# --- ink: fine XDoG + canny, two weights ---
f=sm
g1=cv2.GaussianBlur(f,(0,0),0.8*S); g2=cv2.GaussianBlur(f,(0,0),0.8*S*1.6)
D=g1-0.99*g2
ink=np.clip(-np.tanh(120*(D+0.004)),0,1)
ed=cv2.Canny(cv2.GaussianBlur(smooth,(0,0),1.0),35,95).astype(np.float32)/255
ink=np.maximum(ink*0.85,cv2.GaussianBlur(ed,(0,0),0.6)*0.9)
ink=ink*(1-sky)

# --- 5 flat tones (navy-grey), more levels = more legible forms ---
pal=np.array([[95,72,55],[140,118,100],[182,165,150],[214,203,193],[255,255,255]],np.float32)  # BGR dark->paper
q=np.digitize(sm,[0.22,0.40,0.58,0.76])
tone=pal[q]
tone=cv2.GaussianBlur(tone,(0,0),0.6)
paper=np.array([255,255,255],np.float32)
tone=tone*(1-sky[...,None])+paper*sky[...,None]

# --- flags in colour ---
def box(x0,y0,x1,y1):
    m=np.zeros((H,W),bool); m[int(y0*S):int(y1*S),int(x0*S):int(x1*S)]=True; return m
flag=np.zeros((H,W),np.uint8)
red=((h<9)|(h>170))&(s>80)&(v>60); green=(h>45)&(h<95)&(s>70)&(v>40)
for seed in [((red|green)&box(835,80,1130,360)), ((((h>95)&(h<125)&(s>40)&(v>110)))&box(1170,280,1390,400))]:
    m=cv2.morphologyEx(seed.astype(np.uint8),cv2.MORPH_CLOSE,np.ones((25,25),np.uint8))
    c,_=cv2.findContours(m,cv2.RETR_EXTERNAL,cv2.CHAIN_APPROX_SIMPLE)
    if c: cv2.drawContours(flag,[cv2.convexHull(max(c,key=cv2.contourArea))],-1,1,-1)
flagf=cv2.GaussianBlur(flag.astype(np.float32),(0,0),1.2)
col=hsv.copy(); col[...,1]=np.clip(col[...,1]*1.15,0,255); col[...,2]=np.clip(col[...,2]*1.03,0,255)
col=cv2.cvtColor(col.astype(np.uint8),cv2.COLOR_HSV2BGR).astype(np.float32)

INK=np.array([90,44,2],np.float32)
out=tone*(1-ink[...,None])+INK*ink[...,None]
out=out*(1-flagf[...,None])+col*flagf[...,None]
# soft bottom + outer-side fade only (inner side handled in CSS mask)
m=np.clip((0.99-yy)/0.12,0,1)*np.ones_like(xx)
m=cv2.GaussianBlur(m.astype(np.float32),(0,0),8)
out=out*m[...,None]+paper*(1-m[...,None])
cv2.imwrite(sys.argv[1],np.clip(out,0,255).astype(np.uint8))
