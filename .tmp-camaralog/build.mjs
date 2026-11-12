import fs from 'node:fs/promises';
import {Workbook,SpreadsheetFile} from 'file:///C:/Users/Thomas/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/@oai/artifact-tool/dist/artifact_tool.mjs';
const wb=Workbook.create();
const s=wb.worksheets.add('照片记录');
const rows=[
['照片','手写记录（英文）','中文整理','条形码内容（Code 128）','二维码内容','收货贴标','核对说明'],
['1-照片-1.jpg','Broken Screen','屏幕损坏','X004HIRJCJ','S8951DSRTMS68-20M','第4板；1/3','与照片4主标签编码相同，分别保留故障记录'],
['2-照片-2.jpg','NO Signal','无信号','X004HIQ0BZ','S8951DSTM-30M','第6板；6/8','条码中的 Q、0 已经机器解码确认'],
['3-照片-3.jpg','Pipe locator does not charge\nBroken glass screen &\nBroken pipe locator case','管线定位器无法充电；屏幕玻璃破损；管线定位器外壳损坏','X004HIO7LP','S8951DSRTMS68-30M','第10板；11/14','主箱记录；被黑笔涂掉的小标签无法读取'],
['4-照片-4.jpg','Pipe locator Does not Charge efficiently','管线定位器充电效率低','X004HIRJCJ','S8951DSRTMS68-20M','第10板；10/11','与照片1主标签编码相同，不据此判定为同一件实物'],
['5-照片-5.jpg','Broken Screen glass','屏幕玻璃破损','X004HIRJF1','S8951DSRTMS68-50M','第5板；6/9','主箱记录；背景运输标签未与本箱关联']
];
s.getRange('A2').values=[['照片与故障识别记录']];
s.getRange('A3').values=[['英文按语义合并换行；条码及二维码为机器解码结果，照片原件保留。']];
s.getRange('A5:G10').values=rows;
s.tables.add('A5:G10',true,'PhotoRecords');
function style(sh,range,widths){
 sh.showGridLines=false;sh.getRange(range).format.font={name:'Arial',size:11};
 sh.getRange(range).format.verticalAlignment='center';
 for(const [col,width] of Object.entries(widths))sh.getRange(`${col}:${col}`).format.columnWidth=width;
}
style(s,'A1:G10',{A:20,B:43,C:36,D:29,E:32,F:22,G:46});
s.getRange('A2').format.font={bold:true,size:16};
s.getRange('A3').format.font={size:10,color:'#64748B'};
s.getRange('A5:G5').format={fill:'#263D52',font:{bold:true,color:'#FFFFFF'},rowHeight:30};
s.getRange('A6:G10').format.wrapText=true;s.getRange('A6:G10').format.rowHeight=80;
s.getRange('D6:E10').setNumberFormat('@');
s.freezePanes.freezeRows(5);s.freezePanes.freezeColumns(1);
const b=wb.worksheets.add('其他可见编码');
b.getRange('A2').values=[['背景箱子及未读标签']];
b.getRange('A4:E7').values=[
 ['照片','位置','编码类型','内容','说明'],
 ['3-照片-3.jpg','右下方背景箱标签','QR Code','S8951DM-50M','机器解码成功；不属于主箱，未关联手写故障'],
 ['3-照片-3.jpg','主箱右侧小标签','条形码','无法读取','标签被黑笔涂盖'],
 ['5-照片-5.jpg','右侧背景箱运输标签','条形码','未成功解码','标签倾斜、局部遮挡，不从可见文字猜测条码内容']
];
b.tables.add('A4:E7',true,'OtherCodes');
style(b,'A1:E7',{A:20,B:30,C:20,D:30,E:65});
b.getRange('A2').format.font={bold:true,size:16};
b.getRange('A4:E4').format={fill:'#263D52',font:{bold:true,color:'#FFFFFF'},rowHeight:30};
b.getRange('A5:E7').format.wrapText=true;b.getRange('A5:E7').format.rowHeight=50;
wb.recalculate();
console.log((await wb.inspect({kind:'table',range:'照片记录!A5:G10',tableMaxRows:6,tableMaxCols:7,maxChars:3000})).ndjson);
for(const [sheet,range,name] of [['照片记录','A5:G10','main'],['其他可见编码','A2:E7','other']]){
const blob=await wb.render({sheetName:sheet,range,scale:1,format:'png'});
await fs.writeFile(`.tmp-camaralog/${name}.png`,new Uint8Array(await blob.arrayBuffer()));
}
await (await SpreadsheetFile.exportXlsx(wb)).save('camaralog/照片识别记录.xlsx');
