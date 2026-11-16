import json, re, hashlib
from pathlib import Path
from io import BytesIO
from xml.sax.saxutils import escape
from reportlab.platypus import SimpleDocTemplate, Paragraph, Spacer, Image, PageBreak, KeepTogether
from reportlab.lib.styles import getSampleStyleSheet, ParagraphStyle
from reportlab.lib.enums import TA_LEFT
from reportlab.lib import colors
from reportlab.lib.pagesizes import A4
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
import reportlab
from pypdf import PdfReader, PdfWriter

ROOT=Path('Document/Camara'); TMP=Path('tmp/camara')
DATA=json.loads((TMP/'translations.json').read_text(encoding='utf8'))
fonts=Path(reportlab.__file__).parent/'fonts'
pdfmetrics.registerFont(TTFont('Vera',str(fonts/'Vera.ttf')))
pdfmetrics.registerFont(TTFont('VeraBold',str(fonts/'VeraBd.ttf')))
pdfmetrics.registerFontFamily('Vera',normal='Vera',bold='VeraBold',italic='Vera',boldItalic='VeraBold')
styles=getSampleStyleSheet()
styles.add(ParagraphStyle(name='BodyCam',fontName='Vera',fontSize=10.3,leading=15,spaceAfter=9,textColor=colors.black))
styles.add(ParagraphStyle(name='TitleCam',fontName='VeraBold',fontSize=18,leading=23,spaceAfter=18,textColor=colors.black))
styles.add(ParagraphStyle(name='HeadCam',fontName='VeraBold',fontSize=12,leading=17,spaceBefore=8,spaceAfter=10,keepWithNext=True,textColor=colors.black))
styles.add(ParagraphStyle(name='SmallCam',fontName='Vera',fontSize=8.5,leading=12,spaceAfter=12,textColor=colors.HexColor('#555555')))

def p(text,style='BodyCam'):return Paragraph(escape(text),styles[style])
def footer(canvas,doc):
    canvas.setFont('Vera',8); canvas.setFillColor(colors.HexColor('#666666'))
    canvas.drawRightString(A4[0]-45,25,str(doc.page))
def make_pdf(target,story,title,numbered=True):
    doc=SimpleDocTemplate(target,pagesize=A4,rightMargin=45,leftMargin=45,topMargin=44,bottomMargin=44,title=title,author='')
    doc.build(story,onFirstPage=footer if numbered else (lambda c,d:None),onLaterPages=footer if numbered else (lambda c,d:None))
def illustration(file,maxh=590):
    im=Image(str(file)); ratio=min(505/im.imageWidth,maxh/im.imageHeight)
    im.drawWidth=im.imageWidth*ratio; im.drawHeight=im.imageHeight*ratio
    im.hAlign='CENTER';return im

for lang in ['en','es']:
    styles['BodyCam'].fontSize=10.3; styles['BodyCam'].leading=15; styles['BodyCam'].spaceAfter=9
    label='English' if lang=='en' else 'Español'
    notes=DATA['notes_'+lang]
    story=[p(notes[0],'TitleCam'),p(label,'SmallCam')]
    for t in notes[1:7]:story.append(p(t))
    blocks=[(7,12,'image1.png'),(12,15,'image2.png'),(15,len(notes),'image3.jpeg')]
    for start,end,filename in blocks:
        story.append(PageBreak());story.append(p(notes[start],'HeadCam'))
        # Full original screenshot, followed by a complete translation of its relevant content.
        story.append(illustration(TMP/filename,490 if filename!='image3.jpeg' else 220))
        story.append(Spacer(1,14))
        for t in notes[start+1:end]:story.append(p(t))
    make_pdf(str(ROOT/f'1_{"English" if lang=="en" else "Spanish"}.pdf'),story,notes[0])

    source=PdfReader(ROOT/'510 维修SOP - 20260518.pdf')
    styles['BodyCam'].fontSize=9.5; styles['BodyCam'].leading=13.2; styles['BodyCam'].spaceAfter=7
    writer=PdfWriter()
    for index,parts in enumerate(DATA['sop_'+lang]):
        title=('510 Camera Repair SOP' if lang=='en' else 'SOP de reparación de cámaras 510')
        page_label=(f'Translation of source page {index+1} of 11' if lang=='en' else f'Traducción de la página original {index+1} de 11')
        story=[p(title,'TitleCam'),p(label+' | 2026-05-18 | '+page_label,'SmallCam'),p(parts[0],'HeadCam')]
        for t in parts[1:]:story.append(p(t))
        story.append(Spacer(1,10))
        story.append(p('The following page preserves the original illustrations and layout.' if lang=='en' else 'La página siguiente conserva las ilustraciones y la disposición originales.','SmallCam'))
        buffer=BytesIO();make_pdf(buffer,story,title,numbered=False)
        for page in PdfReader(buffer).pages:writer.add_page(page)
        writer.add_page(source.pages[index])
    writer.add_metadata({'/Title':title,'/Subject':label+' translation with original illustrated reference pages','/Author':''})
    writer.write(ROOT/f'510_Repair_SOP_20260518_{"English" if lang=="en" else "Spanish"}.pdf')

for f in ROOT.iterdir():
    if f.suffix=='.pdf':print(f.name,len(PdfReader(f).pages),f.stat().st_size)
for original,copy in [(Path('C:/Users/Thomas/Desktop/1.docx'),ROOT/'1.docx'),(Path('C:/Users/Thomas/Downloads/510 维修SOP - 20260518.pdf'),ROOT/'510 维修SOP - 20260518.pdf')]:
    assert hashlib.sha256(original.read_bytes()).digest()==hashlib.sha256(copy.read_bytes()).digest()
print('Originals verified byte-for-byte. Translation source pages:',len(DATA['sop_en']),len(DATA['sop_es']))
