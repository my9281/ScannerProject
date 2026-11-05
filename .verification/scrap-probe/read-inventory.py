import openpyxl,pathlib,json,collections,warnings
warnings.filterwarnings('ignore',category=UserWarning,module='openpyxl')
file=pathlib.Path(r'C:/Users/Thomas/Downloads/20261023 02_46_39 accessoriesWarehouse.xlsx')
book=openpyxl.load_workbook(file,read_only=True,data_only=True)
records=[];summary=[]
for sheet in book:
    it=iter(sheet.values);headers=next(it);count=0;types=collections.Counter()
    for n,row in enumerate(it,2):
        if not any(x is not None for x in row):continue
        e=list(row);count+=1
        if e[12]=='出库':types[str(e[14])]+=1
        records.append({'sheet':sheet.title,'exportRow':n,'sn':str(e[8] or ''),'number':str(e[2] or ''),'rma':str(e[3] or ''),'sku':str(e[6] or ''),'quantity':e[7],'exportTime':str(e[9] or ''),'exportBusinessTime':str(e[10] or ''),'direction':str(e[12] or ''),'warehouse':str(e[13] or ''),'type':str(e[14] or ''),'remark':str(e[15] or '')})
    summary.append({'sheet':sheet.title,'rows':count,'outboundTypes':dict(types)})
base=pathlib.Path('.verification/inventory-probe');base.mkdir(exist_ok=True)
(base/'export.json').write_text(json.dumps({'file':str(file),'sheets':summary,'records':records},ensure_ascii=False),encoding='utf-8')
print(json.dumps(summary,ensure_ascii=True))
