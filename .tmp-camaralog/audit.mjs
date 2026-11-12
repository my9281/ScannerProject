import fs from 'node:fs/promises';
import {SpreadsheetFile,FileBlob} from 'file:///C:/Users/Thomas/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/@oai/artifact-tool/dist/artifact_tool.mjs';
const wb=await SpreadsheetFile.importXlsx(await FileBlob.load('camaralog/照片识别记录.xlsx'));
const original=JSON.stringify(wb.worksheets.getItem('照片记录').getRange('A1:G25').values);
if(process.argv.includes('--preview')){const p=await wb.render({sheetName:'照片记录',range:'A21:G25',scale:1,format:'png'});await fs.writeFile('.tmp-camaralog/audit-before.png',new Uint8Array(await p.arrayBuffer()));process.exit(0);}
const a=wb.worksheets.add('审计挑出');a.showGridLines=false;
a.getRange('A2').values=[['手写记录审计：不像维修记录的内容']];
a.getRange('A3').values=[['按照片文字审计，不判断实际故障真伪。20条均未记载维修动作、更换件或修后测试。']];
a.getRange('A5:F11').values=[
['分类','照片编号','手写原文','审计判断','缺少的证据','建议补充'],
['优先挑出：缺件备注','16','NO WIRE','更像缺线或配件不齐备注；不能据此认定设备本体故障。','未说明缺哪根线、标准配置是否包含、是否影响使用。','核对装箱清单，写明缺失配件名称、数量及测试影响。'],
['优先挑出：测试状态','20','Can not Self leveling；待测','“待测”是流程状态；与无法自动调平的结论并列，测试依据不清。','未说明“待测”针对整机、该功能还是修后复测。','分开填写客户反馈、实测结论、待测项目及测试日期。'],
['待核实：功能描述','6、8、9、11、12、13、14、15、17、18（另见20）','NO self leveling','10条重复写法只表述无自动调平，未区分型号不支持、未开启或功能失效；不直接判为无效记录。','缺少对应型号功能依据、启用方式及实际画面测试。','先核对该型号是否支持，再记录测试条件、预期表现及实际表现。'],
['描述过于笼统','7','Bad screen','像故障标题，但无法据此确定维修项目。','未区分不亮、花屏、破裂、触控异常等现象。','补充具体现象、通电结果和故障照片。'],
['缺少测试依据','4','Pipe locator Does not Charge efficiently','有故障含义，但“充电效率低”缺少可核对的量化结果。','未记录充电器、初始电量、时长、电压电流或电量变化。','补充充电测试条件、时长和结果。'],
['整体记录性质','1–20','全部手写标签','目前均为故障、缺件或状态备注，不能作为已完成维修的证明；其他损坏和无信号描述仍可作故障线索。','未记录检测过程、维修动作、更换件、处理人、日期和复测结论。','实际维修时另记处理动作与复测结果，不修改照片原文。']
];
a.tables.add('A5:F11',true,'AuditFindings');
a.getRange('A1:F11').format.font={name:'Arial',size:11};a.getRange('A2').format.font={bold:true,size:16};
a.getRange('A3').format.font={size:10,color:'#64748B'};
a.getRange('A5:F5').format={fill:'#263D52',font:{bold:true,color:'#FFFFFF'},rowHeight:30};
for(const [c,w] of Object.entries({A:26,B:34,C:42,D:62,E:52,F:57}))a.getRange(`${c}:${c}`).format.columnWidth=w;
a.getRange('A6:F11').format.wrapText=true;a.getRange('A6:F11').format.verticalAlignment='center';a.getRange('A6:F11').format.rowHeight=90;
a.getRange('A6:A7').format.fill='#FFF2CC';a.freezePanes.freezeRows(5);
wb.recalculate();if(JSON.stringify(wb.worksheets.getItem('照片记录').getRange('A1:G25').values)!==original)throw Error('Source changed');
console.log((await wb.inspect({kind:'table',range:'审计挑出!A5:F11',tableMaxRows:7,tableMaxCols:6,maxChars:1800})).ndjson);
const p=await wb.render({sheetName:'审计挑出',range:'A5:F11',scale:1,format:'png'});await fs.writeFile('.tmp-camaralog/audit.png',new Uint8Array(await p.arrayBuffer()));
await (await SpreadsheetFile.exportXlsx(wb)).save('camaralog/照片识别记录.xlsx');
