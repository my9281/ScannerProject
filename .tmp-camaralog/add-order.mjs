import fs from 'node:fs/promises';
import {SpreadsheetFile,FileBlob} from 'file:///C:/Users/Thomas/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/@oai/artifact-tool/dist/artifact_tool.mjs';
const path='camaralog/第三批/照片识别记录_第三批.xlsx';
const wb=await SpreadsheetFile.importXlsx(await FileBlob.load(path)),s=wb.worksheets.getItem('照片记录');const old=JSON.stringify(s.getRange('A1:G10').values);
if(process.argv.includes('--preview')){const p=await wb.render({sheetName:'照片记录',range:'A5:G10',scale:1,format:'png'});await fs.writeFile('.tmp-camaralog/order-before.png',new Uint8Array(await p.arrayBuffer()));process.exit(0);}
const data=JSON.parse(await fs.readFile('.tmp-camaralog/third-batch-decoded.json','utf8'));const codes=data.find(x=>x.file==='6-照片-1.jpg').codes;
const qr=codes.find(x=>x.format==='QR Code');const bar=codes.find(x=>x.format==='Code 128'&&x.text.startsWith('X00'));
const row=['6-照片-1.jpg；6-照片-2.jpg','Not turn on','无法开机',bar?.text??'X004HIRJF1',qr?.text??'未成功解码','散货1；1/5','按用户说明，两张照片为同一单；编码取照片1、手写取照片2；后层被遮挡文字不补写'+(bar?'':'；条码未成功解码，内容由标签印字读取')];
s.tables.items[0].rows.add(null,[row]);s.getRange('A11:G11').copyFrom(s.getRange('A6:G6'),'all');s.getRange('A11:G11').values=[row];s.getRange('A11:G11').format.rowHeight=100;
wb.recalculate();if(old!==JSON.stringify(s.getRange('A1:G10').values))throw Error('Original changed');console.log(JSON.stringify(row));
const p=await wb.render({sheetName:'照片记录',range:'A11:G11',scale:1,format:'png'});await fs.writeFile('.tmp-camaralog/order.png',new Uint8Array(await p.arrayBuffer()));
await (await SpreadsheetFile.exportXlsx(wb)).save(path);
