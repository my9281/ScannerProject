# Goods Receiving Procedure

[中文](../sop/收货SOP.md) | [English](../sop-en/Receiving-SOP.md) | [Español](../sop-es/Recepcion-SOP.md)

[Back to SOP index](README.md) | [Multilingual reading guide](Language-Guide.md)

Translation/index review: 2026-11-09. Workflow status is unchanged; unresolved rules remain pending confirmation.

This procedure is for the receiving manager. It explains how to record received goods in the shared spreadsheet **“美国Logistar仓收件登记表-更新版9.02”**. Receiving registration and inventory putaway are separate processes; this procedure covers receiving registration.

Chinese button names and spreadsheet tab names are retained so staff can identify them in the system.

## 1. Export advance shipping notices and work orders

Before starting receiving, export the advance shipping notices and work orders from the BLUETTI after-sales system and prepare the documents needed for this delivery.

## 2. Enter the date and create a receiving record

At the start of each receiving day, enter the date in **column A** at the beginning of that day's entries in the shared spreadsheet.

In WPF, open “扫码录单” (scan entry) and click **“+ 创建新记录”** (create a new record) to keep this receiving session separate from previous records.

**Before scanning, create the TXT and Excel files for this receiving session. Then scan the tracking number.**

## 3. Scan the tracking number and the serial numbers

### Full pallet deliveries

Full pallet deliveries are uncommon. Follow these additional steps when one arrives. The entire pallet may have only one delivery receipt and one shipping document, stapled to one of the boxes. Find the shipping document and confirm the tracking number for the pallet. Record that number even if the other boxes have no individual shipping labels.

For example, the tracking number for the reference pallet is **439752659**, shipped by ArcBest. The label also shows it as “439 752 659”. After creating the TXT and Excel files, scan this tracking number first, then proceed with unpacking and scanning the equipment serial numbers.

### Break down the pallet and clear the wrapping

After scanning the pallet tracking number, break down the pallet. Keep all paper documents, including the delivery receipt and shipping documents. Put the removed stretch wrap in the trash promptly. Stretch wrap takes up considerable space; do not leave it piled in the receiving area.

### Scanning sequence

For a full pallet, first scan the pallet tracking number and break down the pallet as described above, then process each unit in turn. For individually delivered packages, scan the tracking number on the box first, then follow these steps:

1. Open the equipment box.
2. Take the unit out of the box.
3. Scan the serial number (SN) on the unit.
4. Once the system identifies the model, it automatically prints the label.
5. If the model is not recognized, select the actual model in the pop-up window and confirm. If it is not listed, enter and use a new model.
6. After scanning each unit and printing its labels, **place one label inside the box and attach another to the outside**. Check that both labels match the unit.

The pop-up selection is the **equipment model**, not the “退件类型” (return type) in the shared spreadsheet.

WPF automatically saves the records to Excel in three columns: model, SN, and OID (tracking number).

Also record the following in **column A** of the shared spreadsheet during receiving:

- **EL10, EL100, and EB3A** come in different colors. Enter the actual color in the corresponding row.
- For **cables**, take a photo and upload it to column A of the corresponding row.

If the wrong model was selected, correct the model in the corresponding Excel row before copying the data. WPF currently has no option to edit saved records. Scanning the same SN again does not overwrite the original Excel record. Editing Excel also does not change labels already printed; incorrect labels must be handled separately.

## 4. Export Excel and copy records by tracking number

After scanning the SNs of all units in this receiving session, export the receiving Excel file. WPF automatically writes the data to the scan workbook. Click **“打开扫描 Excel”** (open scan Excel) to open the current record.

Select the records for each tracking number and copy them to the shared spreadsheet:

- **FedEx shipments:** use the “eBay&无源头退货登记表2025” tab.
- **Other carriers or shipping channels:** use the “亚马逊收件登记表2025” tab.

Copy data rows only, without the headers. Match the columns as follows:

| Scan Excel file | Shared spreadsheet |
| --- | --- |
| Column A: model | Column B: model |
| Column B: SN | Column C: SN |
| Column C: OID | Column D: tracking number |

Paste SNs and tracking numbers as text, preserving every character so long numbers are not truncated or changed.

## 5. Verify the receiving records

Confirm that the records are on the correct tab, that the model, SN, and tracking number columns match, and that the shared spreadsheet has been saved.

The manager copies the three columns from the scan Excel file into columns B–D of the shared spreadsheet and adds the date, the colors for the specified models, and cable photos in column A. **Colleagues in China complete the remaining columns.** Do not overwrite existing entries in column A or the columns maintained by those colleagues.

## 6. Move the units to the area awaiting repair

After completing scanning, Excel export, and receiving registration, move the units to the area awaiting repair.
