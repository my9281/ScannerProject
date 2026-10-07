from pypdf import PdfReader, PdfWriter, Transformation
from pypdf.generic import RectangleObject
from copy import deepcopy
from pathlib import Path
src=PdfReader(r'C:/Users/Thomas/Desktop/Elite_100_mini_QUICK_START_GUIDE_US_EN-FR-compressed.pdf')
w=PdfWriter()
W,H=841.889764,595.275591
margin=20
for i in range(5):
    target=w.add_blank_page(width=W,height=H)
    for side,idx in [(0,i),(1,4-i)]:
        p=deepcopy(src.pages[side])
        box=p.mediabox
        x0=float(box.left)+idx*float(box.width)/5
        y0=float(box.bottom)
        pw=float(box.width)/5
        ph=float(box.height)
        p.cropbox=RectangleObject((x0,y0,x0+pw,y0+ph))
        scale=min((W-3*margin)/2/pw,(H-2*margin)/ph)
        x=margin+side*((W-margin)/2)+((W-3*margin)/2-pw*scale)/2
        y=(H-ph*scale)/2
        target.merge_transformed_page(p,Transformation().translate(-x0,-y0).scale(scale).translate(x,y))
out=Path(r'D:/GitRepos/ScannerProject/output/pdf/Elite_100_mini_A4_5pages.pdf')
out.parent.mkdir(parents=True,exist_ok=True)
w.write(out)
assert len(PdfReader(out).pages)==5
print('Verified 5 A4 landscape pages')
