import fs from 'node:fs/promises';
import { Workbook, SpreadsheetFile } from 'file:///C:/Users/Thomas/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/@oai/artifact-tool/dist/artifact_tool.mjs';
const dir = 'D:/GitRepos/ScannerProject/outputs/scan_20261023';
const rows = [
['SANYI-X004SJ466J','Pipeline camera'],
['SANYI-X004Z9R7S1','XSP-S5517DC'],
['SANYI-X004TR4X4F','S810ASMKT-100M'],
['SANYI-X004HFUJND','512Hz Receiver Pipe Locator'],
['SANYI-X0053S67OX','S880DSTKM-7C-50M'],
['SANYI-X004S5PMT3','S860DSTKM-7C-30M'],
['SANYI-X004J0BBIN','Pipeline camera'],
['SANYI-X0053XZ5B9','S860DSTKMS68-7C-30M'],
['SANYI-X004TQW30R','S810ASMKT-50M'],
['SANYI-X004S5X9NT','Pipeline camera'],
['SANYI-X0053FYWGV','S880DSTKMS68-7C-30M'],
['SANYI-X003W9CS39','S8417DC-30m'],
['SANYI-X004HIO7LP','Pipeline camera'],
['SANYI-X004HIRJF1','Pipeline camera'],
['SANYI-X004HIRJCJ','Pipeline camera'],
['SANYI-X004IJHF6H','Pipeline camera'],
['SANYI-X004IJV3IX','Pipeline camera'],
];
const wb = Workbook.create();
const s = wb.worksheets.add('产品清单');
s.showGridLines = false;
s.getRange('A1:B18').values = [['产品代码','产品标题'],...rows];
s.getRange('A1:B18').format.font = { name:'Arial',size:11,color:'#172033' };
s.getRange('A1:B18').format.rowHeight = 25;
s.getRange('A1:B18').format.verticalAlignment = 'center';
s.getRange('A1:A18').format.columnWidth = 29;
s.getRange('B1:B18').format.columnWidth = 37;
s.getRange('A1:B18').setNumberFormat('@');
s.tables.add('A1:B18',true,'Products');
s.getRange('A1:B1').format.fill = '#24476B';
s.getRange('A1:B1').format.font = {name:'Arial',size:11,bold:true,color:'#FFFFFF'};
s.getRange('A1:B1').format.horizontalAlignment = 'center';
wb.recalculate();
console.log((await wb.inspect({kind:'table',range:'产品清单!A1:B18',include:'values',tableMaxRows:18,tableMaxCols:2,maxChars:6000})).ndjson);
const preview = await wb.render({sheetName:'产品清单',range:'A1:B18',scale:1.5,format:'png'});
await fs.writeFile(dir+'/preview.png',new Uint8Array(await preview.arrayBuffer()));
await (await SpreadsheetFile.exportXlsx(wb)).save(dir+'/产品代码和产品标题.xlsx');
if(rows.length!==17 || new Set(rows.map(r=>r[0])).size!==17) throw Error('Row verification failed');
