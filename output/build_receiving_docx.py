from pathlib import Path
import re
from docx import Document
from docx.shared import Inches, Pt, RGBColor
from docx.oxml import OxmlElement
from docx.oxml.ns import qn

root = Path(__file__).resolve().parents[1]
doc = Document()
sec = doc.sections[0]
sec.page_width, sec.page_height = Inches(8.5), Inches(11)
sec.top_margin = sec.bottom_margin = Inches(.7)
sec.left_margin = sec.right_margin = Inches(.75)
for name in ['Normal', 'Title', 'Heading 1', 'Heading 2']:
    st = doc.styles[name]
    st.font.name = 'Arial'
    st.font.color.rgb = RGBColor(0,0,0)
    st.element.rPr.rFonts.set(qn('w:eastAsia'), 'Microsoft YaHei')
doc.styles['Normal'].font.size = Pt(11)
doc.styles['Normal'].paragraph_format.space_after = Pt(4)
doc.styles['Normal'].paragraph_format.line_spacing = 1.0
doc.styles['Title'].font.size = Pt(21)
doc.styles['Heading 1'].font.size = Pt(14)
doc.styles['Heading 2'].font.size = Pt(12)
for name in ['Title', 'Heading 1', 'Heading 2']:
    doc.styles[name].paragraph_format.space_before = Pt(10)
    doc.styles[name].paragraph_format.space_after = Pt(4)
for style in doc.styles:
    for border in list(style.element.iter(qn('w:pBdr'))):
        border.getparent().remove(border)

def rich(p, text):
    for i, part in enumerate(text.split('**')):
        p.add_run(part).bold = bool(i % 2)

lines = (root/'docs/sop-es/Recepcion-SOP.md').read_text(encoding='utf-8').splitlines()
i = 0
while i < len(lines):
    line = lines[i].strip()
    if line == '## Referencia': break
    if not line:
        i += 1
        continue
    if line.startswith('|'):
        rows=[]
        while i < len(lines) and lines[i].startswith('|'):
            cells=[s.strip() for s in lines[i].strip('|').split('|')]
            if not all(re.fullmatch(r'[- :]+', s) for s in cells): rows.append(cells)
            i+=1
        table=doc.add_table(rows=0, cols=2)
        table.style='Table Grid'
        for n,row in enumerate(rows):
            cells=table.add_row().cells
            for c,t in zip(cells,row):
                rich(c.paragraphs[0],t)
                if n==0:
                    for r in c.paragraphs[0].runs: r.bold=True
                    shade=OxmlElement('w:shd'); shade.set(qn('w:fill'),'EEEEEE'); c._tc.get_or_add_tcPr().append(shade)
        borders=OxmlElement('w:tblBorders')
        for edge in ['top','left','bottom','right','insideH','insideV']:
            e=OxmlElement('w:'+edge); e.set(qn('w:val'),'single'); e.set(qn('w:sz'),'4'); e.set(qn('w:color'),'D9D9D9'); borders.append(e)
        table._tbl.tblPr.append(borders)
        doc.add_paragraph().paragraph_format.space_after=Pt(0)
        continue
    if line.startswith('# '): p=doc.add_paragraph(line[2:], 'Title')
    elif line.startswith('### '): p=doc.add_paragraph(line[4:], 'Heading 2')
    elif line.startswith('## '): p=doc.add_paragraph(re.sub(r'^(\d+)\. ',r'\1 ',line[3:]), 'Heading 1')
    else:
        p=doc.add_paragraph()
        if line.startswith('- '):
            p.style='List Bullet'; line=line[2:]
        rich(p,line)
    i+=1
doc.core_properties.title='Procedimiento de recepción de mercancía'
doc.core_properties.language='es-MX'
doc.save(root/'docs/sop-es/Recepcion-SOP.docx')
