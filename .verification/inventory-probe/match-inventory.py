import json,pathlib,collections
base=pathlib.Path('.verification/inventory-probe')
export=json.loads((base/'export.json').read_text(encoding='utf-8'))
pages=json.loads((base/'page-times.json').read_text(encoding='utf-8'))
old=json.loads(pathlib.Path('.verification/outbound-combined/combined-audit.json').read_text(encoding='utf-8'))
key=lambda s:str(s or '').strip().upper()
identity=lambda r:(key(r['sn']),str(r['number'] or ''),str(r['sku'] or ''),str(r['direction'] or ''))
api={identity(r):r for r in pages}
bySN=collections.defaultdict(list)
out=[r for r in export['records'] if r['direction']=='出库']
for original in out:
    r=dict(original);p=api.get(identity(r))
    r.update(pageTime=p['handlerDate'] if p else '',targetWarehouse=(p.get('targetWarehouse') or '') if p else '')
    r['comparisonTime']=r['pageTime'] or r['exportTime']
    if key(r['sn']):bySN[key(r['sn'])].append(r)
for rs in bySN.values():rs.sort(key=lambda r:(r['comparisonTime'],r['sheet'],r['number']))
append=[];details={};eligible=matched=excluded=0;unique=set();missingApi=0
for row,info in zip(old['rows'],old['audits']):
    sn=key(row[4]);hits=[];notes=[]
    if row[7]>0:status='已有有效出库记录'
    elif not sn:status='未填SN'
    else:
        eligible+=1;unique.add(sn);allHits=bySN.get(sn,[])
        hits=[r for r in allHits if info['day'] and r['comparisonTime'][:10]>=info['day']]
        excluded+=len(allHits)-len(hits)
        status='收件日期待核对' if not info['day'] else '已匹配出库记录' if hits else '出库早于收件日期，已排除' if allHits else '未匹配到出库记录'
        if hits:matched+=1
        if len(allHits)>len(hits):notes.append(f'排除{len(allHits)-len(hits)}条早于收件日或收件日期不明的记录')
        if any(not r['pageTime'] for r in hits):notes.append('部分流水无页面时间，按导出原值校验日期');missingApi+=1
        for r in hits:
            k=(r['sheet'],r['exportRow'])
            if k not in details:details[k]={'record':r,'rows':[]}
            details[k]['rows'].append(info['outputRow'])
    append.append(['\n'.join(r['comparisonTime'] for r in hits),status,len(hits),'\n'.join(r['number'] for r in hits),'\n'.join(r['sheet'] for r in hits),'\n'.join(r['type'] for r in hits),'\n'.join(r['exportTime'] for r in hits),'；'.join(notes)])
summary={'eligibleRows':eligible,'eligibleUniqueSN':len(unique),'matchedRows':matched,'matchedUniqueSN':len({key(d['record']['sn']) for d in details.values()}),'matchedFlowRecords':len(details),'excludedDatePairs':excluded,'exportOutgoingRows':len(out),'matchedRowsWithoutPageTime':missingApi,'statuses':dict(collections.Counter(r[1] for r in append))}
(base/'inventory-match.json').write_text(json.dumps({'summary':summary,'append':append,'details':list(details.values()),'sheets':export['sheets'],'sourceExport':export['file']},ensure_ascii=False),encoding='utf-8')
print(json.dumps(summary,ensure_ascii=True))
