from pathlib import Path
from io import BytesIO
from PIL import Image
from pypdf import PdfReader, PdfWriter
from reportlab.pdfgen.canvas import Canvas
from reportlab.lib.pagesizes import A4, landscape
from reportlab.lib.utils import ImageReader

root=Path('Document/Camara')
for lang in ('English','Spanish'):
    writer=PdfWriter()
    with (root/f'1_{lang}.pdf').open('rb') as stream:
        writer.add_page(PdfReader(stream).pages[0])
    for number in range(1,4):
        image=root/'Translated_Images'/f'Image_{number}_{lang}.png'
        with Image.open(image) as im:
            width,height=im.size
        pw,ph=A4 if height>width else landscape(A4)
        margin=24
        scale=min((pw-2*margin)/width,(ph-2*margin)/height)
        dw,dh=width*scale,height*scale
        buffer=BytesIO()
        canvas=Canvas(buffer,pagesize=(pw,ph))
        canvas.drawImage(ImageReader(str(image)),(pw-dw)/2,(ph-dh)/2,width=dw,height=dh)
        canvas.showPage();canvas.save()
        writer.add_page(PdfReader(buffer).pages[0])
    writer.add_metadata({'/Title':f'Camera repair and refurbishment instructions - {lang}', '/Subject':'Translated text and localized illustrations'})
    output=root/f'1_{lang}_Translated_Images.pdf'
    with output.open('wb') as stream:writer.write(stream)
    check=PdfReader(output)
    assert len(check.pages)==4
    assert all(len(check.pages[i].images)==1 for i in (1,2,3))
    print(output,len(check.pages),output.stat().st_size)
