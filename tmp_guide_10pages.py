from pathlib import Path
from PIL import Image
from reportlab.pdfgen import canvas
from reportlab.lib.utils import ImageReader
from pypdf import PdfReader

root=Path(r'D:/GitRepos/ScannerProject')
output=root/'output/pdf/Elite_100_mini_A4_10pages.pdf'
output.parent.mkdir(parents=True,exist_ok=True)
W,H=595.275591,841.889764
margin=28.346457
c=canvas.Canvas(str(output),pagesize=(W,H))
c.setTitle('Elite 100 mini - A4 - 10 pages')
for side in (1,2):
    im=Image.open(root/f'guide_full-{side}.png')
    for panel in range(5):
        cut=im.crop((round(panel*im.width/5),0,round((panel+1)*im.width/5),im.height))
        scale=min((W-2*margin)/cut.width,(H-2*margin)/cut.height)
        ww,hh=cut.width*scale,cut.height*scale
        c.drawImage(ImageReader(cut),(W-ww)/2,(H-hh)/2,width=ww,height=hh)
        c.showPage()
c.save()
r=PdfReader(output)
assert len(r.pages)==10
assert all(abs(float(p.mediabox.width)-W)<0.01 and abs(float(p.mediabox.height)-H)<0.01 for p in r.pages)
thumbs=[]
for side in (1,2):
    im=Image.open(root/f'guide_full-{side}.png')
    for panel in range(5):
        cut=im.crop((round(panel*im.width/5),0,round((panel+1)*im.width/5),im.height))
        cut.thumbnail((180,270))
        thumbs.append(cut.copy())
print('Verified 10 A4 portrait pages:',output)
