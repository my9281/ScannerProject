import fs from 'node:fs/promises';
import {FileBlob,SpreadsheetFile} from 'file:///C:/Users/Thomas/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/@oai/artifact-tool/dist/artifact_tool.mjs';
const dir='D:/GitRepos/ScannerProject/outputs/aug_rebound/';
const d=JSON.parse(await fs.readFile(dir+'stock_data.json','utf8'));
const w=await SpreadsheetFile.importXlsx(await FileBlob.load('C:/Users/Thomas/Desktop/盘库.xlsx'));
const s=w.worksheets.getItemAt(0);
await fs.writeFile(dir+'stock-before.png',new Uint8Array(await (await w.render({sheetName:s.name,range:'A1:D6',scale:1.5,format:'png'})).arrayBuffer()));
const heads=['机型（收货记录）','匹配状态','SN（收货记录）','RMA编号','收货记录CSV行号'];
const vals=Array.from({length:308},()=>[null,null,null,null,null]);vals[0]=heads;
const summary=new Map();let matched=0;
for(const m of d.matches){const r=m.records;const unique=k=>[...new Set(r.map(x=>String(x[k]??'').trim()).filter(Boolean))].join(' / ');const models=unique('model');vals[m.row-1]=[models||null,r.length?(models?(r.length>1?'匹配多条收货记录':'已匹配'):'已匹配，机型为空'):'未匹配',unique('sn')||null,unique('rma')||null,unique('row')||null];if(r.length){matched++;summary.set(models||'机型为空',(summary.get(models||'机型为空')||0)+1);}}
s.getRange('E1:I308').values=vals;
s.getRange('E1:I1').format={fill:'#284B63',font:{bold:true,color:'#FFFFFF'},wrapText:true,rowHeight:32};
s.getRange('E:E').format.columnWidth=24;s.getRange('F:F').format.columnWidth=25;s.getRange('G:G').format.columnWidth=38;s.getRange('H:H').format.columnWidth=27;s.getRange('I:I').format.columnWidth=23;
s.freezePanes.freezeRows(1);
const sum=w.worksheets.add('匹配机型汇总');const rows=[['机型','基础表记录数'],...[...summary],['已匹配合计',matched],['未匹配',d.matches.length-matched],['基础表记录合计',d.matches.length]];
sum.getRange(`A1:B${rows.length}`).values=rows;sum.getRange('A:A').format.columnWidth=26;sum.getRange('B:B').format.columnWidth=20;sum.getRange('A1:B1').format={fill:'#284B63',font:{bold:true,color:'#FFFFFF'}};
sum.getRange('D1').values=[['匹配依据：基础表跟踪号 ↔ 收货记录物流单号；FedEx纯数字长单号取后12位。']];sum.getRange('D2').values=[['同单多条记录保留全部不同机型；未匹配记录不推测机型。']];sum.getRange('D4').values=[['来源：美国Logistar仓收件登记表-更新版9.02.csv（行号含前两行表头）']];sum.getRange('D:D').format.columnWidth=100;
w.recalculate();
console.log((await w.inspect({kind:'table',range:'Sheet1!A209:I213',tableMaxRows:5,tableMaxCols:9,maxChars:2000})).ndjson);
await fs.writeFile(dir+'stock-after.png',new Uint8Array(await (await w.render({sheetName:s.name,range:'A209:I213',scale:1.5,format:'png'})).arrayBuffer()));
await (await SpreadsheetFile.exportXlsx(w)).save(dir+'盘库_按FedEx后12位关联机型.xlsx');
console.log(JSON.stringify({matched,missing:d.matches.length-matched,summary:[...summary]}));


