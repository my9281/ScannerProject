<template>
    <div class="bs-mt-10">
        <!--  查询表单组件    -->
        <form-drawer ref="formDrawerRef" :form="form" :form-drawer="formDrawer" @search="handleQuery" @refresh="handleRest">
          <template slot="logisticsCompany">
              <el-form-item label="" prop="" class="after-sale__form-item">
                <el-select v-model="form.logisticsCompany" class="bs-input" clearable filterable allow-create
                           default-first-option
                           :placeholder="$t('message.commons.logisticsProvider')"
                           @blur="onAfterSaleSource($event,'logisticsCompany')">
                  <el-option v-for="item in logisticsCompanyList"
                             :key="item.id"
                             :label="item.name"
                             :value="item.name"
                  ></el-option>
                </el-select>
              </el-form-item>
            </template>
        </form-drawer>
        <div class="after-sale__work-order bs-flex bs-flex-items-center">
            <form-component v-if="formObj.length > 0"
                            :rule-form="form"
                            :option="option"
                            :rules="rules"
                            :form-obj="formObj"
                            ref="formComponent"
                            @search="handleQuery">
              <template slot="logisticsCompany">
                <el-form-item label="" prop="" class="after-sale__form-item">
                  <el-select v-model="form.logisticsCompany" class="bs-input" clearable filterable allow-create
                             default-first-option
                             :placeholder="$t('message.commons.logisticsProvider')"
                             @blur="onAfterSaleSource($event,'logisticsCompany')">
                    <el-option v-for="item in logisticsCompanyList"
                               :key="item.id"
                               :label="item.name"
                               :value="item.name"
                    ></el-option>
                  </el-select>
                </el-form-item>
              </template>
            </form-component>
            <div class="after-sale__btn-group-list bs-flex">
                    <group-btn :is-show="false" :type="'className'" :class-name="'blue'" :icon="'el-icon-search'" :content="$t('message.buttons.search')"
                               @click.native="handleQuery"></group-btn>
                    <group-btn :is-show="false" :type="'className'" :class-name="'blue'" :icon="'el-icon-search'" :content="$t('message.buttons.searchMore')"
                               @click.native="$refs.formDrawerRef.visible()"></group-btn>
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
                <template slot="rmaNo" slot-scope="{params}">
                    <el-tooltip placement="bottom" effect="dark" popper-class="afterSaleCustomerInfo"
                                v-if="params.row.warrantySituation">
                        <div slot="content">
                            <p v-if="params.row.warrantySituation" class="bs-flex bs-flex-items-center bs-flex-space-between">
                              <span>{{ $t('message.customerWorkOrderManage.warrantySituation') }}: </span>
                              <span class=" bs-padding-left-10 flex">{{ params.row.warrantySituation }}</span>
                            </p>
                        </div>
                        <span v-if="params.row.rmaNo" class="bs-flex bs-flex-items-center bs-gap-6">
                              <span class="bs-over-text link-text" @click="handleView(params.row)">{{ params.row.rmaNo }}</span>
                              <i class="buletti-icons mi-a-fuzhicopy bs-text-blue" @click="handleAccessoryNo(params.row)"></i>
                        </span>
                    </el-tooltip>
                    <span v-else class="bs-flex bs-flex-items-center bs-gap-6">
                       <span class="bs-over-text link-text" @click="handleView(params.row)" v-if="params.row.rmaNo">{{ params.row.rmaNo }}</span>
                        <i class="buletti-icons mi-a-fuzhicopy bs-text-blue" @click="handleAccessoryNo(params.row)" v-if="params.row.rmaNo"></i>
                    </span>
                </template>
                <template slot="customerName" slot-scope="{params}">
                    <el-tooltip placement="bottom" effect="dark" popper-class="afterSaleCustomerInfo">
                        <div slot="content">
                            <p v-if="params.row.customerName" class="bs-flex bs-flex-space-between">
                                <i class="buletti-icons mi-a-Group36309 bs-text-blue"></i>
                                <span class=" bs-padding-left-10 flex">{{ params.row.customerName }}</span>
                                <i class="buletti-icons mi-a-fuzhicopy bs-text-blue" @click="handleEmail(params.row)"></i>
                            </p>
                            <p v-if="params.row.customerEmail" class="bs-flex bs-flex-space-between">
                                <i class="buletti-icons mi-a-Group36310 bs-text-blue"></i>
                                <span class=" bs-padding-left-10 flex">{{ params.row.customerEmail }}</span>
                                <i class="buletti-icons mi-a-fuzhicopy bs-text-blue" @click="handleEmail(params.row)"></i>
                            </p>
                            <p v-if="params.row.customerPhone"
                               class="bs-flex bs-flex-space-between">
                                <i class="buletti-icons mi-a-Group36312 bs-text-blue"></i>
                                <span class=" bs-padding-left-10 flex">{{ params.row.customerPhone }}</span>
                                <i class="buletti-icons mi-a-fuzhicopy bs-text-blue" @click="handlePhone(params.row)"></i>
                            </p>
                            <p v-if="params.row.shippingAddress" class="bs-flex bs-flex-space-between">
                                <i class="buletti-icons mi-a-Group36314 bs-text-blue"></i>
                                <span class="-2 bs-padding-left-10 flex">{{ params.row.shippingAddress }}</span>
                                <i class="buletti-icons mi-a-fuzhicopy bs-text-blue" @click="handleCustomerAddress(params.row)"></i>
                            </p>
                        </div>
                        <span class="bs-flex bs-flex-items-center bs-gap-6" v-if="params.row.customerName">
                            <i class="buletti-icons mi-a-Group36315 bs-text-blue"></i>
                            <span class="bs-over-text"> {{ params.row.customerName }}</span>
                        </span>
                    </el-tooltip>
                </template>
                <template slot="shippingOrderNo" slot-scope="{params}">
                  <el-tooltip placement="bottom" effect="dark" popper-class="afterSaleCustomerInfo" v-if="params.row.shippingOrderNo">
                    <div slot="content">
                      <div class="tooltip-border bs-gap-10 bs-flex bs-flex-items-center">
                        <div>
                          <p class="bs-flex bs-flex-items-center bs-flex-space-between">
                            <span class="bs-flex bs-flex-items-center bs-flex-space-between">
                              <span class="bs-flex-1">
                                <a class="bs-text-font-16 bs-underline-white bs-flex-1"
                                   :href="'https://www.17track.net/#nums=' + params.row.shippingOrderNo"
                                   target="_blank">
                                  {{ $t('message.commons.returnWaybillNumber') }}：{{ params.row.shippingOrderNo }}
                                </a>
                              </span>
                              <i class="buletti-icons mi-a-fuzhicopy bs-text-blue bs-padding-left-10" @click="handleCompany(params.row)"></i>
                            </span>
                          </p>
                        </div>
                      </div>
                    </div>
                    <div class="bs-over-text">{{ params.row.shippingOrderNo }}</div>
                  </el-tooltip>
                  <span v-else>--</span>
               </template>
                <template slot="outboundProductMaterialVO" slot-scope="{params}">
                    <el-tooltip placement="bottom" effect="dark" popper-class="afterSaleCustomerInfo"
                                v-if="params.row.outboundProductMaterialVO && params.row.outboundProductMaterialVO.totalCount !== 0">
                        <div slot="content">
                            <div class="bs-flex bs-flex-items-center bs-flex-space-between bs-mb-6">
                                <p>{{ $t('message.commons.total') }}：{{ params.row.outboundProductMaterialVO.totalCount }}{{ $t('message.commons.piece') }}</p>
                            </div>
                            <div v-if="params.row.outboundProductMaterialVO.received  && params.row.outboundProductMaterialVO.received.length > 0">
                                <div class="bs-flex bs-flex-items-center bs-flex-space-between">
                                    <p>
                                        <span class="bs-text-bg-orange icon"><i class="buletti-icons mi-a-Group36330"></i></span>
                                        <span class="bs-text-orange bs-padding-left-10">{{ $t('message.executionOutboundOperation.receivedSent') }}</span>
                                    </p>
                                    <p class="bs-text-orange">{{ params.row.outboundProductMaterialVO.received.length }}{{ $t('message.commons.item') }}</p>
                                </div>
                                <div class="tooltip-bottom-border received bs-mb-10" v-for="item in params.row.outboundProductMaterialVO.received">
                                    <p class="">{{ $t('message.commons.sku') }}: {{ item.sku || item.model }}</p>
                                    <p class="" v-if="item.sn">{{ $t('message.commons.sn') }}:{{ item.sn }}</p>
                                    <p class="">{{ $t('message.commons.quantity') }}: X{{ item.count }}</p>
                                </div>
                            </div>
                            <div v-if="params.row.outboundProductMaterialVO.nonReceived  && params.row.outboundProductMaterialVO.nonReceived.length > 0">
                                <div class="bs-flex bs-flex-items-center bs-flex-space-between">
                                    <p>
                                        <span class="bs-text-bg-err icon"><i class="buletti-icons mi-a-Group36331"></i></span>
                                        <span class="bs-text-err bs-padding-left-10">{{ $t('message.executionOutboundOperation.returnedNotReceived') }}</span>
                                    </p>
                                    <p class="bs-text-err">{{ params.row.outboundProductMaterialVO.nonReceived.length }}{{ $t('message.commons.item') }}</p>
                                </div>
                                <div class="tooltip-bottom-border nonReceived bs-mb-10"
                                     v-for="item in params.row.outboundProductMaterialVO.nonReceived">
                                    <p class="">{{ $t('message.commons.sku') }}: {{ item.sku || item.model }}</p>
                                    <p class="" v-if="item.sn">{{ $t('message.commons.sn') }}: {{ item.sn }}</p>
                                    <p class="">{{ $t('message.commons.quantity') }}: X{{ item.count }}</p>
                                </div>
                            </div>
                            <div v-if="params.row.outboundProductMaterialVO.shipped  && params.row.outboundProductMaterialVO.shipped.length > 0">
                                <div class="bs-flex bs-flex-items-center bs-flex-space-between">
                                    <p>
                                        <span class="bs-text-bg-green icon"><i class="buletti-icons mi-a-Group36332"></i></span>
                                        <span class="bs-text-green bs-padding-left-10">{{ $t('message.executionOutboundOperation.issued') }}</span>
                                    </p>
                                    <p class="bs-text-green">{{ params.row.outboundProductMaterialVO.shipped.length }}{{ $t('message.commons.item') }}</p>
                                </div>
                                <div class="tooltip-bottom-border shipped bs-mb-10"
                                     v-for="item in params.row.outboundProductMaterialVO.shipped">
                                    <p class="">{{ $t('message.commons.sku') }}: {{ item.sku || item.model }}</p>
                                    <p class="" v-if="item.sn">{{ $t('message.commons.sn') }}: {{ item.sn }}</p>
                                    <p class="">{{ $t('message.commons.quantity') }}: X{{ item.count }}</p>
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
                          <p class="bs-flex bs-flex-items-center bs-flex-space-between" v-if="item.sn || item.materialNo">
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
                <template slot="workOrderStatus" slot-scope="{params}">
                    <span class="table-after__status " :class="(params.row.workOrderStatus === 0 || params.row.workOrderStatus === 12) ? 'bs-text-bg-err':params.row.workOrderStatus === 2 ?'bs-text-bg-orange':'bs-text-bg-green'"></span>
                    <span> {{ params.row.workOrderStatusI18n ? params.row.workOrderStatusI18n : '--' }}</span>
                </template>
                <template slot="status" slot-scope="{params}">
                    <p>
                        <span class="table-after__status " :class="params.row.status === 0 ? 'bs-text-bg-err':params.row.status === 2 ?'bs-text-bg-orange':'bs-text-bg-green'"></span>
                        <span> {{ params.row.statusI18n }}</span>
                    </p>
                </template>
                <template slot="pushWms" slot-scope="{params}">
                     <span>
                         <span class="table-after__status " :class="params.row.pushWms === 1 || params.row.pushWms === 2 ? 'bs-text-bg-green':'bs-text-bg-err'">
                        </span>
                        <span v-if="params.row.pushWms === 0">{{ $t('message.executionOutboundOperation.notPushed') }}</span>
                        <span v-else-if="params.row.pushWms === 1">{{ $t('message.executionOutboundOperation.hasPushed') }}</span>
                        <span v-else-if="params.row.pushWms === 2">{{ $t('message.executionOutboundOperation.partialPush') }}</span>
                     </span>
                </template>
                <template slot="table-groups" slot-scope="{params}">
                    <group-btn :is-show="false" :type="'className'" :class-name="'el-button--text'" :content="$t('message.buttons.view')"
                               @click.native="handleView(params.row)" v-if="$permissionList[permission.outboundWorkOrderPool.view]"></group-btn>
                    <group-btn :is-show="false" :type="'className'" :class-name="'el-button--text'" v-if="$permissionList[permission.outboundWorkOrderPool.view]"
                               :content="$t('message.executionOutboundOperation.changeLogistics')" @click.native="handleView(params.row)"></group-btn>

                </template>
            </table-component>
        </div>
    </div>
</template>
<script>
    module.exports = {
      data() {
        return {
          permission: {
            ...this.$actionsList.outBound
          },
          siteId: win.StorageUtil.sessionStorageGet('resource'),
          userInfo: this.$api.getSession('userInfo'),
          listCacheTab: '1',
          form: {
            workOrderNo: '',
            shipper: '',
            siteId: this.siteId,
            status: '',
            handler: '',
            customerPhone: '',
            customerName: '',
            customerEmail: '',
            customerAddress: '',
            outWarehouseStatus: '',
            type:1,
            model: '',
            sn: '',
            rmaNo: '',
            sku: '',
            logisticsCompany:'',
            shippingOrderNo:'',
          },
          logisticsCompanyList:[],
          afterSaleReasonList:[],
          purchaseChannelsList:[],
          option: {
            labelPosition: 'left',
            width: '0px',
            inline: true,
            refactor: true
          },
          rules: {},
          tableOption: {
            height: 'auto',
            pageNo: 1,
            pageSize: window.framework.layout.PAGE_SIZE,
            total: 0,
            selectable: false,
            refactor: true,
            tableKey:1,
          },
          dataWms: '',
          currentWms: {},
          tableData: [],
          currentData: null,
          dataName: null,
          postSaleWorkOrderStatusList: [],
          outWarehouseStatusList: [],
          userList: [],
          isBtn: false,
          siteInfo: {},
        }
      },
      components: {
        "form-component": httpVueLoader("/views/components/formComponent.vue"),
        "group-btn": httpVueLoader("/views/components/groupBtn.vue"),
        "table-component": httpVueLoader("/views/components/tableComponent.vue"),
      },
      computed: {
        has_view() {
          return this.permission.outboundWorkOrderPool.view;
        },

        tableDataHeader() {
          let obj = [
            {
              type: 'operate',
              width: '180',
              align: 'left',
              prop: '',
              fixed: 'right',
              isHeader: false,
              hide: false,
              label: this.$t('message.commons.operation'),
            },
            {
              type: 'index',
              label: '',
              align: 'left',
            },
            {
              type: 'slot',
              width: '150',
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
                return row.rmaNo
              },
            },
            {
              type: 1,
              width: '140',
              align: 'left',
              prop: 'afterSaleTypeName',
              label: this.$t('message.commons.afterSalesProcessing'),
              hide: false,
              isHeader: false,
              fun: (name, row) => {
                return row.afterSaleTypeName
              },
            }, {
              type: 1,
              width: '150',
              align: 'left',
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
              width: '220',
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
              width: '180',
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
              width: '220',
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
              },
              form: {
                formType: 'input',
                name: this.$t('message.commons.sn'),
                params: [
                  {
                    field: "sn",
                    fieldValue: "",
                    placeholder: this.$t('message.commons.enterConfirmSearch'),
                  },
                ],
              },
            },
            {
              type: '1',
              width: '120',
              align: 'left',
              prop: 'logisticsCompany',
              hide: false,
              label: this.$t('message.commons.logisticsProvider'),
              isHeader: false,
              form: {
                formType: 'select',
                selectType: 'select',
                name: this.$t('message.commons.logisticsProvider'),
                params: [
                  {
                    field: "logisticsCompany",
                    fieldValue: "",
                    placeholder: this.$t('message.commons.enterConfirmSearch'),
                  },
                ],
                options: this.logisticsCompanyList
              },
              fun: (name, row) => {
                return row.logisticsCompany ? row.logisticsCompany : '--'
              },
            },
            {
              type: 'slot',
              width: '120',
              align: 'left',
              prop: 'shippingOrderNo',
              hide: false,
              label: this.$t('message.warehouseReceiptDeliveryManage.transportNoteNumber'),
              isHeader: false,
              form: {
                formType: 'input',
                name: this.$t('message.warehouseReceiptDeliveryManage.transportNoteNumber'),
                params: [
                  {
                    field: "shippingOrderNo",
                    fieldValue: "",
                    placeholder: this.$t('message.commons.enterConfirmSearch'),
                  },
                ],
              },
              fun: (name, row) => {
                return '--'
              },
            },
            {
              type: 1,
              width: '60',
              align: 'left',
              label: this.$t('message.commons.quantity'),
              prop: 'quantity',
              isHeader: false,
              hide: false,
              fun: (name, row) => {
                return row.quantity ? row.quantity : '--';
              },
            },
            {
              type: 1,
              width: '120',
              align: 'left',
              prop: 'handler',
              hide: false,
              label: this.$t('message.executionOutboundOperation.customerServicePersonnel'),
              isHeader: false,
              form: {
                formType: 'select',
                selectType: 'selectUserId',
                name: this.$t('message.executionOutboundOperation.customerServicePersonnel'),
                params: [
                  {
                    field: "handler",
                    fieldValue: "",
                    placeholder: this.$t('message.commons.enterConfirmSearch'),
                  },
                ],
                options: this.userList
              },
              fun: (name, row) => {
                return row.handler ? row.handler : '--';
              },
            },
            {
              type: 1,
              width: 'auto',
              minWidth: '120',
              align: 'left',
              prop: 'shipper',
              label: this.$t('message.executionOutboundOperation.shipper'),
              hide: false,
              isHeader: false,
              fun: (name, row) => {
                return row.shipper ? row.shipper : '--';
              },
            },
          ];
          if (this.siteInfo.wmsEnable) {
            obj.push({
              type: 'slot',
              width: '120',
              minWidth:'120',
              align: 'left',
              fixed: 'right',
              prop: 'pushWms',
              label: this.$t('message.executionOutboundOperation.isPushWMS'),
              hide: false,
              isHeader: false,
              fun: (name, row) => {
                return row.pushWms ? row.pushWms : '--';
              },
            });
          }
          return obj;
        },
        formObj() {
          return [
            {
              type: 'input',
              label: '',
              title: this.$t('message.customerWorkOrderManage.orderSearch'),
              main: true,
              name: 'rmaNo',
              disabled: false,
              placeholder: this.$t('message.customerWorkOrderManage.rmaNo'),
              icon: 'el-icon-search',
              className: 'after-sale__form-item',
            },
            {
              type: 'input',
              label: '',
              name: 'sn',
              main: true,
              disabled: false,
              placeholder: 'SN',
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
              name: 'handler',
              disabled: false,
              placeholder: this.$t('message.executionOutboundOperation.customerPersonnel'),
              list: this.userList,
              className: 'after-sale__form-item',
            },
            {
              type: 'slot',
              label: '',
              name: 'logisticsCompany',
              disabled: false,
              placeholder: this.$t('message.commons.logisticsProvider'),
              icon: 'el-icon-search',
              className: 'after-sale__form-item'
            },
            {
              type: 'input',
              label: '',
              name: 'shippingOrderNo',
              disabled: false,
              placeholder: this.$t('message.warehouseReceiptDeliveryManage.transportNoteNumber'),
              icon: 'el-icon-search',
              className: 'after-sale__form-item'
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
      },
      mounted() {
        const info = win.StorageUtil.sessionStorageGet('msgInfo') ? win.StorageUtil.sessionStorageGet("currentPage") : null;
        //只有本列表自己写入的缓存才恢复，否则其他出库列表的查询条件会串到这里
        if (info && info.tab === this.listCacheTab) {
          //如果有就读取缓存里面的数据
          if (info.pageNo) {
            this.tableOption.pageNo = Number(info.pageNo);
          }
          if (info.form) {
            this.form = info.form;
          }
        } else {
          this.tableOption.pageNo = 1;
          //这个主要是从其他页面第一次进入列表页，清掉缓存里面的数据
          win.StorageUtil.sessionStorageDelete("currentPage");
        }
        if (this.$route.query && this.$route.query.rmaNo) {
          this.form.rmaNo = this.$route.query.rmaNo;
        }
        this.siteInfo = win.StorageUtil.sessionStorageGet('siteInfo');
        this.getEnumsAll();
        this.getSiteUserAll();
        this.getGroupDict();
        this.$nextTick(()=>{
          this.getList();
        })

      },
      destroyed() {
        win.StorageUtil.sessionStorageDelete("msgInfo");
      },
      methods: {
        getGroupDict() {
          this.$api.getBaseInfoAll(this.siteId).then(res => {
            if (res.msgCode === 0 && res.data) {
              let groupList = res.data.groupList;
              this.afterSaleReasonList = groupList.afRe;
              this.purchaseChannelsList = groupList.puCh;
              this.logisticsCompanyList = groupList.lgPr;
            }
          })
        },
        /**
         * 状态枚举
         * @returns {Promise<void>}
         */
         getEnumsAll() {
           this.$api.getEnumsAll().then(res => {
            if (res.msgCode === 0 && res.data) {
              this.postSaleWorkOrderStatusList = res.data.postSaleWorkOrderStatus;
              this.outWarehouseStatusList = res.data.outWarehouseStatus;
            }
          })
        },
        getList() {
          let params = Object.assign({}, this.form, {
            pageNo: this.tableOption.pageNo,
            pageSize: this.tableOption.pageSize,
          });
          // this.tableOption.tableKey++;
          this.getLoading();
          this.$api.getShippingList(params).then(res => {
            this.isLoading.close();
            if (res.msgCode === 0 && res.data) {
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
        /**
         * 清空数据
         */
        clearForm() {
          this.form = {
            workOrderNo: '',
            shipper: '',
            siteId: this.siteId,
            status: '',
            handler: '',
            customerPhone: '',
            customerName: '',
            customerEmail: '',
            customerAddress: '',
            model: '',
            outWarehouseStatus: '',
            type:1,
            sku: '',
            rmaNo: '',
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
        handleCompany(item) {
          this.getCopy(item.shippingOrderNo);
        },
        handleView(row) {
          win.StorageUtil.sessionStorageSet('currentPage', {
            tab: this.listCacheTab,
            pageNo: this.tableOption.pageNo,
            form: this.form
          });
          this.$router.push({
            path: '/afterSaleClient/inventory/client/outBoundRefactor/route/v2/view',
            query: {
              routePath:'/afterSaleClient/inventory/client/completeOutBound/route/v2/workOutboundList',
              id: row.workOrderId,
              afterSaleType: row.afterSaleType,
              orderType: 1,
              outStatus: row.status,
              rmaNo: row.rmaNo,
              active: '1',
            }
          });
        },
        handleShipping(row) {
          this.outOperationPickup(row);
        },
        outOperationChange(row) {
          this.$api.outOperationChange(row.workOrderId).then(res => {
            this.$refs.confirmationComponent.dialogClose();
            if (res.code === 0) {
              this.getList();
            }
          })
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
        pickupFun(row, type) {
          this.getLoading();
          this.$api.outOperationPickup(row.workOrderId, row.type).then(res => {
            if (type) this.$refs.confirmation.dialogClose();
            this.isLoading.close();
            if (res.msgCode === 0) {
              if (!type) {
                this.goReceivingSendingPage(row);
              } else {
                this.getList();
              }
            }
          })
        },
        goReceivingSendingPage(row) {
          win.StorageUtil.sessionStorageSet('currentPage', this.tableOption.pageNo);
          let id = row.workOrderId;
          if (row.productType == 1) {
            id = row.accessoryWorkOrderId;
          }
          this.$router.push({
            path: "/afterSaleClient/inventory/client/outBoundRefactor/route/v2/receivingSending",
            query: {
              id,
              type: row.type,
              afterSaleType: row.afterSaleType,
              realRightType: row.realRightType,
              workOrderStatus: row.workOrderStatus,
              isWorkOrderType: true,
              productType: row.productType,
              rmaNo: row.rmaNo,
            }
          });
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

        sizeChange(size) {
          this.tableOption.pageSize = size;
          this.tableOption.pageNo = 1;
          this.getList();
        },
        pageChange(page) {
          this.tableOption.pageNo = page;
          this.getList();
        },
        handleSn(item) {
          this.getCopy(item.sn || item.materialNo);
        },
        handleQuery() {
          this.tableOption.pageNo = 1;
          this.getList();
          this.$refs.tableComponentRef.getTag(this.form);
        },
        onAfterSaleSource(e, name) {
          if (e.target.value) {
            this.form[name] = e.target.value;
          }
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
          this.$emit('other-demand', true);
        },
        handleCloseWorkorder(item) {
          this.currentData = item;
          this.dataName = this.$t('message.refurbishmentInboundOutbound.returnOrder');
          this.$refs.confirmationComponent.visible();
        },
        deleteFun() {
          if (this.currentData.isFlag) {
            this.outOperationChange(this.currentData);
          }
          else {
            this.getLoading();
            this.$api.chargeBack(this.currentData.workOrderId, this.currentData.type).then(res => {
              this.$refs.confirmationComponent.dialogClose();
              this.isLoading.close();
              if (res.msgCode === 0) {
                this.getList();
              }
            })
          }
        },
        cancelComponent() {
          this.$refs.confirmationComponent.dialogClose();
        },
        cancelWMS() {
          this.$refs.confirmation.dialogClose();
        },
      },
    }
</script>

