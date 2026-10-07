from PIL import Image
from reportlab.pdfgen import canvas
from reportlab.lib.utils import ImageReader
from pathlib import Path
out=Path(r'D:/GitRepos/ScannerProject/output/pdf/Elite_100_mini_A4_5pages.pdf')
out.parent.mkdir(parents=True,exist_ok=True)
imgs=[Image.open(r'D:/GitRepos/ScannerProject/guide_full-1.png'),Image.open(r'D:/GitRepos/ScannerProject/guide_full-2.png')]
W,H=841.889764,595.275591
c=canvas.Canvas(str(out),pagesize=(W,H))
for i in range(5):
    for side,j in [(0,i),(1,4-i)]:
        im=imgs[side]
        cut=im.crop((round(j*im.width/5),0,round((j+1)*im.width/5),im.height))
        scale=min((W-60)/2/cut.width,(H-40)/cut.height)
        ww,hh=cut.width*scale,cut.height*scale
        xx=20+side*(W-20)/2+((W-60)/2-ww)/2
        c.drawImage(ImageReader(cut),xx,(H-hh)/2,width=ww,height=hh)
    c.showPage()
c.save()
print(out)
