import fs from "node:fs/promises";
import { FileBlob, SpreadsheetFile } from "@oai/artifact-tool";
const workbook = await SpreadsheetFile.importXlsx(await FileBlob.load("writer-test.xlsx"));
console.log((await workbook.inspect({ kind: "table", sheetId: "出库检测", range: "A1:F7", include: "values,formulas", tableMaxRows: 10, tableMaxCols: 6, maxChars: 6000 })).ndjson);
const preview = await workbook.render({ sheetName: "出库检测", range: "A1:F7", scale: 2, format: "png" });
await fs.writeFile("writer-test.png", new Uint8Array(await preview.arrayBuffer()));
