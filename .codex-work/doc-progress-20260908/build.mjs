import fs from 'node:fs/promises';
import {Workbook,SpreadsheetFile} from '@oai/artifact-tool';
const rows=[["登录与加密会话","Windows","2026-08-05","34156e4","已实现","账号登录、会话和记住密码；Windows DPAPI"],["扫码去重与日志","Windows","2026-08-06","03d4009 / 6df717b","已实现","运行期忽略大小写去重，日志含时间"],["OID首扫及重扫","Windows","2026-08-06","0a76864","已实现","首扫记录，重扫按份数打印"],["设备型号识别","Windows","2026-08-06","dfaebc4","已实现","标签突出型号和尾五位"],["中文尾号播报","Windows","2026-08-06","2e4d2a1 / 223dfdf","已实现","打印后异步播报"],["主界面三语言","Windows","2026-08-06","a34ffd0","已实现","中文、英语、西班牙语"],["离线扫码与小屏布局","Android/MAUI","2026-08-07","c80cd6f","已实现","单列滚动页面、本地模式"],["扫描日志上传","MAUI","2026-08-08","6af01f2 / 5cc88e6","已实现","上传服务及可靠性修复"],["移动端禁用自动打印","Android","2026-08-08","023c1cd","已实现","只记录，打印平台隔离"],["紧急工单导入服务","Windows","2026-08-11","621769e","已实现","后续规则演进，日期为服务首次入库"],["上传站点","Web","2026-08-11","621769e","已实现","WmsUploadSite 扫描日志上传配套站点"],["入库清单检测","Windows","2026-08-17","a5130f5","已实现","读取基础Excel、SN匹配"],["在线登录与工单API","MAUI","2026-08-18","adab364","已实现","登录、会话存储、工单接口"],["出库检测与Excel导出","Windows","2026-09-04","900f3e9","已实现","文本与基础数据匹配、SKU汇总"],["库位费比对","Windows","2026-09-04","900f3e9","已实现","模板多工作表处理并输出工作簿"],["扫描日志上传","Windows","2026-09-04","900f3e9","已实现","ScanUploadService 首次入库"],["Intel macOS目标配置","MAUI","2026-09-08","本对话 / MaUIScanner.csproj","待实机验证","x64编译通过；没有Mac安装包或实机验收"],["SN/OID工单上下文迁移","MAUI","2026-09-08","本对话 / WorkOrderService.cs","部分迁移","核心匹配已接入，未新增完整工单规则回归"],["紧急工单XLSX导入","MAUI","2026-09-08","本对话 / CsvImportService.cs","已实现","复用WPF导入器；CSV仍用旧列映射"],["紧急工单CSV规则对齐","MAUI",null,"当前代码核查","待迁移","现用J/Y/AD/AR列，未对齐H/K/L/O/S"],["手动选择型号","MAUI","2026-09-08","本对话 / MainPage.xaml.cs","部分迁移","能选择内置型号，未支持添加并保存自定义型号"],["纸张规格保存","MAUI","2026-09-08","本对话 / MainViewModel.cs","已实现","4×6、4×4选择保存到Preferences"],["macOS标签PDF及打印","macOS","2026-09-08","本对话 / LabelPrinter.cs","待实机验证","系统打印面板；取消结果和份数行为待完善"],["4×4独立标签排版","MAUI",null,"当前代码核查","待迁移","当前缩放4×6内容，非WPF独立布局"],["入库匹配与本月导出","MAUI","2026-09-08","本对话 / OperationsPage.xaml.cs","部分迁移","已接入；月份比例统计和跨页面缓存未迁移"],["出库检测及Excel导出","MAUI","2026-09-08","本对话 / OperationsPage.xaml.cs","部分迁移","已接入处理与导出；明细列表和托盘标签未接入"],["库位费比对导出","MAUI","2026-09-08","本对话 / OperationsPage.xaml.cs","已实现","复用WPF解析及比对服务，分享输出"],["新增工具页小屏布局","Android/MAUI","2026-09-08","本对话 / OperationsPage.xaml","待实机验证","主体单列；并排导出按钮需窄屏验收"],["新增工具页多语言","MAUI",null,"当前代码核查","待迁移","新增页面为中文硬编码"],["Mac打包签名及标签机验收","macOS",null,"本对话待办","待实机验证","尚无.app/.pkg交付和打印实测"],["项目文档与进度表","项目","2026-09-08","本次文档整理","已实现","README、功能文档和进度工作簿"]];
const wb=Workbook.create();
const s=wb.worksheets.add('功能进度');
s.showGridLines=false;
s.getRange('A2').values=[['ScannerProject 功能进度']];
s.getRange('A3').values=[['登记日期：2026-09-08；Git日期为首次可见记录，对话日期为本次整理归档日期。']];
s.getRange('A4').values=[['未填日期表示尚未迁移；已实现表示代码存在，不代表全平台实机验收。']];
s.getRange('A6:G6').values=[['编号','功能','平台','加入/登记日期','当前状态','实现说明与剩余工作','日期依据']];
const data=rows.map((r,i)=>[i+1,r[0],r[1],r[2]?new Date(r[2]+'T00:00:00Z'):null,r[4],r[5],/^[0-9a-f]{7}/.test(r[3])?'Git '+r[3]:r[3]]);
s.getRange('A7:G'+(data.length+6)).values=data;
s.getRange('D7:D'+(data.length+6)).setNumberFormat('yyyy-mm-dd');
s.tables.add('A6:G'+(data.length+6),true,'FeatureProgress');
s.freezePanes.freezeRows(6);
s.getRange('A1:G'+(data.length+6)).format.font={name:'Arial',size:11};
s.getRange('A2').format.font={size:16,bold:true};
const widths=[7,30,19,19,18,60,48];
widths.forEach((w,i)=>s.getRangeByIndexes(5,i,data.length+1,1).format.columnWidth=w);
s.getRange('A6:G6').format={fill:'#243850',font:{color:'#FFFFFF',bold:true},rowHeight:30};
s.getRange('A7:G'+(data.length+6)).format.rowHeight=48;
s.getRange('A6:G'+(data.length+6)).format.verticalAlignment='center';
s.getRange('B7:G'+(data.length+6)).format.wrapText=true;
s.getRange('E7:E'+(data.length+6)).dataValidation={rule:{type:'list',values:['已实现','部分迁移','待迁移','待实机验证']}};
s.getRange('E7:E'+(data.length+6)).conditionalFormats.add('containsText',{text:'待',format:{fill:'#FFF0CC'}});
const t=wb.worksheets.add('对话与里程碑');
t.showGridLines=false;
t.getRange('A2').values=[['对话与项目里程碑']];
t.getRange('A4:D4').values=[['阶段','登记日期','用户要求 / 结论','落实及证据范围']];
t.getRange('A5:D8').values=[
['可行性核查',new Date('2026-09-08'),'确认Intel Mac能否使用Scanner','识别WPF平台限制，验证MAUI x64代码编译'],
['迁移范围确认',new Date('2026-09-08'),'要求迁移完整业务逻辑','以WPF为基准，不能用MAUI壳的编译通过替代功能对齐'],
['执行迁移',new Date('2026-09-08'),'直接迁移，保留Android小屏布局','加入工具页、共享处理服务和Mac打印初版，仍有进度表所列差异'],
['文档整理',new Date('2026-09-08'),'README、功能文档、Excel功能加入时间','结合本对话、当前代码及Git历史；没有逐条对话精确时间戳']
];
t.getRange('A10').values=[['时间说明：以上为本次对话整理登记日期，不是逐条消息的精确发生时间。']];
t.getRange('A11').values=[['历史功能日期来自Git提交；代码首次入库可能晚于实际开发。未读取其他未提供的对话。']];
t.getRange('A13:B13').values=[['状态统计','条目数']];
['已实现','部分迁移','待迁移','待实机验证'].forEach((v,i)=>{t.getRange('A'+(14+i)).values=[[v]];t.getRange('B'+(14+i)).formulas=[["=COUNTIFS('功能进度'!E7:E"+(data.length+6)+',A'+(14+i)+')']];});
t.getRange('A1:D17').format.font={name:'Arial',size:11};
t.getRange('A2').format.font={size:16,bold:true};
[22,18,48,68].forEach((w,i)=>t.getRangeByIndexes(3,i,14,1).format.columnWidth=w);
t.getRange('A4:D4').format={fill:'#243850',font:{color:'#FFFFFF',bold:true},rowHeight:30};
t.getRange('A5:D8').format.wrapText=true;t.getRange('A5:D8').format.rowHeight=64;
t.getRange('B5:B8').setNumberFormat('yyyy-mm-dd');
wb.recalculate();
console.log((await wb.inspect({kind:'table',range:'对话与里程碑!A13:B17',include:'values,formulas',tableMaxRows:5,tableMaxCols:2})).ndjson);
const out='D:/GitRepos/ScannerProject/outputs/doc-progress-20260908';
await fs.mkdir(out,{recursive:true});
for(const [sheetName,range,name] of [['功能进度','A1:G12','progress'],['对话与里程碑','A1:D17','milestones']]){
const image=await wb.render({sheetName,range,scale:1,format:'png'});
await fs.writeFile('D:/GitRepos/ScannerProject/.codex-work/doc-progress-20260908/'+name+'.png',new Uint8Array(await image.arrayBuffer()));
}
await (await SpreadsheetFile.exportXlsx(wb)).save(out+'/项目进度表.xlsx');
