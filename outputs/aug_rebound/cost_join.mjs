import fs from 'node:fs/promises';
import {FileBlob,SpreadsheetFile} from 'file:///C:/Users/Thomas/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/@oai/artifact-tool/dist/artifact_tool.mjs';
const dir='D:/GitRepos/ScannerProject/outputs/aug_rebound/';
const d=JSON.parse(await fs.readFile(dir+'cost_join.json','utf8'));
const w=await SpreadsheetFile.importXlsx(await FileBlob.load(dir+'盘库_机型关联_追踪号文本.xlsx'));const s=w.worksheets.getItemAt(0);
const before=s.getRange('A1:H308').values;const formulas=s.getRange('A1:H308').formulas;
const headers=['包裹长度（成本表）','包裹宽度（成本表）','包裹高度（成本表）','尺寸单位（原代码）','寄件人州/省','寄件人城市','收件人州/省','收件人城市','成本表匹配状态','成本表来源行号'];
const vals=Array.from({length:308},()=>Array(10).fill(null));vals[0]=headers;
for(const rec of d){if(!rec.matches.length){vals[rec.row-1][8]='未匹配';continue;}const m=rec.matches[0];const v=[...m.values.slice(0,4),...m.values.slice(10)].map(x=>x===''?null:x);for(const i of [0,1,2])if(v[i]!==null&&Number.isFinite(Number(v[i])))v[i]=Number(v[i]);vals[rec.row-1]=[...v,'已匹配',m.row];}
s.getRange('I1:R308').values=vals;s.getRange('I1:R1').format={fill:'#284B63',font:{bold:true,color:'#FFFFFF'},rowHeight:42,wrapText:true};s.getRange('I:R').format.columnWidth=23;s.getRange('N:N').format.columnWidth=30;s.getRange('P:P').format.columnWidth=30;
for(const c of ['I','J','K'])s.getRange(`${c}2:${c}307`).setNumberFormat('0.##');
const note=w.worksheets.add('尺寸地址匹配说明');
const notes=[['项目','说明'],['匹配依据','基础表跟踪号与成本表跟踪单号匹配；纯数字长单号取后12位。'],['匹配结果','306条：251条已匹配，55条未匹配；成本表无重复匹配单号。'],['尺寸口径','成本表只有包裹长度、宽度、高度一组字段，未说明是申报还是实际；不能据此提供两套尺寸。'],['申报尺寸','本次两份文件未提供单独的申报长宽高，未推测或填造。'],['尺寸单位','保留成本表原代码 C / I，便于核对。'],['地址字段','采用成本表当前寄件人、收件人的州/省和城市字段。'],['来源','bluetti 打单成本价_中文表头.xlsx，Data页；来源行号含首行表头。'],['基础表','采用本次读取的用户手动修改版本，保留原有8列数据、公式及原机型汇总。']];
note.getRange(`A1:B${notes.length}`).values=notes;note.getRange('A:A').format.columnWidth=22;note.getRange('B:B').format.columnWidth=105;note.getRange('B1:B10').format.wrapText=true;note.getRange('A1:B1').format={fill:'#284B63',font:{bold:true,color:'#FFFFFF'}};note.getRange('A1:B10').format.autofitRows();
w.recalculate();
if(JSON.stringify(before)!==JSON.stringify(s.getRange('A1:H308').values)||JSON.stringify(formulas)!==JSON.stringify(s.getRange('A1:H308').formulas))throw Error('Source preservation failed');
console.log((await w.inspect({kind:'table',range:'Sheet1!I1:R3',tableMaxRows:3,tableMaxCols:10,maxChars:2000})).ndjson);
await fs.writeFile(dir+'cost-preview.png',new Uint8Array(await (await w.render({sheetName:s.name,range:'I1:P6',scale:1.5,format:'png'})).arrayBuffer()));
await (await SpreadsheetFile.exportXlsx(w)).save(dir+'盘库_补充长宽高及寄收州城市.xlsx');

