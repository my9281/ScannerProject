import fs from 'node:fs/promises';
import path from 'node:path';
import assert from 'node:assert/strict';
import { Workbook, SpreadsheetFile } from '@oai/artifact-tool';

const root=path.resolve('.verification/outbound-match');
const output=path.resolve('outputs/01a16445-7eb1-73a1-910c-60167e7f1c01');
const input=JSON.parse(await fs.readFile(path.join(root,'input.json'),'utf8'));
const result=JSON.parse(await fs.readFile(path.join(root,'result.json'),'utf8'));
const clean=process.argv.includes('--clean');
const dateValue=(y,m,d)=>{
 const t=new Date(Date.UTC(Number(y),Number(m)-1,Number(d)));
 return t.getUTCFullYear()===Number(y)&&t.getUTCMonth()===Number(m)-1&&t.getUTCDate()===Number(d)?t.toISOString().slice(0,10):null;
};
function fullDate(raw){
 const s=String(raw??'').trim();let m=s.match(/^(\d{4})[./-](\d{1,2})[./-](\d{1,2})(?=$|[\s(（])/);
 if(m)return dateValue(m[1],m[2],m[3]);
 m=s.match(/^(\d{4})\.(\d{2})(\d{2})$/);return m?dateValue(m[1],m[2],m[3]):null;
}
let carry=null,filled=0,inferred=0;
const cleanedDates=input.map((row,i)=>{
 const raw=String(row[0]??'').trim();let day=fullDate(raw),note='';
 if(!day&&raw){
  const next=input.slice(i+1).map(r=>fullDate(r[0])).find(Boolean);
  const md=raw.match(/^(\d{1,2})\.(\d{1,2})$/),dom=raw.match(/^(\d{1,2})$/);
  if(carry&&next&&(md||dom)){
   const candidate=md?dateValue(carry.slice(0,4),md[1],md[2]):dateValue(carry.slice(0,4),carry.slice(5,7),dom[1]);
   if(candidate&&candidate>=carry&&candidate<=next&&carry.slice(0,4)===next.slice(0,4)&&(!dom||carry.slice(0,7)===next.slice(0,7))){day=candidate;note=`原日期“${raw}”，结合前后日期补全为${day}`;inferred++;}
  }
 }
 if(day){carry=day;if(raw&&!note&& !/^\d{4}[./-]\d{1,2}[./-]\d{1,2}$/.test(raw)&&!/^\d{4}\.\d{4}$/.test(raw))note=`原日期列内容：${raw}`;}
 else if(/^\d{1,2}月$/.test(raw)){carry=null;note=`原日期“${raw}”缺少具体日，待核对`;}
 else {day=carry;if(day)filled++;if(raw)note=`原日期列非日期内容：${raw}`;}
 if(!day)note=[note,'无法确定收件日期，出库匹配暂不保留'].filter(Boolean).join('；');
 return {day,note,source:row[0]};
});
const serial=day=>(Date.parse(day+'T00:00:00Z')-Date.UTC(1899,11,30))/86400000;
const acceptedByRecord=new Map();let removedPairs=0,affectedRows=0,removedForMissingDate=0;
const recordKey=m=>[key(m.sn),m.shipmentNo,m.workOrderId,m.outTime].join('|');
const key=s=>String(s??'').trim().toUpperCase();
const bySn=new Map(), errors=new Map();
for(const m of result.matches){let k=key(m.sn);if(!bySn.has(k))bySn.set(k,[]);bySn.get(k).push(m);}
for(const e of result.errors){let k=key(e.sn);if(!errors.has(k))errors.set(k,[]);errors.get(k).push(e);}
const categories=Object.entries(result.totals).map(([k,v])=>`${k} ${v}条`).join('；');
const rows=input.map(([date,model,sn],index)=>{
 const k=key(sn), allHits=bySn.get(k)??[], problems=errors.get(k)??[];
 const receipt=cleanedDates[index];
 const hits=clean?allHits.filter(h=>receipt.day&&fullDate(h.outTime)&&fullDate(h.outTime)>=receipt.day):allHits;
 const rejected=allHits.length-hits.length;
 if(clean){
  if(rejected){affectedRows++;removedPairs+=rejected;if(!receipt.day)removedForMissingDate+=rejected;}
  for(const h of hits){const id=recordKey(h);if(!acceptedByRecord.has(id))acceptedByRecord.set(id,[]);acceptedByRecord.get(id).push({row:index+1,date:receipt.day});}
 }
 let status=!k?'未填SN':problems.length?'需核对':hits.length>1?'多条出库记录':hits.length===1?'已匹配':'未查到出库记录';
 const notes=[];
 if(clean&&receipt.note)notes.push(receipt.note);
 if(clean&&rejected)notes.push(receipt.day?`已移除${rejected}条早于收件日期或日期无效的出库匹配`:`收件日期不明确，已移除${rejected}条出库匹配`);
 if(clean&&!receipt.day)status='收件日期待核对';
 else if(clean&&allHits.length&&!hits.length)status='无有效出库记录';
 if(hits.some(h=>!h.outTime)){status='出库时间缺失';notes.push('详情记录存在，但出库时间为空');}
 if(problems.length)notes.push(...problems.map(e=>e.reason));
 if(!allHits.length&&k)notes.push('仅表示当前站点五类已出库列表中未精确匹配，不代表未出库');
 if(/^[+-]?\d+(?:\.\d+)?E[+-]?\d+$/i.test(String(sn).trim()))notes.push('原SN为科学计数法，未推测或还原丢失的数字');
 if(hits.length>1)notes.push('保留全部记录，逐条对应见出库明细');
 return [clean?(receipt.day?serial(receipt.day):''):date,model,sn,status,hits.map(h=>h.outTime||'时间缺失').join('\n'),hits.length,
  [...new Set(hits.map(h=>h.shipmentNo))].join('\n'),[...new Set(hits.map(h=>h.workOrderId))].join('\n'),
  [...new Set(hits.map(h=>h.category))].join('\n'),notes.join('；')];
});
assert.equal(rows.length,input.length);
for(let i=0;i<input.length;i++)assert.deepEqual(rows[i].slice(clean?1:0,3),input[i].slice(clean?1:0));
const counts={};for(const r of rows)counts[r[3]]=(counts[r[3]]??0)+1;
const retainedMatches=clean?result.matches.filter(m=>acceptedByRecord.has(recordKey(m))):result.matches;
const uniqueMatched=clean?new Set(retainedMatches.map(m=>key(m.sn))).size:bySn.size, nonempty=input.filter(r=>key(r[2])).length;
const sourceSNs=new Set(input.map(r=>key(r[2])).filter(Boolean));
assert([...bySn.keys()].every(k=>sourceSNs.has(k)));
assert(result.errors.length===0,'Resolve all failed lookups before final delivery');
assert(result.missingProductRows===0,'Unindexed records need investigation');
const wb=Workbook.create();
function sheet(name,headers,data,widths){
 const s=wb.worksheets.add(name);s.showGridLines=false;
 const last=String.fromCharCode(64+headers.length), end=6+data.length;
 s.getRange(`A1:${last}${Math.max(end,7)}`).format.font={name:'Arial',size:11,color:'#233448'};
 s.getRange(`A1:${last}${Math.max(end,7)}`).format.verticalAlignment='center';
 s.getRange(`A6:${last}6`).values=[headers];
 if(data.length)s.getRange(`A7:${last}${end}`).values=data;
 s.getRange(`A6:${last}${Math.max(end,7)}`).format.rowHeight=25;
 for(let c=0;c<widths.length;c++)s.getRange(`${String.fromCharCode(65+c)}6:${String.fromCharCode(65+c)}${Math.max(end,7)}`).format.columnWidth=widths[c];
 s.getRange(`A6:${last}6`).format={fill:'#244668',font:{name:'Arial',size:11,bold:true,color:'#FFFFFF'},horizontalAlignment:'center',verticalAlignment:'center',rowHeight:32};
 if(data.length){const t=s.tables.add(`A6:${last}${end}`,true,name==='匹配结果'?'MatchResults':'OutboundDetails');t.showFilterButton=true;}
 s.freezePanes.freezeRows(6);s.tabColor=name==='匹配结果'?'#244668':'#7194B6';
 return s;
}
const main=sheet('匹配结果',['仓库收件日期','机型','SN','匹配状态','出库时间（详情原值）','出库记录数','出库编号','工单编号','出库类型','核对说明'],rows,[17,21,32,23,27,15,32,35,27,66]);
main.getRange('A2').values=[['收件登记表 · SN出库时间匹配']];main.getRange('A2').format.font={name:'Arial',size:16,bold:true,color:'#244668'};
const queriedAt=new Intl.DateTimeFormat('zh-CN',{timeZone:'Asia/Shanghai',dateStyle:'short',timeStyle:'medium',hour12:false}).format(new Date(result.queryTimeUtc));
main.getRange('A3').values=[[`来源：BLUETTI ${result.siteName}。查询完成：${queriedAt} 北京时间。`]];
main.getRange('A4').values=[[`保留 ${input.length} 行原始数据；${nonempty} 行有SN；${sourceSNs.size} 个不同SN；已匹配 ${uniqueMatched} 个不同SN。`]];
main.getRange('A5').values=[[clean?'收件日期向下补齐；仅保留不早于对应行收件日期的出库记录。同日出库保留，未换算系统时区。':'按SN精确匹配出库产品；保留全部历史出库。详情时间按系统原值，不推测时区。']];
const end=rows.length+6;
main.getRange(`A7:E${end}`).setNumberFormat('@');main.getRange(`G7:J${end}`).setNumberFormat('@');
if(clean)main.getRange(`A7:A${end}`).setNumberFormat('yyyy-mm-dd');
main.getRange(`A7:C${end}`).format.wrapText=true;
main.getRange(`E7:J${end}`).format.wrapText=true;main.getRange(`E7:J${end}`).format.verticalAlignment='top';
main.getRange(`F7:F${end}`).setNumberFormat('0');main.getRange(`F7:F${end}`).format.horizontalAlignment='right';
main.getRange(`D7:D${end}`).conditionalFormats.add('containsText',{text:'需核对',format:{fill:'#FFF1D5',font:{color:'#8B4A00'}}});
main.getRange(`D7:D${end}`).conditionalFormats.add('containsText',{text:'缺失',format:{fill:'#FCE5E5',font:{color:'#9C2626'}}});
for(let i=0;i<rows.length;i++){
 const r=rows[i], lines=Math.max(1,...r.map(v=>String(v).split('\n').length));
 const noteLines=Math.ceil(String(r[9]).length/31);
 const textWidth=s=>[...String(s)].reduce((n,c)=>n+(c.charCodeAt(0)>255?2:1),0);
 const sourceLines=Math.max(...r.slice(0,3).map((v,c)=>Math.max(...String(v).split('\n').map(t=>Math.ceil(textWidth(t)/[16,20,31][c])))));
 main.getRange(`A${i+7}:J${i+7}`).format.rowHeight=Math.max(25,Math.max(lines,noteLines,sourceLines)*17+8);
}
const detailRows=retainedMatches.map(m=>[m.sn,m.outTime,m.shipmentNo,m.sku,m.count??'',m.category,m.workOrderId,m.rmaNo,m.handlerTime,m.source,...(clean?[acceptedByRecord.get(recordKey(m)).map(r=>r.row).join('\n'),[...new Set(acceptedByRecord.get(recordKey(m)).map(r=>r.date))].join('\n')]:[])]);
const detail=sheet('出库明细',['SN','出库时间（详情原值）','出库编号','出库SKU','数量','出库类型','工单编号','RMA','操作时间（仅供区分）','详情页面',...(clean?['适用原表数据行号','对应收件日期']:[])],detailRows,[32,27,32,34,10,27,35,25,28,90,...(clean?[24,25]:[])]);
detail.getRange('A2').values=[['精确匹配的出库记录']];detail.getRange('A2').format.font={name:'Arial',size:16,bold:true,color:'#244668'};
detail.getRange('A3').values=[['来源：https://after-sales.bluettipower.com/。出库时间取 shipmentDetail.shipmentInfoList[].outTime。']];
detail.getRange('A4').values=[[clean?`日期校验后保留 ${detailRows.length} 条出库记录；每条记录适用的收件行与日期见K、L列。`:`检索 ${result.listRows} 条已出库列表记录，逐条读取 ${result.candidateOrders} 个匹配工单详情，返回 ${detailRows.length} 条去重记录。`]];
detail.getRange('A5').values=[[categories]];
if(detailRows.length){detail.getRange(`A7:D${detailRows.length+6}`).setNumberFormat('@');detail.getRange(`F7:J${detailRows.length+6}`).setNumberFormat('@');detail.getRange(`E7:E${detailRows.length+6}`).setNumberFormat('0');}
// A small read-only provenance note preserves the source's non-table first line without treating it as a record count.
main.getRange('L6').values=[['原CSV说明']];main.getRange('L7').values=[['原CSV首行“8774”作为原始说明保留。']];
main.getRange('L8').values=[['CSV按引号及单元格内换行解析，未填SN行保留。']];
main.getRange('L9').values=[['原始前三列逐格一致；换货机不计作原SN出库。']];
if(clean){
 main.getRange('L9').values=[['机型与SN逐格保留；日期列的非日期原值转存核对说明。']];
 main.getRange('L10').values=[[`向下补齐${filled}行，结合前后日期补全${inferred}行，日期待核对${cleanedDates.filter(r=>!r.day).length}行。`]];
 main.getRange('L11').values=[[`移除${removedPairs}个行级出库匹配，涉及${affectedRows}行；明细中移除${result.matches.length-retainedMatches.length}条无有效对应收件行的记录。`]];
 main.getRange('L6:L11').format.columnWidth=115;
 if(detailRows.length){detail.getRange(`K7:L${detailRows.length+6}`).format.wrapText=true;detail.getRange(`K7:L${detailRows.length+6}`).setNumberFormat('@');
  for(let i=0;i<detailRows.length;i++)detail.getRange(`A${i+7}:L${i+7}`).format.rowHeight=Math.max(25,Math.max(detailRows[i][10].split('\n').length,detailRows[i][11].split('\n').length)*17+8);
 }
 await fs.writeFile(path.join(root,'clean-audit.json'),JSON.stringify({cleanedDates,removedPairs,affectedRows,removedForMissingDate,removedDetailRecords:result.matches.length-retainedMatches.length,filled,inferred,retainedMatches,rows},null,2));
}
main.getRange('L6:L9').format.columnWidth=100;main.getRange('L6').format.font={bold:true};
await fs.mkdir(output,{recursive:true});
console.log(JSON.stringify({rows:rows.length,uniqueSN:sourceSNs.size,uniqueMatched,records:detailRows.length,counts}));
await fs.writeFile(path.join(root,clean?'clean-summary.json':'summary.json'),JSON.stringify({rows:rows.length,uniqueSN:sourceSNs.size,uniqueMatched,records:detailRows.length,counts, ...(clean?{filled,inferred,removedPairs,affectedRows,removedDetailRecords:result.matches.length-retainedMatches.length,uncertainDates:cleanedDates.filter(r=>!r.day).length}: {})},null,2));
const inspection=await wb.inspect({kind:'region',sheetId:main.name,range:'A6:F10',maxChars:2000});console.log(inspection.ndjson??inspection);
for(const [s,range,n]of [[main,'A2:J14','main'],[detail,'A2:I13','details']]){
 const png=await wb.render({sheetName:s.name,range,scale:1.25,format:'png'});
 await fs.writeFile(path.join(root,`${clean?'clean-':''}${n}-preview.png`),new Uint8Array(await png.arrayBuffer()));
}
const xlsx=await SpreadsheetFile.exportXlsx(wb);
await xlsx.save(path.join(output,clean?'美国Logistar收件登记表_日期清洗及出库校验.xlsx':'美国Logistar收件登记表_SN出库时间匹配.xlsx'));
console.log('EXPORTED');
