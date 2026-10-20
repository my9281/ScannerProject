import fs from 'node:fs/promises';
import {Workbook,SpreadsheetFile} from 'file:///C:/Users/Thomas/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/@oai/artifact-tool/dist/artifact_tool.mjs';
const dir=new URL('.',import.meta.url);
const data=JSON.parse(await fs.readFile(new URL('data.json',dir),'utf8'));
const wb=Workbook.create();
const s=wb.worksheets.add('卡板数量汇总');
const d=wb.worksheets.add('统计明细');
s.getRange('A2').values=[['卡板数量统计']];
s.getRange('A4:B4').values=[['卡板','数量合计']];
d.getRange('A1:D1').values=[['卡板','条码','数量','原表行号']];
d.getRange('F1').values=[['来源：camara.xlsx / Sheet2 / C列数量（与I列一致）']];
d.getRange('A2:D141').values=data.detail;
data.groups.forEach((g,i)=>{
 const r=i+5;
 s.getRange(`A${r}`).values=[[g[0]]];
 s.getRange(`B${r}`).formulas=[[`=SUMIFS('统计明细'!$C$2:$C$141,'统计明细'!$A$2:$A$141,A${r})`]];
});
s.getRange('A22').values=[['总合计']];
s.getRange('B22').formulas=[['=SUM(B5:B21)']];
for(const sh of [s,d]){
 sh.showGridLines=false;
 sh.getUsedRange().format.font={name:'Arial',size:11};
 sh.getUsedRange().format.rowHeight=23;
 sh.getRange('A:A').format.columnWidth=22;
 sh.getRange('B:B').format.columnWidth=22;
}
s.getRange('A2').format.font={size:16,bold:true};
s.getRange('A4:B4').format={fill:'#24476A',font:{color:'#FFFFFF',bold:true},horizontalAlignment:'center'};
s.getRange('A22:B22').format={fill:'#E5EDF5',font:{bold:true}};
s.getRange('B5:B22').setNumberFormat('#,##0');
d.getRange('A1:D1').format={fill:'#24476A',font:{color:'#FFFFFF',bold:true}};
d.getRange('C:D').format.columnWidth=14;
d.getRange('C2:D141').setNumberFormat('0');
d.getRange('F:F').format.columnWidth=75;
d.freezePanes.freezeRows(1);
wb.recalculate();
const vals=s.getRange('B5:B21').values.flat();
if(vals.some((v,i)=>v!==data.groups[i][3])||s.getRange('B22').values[0][0]!==587)throw new Error('Totals do not reconcile');
console.log((await wb.inspect({kind:'table',range:'卡板数量汇总!A4:B22',tableMaxRows:20,tableMaxCols:2})).ndjson);
for(const [sh,range,file] of [[s,'A1:C23','summary.png'],[d,'A1:D12','detail.png']]){
 const blob=await wb.render({sheetName:sh.name,range,scale:1.5,format:'png'});
 await fs.writeFile(new URL(file,dir),new Uint8Array(await blob.arrayBuffer()));
}
await (await SpreadsheetFile.exportXlsx(wb)).save('D:/GitRepos/ScannerProject/outputs/pallet_summary/camara_卡板数量汇总.xlsx');
