import csv,json,pathlib,collections,re
root=pathlib.Path('.verification/outbound-combined');root.mkdir(exist_ok=True)
sources=[]
for n in [1,2]:
    p=pathlib.Path(f'C:/Users/Thomas/Downloads/美国Logistar仓收件登记表-更新版9.02({n}).csv')
    raw=list(csv.reader(p.open(encoding='utf-8-sig',newline='')))
    width=max(map(len,raw))
    rows=[{'values':r+['']*(width-len(r)),'csvRecord':i+1} for i,r in enumerate(raw[2:],2) if r]
    header=raw[1]+['']*(width-len(raw[1]))
    used=[c for c in range(width) if header[c] or any(r['values'][c] for r in rows)]
    sources.append({'name':p.name,'label':f'原表{n}','metadata':raw[0],'headers':header,'rows':rows,'usedColumns':used})
    sns=[r['values'][2].strip().upper() for r in rows if r['values'][2].strip()]
    print(json.dumps({'source':n,'rows':len(rows),'nonemptySN':len(sns),'uniqueSN':len(set(sns)),'width':width,'usedColumns':[{ 'index':c+1,'header':header[c],'nonempty':sum(bool(r['values'][c]) for r in rows)} for c in used]},ensure_ascii=True))
data=[r['values'][:3] for s in sources for r in s['rows']]
(root/'input.json').write_text(json.dumps(data,ensure_ascii=False),encoding='utf-8')
(root/'sources.json').write_text(json.dumps(sources,ensure_ascii=False),encoding='utf-8')
a={r['values'][2].strip().upper() for r in sources[0]['rows'] if r['values'][2].strip()}
b={r['values'][2].strip().upper() for r in sources[1]['rows'] if r['values'][2].strip()}
print(json.dumps({'additionalUniqueSN':len(b-a),'overlapUniqueSN':len(a&b),'combinedRows':len(data)}))
counter=collections.Counter(r['values'][0] for r in sources[1]['rows'] if r['values'][0] and not re.match(r'^\d{4}[./-]\d{1,2}[./-]\d{1,2}\s*$',r['values'][0]))
print(json.dumps({'nonstandardDateValues':dict(counter)},ensure_ascii=True))
