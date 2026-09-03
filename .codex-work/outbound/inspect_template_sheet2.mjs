import fs from "node:fs/promises";
import { FileBlob, SpreadsheetFile } from "@oai/artifact-tool";
const workbook = await SpreadsheetFile.importXlsx(await FileBlob.load("C:/Users/69969/OneDrive/Desktop/打印模板.xlsx"));
console.log((await workbook.inspect({ kind: "table", sheetId: "Sheet2", range: "A1:F40", include: "values,formulas", maxChars: 20000, tableMaxRows: 40, tableMaxCols: 6 })).ndjson);
console.log((await workbook.inspect({ kind: "computedStyle", sheetId: "Sheet2", range: "A1:F30", maxChars: 16000 })).ndjson);
const preview = await workbook.render({ sheetName: "Sheet2", range: "A1:F30", scale: 2, format: "png" });
await fs.writeFile("template-sheet2.png", new Uint8Array(await preview.arrayBuffer()));
