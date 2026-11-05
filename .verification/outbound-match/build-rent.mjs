import fs from 'node:fs/promises';
import {FileBlob,SpreadsheetFile} from '@oai/artifact-tool';
const data=JSON.parse(await fs.readFile('.verification/inventory-probe/rent-match.json','utf8'));
const wb=await SpreadsheetFile.importXlsx(await FileBlob.load(data.source));
const s=wb.worksheets.getItem('仓租结算');
for(const r of data.rows){const n=r.excelRow;s.getRange(`D${n}:H${n}`).values=[[r.end,r.time,null,null,r.basis]];s.getRange(`F${n}:G${n}`).formulas=[[`=MAX(0,D${n}-MAX(C${n},DATE(2026,9,1))+1)`,`=MAX(0,D${n}-C${n}+1)`]];s.getRange(`A${n}:K${n}`).format.rowHeight=62;}
const end=data.rows.length+1;
s.getRange(`D2:D${end}`).setNumberFormat('yyyy-mm-dd');s.getRange(`E2:E${end}`).setNumberFormat('yyyy-mm-dd hh:mm:ss');s.getRange(`F2:G${end}`).setNumberFormat('0');s.getRange(`H2:H${end}`).format.wrapText=true;
wb.recalculate();
console.log((await wb.inspect({kind:'table',range:'仓租结算!A1:H5',include:'values,formulas',tableMaxRows:5,tableMaxCols:8})).ndjson);
console.log((await wb.inspect({kind:'match',searchTerm:'#REF!|#DIV/0!|#VALUE!|#NAME\\?|#NUM!|#SPILL!|#CALC!',options:{useRegex:true,maxResults:10},summary:'formula errors'})).ndjson);
for(const [sheet,range,file] of [['仓租结算','A1:H7','rent-updated'],['待核对','A1:E7','rent-pending']]){const png=await wb.render({sheetName:sheet,range,scale:1.1,format:'png'});await fs.writeFile(`.verification/inventory-probe/${file}.png`,new Uint8Array(await png.arrayBuffer()));}
const out='outputs/01a16445-7eb1-73a1-910c-60167e7f1c01/2026年9月-仓租结算_重新核算截止日期.xlsx';await(await SpreadsheetFile.exportXlsx(wb)).save(out);console.log('EXPORTED');
