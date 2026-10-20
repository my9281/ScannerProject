import fs from 'node:fs/promises';
import {FileBlob,SpreadsheetFile} from 'file:///C:/Users/Thomas/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/@oai/artifact-tool/dist/artifact_tool.mjs';
const dir='D:/GitRepos/ScannerProject/outputs/aug_rebound/';
const data=JSON.parse(await fs.readFile(dir+'declared_join.json','utf8'));
const w=await SpreadsheetFile.importXlsx(await FileBlob.load(dir+'盘库_补充尺寸重量及寄收城市州.xlsx'));const s=w.worksheets.getItemAt(0);
const values=Array.from({length:308},()=>Array(5).fill(null));values[0]=['申报单重（订单导出原值）','申报重量单位','申报商品及单重明细','申报重量匹配状态','订单导出来源行号'];
for(const rec of data){const rs=rec.declared;const available=rs.filter(x=>x.weight!==null&&x.weight!=='');const multi=rs.length>1;values[rec.row-1]=[available.length?(multi?available.map(x=>String(x.weight)).join(' / '):available[0].weight):null,available.length?'源表未注明':null,rs.length?rs.map(x=>`${String(x.model??'').trim()||'商品名称为空'}：${x.weight===null||x.weight===''?'单重为空':x.weight}`).join('；'):null,!rs.length?'未匹配到订单':!available.length?'订单申报单重为空':multi?'同单多个商品，单重分别列示':available[0].weight===0?'已匹配，源表单重为0':'已匹配',rs.length?rs.map(x=>x.row).join(' / '):null];
const c=rec.cost;if(c.length===1){const v=c[0].values.map(x=>x===''?null:x);for(const i of [0,1,2,4,6,8])if(v[i]!==null&&Number.isFinite(Number(v[i])))v[i]=Number(v[i]);s.getRange(`I${rec.row}:X${rec.row}`).values=[[...v,'已匹配',c[0].row]];}else if(!c.length)s.getRange(`I${rec.row}:X${rec.row}`).values=[[...Array(14).fill(null),'未匹配',null]];else throw Error('Duplicate cost tracking');
}
s.getRange('Y1:AC308').values=values;s.getRange('Y1:AC1').format={fill:'#284B63',font:{bold:true,color:'#FFFFFF'},wrapText:true,rowHeight:42};s.getRange('Y:AC').format.columnWidth=29;s.getRange('AA:AA').format.columnWidth=65;s.getRange('AA2:AB307').format.wrapText=true;s.getRange('Y2:Y307').setNumberFormat('0.###');
const n=w.worksheets.getItem('尺寸重量匹配说明');n.getRange('A11:B15').values=[['申报重量来源','副本订单列表导出.xlsx，订单列表页“申报单重”；按服务商单号匹配。'],['申报重量单位','源表没有单位字段，本次不推定为kg或lb，不与成本表重量直接作差。'],['多商品单号','申报单重按商品分别列出；源表没有数量字段，因此不相加推算整票申报重量。'],['零值','源表申报单重为0的保留0，并在匹配状态中标记。'],['重新匹配结果','成本表251条匹配；订单导出277条匹配，29条未匹配。']];n.getRange('B11:B15').format.wrapText=true;n.getRange('A11:B15').format.autofitRows();
w.recalculate();console.log((await w.inspect({kind:'table',range:'Sheet1!Y1:AC6',tableMaxRows:6,tableMaxCols:5,maxChars:1500})).ndjson);
await fs.writeFile(dir+'declared-preview.png',new Uint8Array(await (await w.render({sheetName:s.name,range:'Y1:AC6',scale:1.5,format:'png'})).arrayBuffer()));
await (await SpreadsheetFile.exportXlsx(w)).save(dir+'盘库_重新匹配_含申报重量.xlsx');

