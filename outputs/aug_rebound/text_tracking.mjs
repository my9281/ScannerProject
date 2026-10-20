import fs from 'node:fs/promises';
import {FileBlob,SpreadsheetFile} from 'file:///C:/Users/Thomas/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/@oai/artifact-tool/dist/artifact_tool.mjs';
const dir='D:/GitRepos/ScannerProject/outputs/aug_rebound/';
const w=await SpreadsheetFile.importXlsx(await FileBlob.load(dir+'盘库_按FedEx后12位关联机型.xlsx'));
const s=w.worksheets.getItemAt(0);const r=s.getRange('A2:A307');r.values=r.values.map(([v])=>[v===null||v===''?v:String(v)]);s.getRange('A1:A308').setNumberFormat('@');s.getRange('A:A').format.columnWidth=22;
w.recalculate();
await fs.writeFile(dir+'tracking-text-preview.png',new Uint8Array(await (await w.render({sheetName:s.name,range:'A1:F5',scale:1.5,format:'png'})).arrayBuffer()));
await (await SpreadsheetFile.exportXlsx(w)).save(dir+'盘库_机型关联_追踪号文本.xlsx');
