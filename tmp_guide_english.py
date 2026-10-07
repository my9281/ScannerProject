from pathlib import Path
from PIL import Image
from reportlab.pdfgen import canvas
from reportlab.lib.utils import ImageReader
from pypdf import PdfReader

root=Path(r'D:/GitRepos/ScannerProject')
out=root/'output/pdf/Elite_100_mini_A4_English.pdf'
W,H=595.275591,841.889764
margin=28.346457
c=canvas.Canvas(str(out),pagesize=(W,H))
c.setTitle('Elite 100 mini - English Quick Start Guide')
images={s:Image.open(root/f'guide_full-{s}.png') for s in (1,2)}
# Cover, box/app/firmware, charging, power/discharging, display, support.
order=[(1,4),(2,3),(2,2),(2,1),(2,0),(2,4)]
for side,panel in order:
    im=images[side]
    cut=im.crop((round(panel*im.width/5),0,round((panel+1)*im.width/5),im.height))
    scale=min((W-2*margin)/cut.width,(H-2*margin)/cut.height)
    ww,hh=cut.width*scale,cut.height*scale
    c.drawImage(ImageReader(cut),(W-ww)/2,(H-hh)/2,width=ww,height=hh)
    c.showPage()
c.save()
r=PdfReader(out)
assert len(r.pages)==6
assert all(abs(float(p.mediabox.width)-W)<.01 and abs(float(p.mediabox.height)-H)<.01 for p in r.pages)
print('Verified 6 A4 portrait pages:',out)
