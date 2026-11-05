<template>
    <div class="bs-mt-10">
      <!--  查询表单组件    -->
      <form-drawer ref="formDrawerRef" :form="form" :form-drawer="formDrawer" @search="handleQuery" @refresh="handleRest"></form-drawer>
      <div class="after-sale__work-order bs-flex bs-flex-items-center">
            <form-component v-if="formObj.length > 0"
                            :rule-form="form"
                            :option="option"
                            :rules="rules"
                            :form-obj="formObj"
                            ref="formComponent"
                            @search="handleQuery">
            </form-component>
            <div class="after-sale__btn-group-list">
                <div class="bs-flex bs-flex-space-end bs-mb-16">
                    <group-btn :is-show="false" :type="'className'" :class-name="'blue'" :icon="'el-icon-search'" :content="$t('message.buttons.search')"
                               @click.native="handleQuery"></group-btn>
                    <group-btn :is-show="false" :type="'className'" class-name="blue" :content="$t('message.buttons.searchMore')" :icon="'el-icon-search'"
                               @click.native="$refs.formDrawerRef.visible()"></group-btn>
                    <export-down  :options="exportOptions">{{exportOptions.label}}</export-down>
                </div>
            </div>
        </div>
        <div>
            <table-component
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
                @page-change="pageChange">
                <template slot="productMaterialVO" slot-scope="{params}">
                  <el-tooltip placement="bottom" effect="dark" popper-class="afterSaleCustomerInfo" v-if="params.row.productMaterialVO && params.row.productMaterialVO.length > 0">
                    <div slot="content">
                      <div class="tooltip-border bs-flex bs-gap-10 bs-flex-items-center" v-for="(item,i) in params.row.productMaterialVO">
                        <span class="after-sale__no">{{ i + 1 }}</span>
                        <div class="bs-flex-1">
                          <p class="">{{ $t('message.commons.sku') }}: {{ item.sku || item.model }}</p>
                        </div>
                        <div class="">X{{ item.count }}</div>
                      </div>
                    </div>
                    <span v-if="params.row.productMaterialVO.length  === 1">
                      <p class="bs-flex bs-flex-items-center"> <span class="bs-flex-1 bs-over-text"> {{ params.row.productMaterialVO[0].sku || params.row.productMaterialVO[0].model }} * {{ params.row.productMaterialVO[0].count }}</span></p>
                    </span>
                    <span v-if="params.row.productMaterialVO.length > 1">
                      <i class="buletti-icons mi-a-Group36315 bs-text-blue"></i>
                      <span v-if="params.row.productMaterialVO">{{ params.row.productMaterialVO.length }}</span>
                    </span>
                    <span v-if="params.row.productMaterialVO.length === 0">--</span>
                  </el-tooltip>
                  <span v-else>--</span>
                </template>
                <template slot="sn" slot-scope="{params}">
                  <el-tooltip placement="bottom" effect="dark" popper-class="afterSaleCustomerInfo" v-if="params.row.productMaterialVO && params.row.productMaterialVO.length > 0">
                    <div slot="content">
                      <div class="tooltip-border bs-flex bs-gap-10 bs-flex-items-center" v-for="(item,i) in params.row.productMaterialVO">
                        <span class="after-sale__no">{{ i + 1 }}</span>
                        <div class="bs-flex-1">
                          <p class="bs-flex bs-flex-items-center bs-flex-space-between"
                             v-if="item.sn || item.materialNo">
                            <span class="bs-flex bs-flex-items-center bs-flex-space-between">
                              <span class="bs-text-font-16  bs-flex-1">{{ params.row.productMaterialVO[0].sn ? 'SN：' : $t('message.commons.partNo') + '：' }}{{ item.sn || item.materialNo }} </span>
                              <i class="buletti-icons mi-a-fuzhicopy bs-text-blue bs-padding-left-10" @click="handleSn(item)"></i>
                            </span>
                          </p>
                        </div>
                        <div class="">X{{ item.count }}</div>
                      </div>
                    </div>
                    <span v-if="params.row.productMaterialVO.length  === 1">
                      <p class="bs-flex bs-flex-items-center" v-if="params.row.productMaterialVO[0].sn || params.row.productMaterialVO[0].materialNo">
                        <span class="bs-flex-1 bs-over-text">{{ params.row.productMaterialVO[0].sn || params.row.productMaterialVO[0].materialNo }}</span>
                      </p>
                    </span>
                    <span v-if="params.row.productMaterialVO.length > 1">
                      <i class="buletti-icons mi-a-Group36315 bs-text-blue"></i>
                      <span v-if="params.row.productMaterialVO">{{ params.row.productMaterialVO.length }}</span>
                    </span>
                    <span v-if="params.row.productMaterialVO.length === 0">--</span>
                  </el-tooltip>
                  <span v-else>--</span>
                </template>
                <template slot="rmaNo" slot-scope="{params}">
                    <el-tooltip placement="bottom" effect="dark" popper-class="afterSaleCustomerInfo"
                                v-if="params.row.rmaNo || params.row.warrantySituation || params.row.remark">
                        <div slot="content">
                            <p v-if="params.row.rmaNo" class="bs-flex  bs-flex-space-between">
                              <span>{{ $t('message.customerWorkOrderManage.rmaNo') }}: </span>
                              <span class=" bs-padding-left-10 flex">{{ params.row.rmaNo }}</span>
                            </p>
                            <p v-if="params.row.warrantySituation" class="bs-flex  bs-flex-space-between">
                              <span>{{ $t('message.customerWorkOrderManage.warrantySituation') }}: </span>
                              <span class=" bs-padding-left-10 flex">{{ params.row.warrantySituation }}</span>
                            </p>
                            <p v-if="params.row.remark" class="bs-flex  bs-flex-space-between">
                              <span>{{ $t('message.commons.remark') }}: </span>
                              <span class=" bs-padding-left-10 flex">{{ params.row.remark }}</span>
                            </p>
                        </div>
                        <span v-if="params.row.rmaNo" class="bs-flex bs-flex-items-center bs-gap-6">
                            <span class="bs-over-text">{{ params.row.rmaNo }}</span>
                             <i class="buletti-icons mi-a-fuzhicopy bs-text-blue" @click="handleAccessoryNo(params.row)"></i>
                        </span>
                        <span v-else>--</span>
                    </el-tooltip>
                    <span v-else>--</span>
                </template>
                <template slot="workOrderStatus" slot-scope="{params}">
                    <span class="table-after__status "
                          :class="params.row.workOrderStatus === 1 ? 'bs-text-bg-orange' : params.row.workOrderStatus === 2 ?'bs-text-bg-green' : 'bs-text-bg-err'">
                    </span>
                    <span> {{ params.row.workOrderStatusI18n ? params.row.workOrderStatusI18n : '--' }}</span>
                </template>
                <template slot="customerName" slot-scope="{params}">
                    <el-tooltip placement="bottom" effect="dark" popper-class="afterSaleCustomerInfo">
                        <div slot="content">
                            <p v-if="params.row.customerName" class="bs-flex  bs-flex-space-between">
                                <i class="buletti-icons mi-a-Group36309 bs-text-blue"></i>
                                <span class=" bs-padding-left-10 flex">{{ params.row.customerName }}</span>
                                <i class="buletti-icons mi-a-fuzhicopy bs-text-blue" @click="handleEmail(params.row)"></i>
                            </p>
                            <p v-if="params.row.customerEmail" class="bs-flex  bs-flex-space-between">
                                <i class="buletti-icons mi-a-Group36310 bs-text-blue"></i>
                                <span class=" bs-padding-left-10 flex">{{ params.row.customerEmail }}</span>
                                <i class="buletti-icons mi-a-fuzhicopy bs-text-blue" @click="handleEmail(params.row)"></i>
                            </p>
                            <p v-if="params.row.customerPhone"
                               class="bs-flex  bs-flex-space-between">
                                <i class="buletti-icons mi-a-Group36312 bs-text-blue"></i>
                                <span class=" bs-padding-left-10 flex">{{ params.row.customerPhone }}</span>
                                <i class="buletti-icons mi-a-fuzhicopy bs-text-blue" @click="handlePhone(params.row)"></i>
                            </p>
                            <p v-if="params.row.shippingAddress"
                               class="bs-flex  bs-flex-space-between">
                                <i class="buletti-icons mi-a-Group36314 bs-text-blue"></i>
                                <span class="-2 bs-padding-left-10 flex">{{ params.row.shippingAddress }}</span>
                                <i class="buletti-icons mi-a-fuzhicopy bs-text-blue" @click="handleCustomerAddress(params.row)"></i>
                            </p>
                        </div>
                        <span class="bs-flex bs-flex-items-center bs-gap-6">
                            <i class="buletti-icons mi-a-Group36315 bs-text-blue"></i>
                            <span class="bs-over-text"> {{ params.row.customerName }}</span>
                        </span>
                    </el-tooltip>
                </template>
                <template slot="outboundProductMaterialVO" slot-scope="{params}">
                    <el-tooltip placement="bottom" effect="dark" popper-class="afterSaleCustomerInfo"
                                v-if="params.row.outboundProductMaterialVO && params.row.outboundProductMaterialVO.totalCount !== 0">
                        <div slot="content">
                            <div class="bs-flex bs-flex-items-center bs-flex-space-between bs-mb-6">
                                <p> {{ $t('message.commons.total') }}：{{ params.row.outboundProductMaterialVO.totalCount }}
                                    {{ $t('message.commons.piece') }}</p>
                            </div>
                            <div v-if="params.row.outboundProductMaterialVO.received  && params.row.outboundProductMaterialVO.received.length > 0">
                                <div class="bs-flex bs-flex-items-center bs-flex-space-between">
                                    <p>
                                        <span class="bs-text-bg-orange icon">
                                            <i class="buletti-icons mi-a-Group36184"></i></span>
                                        <span class="bs-text-orange bs-padding-left-10">{{ $t('message.executionOutboundOperation.receivedSent') }}</span>
                                    </p>
                                    <p class="bs-text-orange">{{ params.row.outboundProductMaterialVO.received.length }}
                                        {{ $t('message.commons.item') }}</p>
                                </div>
                                <div class="tooltip-bottom-border received bs-mb-10"
                                     v-for="item in params.row.outboundProductMaterialVO.received">
                                    <p class="">{{ $t('message.commons.sku') }}:
                                       {{ item.sku }}</p>
                                    <p class="" v-if="item.sn">
                                        {{ $t('message.commons.sn') }}:{{ item.sn }}</p>
                                    <p class="">
                                        {{ $t('message.commons.quantity') }}: X{{ item.count }}</p>
                                </div>
                            </div>
                            <div v-if="params.row.outboundProductMaterialVO.nonReceived  && params.row.outboundProductMaterialVO.nonReceived.length > 0">
                                <div class="bs-flex bs-flex-items-center bs-flex-space-between">
                                    <p>
                                        <span class="bs-text-bg-err icon"><i
                                            class="buletti-icons mi-zhuanyikehu"></i></span>
                                        <span class="bs-text-err bs-padding-left-10">{{ $t('message.executionOutboundOperation.returnedNotReceived') }}</span>
                                    </p>
                                    <p class="bs-text-err">{{ params.row.outboundProductMaterialVO.nonReceived.length }}{{ $t('message.commons.item') }}</p>
                                </div>
                                <div class="tooltip-bottom-border nonReceived bs-mb-10"
                                     v-for="item in params.row.outboundProductMaterialVO.nonReceived">
                                    <p class="">{{ $t('message.commons.sku') }}:
                                       {{ item.sku }}</p>
                                    <p class="" v-if="item.sn">{{ $t('message.commons.sn') }}:{{ item.sn }}</p>
                                    <p class="">{{ $t('message.commons.quantity') }}: X{{ item.count }}</p>
                                </div>
                            </div>
                            <div v-if="params.row.outboundProductMaterialVO.shipped  && params.row.outboundProductMaterialVO.shipped.length > 0">
                                <div class="bs-flex bs-flex-items-center bs-flex-space-between">
                                    <p>
                                        <span class="bs-text-bg-green icon"><i class="buletti-icons mi-a-Group3970"></i></span>
                                        <span class="bs-text-green bs-padding-left-10">{{ $t('message.executionOutboundOperation.issued') }}</span>
                                    </p>
                                    <p class="bs-text-green">{{ params.row.outboundProductMaterialVO.shipped.length }}
                                        {{ $t('message.commons.item') }}</p>
                                </div>
                                <div class="tooltip-bottom-border shipped bs-mb-10"
                                     v-for="item in params.row.outboundProductMaterialVO.shipped">
                                    <p class="">{{ $t('message.commons.sku') }}:
                                        {{ item.sku }}</p>
                                    <p class=""
                                       v-if="item.sn">
                                        {{ $t('message.commons.sn') }}:{{ item.sn }}</p>
                                    <p class="">
                                        {{ $t('message.commons.quantity') }}: X{{ item.count }}</p>
                                </div>
                            </div>
                        </div>
                        <span>
                            <i class="buletti-icons mi-a-Group36315 bs-text-blue"></i>
                            <span v-if="params.row.outboundProductMaterialVO">{{ params.row.outboundProductMaterialVO.totalCount }}</span>
                            <span v-else>--</span>
                        </span>
                    </el-tooltip>
                    <span v-else>--</span>
                </template>
                <template slot="pushWms" slot-scope="{params}">
                     <span v-if="params.row.pushWms">
                         <span class="table-after__status " :class="params.row.pushWms === 1 ? 'bs-text-bg-green':'bs-text-bg-err'">
                        </span>
                     <span> {{ params.row.pushWms === 1 ? $t('message.executionOutboundOperation.hasPushed') : $t('message.executionOutboundOperation.notPushed') }}</span>
                     </span>
                    <span v-else>--</span>
                </template>
                <template slot="table-groups" slot-scope="{params}">
                    <group-btn :is-show="false" :type="'className'"
                               :class-name="'el-button--text'" :content="$t('message.buttons.view')"
                               @click.native="handleView(params.row)"
                               v-if="$permissionList[permission.view]"></group-btn>
                    <span v-if="params.row.pushWms !== 1">
                            <group-btn :is-show="false" :type="'className'"
                                       :class-name="'el-button--text'"
                                       :content="siteInfo.wmsEnable ? $t('message.executionOutboundOperation.pushWMS') :$t('message.executionOutboundOperation.continueCollectOrders')"
                                       @click.native="handleContinueSending(params.row)"
                                       v-if="$permissionList[permission.receivingSending]"></group-btn>
                    </span>
                    <group-btn :is-show="false" :type="'className'"
                               :class-name="'el-button--text'"
                               :content="$t('message.executionOutboundOperation.noodleListInfo')"
                               @click.native="handleNoodleList(params.row)"
                               v-if="$permissionList[permission.noodle]"></group-btn>
                </template>
            </table-component>
        </div>
      <!--        面单信息-->
        <noodle-list ref="noodleList"></noodle-list>
        <confirm-dialog ref="confirmationComponent" :data-name="dataName" :type="'del'">
            <div slot="button-group">
                <group-btn :is-show="false" :type="'save'" :content="$t('message.buttons.confirm')"
                           @click.native="deleteFun"></group-btn>
                <group-btn :is-show="false" :type="'className'" :content="$t('message.buttons.cancel')"
                           :class-name="'light-blue'"
                           @click.native="cancelComponent"></group-btn>
            </div>
        </confirm-dialog>
        <confirm-dialog ref="confirmation" :data-name="dataWms" :type="'del'">
            <div slot="button-group">
                <group-btn :is-show="false" :type="'save'" :content="$t('message.buttons.confirm')"
                           @click.native="submitWms"></group-btn>
                <group-btn :is-show="false" :type="'className'" :content="$t('message.buttons.cancel')"
                           :class-name="'light-blue'"
                           @click.native="cancelWMS"></group-btn>
            </div>
        </confirm-dialog>
    </div>
</template>
<script>
    module.exports = {
      name: 'myPendingPoolIndex',
      components: {
        "form-component": httpVueLoader("/views/components/formComponent.vue"),
        "group-btn": httpVueLoader("/views/components/groupBtn.vue"),
        "table-component": httpVueLoader("/views/components/tableComponent.vue"),
        "confirm-dialog": httpVueLoader("/views/components/confirmDialog.vue"),
        "noodle-list": httpVueLoader("/views/outboundOperationManage/outboundWorkOrderPool/noodleList.vue"),
        "export-down": httpVueLoader("/webjars/plugins/bluetti-components/exportAndDownloadFile.vue"),
      },
      data() {
        return {
          permission: {
            ...this.$actionsList.outBound.myPendingPool,
          },
          siteId: win.StorageUtil.sessionStorageGet('resource'),
          form: {
            workOrderNo: '',
            shipper: '',
            siteId: this.siteId,
            handler: '',
            customerPhone: '',
            customerName: '',
            customerEmail: '',
            customerAddress: '',
            sku: '',
            rmaNo: '',
          },
          option: {
            labelPosition: 'left',
            width: '0px',
            inline: true,
            refactor: true
          },
          rules: {},
          tableOption: {
            height: 550,
            pageNo: 1,
            pageSize: window.framework.layout.PAGE_SIZE,
            total: 0,
            selectable: false,
            refactor: true
          },
          tableData: [],
          currentData: null,
          dataName: null,
          postSaleWorkOrderStatusList: [],
          afterSaleReasonList:[],
          purchaseChannelsList:[],
          userList: [],
          isBtn: false,
          siteInfo: {},
          dataWms: '',
          currentWms: {},
        }
      },
      computed: {
        has_view() {
          return this.permission.view;
        },
        has_noodle() {
          return this.permission.noodle;
        },
        has_del() {
          return this.permission.del;
        },
        has_send() {
          return this.permission.receivingSending;
        },
        exportOptions(){
          return {
            label:this.$t('message.buttons.export'),
            method:'post',
            fileName:win.utilsTool.acquisitionTime() + ' myPendingPool.xlsx',
            params:this.form,
            apiUrl:$.api($.apiModule.bluAfterSale,'/client/outOperation/exportOtherWork'),
            size:'default',
            icon:'buletti-icons mi-daochu bs-padding-right-2',
            customClass:'bt-download default',
          };
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
              label: '',
              name: 'rmaNo',
              main: true,
              title: this.$t('message.customerWorkOrderManage.orderSearch'),
              disabled: false,
              placeholder: this.$t('message.customerWorkOrderManage.rmaNo'),
              icon: 'el-icon-search',
              className: 'after-sale__form-item',
            },
            {
              type: 'input',
              name: 'sku',
              main: true,
              disabled: false,
              placeholder: this.$t('message.commons.sku'),
              icon: 'el-icon-search',
              className: 'after-sale__form-item',
            },
            {
              type: 'selectUserId',
              label: '',
              main: true,
              width: 100,
              name: 'handler',
              disabled: false,
              placeholder: this.$t('message.executionOutboundOperation.customerPersonnel'),
              list: this.userList,
              className: 'after-sale__form-item',
            },

            {
              type: 'input',
              label: '',
              title: this.$t('message.customerWorkOrderManage.userSearch'),
              name: 'customerName',
              disabled: false,
              placeholder: this.$t('message.commons.customerName'),
              icon: 'el-icon-search',
              className: 'after-sale__form-item',
            },
            {
              type: 'input',
              label: '',
              name: 'customerPhone',
              disabled: false,
              placeholder: this.$t('message.commons.contactNumber'),
              icon: 'el-icon-search',
              className: 'after-sale__form-item',
            },
            {
              type: 'input',
              label: '',
              name: 'customerEmail',
              disabled: false,
              placeholder: this.$t('message.commons.contactEmail'),
              icon: 'el-icon-search',
              className: 'after-sale__form-item',
            },

            {
              type: 'input',
              label: '',
              name: 'customerAddress',
              disabled: false,
              placeholder: this.$t('message.commons.contactAddress'),
              icon: 'el-icon-search',
              className: 'after-sale__form-item',
            },
          ];
        },
        tableDataHeader() {
          return [
            {
              type: 1,
              width: '100',
              align: 'left',
              prop: 'type',
              label: this.$t('message.executionOutboundOperation.workOrderType'),
              hide: false,
              isHeader: false,
              fun: (name, row) => {
                return row.typeI18n ? row.typeI18n : '--';
              },
            },
            {
              type: 'index',
              align: 'left',
            },
            {
              type: 'slot',
              width: '220',
              align: 'left',
              prop: 'rmaNo',
              label: this.$t('message.customerWorkOrderManage.rmaNo'),
              hide: false,
              isHeader: false,
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
                return row.rmaNo;
              },
            },
            {
              type: 1,
              width: '100',
              align: 'left',
              prop: 'afterSaleTypeName',
              label: this.$t('message.commons.afterSalesProcessing'),
              hide: false,
              isHeader: false,
              fun: (name, row) => {
                return row.afterSaleTypeName ? row.afterSaleTypeName : '--';
              },
            },
            {
              type: 1,
              align: 'left',
              width: '160',
              prop: 'faultTypes',
              label: this.$t('message.executionOutboundOperation.faultContent'),
              hide: false,
              isHeader: false,
              fun: (name, row) => {
                return row.faultTypes && row.faultTypes.length > 0 ? row.faultTypes.join(',') : '--';
              },
            },
            {
              type: 1,
              width: '200',
              align: 'left',
              prop: 'orderNo',
              label: this.$t('message.customerWorkOrderManage.orderNumber'),
              hide: false,
              isHeader: false,
              fun: (name, row) => {
                return row.orderNo ? row.orderNo : '--';
              },
            },
            {
              type: 'slot',
              align: 'left',
              width: '120',
              prop: 'customerName',
              label: this.$t('message.commons.customerInfo'),
              isHeader: true,
              hide: false,
              headerPromptMessage: this.$t('message.executionOutboundOperation.payAttentionTip'),
              form: {
                formType: 'input',
                name: this.$t('message.commons.customerInfo'),
                params: [
                  {
                    field: "customerName",
                    fieldValue: "",
                    placeholder: this.$t('message.commons.customerName'),
                  },
                  {
                    field: "customerEmail",
                    fieldValue: "",
                    placeholder: this.$t('message.commons.contactEmail'),
                  },
                  {
                    field: "customerPhone",
                    fieldValue: "",
                    placeholder: this.$t('message.commons.contactNumber'),
                  },
                  {
                    field: "customerAddress",
                    fieldValue: "",
                    placeholder: this.$t('message.commons.contactAddress'),
                  },
                ],
              },
              fun: (name, row) => {
                return row.customerName;
              },
            },
            {
              type: 'slot',
              prop: 'productMaterialVO',
              align: 'left',
              width: '240',
              isHeader: false,
              hide: false,
              label: this.$t('message.commons.sku'),
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
            },
            {
              type: 'slot',
              width: '220',
              align: 'left',
              prop: 'sn',
              hide: false,
              label: this.$t('message.commons.sn'),
              isHeader: false,
              fun: (name, row) => {
                return '--';
              }
            },
            {
              type: 'slot',
              width: '80',
              align: 'left',
              label: this.$t('message.commons.quantity'),
              prop: 'outboundProductMaterialVO',
              isHeader: false,
              hide: false,
            },
            {
              type: 'slot',
              width: '160',
              align: 'left',
              prop: 'workOrderStatus',
              label: this.$t('message.customerWorkOrderManage.workOrderStatus'),
              hide: false,
              isHeader: false,
              fun: (name, row) => {
                return row.workOrderStatusI18n ? row.workOrderStatusI18n : '--';
              },
            },
            {
              type: 1,
              width: '80',
              align: 'left',
              prop: 'handler',
              hide: false,
              label: this.$t('message.executionOutboundOperation.customerServicePersonnel'),
              isHeader: false,
              form: {
                formType: 'select',
                selectType: 'selectUserId',
                name: this.$t('message.customerWorkOrderManage.customerServicePersonnel'),
                params: [
                  {
                    field: "handler",
                    fieldValue: "",
                    placeholder: this.$t('message.commons.enterConfirmSearch'),
                  },
                ],
                options: this.userList,
              },
              fun: (name, row) => {
                return row.handler ? row.handler : '--';
              },
            },
            {
              type: 'slot',
              hidden: !this.siteInfo.wmsEnable,
              width: '120',
              align: 'left',
              fixed: 'right',
              prop: 'pushWms',
              label: this.$t('message.executionOutboundOperation.isPushWMS'),
              hide: false,
              isHeader: false,
              fun: (name, row) => {
                return row.pushWms ? row.pushWms : '--';
              },
            },
            {
              type: 'operate',
              width: '300',
              align: 'left',
              prop: '',
              fixed: 'right',
              isHeader: false,
              hide: false,
              label: this.$t('message.commons.operation'),
            },
          ]
        },
      },
      mounted() {
        if (win.StorageUtil.sessionStorageGet('msgInfo')) {
          const info = win.StorageUtil.sessionStorageGet("currentPage");
          //如果有就读取缓存里面的数据
          if (info && info.pageNo) {
            this.tableOption.pageNo = Number(info.pageNo);
          }
          if (info && info.form) {
            this.form = info.form;
          }
        } else {
          this.tableOption.pageNo = 1;
          //这个主要是从其他页面第一次进入列表页，清掉缓存里面的数据
          win.StorageUtil.sessionStorageDelete("currentPage");
        }
        this.getEnumsAll();
        this.getSiteUserAll();
        this.getGroupDict();
        this.getList();
        this.siteInfo = win.StorageUtil.sessionStorageGet('siteInfo');
      },
      destroyed() {
        win.StorageUtil.sessionStorageDelete("msgInfo");
      },
      methods: {
        isWorkOrderStatus(row) {
          return row.workOrderStatus === 11 || row.workOrderStatus === 1 || row.workOrderStatus === 8 || row.workOrderStatus === 9;
        },
        getGroupDict() {
          this.$api.getBaseInfoAll(this.siteId).then(res => {
            if (res.msgCode === 0 && res.data) {
              let groupList = res.data.groupList;
              this.afterSaleReasonList = groupList.afRe;
              this.purchaseChannelsList = groupList.puCh;
            }
          })
        },
        async getEnumsAll() {
          await this.$api.getEnumsAll().then(res => {
            if (res.msgCode === 0 && res.data) {
              this.postSaleWorkOrderStatusList = res.data.postSaleWorkOrderStatus;
            }
          })
        },
        getList() {
          let params = Object.assign({}, this.form, {
            pageNo: this.tableOption.pageNo,
            pageSize: this.tableOption.pageSize
          });
          this.getLoading();
          this.$api.receiptPage(params).then(res => {
            this.isLoading.close();
            if (res.msgCode === 0) {
              this.tableData = res.data.content;
              this.tableOption.total = res.data.totalElements * 1;
            }
          })
        },
        getSiteUserAll() {
          this.$api.getSiteUsers(this.siteId).then(res => {
            if (res.msgCode === 0) {
              this.userList = res.data;
            }
          })
        },
        clearForm() {
          this.form = {
            workOrderNo: '',
            shipper: '',
            siteId: this.siteId,
            handler: '',
            customerPhone: '',
            customerName: '',
            customerEmail: '',
            customerAddress: '',
            model: '',
            rmaNo: '',
            sku: '',
          };
        },
        handleCustomerAddress(item) {
          this.getCopy(item.shippingAddress);
        },
        handlePhone(item) {
          this.getCopy(item.customerPhone);
        },
        handleEmail(item) {
          this.getCopy(item.customerEmail);
        },
        handleAccessoryNo(row) {
          this.getCopy(row.rmaNo);
        },
        handleLogisticsCompany(row) {
          this.getCopy(row.materialNo);
        },
        handleSn(row) {
          this.getCopy(row.materialNo || row.sn);
        },
        handleView(row) {
          win.StorageUtil.sessionStorageSet('currentPage', {
            pageNo: this.tableOption.pageNo,
            form: this.form
          });
          this.$router.push({
            path: '/afterSaleClient/inventory/client/outBoundRefactor/route/v2/view',
            query: {
              id: row.workOrderId,
              afterSaleType: row.afterSaleType,
              orderType: row.type,
              active: '4',
              outStatus: row.status,
              rmaNo: row.rmaNo
            }
          });
        },
        handleShipping(row) {
          win.StorageUtil.sessionStorageSet('currentPage', {
            pageNo: this.tableOption.pageNo,
            form: this.form
          });
          this.$router.push({
            path: '/afterSaleClient/client/wareHouseRoute/route/shippingRegistration',
            query: {
              id: row.accessoryNo,
              active: '4'
            }
          });
        },
        handleNoodleList(row) {
          this.$refs.noodleList.visible(row);
        },
        handleContinueSending(row) {
          this.outOperationPickup(row);
        },
        outOperationPickup(row) {
          if (this.siteInfo.wmsEnable) {
            this.dataWms = this.$t('message.executionOutboundOperation.isSurePushWMS');
            this.currentWms = row;
            this.$refs.confirmation.visible();
          } else {
            // 产品出库
            if (row.productType == 2) {
              this.pickupFun(row);
            }
            // 配件出库
            if (row.productType == 1) {
              this.goReceivingSendingPage(row);
            }
          }

        },
        pickupFun(row, type) {
          this.getLoading();
          this.$api.outOperationPickup(row.workOrderId, row.type).then(res => {
            if (type) this.$refs.confirmation.dialogClose();
            this.isLoading.close();
            if (res.msgCode === 0) {
              if (type) {
                this.getList();
              } else {
                this.goReceivingSendingPage(row);
              }
            }
          })
        },
        goReceivingSendingPage(row) {
          win.StorageUtil.sessionStorageSet('currentPage', this.tableOption.pageNo);
          this.$router.push({
            path: "/afterSaleClient/inventory/client/outBoundRefactor/route/v2/receivingSending",
            query: {
              id: row.workOrderId,
              type: row.type,
              active: '4',
              afterSaleType: row.afterSaleType,
              realRightType: row.realRightType,
              workOrderStatus: row.workOrderStatus,
              isWorkOrderType: row.type === 1,
              productType: row.productType,
              orderType: row.rmaNo ? 1 : 2
            }
          });
        },
        submitWms() {
          const currentWms = this.currentWms;
          // 产品出库
          if (currentWms.productType == 2) {
            this.pickupFun(currentWms, true);
          }
          // 配件出库
          if (currentWms.productType == 1) {
            this.handleSendPreEntry(currentWms);
          }
        },
        handleSendPreEntry(row) {
          this.$api.sendPreEntry(row.accessoryWorkOrderId).then(res => {
            if (res.code === 0) {
              this.getList();
              this.$message.success(this.$t('message.executionOutboundOperation.hasPushWMS'));
            }
            this.$refs.confirmation.dialogClose();
          }).catch(() => {
            this.$refs.confirmation.dialogClose();
          })
        },
        cancelWMS() {
          this.$refs.confirmation.dialogClose();
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
        handleQuery(form) {
          this.form = {...this.form, ...form};
          this.tableOption.pageNo = 1;
          this.getList();
          this.$refs.tableComponentRef.getTag(this.form);
        },
        handleRest() {
          this.clearForm();
          this.tableOption.pageNo = 1;
          this.tableOption.pageSize = window.framework.layout.PAGE_SIZE;
          this.tableOption.total = 0;
          this.getList();
          this.$refs.tableComponentRef.getTag(this.form);
        },
        handleAdd() {
          this.$router.push({
            path: '/afterSaleClient/client/workOrder/route/partsWorkOrderAdd'
          });
        },
        handleCloseWorkorder(item) {
          this.currentData = item;
          this.dataName = this.$t('message.refurbishmentInboundOutbound.returnOrder');
          this.$refs.confirmationComponent.visible();
        },
        deleteFun() {
          this.$api.chargeBack(this.currentData.workOrderId, this.currentData.type).then(res => {
            this.$refs.confirmationComponent.dialogClose();
            if (res.msgCode === 0) {
              this.getList();
            }
          })
        },
        cancelComponent() {
          this.$refs.confirmationComponent.dialogClose();
        },
      },
    }
</script>
