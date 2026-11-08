import sys, pikepdf
sys.path.insert(0,'/tmp/cert/work')
from sp import lines, subpaths
from PIL import Image, ImageDraw, ImageFont
from reportlab.pdfgen import canvas
import io, json

FONT_B='/home/claude/zu/src/ZuLms.Infrastructure/Assets/Fonts/Tajawal-Bold.ttf'
FONT_R='/home/claude/zu/src/ZuLms.Infrastructure/Assets/Fonts/Tajawal-Regular.ttf'

def blk(a,b): return lines[a:b+1]

def filtered(a,b,xmin):
    sp,i=subpaths(a,b)
    head=lines[a:i]                                  # q, clip rect, W* n, rg
    out=list(head)
    for (s,e,x0,x1,y0,y1) in sp:
        if x0>=xmin: out+=lines[s:e+1]
    out+=['f','Q']; return out

def shift(block,dx_pt,dy_pt): return ['q',f'1 0 0 1 {dx_pt/0.75:.3f} {dy_pt/0.75:.3f} cm']+block+['Q']

header=lines[:108]
body=header+blk(108,833)+blk(834,965)                      # "تشهد جامعة الزرقاء بأن"
# "قد شارك في" + "فعاليات"  (phrase spans 479..642pt, centre 560.5)
phrase=blk(1662,2081)+filtered(2082,3798,478)
body+=shift(phrase,396-560.5,-10)
# "خلال الفترة" (drop the word "من"; spans 479..570, centre 524.5)
lab=filtered(4793,5310,475)
body+=shift(lab,396-524.5,22)

pdf=pikepdf.open('/tmp/cert/t.pdf'); pg=pdf.pages[0]
pg.contents_coalesce(); pg.Contents.write('\n'.join(body).encode('latin1'))
pdf.save('/tmp/cert/work/base.pdf')

# ---------- raster labels (Arabic shaped by libraqm) ----------
SC=16  # px per pt
def label_png(text,size_pt,font):
    f=ImageFont.truetype(font,int(size_pt*SC),layout_engine=ImageFont.Layout.RAQM)
    tmp=Image.new('RGBA',(4,4)); d=ImageDraw.Draw(tmp)
    l,t,r,b=d.textbbox((0,0),text,font=f,direction='rtl',language='ar')
    im=Image.new('RGBA',(r-l+8,b-t+8),(0,0,0,0)); d=ImageDraw.Draw(im)
    d.text((4-l,4-t),text,font=f,fill=(0,0,0,255),direction='rtl',language='ar')
    return im,(r-l+8)/SC,(b-t+8)/SC

buf=io.BytesIO(); c=canvas.Canvas(buf,pagesize=(792,612))
def put(text,size,font,cx,top):             # centred on cx, top edge at `top` (pt from top)
    im,w,h=label_png(text,size,font)
    b=io.BytesIO(); im.save(b,'PNG'); b.seek(0)
    from reportlab.lib.utils import ImageReader
    c.drawImage(ImageReader(b),cx-w/2,612-top-h,width=w,height=h,mask='auto')
    return w,h
CL,CR=290,510
put('المحاضر',10.5,FONT_B,CR,498); put('عدد الساعات',10.5,FONT_B,CL,498)
put('تاريخ الإصدار',10.5,FONT_B,CR,534); put('رقم الشهادة',10.5,FONT_B,CL,534)
put('للتحقق من الشهادة',8,FONT_R,692,553)
c.save(); buf.seek(0)

ov=pikepdf.open(buf); base=pikepdf.open('/tmp/cert/work/base.pdf')
base.pages[0].add_overlay(ov.pages[0])
base.save('/tmp/cert/work/Certificate_Template_Clean.pdf')
print('ok')
