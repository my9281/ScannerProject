import fs from 'node:fs/promises';
import {FileBlob,SpreadsheetFile} from '@oai/artifact-tool';
const wb=await SpreadsheetFile.importXlsx(await FileBlob.load('C:/Users/Thomas/Documents/xwechat_files/wxid_k0jq6i5dl9b622_7773/msg/file/2026-10/2026年9月-仓租结算.xlsx'));
const img=await wb.render({sheetName:'仓租结算',range:'A1:H10',scale:1.2,format:'png'});await fs.writeFile('.verification/inventory-probe/rent-original.png',new Uint8Array(await img.arrayBuffer()));
