from pathlib import Path
from pypdf import PdfReader
import subprocess
from PIL import Image,ImageDraw
root=Path('Document/Camara'); tmp=Path('tmp/camara')
for name in ['1_English','1_Spanish','510_Repair_SOP_20260518_English','510_Repair_SOP_20260518_Spanish']:
 folder=tmp/name;folder.mkdir(exist_ok=True)
 for old in folder.glob('p-*.png'):old.unlink()
 subprocess.run(['pdftoppm','-scale-to','1200','-png',str(root/(name+'.pdf')),str(folder/'p')],check=True)
 files=sorted(folder.glob('p-*.png'))
 # Full-resolution pages are retained; overview groups identify layout issues.
 for chunk in range(0,len(files),6):
  batch=files[chunk:chunk+6]; sheet=Image.new('RGB',(900,650*((len(batch)+2)//3)),'#ccc')
  for j,file in enumerate(batch):
   im=Image.open(file).convert('RGB');im.thumbnail((290,615));sheet.paste(im,((j%3)*300+5,(j//3)*650+25));ImageDraw.Draw(sheet).text(((j%3)*300+8,(j//3)*650+5),file.stem,fill='black')
  sheet.save(folder/f'contact-{chunk//6}.jpg')
 print(name,len(files))
