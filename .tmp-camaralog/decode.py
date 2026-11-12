import sys,json
from pathlib import Path
sys.path.insert(0,str(Path('.tmp-camaralog-python').resolve()))
import zxingcpp
from PIL import Image,ImageOps,ImageEnhance
out=[]
for p in Path('camaralog').glob('*.jpg'):
 im=Image.open(p)
 found={}
 for angle in [0,90]:
  for scale in [1,2,3]:
   v=im.rotate(angle,expand=True)
   v=v.resize((v.width*scale,v.height*scale))
   for b in zxingcpp.read_barcodes(v):
    key=(str(b.format),b.text)
    found[key]={'format':str(b.format),'text':b.text,'position':str(b.position)}
 out.append({'file':p.name,'codes':list(found.values())})
print(json.dumps(out,ensure_ascii=False,indent=2))
Path('.tmp-camaralog/decoded.json').write_text(json.dumps(out,ensure_ascii=False,indent=2),encoding='utf-8')
