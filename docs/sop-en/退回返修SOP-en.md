# BLUETTI Return-for-Repair Standard Operating Procedure (SOP)

## 1. Document Information

| Item | Description |
| --- | --- |
| Applicable products | BLUETTI batteries, power stations, and related power products |
| Intended users | Process management staff, including managers and assistants |
| Scope | Repair work-order verification, repair record entry, label printing, shipping label creation, system outbound processing, and loading records |
| Prerequisites | Identification of returned repair products, physical repair, and packing; related instructions are retained to explain the workflow |
| Updated | 2026-10-15 |

English descriptions of interface controls are followed by the Chinese labels shown in the reference screenshots where needed.

### 1.1 Systems and Terminology

| System or term | Purpose or meaning |
| --- | --- |
| WPF | Identifying received products and printing operation labels |
| BLUETTI after-sales | After-sales work orders, inbound inspection, repair records, and outbound processing |
| OMS | Creating FedEx shipping labels and viewing charges and carrier tracking numbers |
| DingTalk Return-for-Repair Outbound Register | Our internal shipment records and vehicle identification records |
| label | An operation label used during receiving or repair; a dual-QR-code label is used after repair |
| SN | Device serial number |
| RMA | After-sales return reference number, handwritten on the shipping label |
| FedEx carrier tracking number | The FedEx tracking number used in DingTalk and after-sales outbound records |

### 1.2 Workflow Overview

Verify repair records → Complete system inspection and repair entries → Print and attach the dual-QR-code label → Create the FedEx shipping label → Handwrite identifiers and take a same-frame photo → Complete the initial DingTalk entry → Process outbound shipment in after-sales → Load the shipment and enter the vehicle number.

> **Using the examples:** Screenshots, photos, and example values illustrate the procedure. Redacted images are for documentation only. Actual entries, printing, and uploads must use complete information for the relevant device and the original operational photos.

### 1.3 SKU Suffixes and System Conversion Rules

SKU suffixes identify two channel groups and their processing status:

| Suffix | Meaning and conversion rule |
| --- | --- |
| `-10` | Factory-new device; the system converts it to `-11` after inspection approval, refurbishment, or repair |
| `-11` | The system SKU status of a `-10` device after inspection approval, refurbishment, or repair |
| `-20` | A device from the bulk procurement channel, received through trade-in or similar arrangements; normally handled through the shelving workflow |
| `-21` | The system SKU status of a `-20` device after refurbishment |

The system may display three-digit suffixes such as `-010`, `-011`, `-020`, and `-021`. These correspond to `-10`, `-11`, `-20`, and `-21`, respectively.

For record entry, printing, and outbound processing, use the complete SKU currently displayed for the device. A normal suffix conversion does not change its SN. Do not treat a difference between the original and processed suffixes as a product mismatch by itself.

## 2. Prerequisite: Identifying Returned Repair Products

During receiving, identify repair returns using **Import Work Orders (工单导入)** in the WPF advance-notice system. Labels printed for repair returns include the word **维修** (repair). Attach a label to both the device and its box.

System maintenance note: The WPF advance-notice work-order import function currently has a known issue to be addressed. An interface may be integrated in the future.

## 3. Repair Record Verification and Processing Decision

### 3.1 Parts and Packaging Record Requirements

After completing a repair, repair staff must record repair or refurbishment details or parts codes on the label, such as U2 or M2.

Record replaced parts and their identifiers according to the actual repair. Boards include U, M, BMS, and A boards, numbered 1–8. Other parts include Fuse, Screen, Wire, and Box. Refer to the parts records for the exact part names and identifiers.

**Current requirement: Fuse replacement is mandatory for EB3A and the EL100 series.** EL100 is a series name; most current units are EL100V2. V2 means Version 2. Record the exact model shown on the device label. Management staff must record actual replacement; the requirement alone does not establish that replacement was performed.

Repair and packing staff replace boxes as needed after repair and record the replacement on the label. Managers and assistants enter this information from the label: **INB means inner box; UNB means outer box.**

### 3.2 Standard Repair Processing Example

The label below shows **OK** together with **Fuse, Box, and Wire**. This is a standard repair case with specific repair details.

Management staff request materials and enter the actual repair operations, then print the dual-QR-code label from the repair details page before proceeding to shipment. Record the inner or outer box code according to the actual replacement; do not infer it from a photo alone.

![Standard repair example: label showing Fuse, Box, and Wire](../sop/images/返修正常流程配件记录示例.jpg)

Search after-sales using the device SN. Confirm that the after-sales handling type is **Returned for Repair (退回维修)** and the reported fault is **Unable to Turn On (无法开机)**. In this example, the Fuse replacement recorded on the label is consistent with the reported fault. Continue through repair processing and record the actual Fuse, Box, and Wire work.

This example illustrates how to compare a work-order fault with repair records. It is not a repair solution for every device that cannot turn on and does not replace post-repair testing.

![Work-order verification: returned for repair and unable to turn on](../sop/images/返修正常流程工单故障核对.png)

### 3.3 Verification When the Label Only Shows OK

If the label only shows **OK**, confirm the following with repair staff:

- Who performed the repair.
- Whether the fault described in the after-sales work order was reproduced.
- Whether repair was performed and what was done.

Record the repair staff member, fault reproduction result, and repair details in the work-order remarks. Do not approve inspection based solely on an OK mark.

First look up the work order by SN and explain the specific reported fault to repair staff. Confirm whether OK refers to that fault. For example, if the work order reports a Bluetooth connection failure, confirm whether it was reproduced, whether it was repaired, and whether Bluetooth now connects normally.

### 3.4 On-Site Communication Example

The photo below illustrates the manager's explanation to repair staff: **This device was returned for repair, and the customer reported a Bluetooth connection failure.** The trilingual fault description represents the content of that on-site conversation.

**The trilingual sticker in this photo documents the communication. It is not a required SOP step and does not represent a repair or inspection result.** After explaining the work-order issue, obtain and record the repair staff's verification response.

![Communication example: explaining the reported Bluetooth connection failure](../sop/images/返修OK故障沟通示例.jpg)

### 3.5 System Processing Decision and Permissions

After verification, select the appropriate processing route:

- **Inspection Passed (检测通过):** If testing confirms normal operation for the reported issue and no repair operation or parts replacement needs to be entered, select Inspection Passed and print from the inspection details.
- **Repair Processing (维修处理):** If repair, parts replacement, or packaging/accessory replenishment was performed and requires recording, select Repair Processing. Enter the actual work in the Product Repair List, complete repair inbound processing, and print from the repair work-order details. Follow this route even if the label also shows OK.

If the label only shows OK and verification is incomplete, complete the inquiry and remarks first. Do not immediately select Inspection Passed.

For inbound disposition, managers and assistants may only use **Repair Processing** and **Inspection Passed**. **Return Work Order (工单退单)** is used by customer service and operations staff; **Scrap Processing (报废处理)** is used by repair staff. The separate outbound interruption recovery option is described in Section 9.

![Inbound inspection screen](../sop/images/after-sales入库检测作业.png)

### 3.6 Inbound Inspection Entry and Confirmation

Select **Inbound Inspection (入库检测)** for the relevant work order. Packaging type, returned accessories, and device appearance describe **the condition when the customer return arrived at the warehouse**, based on receiving records or on-site confirmation. Record replacement boxes, replenished wires, manuals, and packing materials separately in the repair record. Do not overwrite the original receiving condition with the post-repair condition.

The example selections were:

- Package type: Non-original packaging / customer-packed.
- Included accessories: Complete standard accessories.
- Device appearance: Slight scratches.

These selections apply only to this example. Select the actual condition for other devices.

![Example packaging, accessories, and appearance selections](../sop/images/返修入库检测包装配件外观.png)

Select **Repair Processing (维修处理)** under inbound disposition, then click **Complete Inspection (完成检测)**.

![Selecting repair processing and completing inspection](../sop/images/返修入库检测维修处理.png)

In the confirmation dialog, verify the **Operation Date (作业时间)** and click **Confirm (确定)**. Enter the date when this inbound inspection was actually performed. For a later entry, use the original operation date instead of accepting the current default date. The screenshot date is an example only.

![Confirming the operation date for inbound inspection](../sop/images/返修入库检测作业日期确认.png)

## 4. Repair Records and the Dual-QR-Code Label

### 4.1 Printing After Inspection Approval

For a device marked **Inspection Passed (检测通过)**, open the relevant record's **Details (详情)** and click **Print (打印)** to generate the dual-QR-code label.

![Print button in the details page](../sop/images/after-sales详情打印入口.png)

### 4.2 Repair Processing and Material Requests

For a device marked **Repair Processing (维修处理)**, complete material requests, repair records, and label printing as follows.

Go to **Repair Management (维修管理) → Product Repair List (产品维修表)** and find the record by SN. Click **Pending Repair (待维修)** on the right, then select **Request Materials (申领材料)** in the repair page. The button may also display **申领物料**.

![Pending Repair entry in the Product Repair List](../sop/images/返修产品维修表待维修入口.png)

Select the parts and packaging materials actually used, enter the quantities, and click **Save (保存)**. The example includes these codes:

| Code | Material |
| --- | --- |
| UNB | Outer box |
| T, shown as TEPE in the example SKU | Top foam insert |
| B, shown as BEPE in the example SKU | Bottom foam insert |
| UMN | User manual |

![Requesting top and bottom foam inserts, a manual, and an outer box](../sop/images/返修申领包装材料与说明书.png)

The screenshot shows a quantity of 1 for each foam insert and the manual. UNB is selected but still has a quantity of 0; this is an entry in progress. Enter the actual quantity before saving. Selecting an item alone does not complete the request. Record and request Fuse, Wire, and other items actually used, rather than only the packaging visible in the screenshot.

Consult the parts records when requesting materials. The [Common Cables reference](../sop/attachments/常用线材.xlsx) may also be used for wires. Record parts and operations according to the actual repair.

### 4.3 Entering the Repair Record

Add a repair item in the repair page and enter the actual fault and work performed. In this example, select **Power On/Off Fault (开关机故障)** as the fault category, **Unable to Turn On (无法开机)** as the fault symptom, and **Replace (更换)** as the repair action.

The example repair description is:

> The work order reports that the device cannot turn on. Replaced the Fuse and outer box (UNB), replenished the Wire and user manual (UMN), and replaced the top and bottom foam inserts (T and B).

The description must match the actual repair, accessory replenishment, and requested materials. Select **Responsibility / Owner (责任归属)** based on confirmed facts. Do not assign Customer Reason merely because the device could not turn on or the fuse was replaced. Do not enter unconfirmed causes or post-repair test results.

![Repair record screen and description example](../sop/images/返修故障维修记录填写示例.png)

### 4.4 Completing Repair Inbound Processing and Printing

1. After entering the repair details, click **Complete Repair and Receive into Inventory (完成维修入库)**.
2. In the repair completion confirmation dialog, check the operation date, enter the actual repair completion date, and click **Confirm (确定)**. For a later entry, use the date confirmed by repair staff or the repair records, not the date of data entry.
3. Return to the **Product Repair List (产品维修表)**, locate the device, and click **Details (详情)**.
4. Select the **Repair Work Order (维修工单)** tab. Confirm that the relevant record shows **Repair/Refurbishment Completed (完成维修翻新)** and **Received into Inventory (已入库)**. Click **Print (打印)** on the right to print the dual-QR-code label.

Check the system SKU after processing: `-10` devices become `-11` after inspection approval, refurbishment, or repair; `-20` devices become `-21` after refurbishment. Print from the current system record, not the previous SKU. Devices in the `-20` group normally follow the shelving workflow.

![Repair inbound completion confirmation](../sop/images/返修完成维修入库确认.png)

![Print button in the repair work-order details](../sop/images/返修维修工单详情打印.png)

> **Required print location:** Print the repaired device's label from the repair work-order details. Printing from the original inbound inspection page produces the wrong document. After-sales is provided by the customer; our team cannot modify this function. Use the specified print location.

![Repair item entry screen](../sop/images/after-sales维修项目录入.png)

### 4.5 Marking and Attaching the Label

After printing, handwrite a repair mark below the label. The on-site example uses **REPAIRED**. A stamp may be introduced later; handwriting is currently required.

Place the device with its front facing upward, following the up-to-top orientation. Attach the dual-QR-code label to the smallest, narrowest of the four vertical box faces: front, back, left, or right. Cover the original receiving label.

![Dual-QR-code label covering the original receiving label and marked REPAIRED](../sop/images/返修双二维码标签覆盖原标签.jpg)

![Label attached to the narrowest side with the device correctly oriented](../sop/images/返修正面向上最窄侧面贴标.jpg)

Use blank white labels to cover remaining old shipping labels, including old addresses, tracking barcodes, and QR codes, to prevent misdelivery. Keep the new dual-QR-code label's QR codes, SKU, and SN clear and readable.

## 5. Packaging Confirmation and Return Shipping Information

Reuse the original box if it remains usable. An unusable box will already have been replaced during post-repair packing. Management staff record the INB and UNB details from the label.

In after-sales, go to **Inventory Management (库存管理) → Outbound Operations (出库作业执行) → Outbound Work Order Pool (出库工单需求池)**. Search by SN and click **Shipping Label Information (面单信息)** on the right of the record.

![Outbound work order pool](../sop/images/after-sales出库工单需求池.png)

Obtain the customer reference number, recipient details, and return shipping address from the relevant work order. Customer service enters the recipient information, which is validated by after-sales.

### 5.1 Shipping Information Query Example

The following examples show how to locate the device by SN and open Shipping Label Information.

![Shipping label information](../sop/images/after-sales面单信息.png)

Customer details and identifiers in the following screenshots are partially redacted. Use complete information copied from the system when creating the actual label; do not enter values from a redacted screenshot.

![Searching the outbound work order by SN, with partial redaction](../sop/images/返修出库工单查询示例_半打码.png)

![Shipping label information with recipient details redacted](../sop/images/返修面单信息示例_半打码.png)

## 6. Entering Shipping Label Information in OMS

### 6.1 Entry Requirements

- Currency: US dollars.
- Dimension/weight units: Imperial; inches for dimensions and pounds for weight.
- Customer reference number: Copy the corresponding number.
- Recipient information: Use the relevant after-sales work order.
- Sender information: Select the Sender Information tab and use **Select Address (选择地址)** on the right.
- Package weight and dimensions: Open [Model Dimensions and Weights](../sop/attachments/各机型尺寸重量数据.xlsx) and look up the model in Sheet1. Weight is in LB and dimensions are in inches, in length × width × height order. If no entry is available, physically measure the package and enter imperial values.

If the customer did not provide a phone number, enter the placeholder **202-555-0100** in OMS and note internally: Customer did not provide a phone number; OMS uses a placeholder. This is not the customer's actual number.

> **Unit requirement:** The initial interface screenshot below shows metric units. Actual operation requires switching to imperial units.

![Initial OMS entry screen](../sop/images/OMS创建面单填写界面.png)

### 6.2 Recipient and Sender Entry Example

1. Copy the customer reference number from after-sales Shipping Label Information into **Customer Reference Number (客户单号)** in OMS. Select US dollars as the declaration currency.
2. Enter the recipient's name, country, state, city, address, postal code, email, and phone number using complete system information.
3. Select **Imperial (INCH/LB)** for package information and enter the actual box count, weight, length, width, and height. This example uses **1 box, 21 lb, and 16 × 12 × 16 inches**. These are not fixed values for other models.
4. Switch to **Sender Information (发件人信息)**, click **Select Address (选择地址)** on the right, and choose the appropriate warehouse address from the list. Do not use the customer's address as the sender address.

Customer contact details, addresses, and account information in these screenshots are redacted. Copy complete information from the system for actual entries.

![OMS recipient details, USD currency, and imperial package values](../sop/images/返修OMS收件与包裹填写示例_打码.png)

![Selecting the warehouse address in Sender Information](../sop/images/返修OMS发件地址选择示例_打码.png)

## 7. OMS Order Placement, Label Printing, and Photo Records

### 7.1 Checking Charges and Placing the Order

After entering OMS information, proceed using the appropriate shipping channel. This example uses **FedEx-美东-NJ** (FedEx US East–NJ). Open the charge details, verify the channel, package weight, and total quoted amount, and click **Place Order Now (立即下单)**.

Both the charge details and order success dialog show **13.61 USD** in this example. Record the actual USD amount returned for the shipment; the example amount is not a fixed rate for future shipments. The charge details display 9.525 KG, while package entry still uses the previously selected imperial units.

![OMS charge details and Place Order Now](../sop/images/返修OMS费用详情示例.png)

![Successful order: record the amount and select Print Label](../sop/images/返修OMS下单成功示例.png)

### 7.2 Printing and Finding the Carrier Tracking Number

After the order succeeds, click **Print Label (打印标签)**. In the print window, select the actual label printer, enter the number of copies, and click **Print (打印)**. This example prints one copy. Choose the printer installed at the work location.

![OMS print preview with customer details and barcodes redacted](../sop/images/返修OMS面单打印预览_打码.png)

In the OMS order details, locate **Carrier Tracking Number (服务商单号)**. It must match the tracking number on the FedEx label. Use this FedEx number for the after-sales transportation number and the DingTalk carrier/label number fields. Do not use the OMS internal **Order/Waybill Number (运单号)** or the customer reference number instead.

![Carrier tracking number and internal OMS number in order details](../sop/images/返修OMS订单详情单号示例_打码.png)

### 7.3 Handwritten Information and Photo Records

1. Retain the amount returned by OMS and the FedEx carrier tracking number. Confirm they correspond to the printed label for use in DingTalk and after-sales.
2. **Handwrite the device SN and RMA number on the shipping label** to prevent shipment mix-ups and recording errors. The OMS customer reference number is defined by the system and is separate from the handwritten RMA.
3. Take a photo showing the shipping label and the corresponding device's dual-QR-code label together in the same frame.
4. Transfer the photo to the computer for upload during after-sales outbound processing.

### 7.4 Handwriting and Same-Frame Photo Examples

After printing, handwrite the device SN and RMA on the shipping label. This example writes the model and serial portion on two separate lines to illustrate placement. The recorded information must correspond to the complete device SN.

![Shipping label with handwritten model, SN, and RMA](../sop/images/返修面单手写SN与RMA示例_打码.png)

The same-frame photo must show both the handwritten shipping information and the matching dual-QR-code label.

![Shipping label and dual-QR-code label photographed together](../sop/images/返修面单双二维码同框照片示例_打码.png)

**Example photos in this document are redacted. Actual after-sales uploads must retain complete information; do not upload the redacted documentation images.** Transfer the original photo to the computer for upload after claiming the outbound work order.

## 8. DingTalk Return-for-Repair Outbound Register

Complete the register in two stages. Before loading, complete all information except the Carrier Container Number and Confirmation columns. After loading, enter the Carrier Container Number. The Confirmation column is outside the scope of this procedure.

### 8.1 Register Fields

| Field | Entry requirement |
| --- | --- |
| Outbound date | Actual outbound date; update to the actual date if completion crosses into another day |
| Device SN | Complete SN of the relevant device |
| Product SKU | Corresponding SKU |
| Product model | Corresponding model |
| Package dimensions | Length × width × height, in inches |
| Package weight | Weight in pounds |
| Shipping charge | Amount returned by OMS, in US dollars |
| Carrier and label number | Carrier name and tracking number |
| Carrier Container Number (承运货箱号) | Enter the actual carrier vehicle number after loading |
| RMA number | RMA from the relevant after-sales work order |
| Customer reference number | System-defined number matching the one used in OMS |
| Recipient name | Name from the work order |
| Recipient email | Email from the work order |
| Recipient phone | Phone from the work order; if missing, note the OMS placeholder |
| Recipient address | Complete street, city, state, postal code, and country |
| Confirmation | No entry required under this procedure |

### 8.2 Initial Entry and Completion Example

Before creating the OMS label, available information may be entered first: outbound date, device details, dimensions, weight, carrier, and RMA. The screenshot below is an entry in progress. Complete the shipping charge and full tracking number after OMS returns them. **Fedex-** alone is not a complete tracking number.

This is still the initial, pre-loading entry. The second entry is the vehicle number after loading. The screenshot partially masks the SN and RMA; use complete information in the actual register.

![Initial DingTalk entry in progress](../sop/images/返修钉钉首次登记示例_打码.png)

After the OMS order succeeds, update the same row with the shipping charge and **Fedex-[complete carrier tracking number]**. Complete the customer reference number, recipient name, email, phone, and full address. Copy recipient information from the corresponding after-sales work order, not from redacted images.

![Entering the OMS amount and FedEx tracking number](../sop/images/返修钉钉回填金额单号示例_打码.png)

![Completing recipient details; vehicle number remains pending until loading](../sop/images/返修钉钉补齐收件信息示例_打码.png)

Once the charge, tracking number, and recipient information are complete, the initial entry is finished. Enter the Carrier Container Number after the shipment is physically loaded.

## 9. Claiming and Processing Outbound Shipment in After-Sales

### 9.1 Outbound Steps

Prepare the FedEx carrier tracking number and original same-frame photo, then perform these steps in after-sales:

1. Find the work order in the Outbound Work Order Pool and click **Claim and Ship (领单发件)** on the right.
2. In the Work Order Shipping List, click **Outbound (出库)** for the device.
3. Under Outbound Product Information, verify the SKU and SN. Use the processed system SKU and distinguish normal suffix conversions as described in Section 1.3. Under Outbound Logistics Information, select **FedEx**, enter the FedEx carrier tracking number from OMS, and enter the actual time of outbound processing. Keep the date consistent with the DingTalk outbound date.
4. Click **Start Outbound (开始出库)** and follow the page instructions to the Outbound Operation SOP photo-upload page. **Proceeding to this stage changes the work-order status; it cannot be directly returned to its previous status. Prepare the photo first and complete the steps continuously where possible.**
5. Click **Upload Attachment (上传附件)** and upload the same-frame photo transferred to the computer. It must show the shipping label with handwritten SN and RMA together with the corresponding dual-QR-code label. Confirm the attachment is displayed and enter remarks if needed.
6. Click **Complete Outbound (完成出库)** at the bottom of the page. After confirming successful submission, move the goods to the shipping stage. **Return to Work Order Pool (返回工单池)** and **Shipping Information (发货信息)** do not complete outbound processing.

![Claim and Ship entry](../sop/images/after-sales领单发件入口.png)

![Claim and Ship screen and Outbound button](../sop/images/after-sales领单发件界面.png)

![Selecting FedEx as the carrier](../sop/images/after-sales物流商选择.png)

The completed entry below partially masks the SN and transportation number. Use complete values in actual operation.

![Verifying the device, FedEx tracking number, and time before Start Outbound](../sop/images/返修领单出库信息填写示例_打码.png)

![Uploading the photo and selecting Complete Outbound](../sop/images/返修出库照片上传与完成出库.png)

### 9.2 Claim Lock and Interruption Recovery

> **Claim requirement:** A claimed work order is locked to the claiming account. Other accounts cannot operate it. Prepare the required information in advance and complete outbound processing continuously where possible.

If a network interruption or similar issue occurs, return to the relevant page using the same account and continue. **Release Claim (解除领取)** creates an operation record and should be avoided where possible.

If processing is interrupted and the previous status must be restored, use **Return Work Order (工单退单)** on the right of the corresponding work order. This is the outbound status recovery option, distinct from the return-work-order disposition in inbound inspection.

![Return Work Order option for outbound interruption recovery](../sop/images/after-sales出库中断工单退单.png)

## 10. Loading and Vehicle Number Entry

Attach the FedEx shipping label to the corresponding box. After completing system outbound processing, move the repair-return pallet to the loading stage.

**Load the repair-return pallet onto the FedEx Ground vehicle before 3:00 p.m. each day.**

Record which carrier vehicle received the pallet and its vehicle number. After loading, enter that number in the **Carrier Container Number (承运货箱号)** column of the DingTalk Return-for-Repair Outbound Register to complete the shipment record.

## 11. Reference Attachments

| Attachment | Purpose |
| --- | --- |
| [Model Dimensions and Weights](../sop/attachments/各机型尺寸重量数据.xlsx) | Look up package weight and dimensions when creating labels |
| [Common Cables](../sop/attachments/常用线材.xlsx) | Reference for repair material requests |
