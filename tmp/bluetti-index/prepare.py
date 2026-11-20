from pathlib import Path
import json
import pypdfium2 as pdf
from PIL import Image, ImageOps, ImageEnhance
b=Path('tmp/bluetti-index')
d=pdf.PdfDocument('output/pdf/BLUETTI 配件清单合订本.pdf')
m=[]
for i,p in enumerate(d,1):
    text=p.get_textpage().get_text_range().splitlines()
    m.append(text)
    im=p.render(scale=3.0,crop=(37,46,37,87)).to_pil().convert('L')
    im=ImageOps.autocontrast(im,cutoff=0)
    im.save(b/'images'/f'{i:03}.png',compress_level=1)
(b/'meta.json').write_text(json.dumps(m,ensure_ascii=False),encoding='utf8')
print(len(m))

