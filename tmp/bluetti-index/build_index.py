import csv, html, json, re, statistics
from pathlib import Path
from native_qty import qty_from_pdf

base = Path("tmp/bluetti-index")
out = Path("output/pdf")
meta = json.loads((base / "meta.json").read_text(encoding="utf8"))
ocr = json.loads((base / "ocr.json").read_text(encoding="utf-8-sig"))
optional = re.compile(r"optional access|^optional\b|^extra options|additional purchase|not included|can purchase|purchase .*accessor", re.I)
standard = re.compile(r"packing lists?$|^standard (?:accessories|packaging)|^in the box$|what.s in the box|packaging contents|^unboxing$", re.I)
header = re.compile(r"^(?:no\.?|item|picture|image|description|category|type|diagram|specs?|qty\.?|quantity|part no\.?|accessory|remarks|reference|count|amount)(?:\s+(?:no\.?|item|picture|image|description|category|type|diagram|specs?|qty\.?|quantity|part no\.?|accessory|remarks|reference|count|amount))*$", re.I)
noise = re.compile(r"^(?:\W+|[0-9oOIil|() ]+|EN|US|QC|bluetti|certificate|always share excellence|just power on)$", re.I)
item = re.compile(r"\b(?:cabl\w*|cord|adapter|adaptor|charger|connect\w*|wrench|spanner|screws?|batt\w*|card|manual|document\w*|guide|station|portable power|panel|power bank|power hub|power supply|case|bag|tool|bracket|switch|socket|terminal|ground\w*|fuse|washer|anchor|antenna|meter|clamp|strap|diode|accessor\w*|certificat\w*|stand|foot|feet|hardware|plate|bolt|breaker|nut|plug|screen|basket|shovel|base|inverter|gland|box|trolley|kit|module|template|cover|controller|insert|coupler|exten\w*)\b", re.I)
narrative = re.compile(r"^(?:note\s*[:：]|please |before |if |you can |the following |for use |you need |available on |sold on |https?://|see |fig(?:ure)?\.? |table \d|to build |the system|upon |using |recommended |together|the original |[•*]\s*)",re.I)
qty_suffix = re.compile(r"\s*[×xX]\s*(\d{1,3}|N)\s*$")
source_pdf = "BLUETTI 配件清单合订本.pdf"
statuses = ["标准随箱", "选购", "安装配件", "待确认"]
solar = {139:"MC4 Cable",140:"MC4 Cable",141:"MC4 Cable",142:"MC4 Cable",
         143:"MC4 Cable",144:"1.2 meters DC5521 to XT60 connector",145:"1.2 meters DC5521 to XT60 connector",
         146:"MC4 Cable",153:"MC4 Cable",154:"MC4 Cable",155:"MC4 Cable"}
pages=[]
data=[]
prev_scope={}
def read(line):
    s=" ".join(str(w[0]) for w in line).strip()
    for wrong,right in {"Wa rranty":"Warranty","Ca rd":"Card","BLU ETTI":"BLUETTI","BLUE TTI":"BLUETTI","Charing cable":"Charging cable","16AWC":"16AWG"}.items():
        s=re.sub(re.escape(wrong),right,s,flags=re.I)
    return dict(s=s,x=min(w[1] for w in line),y=statistics.median(w[2] for w in line),
                r=max(w[1]+w[3] for w in line),h=statistics.median(w[4] for w in line))
for raw, labels in zip(ocr,meta,strict=True):
    pg = raw["page"]
    model, info, _ = labels
    source, src = info.split(" · 原 PDF 第 ",1)
    pnum = int(src.split(" 页")[0])
    lines=[read(line) for line in raw["lines"] if line]
    lines.sort(key=lambda a:(a["y"],a["x"]))
    scopes=[]
    installation="安装配件" in info
    mixed_grid=pg in (78,148,149,150)
    for l in lines:
        s=l["s"]
        if installation:
            continue
        if optional.search(s):
            scopes.append((l,"选购"))
        elif standard.search(s):
            scopes.append((l,"标准随箱"))
    key=(model,source)
    initial="安装配件" if installation else prev_scope.get(key,"待确认")
    def scope_at(l):
        if pg==78:
            return "标准随箱" if l["x"]<880 else "选购"
        if pg in (148,149,150):
            return "待确认"
        scope=initial
        for marker, value in scopes:
            if marker["y"] <= l["y"]+3:
                scope=value
        return scope
    if not mixed_grid:
        prev_scope[key]=scopes[-1][1] if scopes else initial
    columns=[l for l in lines if re.fullmatch(r"Qty\.?|Quantity",l["s"],re.I)]
    qty_lines=[l for l in lines if re.fullmatch(r"\d{1,2}",l["s"]) and any(abs(l["x"]-c["x"])<65 and l["y"]>c["y"]+10 for c in columns)]
    entries=[]
    consumed=set()
    for idx,l in enumerate(lines):
        s=l["s"]
        if idx in consumed or not item.search(s) or header.fullmatch(s) or narrative.search(s) or optional.search(s) or standard.search(s):
            continue
        if noise.fullmatch(s) or len(s)>125 or re.search(r"www\.|^https|page \d",s,re.I):
            continue
        if re.match(r"^(?:[0-9.]+\s+)?(?:get to know|technical details|installation preparation|adjust the|mounting kit contents|cable color codes)",s,re.I):
            continue
        text=s
        continuation=[]
        for j,other in enumerate(lines[idx+1:],idx+1):
            dy=other["y"]-l["y"]
            if dy>l["h"]*4.4:
                break
            if dy<l["h"]*.65 or other["r"]<l["x"]-125 or other["x"]>l["r"]+90:
                continue
            t=other["s"]
            if j in consumed or narrative.search(t) or optional.search(t) or standard.search(t):
                continue
            if t.startswith("(") or (text.count("(")>text.count(")") and not re.match(r"^[A-Z][a-z]+ Charging",t)) or re.match(r"^(?:MC4\s*[-–]|Type [A-Z] plug|Cigarette Lighter Plug\s*[-–])",t) or text.endswith("&") or (text.endswith("Power") and re.match(r"^Station\b",t)) or (dy < l["h"]*1.85 and re.match(r"^(?:Pair|Card|Cable Pair|Warranty Card)\b",t,re.I)):
                text += " "+t
                continuation.append(j)
        for j in continuation:
            consumed.add(j)
        if continuation and idx>0 and lines[idx-1]["s"].endswith("&") and lines[idx-1]["x"]<l["r"]:
            pass
        text=re.sub(r"^[-–•]\s*","",text)
        text=re.sub(r"\s+"," ",text)
        state=scope_at(l)
        q=""
        match=qty_suffix.search(text)
        if match:
            q=match[1]
            text=text[:match.start()].strip()
        else:
            inline=re.search(r"\s+(\d{1,2})$",text)
            if inline and columns and any(l["r"]>=c["x"]-18 for c in columns):
                q=inline[1]
                text=text[:inline.start()].strip()
            else:
                nearby=[n for n in qty_lines if n["x"]>l["r"]+20 and abs(n["y"]-l["y"])<=l["h"]*0.7]
                if len(nearby)==1:
                    q=nearby[0]["s"]
        if not text or len(text)<=2:
            continue
        verified = (pg==2 and "5A/fioow" in text) or (pg==84 and "70.87in 11.8m" in text)
        if verified:
            text=text.replace("5A/fioow","5A/100W").replace("70.87in 11.8m","70.87in / 1.8m")
        entries.append(dict(name=text,qty=q,status=state,origin="看图核对" if verified else "OCR"))
    if pg in solar:
        entries=[{"name":"Solar Panel","qty":"","status":"标准随箱","origin":"看图核对"},
                 {"name":solar[pg],"qty":"","status":"标准随箱","origin":"看图核对"},
                 {"name":"User Manual & Warranty Card","qty":"","status":"标准随箱","origin":"看图核对"}]
        if pg in [144,145]:
            entries.append({"name":"Carabiner","qty":"2","status":"标准随箱","origin":"看图核对"})
        if pg==153:
            entries.append({"name":"3 meters MC4 to 8 mm cylindrical male connector","qty":"","status":"标准随箱","origin":"看图核对"})
    qty_from_pdf(dict(model=model,source=source,sourcePage=pnum),entries)
    for e in entries:
        e.update(model=model,source=source,sourcePage=pnum,page=pg)
        data.append(e)
    pages.append(dict(model=model,source=source,sourcePage=pnum,page=pg,raw=[l["s"] for l in lines],n=len(entries)))
assert [p["page"] for p in pages]==list(range(1,162))
csv_path=out/"BLUETTI 配件文字索引.csv"
with csv_path.open("w",encoding="utf-8-sig",newline="") as f:
    writer=csv.writer(f)
    writer.writerow(["型号","配件或条目（英文）","识别数量","归类","源文件","原 PDF 页","合订 PDF 页","提取方式"])
    for e in data:
        writer.writerow([e[k] for k in ("model","name","qty","status","source","sourcePage","page","origin")])
payload=json.dumps({"items":data,"pages":pages,"categories":statuses},ensure_ascii=False,separators=(",",":")).replace("</","<\\/")
template=(base/"index-template.html").read_text(encoding="utf-8-sig")
(out/"BLUETTI 配件文字索引.html").write_text(template.replace("__DATA__",payload),encoding="utf8")
print(f"{len(pages)} source pages, {len(data)} candidate rows, {sum(bool(e['qty']) for e in data)} with quantities, {sum(p['n']==0 for p in pages)} without candidate rows")
for model in ["AC180","Elite 200 V2","PV120D"]:
    print(model,[(e["name"],e["qty"],e["status"]) for e in data if e["model"]==model][:20])




