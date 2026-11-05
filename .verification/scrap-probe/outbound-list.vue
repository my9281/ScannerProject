<template>
  <div>
    <el-dialog :title="$t('message.warehouseDivision.outboundList')" :visible.sync="dialogVisible"
               width="60%" :close-on-click-modal="false" center @close="handleClose" class="after-sale__dialog">
      <div class="bs-flex bs-flex-items-center bs-gap-10 bs-mt-20 bs-mb-20">
        <div class="bs-flex bs-flex-items-center bs-gap-6">
          <el-input v-model="form.id" size="small" :placeholder="$t('message.otherWarehousing.deliveryOrderNumber')"></el-input>
        </div>
        <div class="bs-flex bs-flex-items-center bs-gap-6">
          <el-input v-model="form.sku" :placeholder="$t('message.commons.sku')" size="small"></el-input>
        </div>
        <div class="bs-flex bs-flex-items-center bs-gap-6">
          <el-input v-model="form.sn" :placeholder="$t('message.commons.sn')" size="small"></el-input>
        </div>
        <div class="bs-flex bs-flex-items-center bs-gap-6">
          <el-input v-model="form.handler" :placeholder="$t('message.customerWorkOrderManage.recycleOrder.applyUserName')" size="small"></el-input>
        </div>
        <div>
          <group-btn :is-show="false" type="className" class-name="blue" :content="$t('message.buttons.search')" :icon="'el-icon-search'" @click.native="handleQuery"></group-btn>
        </div>
      </div>
      <table-component v-if="tableDataHeader.length > 0"
                       ref="tableComponentRef"
                       :is-show="true"
                       :option="tableOption"
                       :table-data="tableData"
                       :table-data-header="tableDataHeader"
                       @size-change="sizeChange"
                       @page-change="pageChange">
        <template slot="detail" slot-scope="{params}">
          <el-popover
              placement="right"
              popper-class="nest"
              width="650"
              trigger="hover">
            <el-table class="after-sale"
                      ref="tableData"
                      border
                      row-key="id"
                      header-row-class-name="after-sale__table-header-row"
                      :max-height="550"
                      :data="params.row.details">
              <el-table-column :label="$t('message.commons.sku')" prop="sku" width="180"></el-table-column>
              <el-table-column label="SN" prop="sn" width="112"></el-table-column>
              <el-table-column :label="$t('message.warehouseDivision.orderNum')" prop="warehousingId" width="200"></el-table-column>
              <el-table-column :label="$t('message.siteDataSummary.productType')" prop="i18nCategory">
                <template slot-scope="scope">
                  <span>{{ scope.row.type === 1 ? $t('message.commons.accessory') : $t('message.commons.product') }}</span>
                </template>
              </el-table-column>
              <el-table-column :label="$t('message.commons.quantity')" prop="quantity"></el-table-column>
            </el-table>
            <i slot="reference" class="el-icon-menu detail-icon"></i>
          </el-popover>
        </template>
        <template slot="table-groups" slot-scope="{params}">
          <group-btn :is-show="false" :type="'className'" :class-name="'el-button--text'" :content="$t('message.buttons.view')"
                     @click.native="handleView(params.row)"></group-btn>
          <group-btn v-if="params.row.status === 0" :is-show="false" :type="'className'" :class-name="'el-button--text'" :content="$t('message.buttons.revoke')"
                     @click.native="handleRevoke(params.row)"></group-btn>
          <group-btn :is-show="false" :type="'className'" :class-name="'el-button--text'" :content="$t('message.buttons.export')"
                     @click.native="handleExport(params.row)"></group-btn>
          <group-btn v-if="params.row.status === 0" :is-show="false" :type="'className'" :class-name="'el-button--text'" :content="$t('message.warehouseDivision.outboundConfirmation')"
                       @click.native="handleSubmit(params.row)"></group-btn>
        </template>
      </table-component>

    </el-dialog>
    <scrap-outbound ref="scrapOutbound" @success="getList"></scrap-outbound>
  </div>
</template>

<script>
module.exports = {
  name: "outboundList",
  components:{
    "table-component": httpVueLoader("/views/components/tableComponent.vue"),
    "group-btn": httpVueLoader("/views/components/groupBtn.vue"),
    "scrap-outbound": httpVueLoader("/views/warehouseDivision/scrapWarehouse/components/scrapOutbound.vue"),
  },
  data(){
    return {
      dialogVisible: false,
      tableOption: {
        height: 550,
        pageNo: 1,
        pageSize: window.framework.layout.PAGE_SIZE,
        total: 0,
        selectable: false,
      },
      form:{
        id:'',
        sku:'',
        sn:'',
        handler:'',
      },
      tableData: [],
      siteInfo: win.StorageUtil.sessionStorageGet('siteInfo'),
    }
  },
  computed:{
    tableDataHeader(){
      return [
        {
          type: 'operate',
          width: '240',
          align: 'left',
          prop: '',
          fixed: 'right',
          isHeader: false,
          hide: false,
          label: this.$t('message.commons.operation'),
        },
        {
          type: 1,
          minWidth: '220',
          align: 'left',
          prop: 'id',
          isHeader: false,
          hide: false,
          label: this.$t('message.otherWarehousing.deliveryOrderNumber'),
        },
        {
          type: 'slot',
          minWidth: '200',
          align: 'left',
          prop: 'detail',
          isHeader: false,
          hide: false,
          label: this.$t('message.warehouseDivision.outboundList'),
        },
        {
          type: 1,
          minWidth: '200',
          align: 'left',
          prop: 'handlerName',
          isHeader: false,
          hide: false,
          label: this.$t('message.customerWorkOrderManage.recycleOrder.applyUserName'),
        },
        {
          type: 1,
          minWidth: '200',
          align: 'left',
          prop: 'cause',
          isHeader: false,
          hide: false,
          label: this.$t('message.warehouseDivision.scrappingReason'),
        },
        {
          type: 1,
          minWidth: '200',
          align: 'left',
          prop: 'handlerTime',
          isHeader: false,
          hide: false,
          label: this.$t('message.commons.applyTime'),
        },
        {
          type: 1,
          minWidth: '200',
          align: 'left',
          prop: 'status',
          isHeader: false,
          hide: false,
          label: this.$t('message.commons.status'),
          fun: (name, row) => {
            if(row.status === -1) return this.$t('message.commons.revoked');
            if(row.status === 0) return this.$t('message.inboundAndOutboundForecastForm.inProgress');
            if(row.status === 1) return this.$t('message.customerWorkOrderManage.toDo.outStock');
            if(row.status === 2) return this.$t('message.customerWorkOrderManage.toDo.toDispatched');
          },
        },
      ];
    }
  },
  methods:{
    visible(){
      this.dialogVisible = true;
      this.getList();
    },
    handleQuery(){
      this.tableOption.pageNo = 1;
      this.getList();
    },
    getList(){
      let params = Object.assign({}, this.form, {
            pageNo: this.tableOption.pageNo,
            pageSize: this.tableOption.pageSize,
          });
      this.$api.getOutboundList(params).then(res=>{
        if(res.code === 0 && res.data){
          this.tableData = res.data.content;
          this.tableOption.total = res.data.totalElements * 1;
          this.tableOption.pageNo = res.data.pageNo * 1;
        }else {
          this.tableData = [];
          this.tableOption.total = 0;
          this.tableOption.pageNo = 1;
        }
      })
    },
    handleView(row){
      this.$refs.scrapOutbound.visible('view',row);
    },
    handleClose(){
      this.dialogVisible = false;
      Object.assign(this.form,{
        id:'',
        sku:'',
        sn:'',
        handler:'',
      });
    },
    handleRevoke(row){
      this.$confirm(this.$t('message.customerWorkOrderManage.tip3'), this.$t('message.commons.tips'), {
        confirmButtonText: this.$t('message.buttons.confirm'),
        cancelButtonText: this.$t('message.buttons.cancel'),
        type: 'warning'
      }).then(() => {
        this.$api.scrapRevocation(row.id).then(res => {
          if (res.code === 0) {
            this.$message.success(this.$t('message.commons.operationSuccessful'));
            this.getList();
          }
        })
      }).catch(()=>{})
    },
    handleExport(row){
      function createObjectURL(object) {
        return (window.URL) ? window.URL.createObjectURL(object) : window.webkitURL.createObjectURL(object);
      }
      const fileName = this.$t('message.customerWorkOrderManage.applicationTemplate') + '.pdf';
      const xhr = new XMLHttpRequest();
      xhr.open('get', $.api($.apiModule.bluAfterSale,"/client/scrapOut/application/export?id=" + row.id));
      xhr.responseType = 'blob';
      xhr.setRequestHeader("Authorization", win.SsoUtil.getAccessToken());
      xhr.setRequestHeader("x-app-key", $.apiClient);
      xhr.setRequestHeader("x-app-resource", win.StorageUtil.sessionStorageGet("resource"));
      xhr.setRequestHeader('Content-Type', 'application/json;charset=UTF-8');
      xhr.onload = function (e) {
        if (this.status === 200) {
          const blob = this.response;
          if (window.navigator.msSaveOrOpenBlob) {
            navigator.msSaveBlob(blob, fileName);
          } else {
            const a = document.createElement('a');
            const url = createObjectURL(blob);
            a.href = url;
            a.download = fileName;
            document.body.appendChild(a);
            a.click();
            window.URL.revokeObjectURL(url);
          }
        }
      };
      xhr.send(JSON.stringify(this.saleForm));
    },
    handleSubmit(row){
      this.$refs.scrapOutbound.visible('confirm',row);
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
  },
}
</script>

<style scoped>
.detail-icon{
  font-size: 32px;
}
</style>