import fs from 'node:fs/promises';
import {FileBlob,SpreadsheetFile} from 'file:///C:/Users/Thomas/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/@oai/artifact-tool/dist/artifact_tool.mjs';
const dir='D:/GitRepos/ScannerProject/outputs/aug_rebound/';
const w=await SpreadsheetFile.importXlsx(await FileBlob.load(dir+'盘库_重新匹配_含申报重量.xlsx'));
for(const name of ['Sheet1','匹配机型汇总','尺寸重量匹配说明']){const s=w.worksheets.getItem(name);const used=s.getUsedRange();const v=used.values;for(let r=0;r<v.length;r++)for(let c=0;c<v[r].length;c++)if(typeof v[r][c]==='string'&&v[r][c].includes('成本表'))s.getCell(r,c).values=[[v[r][c].replaceAll('成本表','核对表')]];}
const s=w.worksheets.getItemAt(0);let kg=0,lb=0;const pairs=[['L','M'],['N','O'],['P','Q']];
for(const [vc,uc] of pairs){const v=s.getRange(`${vc}2:${vc}307`).values;const u=s.getRange(`${uc}2:${uc}307`).values;for(let i=0;i<v.length;i++){const unit=String(u[i][0]??'').trim().toLowerCase();if(!unit)continue;if(['kg','kgs'].includes(unit)){if(v[i][0]!==null&&v[i][0]!==''&&Number.isFinite(Number(v[i][0]))){v[i][0]=Number(v[i][0])/0.45359237;kg++;}u[i][0]='lb';}else if(['lb','lbs'].includes(unit)){u[i][0]='lb';lb++;}else throw Error('Unexpected weight unit: '+unit);}s.getRange(`${vc}2:${vc}307`).values=v;s.getRange(`${uc}2:${uc}307`).values=u;s.getRange(`${vc}2:${vc}307`).setNumberFormat('0.000');}
const n=w.worksheets.getItem('尺寸重量匹配说明');n.getRange('A6:B6').values=[['重量口径','实际、计费、预估重量均已统一为lb；kg/kgs按1 lb = 0.45359237 kg换算，显示3位小数。']];n.getRange('A12:B12').values=[['申报重量单位','订单导出的申报单重没有注明单位，因此保留原值，未推定为kg或lb。']];n.getRange('A16:B17').values=[['统一重量单位','kg和kgs均表示千克，lb和lbs均表示磅；保留换算精度，仅显示值取3位小数。'],['申报重量待确认','请确认订单导出中申报单重的单位，确认后才能统一换算为lb。']];n.getRange('B1:B17').format.wrapText=true;n.getRange('A1:B17').format.autofitRows();
w.recalculate();console.log(JSON.stringify({kgConverted:kg,lbNormalized:lb}));
await fs.writeFile(dir+'lb-preview.png',new Uint8Array(await (await w.render({sheetName:s.name,range:'L1:Q6',scale:1.5,format:'png'})).arrayBuffer()));
await (await SpreadsheetFile.exportXlsx(w)).save(dir+'盘库_核对表_重量统一lb.xlsx');

