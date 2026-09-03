import fs from "node:fs/promises";
import { FileBlob, SpreadsheetFile } from "@oai/artifact-tool";

const files = [
  ["template", "C:/Users/69969/OneDrive/Desktop/打印模板.xlsx"],
  ["base", "C:/Users/69969/Downloads/2026825 03_29_16 InboundDetection.xlsx"],
];

for (const [label, path] of files) {
  const workbook = await SpreadsheetFile.importXlsx(await FileBlob.load(path));
  console.log(`=== ${label} summary ===`);
  console.log((await workbook.inspect({ kind: "workbook,sheet,table", maxChars: 12000, tableMaxRows: 12, tableMaxCols: 24, tableMaxCellChars: 100 })).ndjson);
  const sheets = (await workbook.inspect({ kind: "sheet", include: "id,name", maxChars: 5000 })).ndjson;
  console.log(sheets);
  const first = workbook.worksheets.getItemAt(0);
  console.log(`=== ${label} first-sheet region ===`);
  console.log((await workbook.inspect({ kind: "region", sheetId: first.name, range: "A1:AZ25", maxChars: 20000, tableMaxRows: 25, tableMaxCols: 52, tableMaxCellChars: 120 })).ndjson);
  console.log(`=== ${label} formulas ===`);
  console.log((await workbook.inspect({ kind: "formula", sheetId: first.name, range: "A1:AZ50", maxChars: 8000, options: { maxResults: 100 } })).ndjson);
  console.log(`=== ${label} styles ===`);
  console.log((await workbook.inspect({ kind: "computedStyle", sheetId: first.name, range: "A1:AZ15", maxChars: 10000 })).ndjson);
  const preview = await workbook.render({ sheetName: first.name, range: "A1:AZ25", scale: 1.5, format: "png" });
  await fs.writeFile(`${label}.png`, new Uint8Array(await preview.arrayBuffer()));
}
