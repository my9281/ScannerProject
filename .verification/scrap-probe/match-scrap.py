import json,pathlib,collections,datetime
base=pathlib.Path('.verification/scrap-probe')
exports=json.loads((base/'export.json').read_text(encoding='utf-8'))
raw=next(s for s in exports if s['name']=='报废仓')['rows']
pages=json.loads((base/'page-times.json').read_text(encoding='utf-8'))
previous=json.loads(pathlib.Path('.verification/outbound-combined/combined-audit.json').read_text(encoding='utf-8'))
key=lambda s:str(s or '').strip().upper()
def identity(sn,number,sku,direction):return (key(sn),str(number or ''),str(sku or ''),str(direction or ''))
lookup=collections.defaultdict(list)
for p in pages:lookup[identity(p['sn'],p['number'],p['sku'],p['direction'])].append(p)
assert len(raw)-1==len(pages)==2023
assert collections.Counter(identity(e[8],e[2],e[6],e[12]) for e in raw[1:])==collections.Counter(identity(p['sn'],p['number'],p['sku'],p['direction']) for p in pages)
records=[];differences=collections.Counter()
for excelRow,e in enumerate(raw[1:],2):
    matches=lookup[identity(e[8],e[2],e[6],e[12])]
    assert matches,('Export row missing from API',excelRow)
    assert len({(m['handlerDate'],m['businessTime']) for m in matches})==1,('Ambiguous API times',excelRow)
    p=matches[0]
    t=datetime.datetime.fromisoformat(p['handlerDate']);exported=datetime.datetime.fromisoformat(e[9])
    differences[(exported-t).total_seconds()/3600]+=1
    records.append({'sn':e[8] or '', 'number':e[2] or '', 'rma':e[3] or '', 'sku':e[6] or '', 'quantity':e[7], 'pageTime':p['handlerDate'], 'exportTime':e[9], 'businessTime':p['businessTime'], 'exportBusinessTime':e[10], 'direction':e[12], 'warehouse':e[13], 'type':e[14], 'exportRow':excelRow})
bySn=collections.defaultdict(list)
for r in records:
    if key(r['sn']):bySn[key(r['sn'])].append(r)
for values in bySn.values():values.sort(key=lambda r:(r['pageTime'],r['number']))
append=[];eligible=[];matched=0;removed=0;snSet=set();matchedSet=set();matchedDetails={}
for old,info in zip(previous['rows'],previous['audits']):
    sn=key(old[4]);notes=[];hits=[];allHits=[]
    if old[7]>0:status='已有有效出库，未补查报废'
    elif not sn:status='未填SN'
    else:
        eligible.append(info['outputRow']);snSet.add(sn);allHits=bySn.get(sn,[])
        hits=[h for h in allHits if info['day'] and h['pageTime'][:10]>=info['day']]
        removed+=len(allHits)-len(hits)
        status='收件日期待核对' if not info['day'] else '多条报废仓流水' if len(hits)>1 else '已匹配报废仓流水' if hits else '无符合日期的报废流水' if allHits else '未匹配到报废仓记录'
        if len(hits)!=len(allHits):notes.append(f'排除{len(allHits)-len(hits)}条早于收件日期或日期无法核对的流水')
        if hits:
            matched+=1;matchedSet.add(sn)
            notes.append('报废仓流水，不代表已完成报废出库')
            if any(h['pageTime']!=h['exportTime'] for h in hits):notes.append('导出与页面时间不同，主列采用页面API处理时间原值')
        for h in hits:
            k=(key(h['sn']),h['number'],h['sku'],h['direction'],h['pageTime'])
            if k not in matchedDetails:matchedDetails[k]={'record':h,'rows':[]}
            matchedDetails[k]['rows'].append(info['outputRow'])
    append.append(['\n'.join(h['pageTime'] for h in hits),'\n'.join(h['exportTime'] for h in hits),status,'\n'.join(h['number'] for h in hits),'\n'.join(h['direction'] for h in hits),'\n'.join(str(h['type'] or '') for h in hits),'\n'.join(str(h['businessTime'] or '') for h in hits),'；'.join(notes)])
summary={'exportRecords':len(records),'eligibleRows':len(eligible),'eligibleUniqueSN':len(snSet),'matchedRows':matched,'matchedUniqueSN':len(matchedSet),'matchedScrapRecords':len(matchedDetails),'excludedDatePairs':removed,'timeDifferenceHours':dict(differences),'statuses':dict(collections.Counter(r[2] for r in append))}
out={'summary':summary,'append':append,'records':records,'matchedDetails':list(matchedDetails.values()),'eligibleOutputRows':eligible,'sourceExport':r'C:/Users/Thomas/Downloads/20261023 02_23_40 accessoriesWarehouse.xlsx'}
(base/'scrap-match.json').write_text(json.dumps(out,ensure_ascii=False),encoding='utf-8')
print(json.dumps(summary,ensure_ascii=True))
