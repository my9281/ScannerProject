<template>
    <div class="bs-mt-10" style="position: relative">
      <!--  查询表单组件    -->
      <form-drawer ref="formDrawerRef" :form="form" :form-drawer="formDrawer" @search="handleQuery" @refresh="handleRest">
      </form-drawer>
      <div class="after-sale__work-order bs-flex bs-flex-items-center">
            <form-component
                v-if="formObj.length > 0"
                :rule-form="form"
                :option="option"
                :form-obj="formObj"
                ref="formComponent"
                @search="handleQuery">
            </form-component>
            <div class="after-sale__btn-group-list">
                <div class="bs-flex bs-flex-space-end">
                  <group-btn :is-show="false" :type="'className'" :class-name="'blue'"
                             :icon="'el-icon-search'" :content="$t('message.buttons.search')"
                             @click.native="handleQuery"></group-btn>
                   <group-btn :is-show="false" :type="'className'" :class-name="'blue'"
                              :icon="'el-icon-refresh'" :content="$t('message.buttons.reset')"
                              @click.native="clearQuery"></group-btn>
                  <group-btn :is-show="false" type="className"
                             class-name="blue"
                             :icon="'el-icon-search'"
                             :content="$t('message.buttons.searchMore')"
                             @click.native="$refs.formDrawerRef.visible()"></group-btn>
                    <group-btn :is-show="false" :type="'className'" :class-name="'default'" :content="$t('message.warehouseDivision.inventoryStatement')" v-has="has_inventory"
                               @click.native="handleInventoryTable"></group-btn>
                    <group-btn :is-show="false" :type="'className'" :class-name="'default'" :content="$t('message.warehouseDivision.scrapOutbound')"
                               @click.native="handleScrapOutbound"></group-btn>
                    <group-btn :is-show="false" :type="'className'" :class-name="'default'" :content="$t('message.warehouseDivision.outboundList')"
                               @click.native="handleShowOutboundList"></group-btn>
                    <group-btn :is-show="false" :type="'className'" :class-name="'default'" :content="$t('message.warehousingInspectMaintenance.recyclingList')"
                               @click.native="handleRecyclingList"></group-btn>
                    <inventory-profit :warehouse-type="130"></inventory-profit>
                    <export-down v-for="(item,i) in exportOptions" :key="i" :options="item" v-has="has_export">{{ item.label }}</export-down>
                    <group-btn :is-show="false" :type="'className'" :class-name="'blue bom-query'" :content="$t('message.commons.bomQuery')" @click.native="openBomQueryDialog">
                    </group-btn>
                </div>
            </div>
        </div>
      <div>
            <table-component
                v-if="tableDataHeader.length > 0"
                ref="tableComponentRef"
                :is-show="true"
                :option="tableOption"
                :table-data="tableData"
                :table-data-header="tableDataHeader"
                :form="form"
                :form-params="formDrawer.formParams"
                @search="handleQuery"
                @refresh="handleRest"
                @size-change="sizeChange"
                @page-change="pageChange"
                @handle-selection-change="handleSelectionChange">
                <template slot="table-groups" slot-scope="{params}">
                  <group-btn v-if="params.row.showRecycleBtn" :is-show="false" :type="'className'" :class-name="'el-button--text'"
                             :content="$t('message.warehousingInspectMaintenance.materialRecycling')" @click.native="materialRecycling(params.row)">
                  </group-btn>
                </template>
            </table-component>
        </div>
      <warehouse-table :title="$t('message.warehouseDivision.scrapWarehouseInventory')" ref="warehouseTable"></warehouse-table>
      <material-recycling_product ref="materialRecycling_product"></material-recycling_product>
      <material-recycling_accessory ref="materialRecycling_accessory"></material-recycling_accessory>
      <recycling-list ref="recyclingList"></recycling-list>
      <scrap-outbound :list="multipleSelection" ref="scrapOutbound" @success="successUpdate"></scrap-outbound>
      <outbound-list ref="outboundList"></outbound-list>
      <bom-query ref="bomQuery" @close-bom-query-dialog="closeBomQueryDialog"></bom-query>
    </div>
</template>
<script>
    module.exports = {
      components: {
        "form-component": httpVueLoader("/views/components/formComponent.vue"),
        "group-btn": httpVueLoader("/views/components/groupBtn.vue"),
        "table-component": httpVueLoader("/views/components/tableComponent.vue"),
        "export-down": httpVueLoader("/webjars/plugins/bluetti-components/exportAndDownloadFile.vue"),
        "warehouse-table": httpVueLoader("/views/warehouseDivision/common/warehouseTable.vue"),
        "material-recycling_product": httpVueLoader("/views/warehouseDivision/scrapWarehouse/components/materialRecycling_product.vue"),
        "material-recycling_accessory": httpVueLoader("/views/warehouseDivision/scrapWarehouse/components/materialRecycling_accessory.vue"),
        "recycling-list": httpVueLoader("/views/warehouseDivision/scrapWarehouse/components/recyclingList.vue"),
        "scrap-outbound": httpVueLoader("/views/warehouseDivision/scrapWarehouse/components/scrapOutbound.vue"),
        "outbound-list": httpVueLoader("/views/warehouseDivision/scrapWarehouse/components/outboundList.vue"),
        "inventory-profit": httpVueLoader("/views/warehouseDivision/common/inventoryProfit.vue"),
        "bom-query": httpVueLoader("/views/components/bomQuery.vue"),
      },
      data() {
        return {
          permission: {
            ...this.$actionsList.warehouseDivision.scrapWarehouse,
          },
          form: {
            number: '',
            outInType: '',
            sn: '',
            sku: '',
            nodeType: '',
            category: '',
            shippingOrderNo: '',
            handlerDate: '',
            warehouseType: '',
            warehouseMasterType: 104,
            rmaNo: '',
            startHandlerDate: '',
            endHandlerDate: '',
          },
          option: {
            labelPosition: 'left',
            width: '0px',
            inline: true,
            refactor: true
          },
          tableData: [],
          tableOption: {
            height: 550,
            pageNo: 1,
            pageSize: window.framework.layout.PAGE_SIZE,
            total: 0,
            selectable: false,
            refactor: true,
            rowKey: 'number',
            id: '111',
          },
          inboundOutboundList: [],
          nodeTypeList: [],
          categoryList: [
            {
              id: 1,
              name: this.$t('message.commons.accessory')
            }, {
              id: 2,
              name: this.$t('message.commons.product')
            }
          ],
          multipleSelection: [],
          isShowBomQueryDialog: false,
        }
      },
      computed: {
        has_export() {
          return this.permission.export;
        },
        has_inventory() {
          return this.permission.inventory;
        },
        formDrawer() {
          return {
            formParams: this.formObj,
            option: {
              labelPosition: 'right',
              width: '0px',
              inline: true
            },
          };
        },
        formObj() {
          return [
            {
              type: 'input',
              name: 'number',
              title: this.$t('message.customerWorkOrderManage.orderSearch'),
              main: true,
              disabled: false,
              placeholder: this.$t('message.warehouseDivision.orderNum'),
              icon: 'el-icon-search',
              className: 'after-sale__form-item'
            },
            {
              type: 'input',
              name: 'rmaNo',
              main: true,
              disabled: false,
              placeholder: this.$t('message.warehouseDivision.afterSalesNumber'),
              icon: 'el-icon-search',
              className: 'after-sale__form-item'
            },
            {
              type: 'input',
              name: 'sn',
              main: true,
              disabled: false,
              placeholder: 'SN',
              icon: 'el-icon-search',
              className: 'after-sale__form-item'
            },
            {
              type: 'input',
              name: 'sku',
              disabled: false,
              placeholder: 'SKU',
              icon: 'el-icon-search',
              className: 'after-sale__form-item'
            },
            {
              type: 'selectId',
              name: 'outInType',
              disabled: false,
              placeholder: this.$t('message.warehouseDivision.incomingOutgoing'),
              icon: 'el-icon-search',
              className: 'after-sale__form-item',
              list: this.inboundOutboundList,
            },
            {
              type: 'selectId',
              name: 'nodeType',
              disabled: false,
              placeholder: this.$t('message.commons.type'),
              icon: 'el-icon-search',
              className: 'after-sale__form-item',
              list: this.nodeTypeList,
            },
            {
              type: 'input',
              name: 'shippingOrderNo',
              disabled: false,
              placeholder: this.$t('message.commons.returnWaybillNumber'),
              icon: 'el-icon-search',
              className: 'after-sale__form-item'
            },
            {
              type: 'selectId',
              name: 'category',
              disabled: false,
              placeholder: this.$t('message.siteDataSummary.productType'),
              icon: 'el-icon-search',
              className: 'after-sale__form-item',
              list: this.categoryList
            },
            {
              type: 'datetimerange',
              name: 'dates',
              disabled: false,
              placeholder: this.$t('message.warehouseDivision.dateTime'),
              icon: 'el-icon-search',
              className: 'after-sale__form-item',
              placeholderStart: this.$t('message.warehouseDivision.startTime'),
              placeholderEnd: this.$t('message.warehouseDivision.endTime'),
            },
            {
              type: 'daterange',
              label: '',
              name: 'businessTimes',
              disabled: false,
              placeholderStart: this.$t('message.warehouseDivision.nsStartBusinessTime'),
              placeholderEnd: this.$t('message.warehouseDivision.nsEndBusinessTime'),
              placeholder: this.$t('message.warehouseDivision.nsBusinessTime'),
              className: 'ware-house-business-time'
            },
          ];
        },
        exportOptions() {
          return [
            {
              label: this.$t('message.buttons.export'),
              method: 'post',
              fileName: win.utilsTool.acquisitionTime() + ' scrapWarehouse.xlsx',
              params: this.form,
              apiUrl: $.api($.apiModule.bluAfterSale, '/client/warehouse/segmentation/export'),
              size: 'default',
              icon: 'buletti-icons mi-daochu bs-padding-right-2',
              customClass: 'bt-download default'
            },
            {
              label: this.$t('message.buttons.exportAll'),
              method: 'post',
              fileName: win.utilsTool.acquisitionTime() + ' accessoriesWarehouse.xlsx',
              params: this.form,
              apiUrl: $.api($.apiModule.bluAfterSale, '/client/warehouse/segmentation/exportAll'),
              size: 'default',
              icon: 'buletti-icons mi-daochu bs-padding-right-2',
              customClass: 'bt-download default'
            },
          ];
        },
        tableDataHeader() {
          return [
            {
              type: 'operate',
              width: '150',
              align: 'left',
              prop: '',
              fixed: 'right',
              isHeader: false,
              hide: false,
              label: this.$t('message.commons.operation'),
            },
            {
              type: 'selection',
              width: '50',
              fixed: 'left',
              align: 'left',
              prop: 'checkBox',
              label: '',
              hide: false,
              isHeader: true,
            },
            {
              type: 'index',
              align: 'left',
            },
            {
              type: 1,
              width: '220',
              align: 'left',
              prop: 'number',
              label: this.$t('message.warehouseDivision.orderNum'),
              hide: false,
              isHeader: false,
              form: {
                formType: 'input',
                name: this.$t('message.warehouseDivision.orderNum'),
                params: [
                  {
                    field: "number",
                    fieldValue: "",
                    placeholder: this.$t('message.commons.enterConfirmSearch'),
                  },
                ],
              },
              fun: (name, row) => {
                return row.number ? row.number : '--';
              },
            },
            {
              type: '1',
              width: 'auto',
              minWidth: '200',
              align: 'left',
              prop: 'rmaNo',
              label: this.$t('message.customerWorkOrderManage.rmaNo'),
              hide: false,
              isHeader: false,
              propCode: 'rmaNo',
              form: {
                formType: 'input',
                name: this.$t('message.customerWorkOrderManage.rmaNo'),
                params: [
                  {
                    field: "rmaNo",
                    fieldValue: "",
                    placeholder: this.$t('message.commons.enterConfirmSearch'),
                  },
                ],
              },
              fun: (name, row) => {
                return row.rmaNo ? row.rmaNo : '--';
              },
            },
            {
              type: 1,
              width: '100',
              minWidth: '100',
              align: 'left',
              prop: 'i18nCategory',
              label: this.$t('message.siteDataSummary.productType'),
              hide: false,
              isHeader: false,
              form: {
                formType: 'select',
                selectType: 'selectId',
                name: this.$t('message.siteDataSummary.productType'),
                params: [
                  {
                    field: "category",
                    fieldValue: "",
                    placeholder: this.$t('message.commons.enterConfirmSearch'),
                  },
                ],
                options: this.categoryList,
              },
              fun: (name, row) => {
                return row.i18nCategory ? row.i18nCategory : '--';
              },
            },
            {
              type: 1,
              width: '220',
              align: 'left',
              prop: 'sku',
              label: this.$t('message.commons.sku'),
              hide: false,
              isHeader: false,
              form: {
                formType: 'input',
                name: this.$t('message.commons.sku'),
                params: [
                  {
                    field: "sku",
                    fieldValue: "",
                    placeholder: this.$t('message.commons.enterConfirmSearch'),
                  },
                ],
              },
              fun: (name, row) => {
                return row.sku ? row.sku : '--';
              },
            },
            {
              type: 1,
              width: '220',
              align: 'left',
              prop: 'sn',
              label: 'SN',
              hide: false,
              isHeader: false,
              form: {
                formType: 'input',
                name: 'SN',
                params: [
                  {
                    field: "sn",
                    fieldValue: "",
                    placeholder: this.$t('message.commons.enterConfirmSearch'),
                  },
                ],
              },
              fun: (name, row) => {
                return row.sn ? row.sn : '--';
              },
            },
            {
              type: 1,
              width: '120',
              align: 'left',
              prop: 'quantity',
              label: this.$t('message.commons.quantity'),
              hide: false,
              isHeader: false,
              fun: (name, row) => {
                return row.quantity ? row.quantity : '--';
              },
            },
            {
              type: 1,
              width: 'auto',
              align: 'left',
              prop: 'handlerDate',
              label: this.$t('message.inboundAndOutboundForecastForm.processingTime'),
              hide: false,
              isHeader: false,
              form: {
                formType: 'datetimerange',
                name: this.$t('message.inboundAndOutboundForecastForm.processingTime'),
                params: [
                  {
                    field: "dates",
                    fieldValue: "",
                    placeholderStart: this.$t('message.warehouseDivision.startTime'),
                    placeholderEnd: this.$t('message.warehouseDivision.endTime'),
                  },
                ],
              },
              fun: (name, row) => {
                return row.handlerDate ? row.handlerDate : '--';
              },
            },
            {
              type: 1,
              width: '100',
              align: 'left',
              prop: 'businessTime',
              label: this.$t('message.customerWorkOrderManage.businessProcessingTime'),
              hide: false,
              isHeader: false,
              fun: (name, row) => {
                return row.businessTime ? row.businessTime : '--';
              },
            },
            {
              type: 1,
              width: '120',
              align: 'left',
              prop: 'i18nOutIn',
              label: this.$t('message.warehouseDivision.incomingOutgoing'),
              hide: false,
              isHeader: false,
              form: {
                formType: 'select',
                selectType: 'selectId',
                name: this.$t('message.warehouseDivision.incomingOutgoing'),
                params: [
                  {
                    field: "outInType",
                    fieldValue: "",
                    placeholder: this.$t('message.commons.enterConfirmSearch'),
                  },
                ],
                options: this.inboundOutboundList,
              },
              fun: (name, row) => {
                return row.i18nOutIn ? row.i18nOutIn : '--';
              },
            },
            {
              type: 1,
              width: 'auto',
              align: 'left',
              prop: 'i18nTargetWarehouseType',
              label: this.$t('message.warehouseDivision.warehouse'),
              hide: false,
              isHeader: false,
              fun: (name, row) => {
                //报废仓本仓出库不展示出库存,产品完全回收不展示出库仓,物料回收展示配件仓
                if (row.outInType === 1 && (row.targetWarehouseType === 130 || row.category === 2)) {
                  return '--';
                } else {
                  return row.i18nTargetWarehouseType ? row.i18nTargetWarehouseType : '--';
                }
              },
            },
            {
              type: 1,
              width: '160',
              align: 'left',
              prop: 'i18nNodeType',
              label: this.$t('message.commons.type'),
              hide: false,
              isHeader: false,
              form: {
                formType: 'select',
                selectType: 'selectId',
                name: this.$t('message.commons.type'),
                params: [
                  {
                    field: "nodeType",
                    fieldValue: "",
                    placeholder: this.$t('message.commons.enterConfirmSearch'),
                  },
                ],
                options: this.nodeTypeList,
              },
              fun: (name, row) => {
                return row.i18nNodeType ? row.i18nNodeType : '--';
              },
            },

          ];
        },
      },
      mounted() {
        this.getEnumsAll();
        this.getList();
      },
      methods: {
        handleSelectionChange(val) {
          this.multipleSelection = val;
        },
        getEnumsAll() {
          this.$api.getEnumsAll().then(res => {
            if (res.msgCode === 0 && res.data) {
              this.nodeTypeList = res.data.segmentWarehouseNodeChangeTypeEnums;
              this.inboundOutboundList = res.data.outInTypeEnums;
            }
          })
        },
        successUpdate() {
          this.$refs.tableComponentRef && this.$refs.tableComponentRef.clearData();
          this.multipleSelection = [];
          this.getList();
        },
        // 获取列表
        getList() {
          const params = Object.assign({}, this.form, {
            pageNo: this.tableOption.pageNo,
            pageSize: this.tableOption.pageSize
          });
          if (this.form.dates && this.form.dates.length > 0) {
            params.startHandlerDate = this.form.dates[0];
            params.endHandlerDate = this.form.dates[1];
          }
          this.getLoading();
          this.$api.warehouseClassificationList(params).then(res => {
            this.isLoading.close();
            if (res.code === 0) {
              this.tableData = res.data.content;
              this.tableData.forEach(item => {
                if (!item.scrapOut) item.postSaleNo = '111';
              })
              this.tableOption.total = res.data.totalElements * 1;
            }
          })
        },
        //查询
        handleQuery() {
          this.tableOption.pageNo = 1;
          this.getList();
          this.$refs.tableComponentRef.getTag(this.form);
        },
        clearQuery() {
          this.form = {
            warehouseMasterType: 104,
          };
          this.tableOption.pageNo = 1;
          this.tableOption.pageSize = window.framework.layout.PAGE_SIZE;
          this.$refs.tableComponentRef && this.$refs.tableComponentRef.clearData();
          this.multipleSelection = [];
          this.getList();
        },
        handleRest() {
          this.form = {
            warehouseMasterType: 104,
          };
          this.tableOption.pageSize = window.framework.layout.PAGE_SIZE;
          this.tableOption.pageNo = 1;
          this.getList();
          this.$refs.tableComponentRef.getTag(this.form);
        },
        sizeChange(size) {
          this.tableOption.pageSize = size;
          this.tableOption.pageNo = 1;
          this.getList();
        },
        pageChange(page) {
          this.tableOption.pageNo = page;
          this.getList();
        },
        //库存表
        handleInventoryTable() {
          this.$refs.warehouseTable.visible(this.form.warehouseMasterType);
        },
        //回收列表
        handleRecyclingList() {
          this.$refs.recyclingList.visible();
        },
        handleScrapOutbound() {
          this.$refs.scrapOutbound.visible();
        },
        handleShowOutboundList() {
          this.$refs.outboundList.visible();
        },
        openBomQueryDialog() {
          this.isShowBomQueryDialog = true;
          this.$refs.bomQuery.visible();
        },
        closeBomQueryDialog() {
          this.isShowBomQueryDialog = false;
        },
        materialRecycling(row) {
          if (row.category === 1) {
            this.$refs.materialRecycling_accessory.visible(row, 1);
          } else {
            this.$refs.materialRecycling_product.visible(row, 1);
          }
        }

      },
    }
</script>
<style scoped>
.bom-query {
  margin-left: 10px;
}

.ware-house-business-time {
  margin-left: 10px;
}
</style>