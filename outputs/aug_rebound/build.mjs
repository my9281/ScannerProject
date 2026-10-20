import fs from 'node:fs/promises';
import {Workbook,SpreadsheetFile} from 'file:///C:/Users/Thomas/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/@oai/artifact-tool/dist/artifact_tool.mjs';
const d=JSON.parse(await fs.readFile(new URL('./data.json',import.meta.url),'utf8'));
const map=new Map();
const key=v=>String(v??'').trim().replace(/\.0$/,'');
for(const r of d.source.slice(1)){const k=key(r[0]);if(!k)continue;if(!map.has(k))map.set(k,new Set());if(String(r[2]??'').trim())map.get(k).add(String(r[2]).trim());}
let matched=0,missing=0,multi=0; const counts=new Map();
const rows=d.rows.map((r,i)=>{if(i===0)return [...r,'机型（按单号关联）'];if(!r[0])return [...r,null];const models=[...(map.get(key(r[0]))??[])];const model=models.length?models.join(' / '):(map.has(key(r[0]))?'订单商品名称为空':'未匹配到订单');if(models.length){matched++;if(models.length>1)multi++;counts.set(model,(counts.get(model)??0)+1);}else missing++;return [...r,model];});
const w=Workbook.create();const s=w.worksheets.add(d.sheet);s.getRange(`A1:G${rows.length}`).values=rows;
s.getRange('A:G').format.columnWidth=20;s.getRange('G:G').format.columnWidth=42;
s.getRange('A1:G1').format={fill:'#284B63',font:{bold:true,color:'#FFFFFF'},rowHeight:32,wrapText:true};
s.getRange(`C2:C${rows.length}`).setNumberFormat('0.00');s.getRange(`E2:F${rows.length}`).setNumberFormat('0.000');
s.getRange(`G2:G${rows.length}`).format.wrapText=true;
s.freezePanes.freezeRows(1);w.recalculate();
console.log((await w.inspect({kind:'table',range:`'${d.sheet}'!A1:G6`,tableMaxRows:6,tableMaxCols:7,maxChars:2000})).ndjson);
await fs.writeFile(new URL('./preview.png',import.meta.url),new Uint8Array(await (await w.render({sheetName:d.sheet,range:'A1:G12',scale:1.5,format:'png'})).arrayBuffer()));
await (await SpreadsheetFile.exportXlsx(w)).save(new URL('./column.xlsx',import.meta.url).pathname.replace(/^\/([A-Za-z]:)/,'$1'));
await fs.writeFile(new URL('./result.json',import.meta.url),JSON.stringify({matched,missing,multi,counts:[...counts].sort((a,b)=>b[1]-a[1]),models:rows.map(r=>r[6])}));
console.log(JSON.stringify({matched,missing,multi,counts:[...counts].sort((a,b)=>b[1]-a[1])}));
