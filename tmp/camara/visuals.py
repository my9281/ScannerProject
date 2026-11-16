from PIL import Image,ImageOps,ImageDraw
from pathlib import Path
import zipfile
p=Path('tmp/camara')
ims=[]
for f in sorted(p.glob('source-*.png')):
 im=Image.open(f).convert('RGB'); im.thumbnail((300,420)); tile=Image.new('RGB',(320,450),'white');tile.paste(im,(10,25));ImageDraw.Draw(tile).text((10,5),f.stem,fill='black');ims.append(tile)
out=Image.new('RGB',(320*4,450*3),'#ccc')
for i,im in enumerate(ims):out.paste(im,((i%4)*320,(i//4)*450))
out.save(p/'overview.jpg')
with zipfile.ZipFile('Document/Camara/1.docx') as z:
 for n in z.namelist():
  if n.startswith('word/media/'):
   (p/Path(n).name).write_bytes(z.read(n))
