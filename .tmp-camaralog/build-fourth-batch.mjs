import fs from 'node:fs/promises';
import {Workbook,SpreadsheetFile} from 'file:///C:/Users/Thomas/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/@oai/artifact-tool/dist/artifact_tool.mjs';
const wb=Workbook.create(),s=wb.worksheets.add('照片记录');
const decoded=JSON.parse(await fs.readFile('.tmp-camaralog/fourth-batch-decoded.json','utf8'));
const english=['NO Self Leveling','Wire cable Broken #2','Cable Broken #3','DAMAGED'];
const chinese=['无法自动调平','线缆损坏（标记#2）','线缆损坏（标记#3）','损坏（未注明具体部件）'];
const labels=['第17板；4/6','第17板；1/1','第17板；2/3','第8板；3/11'];
const notes=['照片有运动模糊；手写内容按可见文字整理','原文标记#2保留，不推断为数量','原文标记#3保留，不推断为数量','仅写DAMAGED；照片可见外箱破损，不将其推断为设备内部故障'];
const rows=[['照片','手写记录（英文）','中文整理','条形码内容（Code 128）','二维码内容','收货贴标','核对说明']];
for(let i=1;i<=4;i++){
const file=`${i}-照片-${i}.jpg`,codes=decoded.find(x=>x.file===file).codes;
const bar=codes.find(x=>x.format==='Code 128'&&x.text.startsWith('X00'));
const qr=codes.find(x=>x.format==='QR Code');
if(!bar||(!qr&&i!==1))throw Error('Missing code '+file+JSON.stringify(codes));
rows.push([file,english[i-1],chinese[i-1],bar.text,qr?.text??'未成功解码',labels[i-1],notes[i-1]+(i===1?'；二维码模糊未解码，标签SKU印字为F5100DJKABTX-40M-7C':'' )]);
}
s.getRange('A2').values=[['照片与故障识别记录（第四批）']];
s.getRange('A3').values=[['英文按原意整理，笔迹或拼写疑点见核对说明；条形码及二维码为机器解码结果。']];
s.getRange('A5:G9').values=rows;s.tables.add('A5:G9',true,'PhotoRecords4');s.showGridLines=false;
s.getRange('A1:G9').format.font={name:'Arial',size:11};s.getRange('A2').format.font={bold:true,size:16};s.getRange('A3').format.font={size:10,color:'#64748B'};
for(const [c,w] of Object.entries({A:20,B:43,C:36,D:29,E:32,F:22,G:46}))s.getRange(`${c}:${c}`).format.columnWidth=w;
s.getRange('A5:G5').format={fill:'#263D52',font:{bold:true,color:'#FFFFFF'},rowHeight:30};
s.getRange('A6:G9').format.wrapText=true;s.getRange('A6:G9').format.rowHeight=80;s.getRange('A5:G9').format.verticalAlignment='center';
s.getRange('D6:E9').setNumberFormat('@');s.freezePanes.freezeRows(5);s.freezePanes.freezeColumns(1);
wb.recalculate();console.log((await wb.inspect({kind:'table',range:'照片记录!A5:G9',tableMaxRows:5,tableMaxCols:7,maxChars:2200})).ndjson);
const p=await wb.render({sheetName:'照片记录',range:'A5:G9',scale:1,format:'png'});await fs.writeFile('.tmp-camaralog/fourth-batch.png',new Uint8Array(await p.arrayBuffer()));
await (await SpreadsheetFile.exportXlsx(wb)).save('camaralog/第四批/照片识别记录_第四批.xlsx');
