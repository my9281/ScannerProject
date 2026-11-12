import fs from 'node:fs/promises';
import {SpreadsheetFile,FileBlob} from 'file:///C:/Users/Thomas/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/@oai/artifact-tool/dist/artifact_tool.mjs';
const wb=await SpreadsheetFile.importXlsx(await FileBlob.load('camaralog/照片识别记录.xlsx'));
const s=wb.worksheets.getItem('照片记录');
const old=JSON.stringify(s.getRange('A5:G15').values);
if(process.argv.includes('--preview')){
const p=await wb.render({sheetName:'照片记录',range:'A11:G15',scale:1,format:'png'});
await fs.writeFile('.tmp-camaralog/third-before.png',new Uint8Array(await p.arrayBuffer()));
console.log((await wb.inspect({kind:'table',range:'照片记录!A11:G15',tableMaxRows:1,maxChars:500})).ndjson);process.exit(0);
}
const decoded=JSON.parse(await fs.readFile('.tmp-camaralog/decoded.json','utf8'));
const labels=['第6板；10/14','第6板；6/10','第6板；6/14','第6板；3/8','第6板；12/14'];
const notes=['与照片9编码相同，保留独立照片记录','与照片8、10编码相同，保留独立故障记录','与照片9、11编码相同，保留独立照片记录','与照片7编码相同，手写故障不同，分别保留','与照片9、11、13编码相同，保留独立照片记录'];
const rows=[];const extra=[];
for(let i=11;i<=15;i++){
 const file=`${i}-照片-${i}.jpg`;const codes=decoded.find(x=>x.file===file).codes;
 const qr=codes.find(x=>x.format==='QR Code');
 const barcode=codes.find(x=>x.format==='Code 128'&&x.text.startsWith('X00'));
 if(!qr||!barcode)throw Error('Missing main code '+file);
 rows.push([file,'NO self leveling','无自动调平功能',barcode.text,qr.text,labels[i-11],notes[i-11]]);
 for(const c of codes)if(c!==qr&&c!==barcode)extra.push([file,'其他可见标签',c.format,c.text,'机器解码成功；与主标签分开保留']);
}
s.tables.items[0].rows.add(null,rows);
s.getRange('A16:G20').copyFrom(s.getRange('A11:G15'),'all');
s.getRange('A16:G20').values=rows;s.getRange('A16:G20').format.rowHeight=80;
const b=wb.worksheets.getItem('其他可见编码');
if(extra.length){b.tables.items[0].rows.add(null,extra);b.getRange(`A8:E${7+extra.length}`).format.wrapText=true;b.getRange(`A8:E${7+extra.length}`).format.rowHeight=50;}
wb.recalculate();
if(JSON.stringify(s.getRange('A5:G15').values)!==old)throw Error('Previous records changed');
console.log(JSON.stringify({rows,extra}));
for(const [sheet,range,name] of [['照片记录','A16:G20','third'],...(extra.length?[['其他可见编码',`A4:E${7+extra.length}`,'third-other']]:[])]){
const p=await wb.render({sheetName:sheet,range,scale:1,format:'png'});await fs.writeFile(`.tmp-camaralog/${name}.png`,new Uint8Array(await p.arrayBuffer()));
}
await (await SpreadsheetFile.exportXlsx(wb)).save('camaralog/照片识别记录.xlsx');
