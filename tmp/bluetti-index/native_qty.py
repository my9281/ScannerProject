from functools import lru_cache
from pathlib import Path
import re
import pypdfium2 as pdf

@lru_cache(maxsize=8)
def source_text(path, number):
    d=pdf.PdfDocument(path)
    p=d[number-1]
    return d,p,p.get_textpage()

def get_matches(textpage, query):
    search=textpage.search(query)
    try:
        while (match:=search.get_next()) is not None:
            start,size=match
            boxes=[textpage.get_charbox(i) for i in range(start,start+size)]
            yield (min(x[0] for x in boxes),min(x[1] for x in boxes),max(x[2] for x in boxes),max(x[3] for x in boxes))

    finally:
        search.close()

def qty_from_pdf(page,entries):
    model=page["model"].split(" / ")[0]
    path=Path("用户手册")/model/(page["source"]+".pdf")
    if not path.is_file() or not entries:
        return
    try:
        d,p,t=source_text(str(path),page["sourcePage"])
        header=list(get_matches(t,"Qty"))+list(get_matches(t,"Quantity"))
        if not header:
            return
        for e in entries:
            if e["qty"] or e["origin"]!="OCR":
                continue
            name=re.split(r"\s+[([]",e["name"],maxsplit=1)[0].strip(" .*")
            terms=re.findall(r"[\w]+",name)
            if len(terms)<2 and name not in ("Documentation","Station"):
                continue
            phrases=[name]
            if len(terms)>3:
                phrases.append(" ".join(terms[:3]))
            for phrase in phrases:
                boxes=[b for b in get_matches(t,phrase) if b[2]-b[0]>3]
                found=[]
                for x,y,r,top in boxes:
                    cols=[c for c in header if c[1]>top and c[0]>r+8]
                    if not cols:
                        continue
                    c=min(cols,key=lambda c:c[1]-top)
                    value=t.get_text_bounded(c[0]-12,y-10,c[2]+12,top+9).strip()
                    if re.fullmatch(r"(?:(?:[x×]\s*)?\d{1,2})(?:\s*(?:pcs?|sets?))?",value,re.I):
                        found.append(value)
                if len(found)==1:
                    e["qty"]=found[0]
                    e["origin"]="OCR 名称／原 PDF 数量"
                    break
    except (ValueError, IndexError, pdf.PdfiumError):
        return


