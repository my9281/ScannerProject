<template>
    <div class="warehouse-tabs">
        <el-tabs v-model="activeName">
            <el-tab-pane v-for="ele in list" v-if="list.length > 0 && ele.show"
                         :key="ele.count"
                         :label="ele.label"
                         :name="ele.count">
                <storage-warehouse v-if="activeName === ele.count && ele.count == 1"></storage-warehouse>
                <self-warehouse v-if="activeName === ele.count && ele.count == 2"></self-warehouse>
                <customer-supply-warehouse v-if="activeName === ele.count && ele.count == 3"></customer-supply-warehouse>
                <scrap-warehouse v-if="activeName === ele.count && ele.count == 4"></scrap-warehouse>
                <accessories-warehouse v-if="activeName === ele.count && ele.count == 5"></accessories-warehouse>
                <trade-in-warehouse v-if="activeName === ele.count && ele.count == 6"></trade-in-warehouse>
                <repair-warehouse v-if="activeName === ele.count && ele.count == 7"></repair-warehouse>
                <prohibited-from-sale v-if="activeName === ele.count && ele.count == 8"></prohibited-from-sale>
            </el-tab-pane>
        </el-tabs>
       <div class="warehouse-tabs__down" v-if="activeName != 8">
         <export-down :options="exportOptions">{{ $t('message.warehouseDivision.exportAllInventory') }}</export-down>
       </div>
    </div>
</template>

<script>
    module.exports = {
      name: 'index',
      components: {
        "storage-warehouse": httpVueLoader("/views/warehouseDivision/storageWarehouse/index.vue"),
        "self-warehouse": httpVueLoader("/views/warehouseDivision/selfWarehouse/main.vue"),
        "customer-supply-warehouse": httpVueLoader("/views/warehouseDivision/customerSupplyWarehouse/main.vue"),
        "scrap-warehouse": httpVueLoader("/views/warehouseDivision/scrapWarehouse/index.vue"),
        "accessories-warehouse": httpVueLoader("/views/warehouseDivision/accessoriesWarehouse/index.vue"),
        "trade-in-warehouse": httpVueLoader("/views/warehouseDivision/tradeWarehouse/index.vue"),
        "repair-warehouse": httpVueLoader("/views/warehouseDivision/repairWarehouse/main.vue"),
        "prohibited-from-sale": httpVueLoader("/views/warehouseDivision/prohibitedFromSale/list.vue"),
        "export-down": httpVueLoader('/webjars/plugins/bluetti-components/exportAndDownloadFile.vue'),
        "group-btn": httpVueLoader("/views/components/groupBtn.vue"),
      },
      data() {
        return {
          activeName: '1',
          permission: {
            ...this.$actionsList.warehouseDivision,
          },
          list: [],
          labelList: [
            this.$t('i18n.route.action.bluas.afterSaleClient.warehouseDivisionMgt.index'),
            this.$t('i18n.route.action.bluas.afterSaleClient.warehouseDivisionMgt.selfWarehouse'),
            this.$t('i18n.route.action.bluas.afterSaleClient.warehouseDivisionMgt.customerSupplyWarehouse'),
            this.$t('i18n.route.action.bluas.afterSaleClient.warehouseDivisionMgt.scrapWarehouse'),
            this.$t('i18n.route.action.bluas.afterSaleClient.warehouseDivisionMgt.accessoriesWarehouse'),
            this.$t('i18n.route.action.bluas.afterSaleClient.warehouseDivisionMgt.tradeInWarehouse'),
            this.$t('i18n.route.action.bluas.afterSaleClient.warehouseDivisionMgt.repairWarehouse'),
            this.$t('i18n.route.action.bluas.afterSaleClient.warehouseDivisionMgt.prohibitedFromSale'),
          ],
          exportOptions: {
            label: this.$t('message.warehouseDivision.exportAllInventory'),
            method: 'post',
            fileName: win.utilsTool.acquisitionTime() + ' allInventoryWarehouse.xlsx',
            params: this.form,
            apiUrl: $.api($.apiModule.bluAfterSale, '/client/warehouse/segmentation/exportInvLot'),
            size: 'default',
            icon: 'buletti-icons mi-daochu bs-padding-right-2',
            customClass: 'bt-download blue',
          },
        }
      },
      mounted() {
        this.list = win.utilsTool.initTabs(this.permission, this.labelList);
        this.activeName = this.list.find(item => item.show)?.count || '1';
        if (this.$route.query && this.$route.query.active) {
          this.activeName = this.$route.query.active;
        }
      },
      methods:{},
    }
</script>
<style scoped>
.warehouse-tabs {
  position: relative;
}

.warehouse-tabs__down {
  position: absolute;
  right: 10px;
  top: 2px;
}

.bt-download.blue {
  color: var(--main-color);
}

.bt-download.blue:hover {
  color: #FFFFFF;
}
</style>
