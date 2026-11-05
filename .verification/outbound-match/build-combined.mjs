import fs from 'node:fs/promises';
import path from 'node:path';
import assert from 'node:assert/strict';
import { Workbook, SpreadsheetFile } from '@oai/artifact-tool';

const base=path.resolve('.verification/outbound-combined');
const output=path.resolve('outputs/01a16445-7eb1-73a1-910c-60167e7f1c01');
const sources=JSON.parse(await fs.readFile(path.join(base,'sources.json'),'utf8'));
const result=JSON.parse(await fs.readFile(path.join(base,'result.json'),'utf8'));
const consolidated=process.argv.includes('--consolidated');
const withInventory=process.argv.includes('--inventory')||consolidated;
const inventory=withInventory?JSON.parse(await fs.readFile(path.resolve('.verification/inventory-probe/inventory-match.json'),'utf8')):null;
const withScrap=process.argv.includes('--scrap')||withInventory;
const scrap=withScrap?JSON.parse(await fs.readFile(path.resolve('.verification/scrap-probe/scrap-match.json'),'utf8')):null;
assert.equal(result.errors.length,0,'Finish failed lookups before delivery');
assert.equal(result.missingProductRows,0);
const key=s=>String(s??'').trim().toUpperCase();
const recKey=m=>[key(m.sn),m.shipmentNo,m.workOrderId,m.outTime].join('|');
function calendar(y,m,d){
 const t=new Date(Date.UTC(+y,+m-1,+d));
 return t.getUTCFullYear()===+y&&t.getUTCMonth()===+m-1&&t.getUTCDate()===+d?t.toISOString().slice(0,10):null;
}
function fullDate(raw){
 const s=String(raw??'').trim();let m=s.match(/^(\d{4})[./-](\d{1,2})[./-](\d{1,2})(?!\d)/);
 if(m)return calendar(m[1],m[2],m[3]);
 m=s.match(/^(\d{4})\.(\d{2})(\d{2})$/)||s.match(/^(\d{4})(\d{2})[/-]?(\d{2})$/);
 return m?calendar(m[1],m[2],m[3]):null;
}
const serial=d=>(Date.parse(d+'T00:00:00Z')-Date.UTC(1899,11,30))/86400000;
const bySn=new Map();for(const m of result.matches){const k=key(m.sn);if(!bySn.has(k))bySn.set(k,[]);bySn.get(k).push(m);}
const accepted=new Map(),audits=[],stats=[];let data=[];
for(const source of sources){
 let carry=null,filled=0,inferred=0,uncertain=0,skipped=0,removed=0;
 const future=Array(source.rows.length).fill(null);let next=null;
 for(let i=source.rows.length-1;i>=0;i--){future[i]=next;next=fullDate(source.rows[i].values[0])||next;}
 for(let i=0;i<source.rows.length;i++){
  const entry=source.rows[i],v=entry.values,raw=String(v[0]??'').trim();
  if(v.every(x=>!String(x??'').trim())){skipped++;continue;}
  let day=fullDate(raw),note='';const next=future[i];
  if(!day&&raw&&carry&&next){
   const md=raw.match(/^(\d{1,2})[./-](\d{1,2})(?!\d)/)||raw.match(/^(\d{1,2})月(\d{1,2})日/);
   const dom=raw.match(/^(\d{1,2})$/),badYear=raw.match(/^205[./-](\d{1,2})[./-](\d{1,2})$/);
   const candidate=md?calendar(carry.slice(0,4),md[1],md[2]):dom?calendar(carry.slice(0,4),carry.slice(5,7),dom[1]):badYear&&carry.startsWith('2025')?calendar(2025,badYear[1],badYear[2]):null;
   if(candidate&&candidate>=carry&&candidate<=next&&carry.slice(0,4)===next.slice(0,4)&&(!dom||carry.slice(0,7)===next.slice(0,7))){day=candidate;inferred++;note=`原日期“${raw}”，结合前后日期补全为${day}`;}
  }
  if(day){carry=day;if(raw&&!note&&!/^\d{4}[./-]\d{1,2}[./-]\d{1,2}$/.test(raw)&&!/^\d{4}\.\d{4}$/.test(raw))note=`原日期列内容：${raw}`;}
  else if(/^\d{1,2}月$/.test(raw)){carry=null;note=`原日期“${raw}”缺少具体日，待核对`;}
  else {day=carry;if(day)filled++;if(raw)note=`原日期列非日期内容：${raw}`;}
  if(!day){uncertain++;note=[note,'无法确定收件日期，出库匹配暂不保留'].filter(Boolean).join('；');}
  const sn=key(v[2]),all=bySn.get(sn)??[],hits=all.filter(m=>day&&fullDate(m.outTime)&&fullDate(m.outTime)>=day);
  const dropped=all.length-hits.length;removed+=dropped;
  let status=!sn?'未填SN':hits.length>1?'多条出库记录':hits.length===1?'已匹配':all.length?'无有效出库记录':'未查到出库记录';
  if(!day)status='收件日期待核对';
  const notes=note?[note]:[];
  if(dropped)notes.push(day?`移除${dropped}条早于收件日期或日期无效的出库匹配`:`收件日期不明确，移除${dropped}条出库匹配`);
  if(!all.length&&sn)notes.push('当前站点未精确匹配到出库记录，不代表未出库');
  if(hits.length>1)notes.push('保留全部有效记录，详见出库明细');
  if(/^[+-]?\d+(?:\.\d+)?E[+-]?\d+$/i.test(String(v[2]).trim()))notes.push('原SN为科学计数法，未推测完整号码');
  const outputRow=data.length+7;
  for(const hit of hits){const k=recKey(hit);if(!accepted.has(k))accepted.set(k,[]);accepted.get(k).push({source:source.label,csvRecord:entry.csvRecord,date:day,outputRow});}
  const row=[source.label,entry.csvRecord,day?serial(day):'',v[1]??'',v[2]??'',hits.map(m=>m.outTime).join('\n'),status,hits.length,
   [...new Set(hits.map(m=>m.shipmentNo))].join('\n'),[...new Set(hits.map(m=>m.workOrderId))].join('\n'),[...new Set(hits.map(m=>m.category))].join('\n'),notes.join('；'),
   ...Array.from({length:9},(_,c)=>v[c+3]??'')];
  data.push(row);audits.push({source:source.label,csvRecord:entry.csvRecord,originalIndex:i,day,rawDate:v[0],outputRow,removed:dropped,hits:hits.map(recKey)});
 }
 stats.push({source:source.label,rawRows:source.rows.length,keptRows:source.rows.length-skipped,blankRowsSkipped:skipped,filled,inferred,uncertain,removed});
}
const retained=result.matches.filter(m=>accepted.has(recKey(m)));
const counts={};for(const r of data)counts[r[6]]=(counts[r[6]]??0)+1;
const uniqueSN=new Set(data.map(r=>key(r[4])).filter(Boolean)).size;
const uniqueMatched=new Set(retained.map(m=>key(m.sn))).size;
const wb=Workbook.create();
function col(n){let s='';for(n++;n;n=Math.floor((n-1)/26))s=String.fromCharCode(65+(n-1)%26)+s;return s;}
function setBase(s,last,rows){s.showGridLines=false;const range=s.getRange(`A1:${col(last-1)}${rows}`);range.format.font={name:'Arial',size:11,color:'#233448'};range.format.verticalAlignment='center';}
function tableSheet(name,headers,rows,widths){
 const s=wb.worksheets.add(name),end=6+rows.length;setBase(s,headers.length,end);
 s.getRange(`A6:${col(headers.length-1)}6`).values=[headers];if(rows.length)s.getRange(`A7:${col(headers.length-1)}${end}`).values=rows;
 s.getRange(`A6:${col(headers.length-1)}${end}`).format.rowHeight=27;
 widths.forEach((w,i)=>s.getRange(`${col(i)}1:${col(i)}${end}`).format.columnWidth=w);
 if(rows.length){const names={'合并匹配结果':'MergedMatches','出库明细':'MergedOutboundDetails','报废仓匹配明细':'ScrapMatchedDetails','报废仓导出核对':'ScrapExportAudit','补查出库明细':'InventoryMatchedDetails'};const t=s.tables.add(`A6:${col(headers.length-1)}${end}`,true,names[name]);t.showFilterButton=true;}
 s.getRange(`A6:${col(headers.length-1)}6`).format={fill:'#244668',font:{name:'Arial',size:11,bold:true,color:'#FFFFFF'},horizontalAlignment:'center',verticalAlignment:'center',rowHeight:34};
 s.freezePanes.freezeRows(6);s.tabColor=name==='合并匹配结果'?'#244668':'#7194B6';return s;
}
function title(s,text){s.getRange('A2').values=[[text]];s.getRange('A2').format.font={name:'Arial',size:16,bold:true,color:'#244668'};}
let extraHeaders=withScrap?['报废仓处理时间（页面）','处理时间（导出原值）','报废仓匹配状态','报废仓流水号','报废仓出入库','报废仓流水类型','业务处理日期（页面）','报废核对说明']:[];
if(withInventory)extraHeaders.push('补查出库处理时间（页面）','补查出库匹配状态','补查出库记录数','补查出库流水号','补查来源仓库','补查出库类型','补查处理时间（导出原值）','补查说明');
const mainWidths=[12,19,18,23,34,29,24,15,33,35,28,80,34,22,23,27,27,65,27,48,48,...(withScrap?[30,30,31,29,18,29,28,80]:[])];
if(withInventory)mainWidths.push(30,32,18,30,25,28,30,65);
const displayData=data.map((r,i)=>[...r,...(withScrap?scrap.append[i]:[]),...(withInventory?inventory.append[i]:[])]);
if(consolidated)for(let i=0;i<displayData.length;i++){const r=displayData[i],a=inventory.append[i];if(a[2]>0){r[5]=a[0];r[6]='已匹配（库存补查）';r[7]=a[2];r[8]=a[3];r[10]=a[5];r[11]='来源：仓库导出表出库流水；使用页面处理时间。'+(a[7]||'');}}
const lastColumn=col(displayData[0].length-1);
const main=tableSheet('合并匹配结果',['来源表','原CSV记录序号','仓库收件日期','机型','SN','出库时间（详情原值）','匹配状态','出库记录数','出库编号','工单编号','出库类型','核对说明','物流单号','退件类型','是否录入BT系统','售后系统收件日期','调拨单号','原第9列备注','新TO单号','原第11列备注','原第12列备注',...extraHeaders],displayData,mainWidths);
if(consolidated)main.getRange('F6').values=[['出库时间（详情或库存页面）']];
title(main,'Logistar两份收件登记表 · 合并出库匹配');
main.getRange('A3').values=[['来源：BLUETTI 美国Logistar站点；使用前次已验证记录，补查新增SN详情。原表1=(1).csv，原表2=(2).csv。']];
main.getRange('A4').values=[[`合并${data.length}条非空记录，${uniqueSN}个不同SN；有效匹配${uniqueMatched}个SN、${retained.length}条出库记录。重复行保留。`]];
main.getRange('A5').values=[['每份原表独立向下补齐收件日期；仅保留不早于对应收件日期的出库时间。两份原始内容见后两张表。']];
if(withScrap)main.getRange('A5').values=[[`报废补查${scrap.summary.eligibleRows}行，匹配${scrap.summary.matchedRows}行。V:AC列记录报废仓流水，采用页面API处理时间并保留导出原值；不等同实际报废出库。`]];
if(withInventory)main.getRange('A5').values=[[`仅补查基础表无有效出库的${inventory.summary.eligibleRows}行，匹配${inventory.summary.matchedRows}行。AD:AK按SN匹配导出表全部出库类型，排除早于收件日期的记录。`]];
if(consolidated)main.getRange('A4').values=[[`合并${data.length}条收件记录；主列已匹配${displayData.filter(r=>r[7]>0).length}条，其中库存补查${inventory.summary.matchedRows}条。重复收件行保留。`]];
if(consolidated)main.getRange('A5').values=[['补查成功的4437条记录已同步到F:K主列；AD:AK保留匹配来源。原详情已匹配的2092条记录保持原值。所有记录均按对应收件日期校验。']];
const end=data.length+6;
main.getRange(`A7:${lastColumn}${end}`).setNumberFormat('@');main.getRange(`B7:B${end}`).setNumberFormat('0');main.getRange(`C7:C${end}`).setNumberFormat('yyyy-mm-dd');main.getRange(`H7:H${end}`).setNumberFormat('0');
main.getRange(`D7:F${end}`).format.wrapText=true;main.getRange(`I7:${lastColumn}${end}`).format.wrapText=true;
main.getRange(`G7:G${end}`).conditionalFormats.add('containsText',{text:'待核对',format:{fill:'#FFF1D5',font:{color:'#8B4A00'}}});
const weighted=s=>[...String(s)].reduce((n,c)=>n+(c.charCodeAt(0)>255?2:1),0);
for(let i=0;i<data.length;i++){
 const r=displayData[i];let lines=1;const widths=mainWidths;
 for(let c=3;c<r.length;c++)lines=Math.max(lines,String(r[c]).split('\n').reduce((n,s)=>n+Math.max(1,Math.ceil(weighted(s)/Math.max(8,widths[c]-2))),0));
 main.getRange(`A${i+7}:${lastColumn}${i+7}`).format.rowHeight=Math.max(27,lines*17+8);
}
const detailRows=retained.map(m=>[m.sn,m.outTime,m.shipmentNo,m.sku,m.count??'',m.category,m.workOrderId,m.rmaNo,m.handlerTime,m.source,
 accepted.get(recKey(m)).map(a=>`${a.source} CSV记录${a.csvRecord}（合并表第${a.outputRow}行）`).join('\n'),[...new Set(accepted.get(recKey(m)).map(a=>a.date))].join('\n')]);
const detail=tableSheet('出库明细',['SN','出库时间（详情原值）','出库编号','出库SKU','数量','出库类型','工单编号','RMA','操作时间（仅供区分）','详情页面','适用收件记录','对应收件日期'],detailRows,[34,29,33,35,10,28,35,25,29,90,57,26]);
title(detail,'日期校验后的出库记录');
detail.getRange('A3').values=[['来源：https://after-sales.bluettipower.com/。时间取出库详情 outTime 原值，未换算时区。']];
detail.getRange('A4').values=[[`保留${retained.length}条去重出库记录。K列列出每条记录适用的收件行，避免重复SN在不同收件日期之间误配。`]];
detail.getRange('A5').values=[['同日出库保留。无法确定收件日期的行暂不保留出库时间。']];
if(detailRows.length){const de=detailRows.length+6;detail.getRange(`A7:L${de}`).setNumberFormat('@');detail.getRange(`E7:E${de}`).setNumberFormat('0');detail.getRange(`K7:L${de}`).format.wrapText=true;
 for(let i=0;i<detailRows.length;i++)detail.getRange(`A${i+7}:L${i+7}`).format.rowHeight=Math.max(27,detailRows[i][10].split('\n').length*17+8);
}
for(const source of sources){
 const s=wb.worksheets.add(source.label+'（原始）');const width=source.headers.length,raw=[source.metadata,source.headers,...source.rows.map(r=>r.values)].map(r=>Array.from({length:width},(_,i)=>r[i]??''));
 setBase(s,width,raw.length);s.getRange(`A1:${col(width-1)}${raw.length}`).setNumberFormat('@');s.getRange(`A1:${col(width-1)}${raw.length}`).values=raw;
 s.getRange(`A2:${col(source.usedColumns.at(-1))}2`).format={fill:'#526B84',font:{name:'Arial',size:11,bold:true,color:'#FFFFFF'},rowHeight:32};
 source.usedColumns.forEach(c=>s.getRange(`${col(c)}1:${col(c)}${raw.length}`).format.columnWidth=c===2?36:c===0?21:c===1?23:36);
 s.freezePanes.freezeRows(2);s.getRange(`A3:${col(source.usedColumns.at(-1))}${raw.length}`).format.rowHeight=25;
 for(let i=2;i<raw.length;i++){
  let lines=1;
  for(const c of source.usedColumns)if(String(raw[i][c]).includes('\n')){s.getRange(`${col(c)}${i+1}`).format.wrapText=true;lines=Math.max(lines,String(raw[i][c]).split('\n').length);}
  if(lines>1)s.getRange(`A${i+1}:${col(source.usedColumns.at(-1))}${i+1}`).format.rowHeight=lines*17+8;
 }
}
let scrapDetails,exportCheck;
if(withScrap){
 const detailValues=scrap.matchedDetails.map(({record:r,rows})=>[r.sn,r.pageTime,r.exportTime,r.number,r.direction,r.type,r.sku,r.businessTime,rows.join('\n'),r.exportRow]);
 scrapDetails=tableSheet('报废仓匹配明细',['SN','处理时间（页面）','处理时间（导出原值）','报废仓流水号','出入库','流水类型','SKU','业务处理日期（页面）','适用合并表行号','导出表行号'],detailValues,[34,30,30,29,15,27,35,28,31,17]);
 title(scrapDetails,'未匹配出库SN · 报废仓流水');
 scrapDetails.getRange('A3').values=[['来源：系统导出Excel的“报废仓”表；处理时间按同一流水号与页面API核对，未自行换算时区。']];
 scrapDetails.getRange('A4').values=[[`共${scrap.summary.matchedScrapRecords}条流水，对应${scrap.summary.matchedRows}条收件记录。处理时间不得早于相应收件日期。`]];
 scrapDetails.getRange('A5').values=[['“入库/报废回收”是转入报废仓；“出库”等其他流水按原类型保留，不据此判定最终报废状态。']];
 if(detailValues.length){const n=detailValues.length+6;scrapDetails.getRange(`A7:J${n}`).setNumberFormat('@');scrapDetails.getRange(`J7:J${n}`).setNumberFormat('0');scrapDetails.getRange(`I7:I${n}`).format.wrapText=true;for(let i=0;i<detailValues.length;i++)scrapDetails.getRange(`A${i+7}:J${i+7}`).format.rowHeight=Math.max(27,detailValues[i][8].split('\n').length*17+8);}
 const exportValues=scrap.records.map(r=>[r.exportRow,r.sn,r.number,r.pageTime,r.exportTime,r.businessTime,r.exportBusinessTime,r.direction,r.type,r.sku,r.quantity,r.rma]);
 exportCheck=tableSheet('报废仓导出核对',['原导出表行号','SN','流水号','处理时间（页面）','处理时间（导出原值）','业务日期（页面）','业务日期（导出原值）','出入库','流水类型','SKU','数量','RMA'],exportValues,[20,34,29,30,30,26,29,15,27,35,10,25]);
 title(exportCheck,'报废仓全部导出记录 · 时间对照');
 exportCheck.getRange('A3').values=[[`原文件：${scrap.sourceExport}`]];
 exportCheck.getRange('A4').values=[['2023条导出记录全部与页面API按SN、流水号、SKU、出入库核对一致。仅提取报废仓业务字段。']];
 exportCheck.getRange('A5').values=[['导出处理时间与页面相差12或13小时；两种原值分别保留，匹配主列使用页面API处理时间。']];
 const n=exportValues.length+6;exportCheck.getRange(`A7:L${n}`).setNumberFormat('@');exportCheck.getRange(`A7:A${n}`).setNumberFormat('0');exportCheck.getRange(`K7:K${n}`).setNumberFormat('0');
}
let inventoryDetails;
if(withInventory){
 const values=inventory.details.map(({record:r,rows})=>[r.sn,r.pageTime,r.exportTime,r.number,r.sheet,r.type,r.sku,r.quantity,r.rma,r.exportRow,rows.join('\n')]);
 inventoryDetails=tableSheet('补查出库明细',['SN','处理时间（页面）','处理时间（导出原值）','出库流水号','来源仓库','出库类型','SKU','数量','RMA','导出表行号','适用合并表行号'],values,[34,30,30,29,25,28,35,12,28,20,32]);
 title(inventoryDetails,'基础表未匹配SN · 导出表出库匹配');
 inventoryDetails.getRange('A3').values=[[`来源：${inventory.sourceExport}`]];
 inventoryDetails.getRange('A4').values=[[`${inventory.summary.matchedFlowRecords}条出库流水，匹配${inventory.summary.matchedRows}条收件记录；仅SN精确匹配、出入库=出库、处理日期不早于收件日期。`]];
 inventoryDetails.getRange('A5').values=[['不按出库类型额外筛选；保留系统原类型。页面与导出处理时间分别保留，日期校验使用页面时间。']];
 inventoryDetails.getRange(`A7:K${values.length+6}`).setNumberFormat('@');
 inventoryDetails.getRange(`H7:H${values.length+6}`).setNumberFormat('0');
 inventoryDetails.getRange(`J7:J${values.length+6}`).setNumberFormat('0');
 inventoryDetails.getRange(`K7:K${values.length+6}`).format.wrapText=true;
}
const summary={mergedRows:data.length,uniqueSN,uniqueMatched,validOutboundRecords:retained.length,matchedRows:data.filter(r=>r[7]>0).length,counts,sources:stats,...(withScrap?{scrap:scrap.summary}: {}),...(withInventory?{inventory:inventory.summary}:{})};
console.log(JSON.stringify(summary));
await fs.writeFile(path.join(base,consolidated?'combined-consolidated-audit.json':withInventory?'combined-inventory-audit.json':withScrap?'combined-scrap-audit.json':'combined-audit.json'),JSON.stringify({summary,audits,rows:displayData,retained,details:detailRows},null,2));
await fs.mkdir(output,{recursive:true});
for(const [s,r,name]of [[main,'A2:H13','merged'],[detail,'A2:I12','details'],[wb.worksheets.getItem('原表1（原始）'),'A1:C10','source1'],[wb.worksheets.getItem('原表2（原始）'),'A1:L9','source2']]){
 const png=await wb.render({sheetName:s.name,range:r,scale:1.1,format:'png'});await fs.writeFile(path.join(base,`${name}-preview.png`),new Uint8Array(await png.arrayBuffer()));
}
if(withScrap)for(const [s,r,name]of [[main,'V6:AC14','scrap-columns'],[scrapDetails,'A2:J12','scrap-details'],[exportCheck,'A2:I12','scrap-export']]){const png=await wb.render({sheetName:s.name,range:r,scale:1.1,format:'png'});await fs.writeFile(path.join(base,`${name}-preview.png`),new Uint8Array(await png.arrayBuffer()));}
if(withInventory){wb.recalculate();console.log((await wb.inspect({kind:'table',range:'合并匹配结果!AD6:AK9',include:'values',tableMaxRows:4,tableMaxCols:8})).ndjson);for(const [s,r,name]of [[main,'AD6:AK12','inventory-columns'],[inventoryDetails,'A2:F12','inventory-details']]){const png=await wb.render({sheetName:s.name,range:r,scale:1.1,format:'png'});await fs.writeFile(path.join(base,`${name}-preview.png`),new Uint8Array(await png.arrayBuffer()));}}
const xlsx=await SpreadsheetFile.exportXlsx(wb);await xlsx.save(path.join(output,consolidated?'美国Logistar合并登记表_出库结果已汇总修正版.xlsx':withInventory?'美国Logistar合并登记表_补查全部出库记录.xlsx':withScrap?'美国Logistar合并登记表_出库及报废仓处理时间核对.xlsx':'美国Logistar两份收件登记表_合并清洗及出库匹配.xlsx'));console.log('EXPORTED');
