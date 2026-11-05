<template>
    <div class="after-sale__view after-sale__refurbishment bs-flex bs-flex-space-between ">
        <div class="after-sale__view-left">
            <el-card class="box-card">
                <div class="after-sale__receipt-sku bs-flex bs-flex-items-center"
                     v-if="refurbishInfo && refurbishInfo.length > 0">
                    <div class="after-sale__receipt-model" v-for="(item,i) in refurbishInfo" :key="i"
                         :class="i === 0 ? 'active' : ''" @click="handleTab(item,i)">
                        <div class="bs-flex bs-flex-items-center bs-flex-start">
                            <div class=" bs-padding-right-10">
                                <span class="after-sale__elliptic" @click.prevent="onHandleEdit(item)">
                                  <i class="el-icon-edit"></i>
                                </span>
                              <!--                              v-if="isEdit"-->
                              <!--                              <span class="after-sale__elliptic" v-else>{{ i + 1}}</span>-->
                            </div>
                            <div>
                                <p class="after-sale__shipping-name bs-mb-6">{{ item.logisticsCompany }}</p>
                                <div class="bs-flex bs-flex-items-center bs-flex-space-between"
                                     v-if="item.shippingOrderNo">
                                    <p class="bs-padding-right-10 after-sale__shippingOrderNo bs-over-text-break-word">
                                        {{ item.shippingOrderNo }}</p>
                                    <p @click.prevent="handleCopy(item)"><i class="el-icon-document-copy"></i></p>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
                <div v-for="(item,i) in refurbishInfo" :key="i" :class="i === 0 ? 'active' : ''"
                     v-if="refurbishInfo.length > 0"
                     class="refurbishment-shipment__main bs-mt-10">
                  <div class="after-sale__receipt bs-flex bs-flex-space-between bs-mb-10 bs-flex-wrap bs-gap-10">
                        <div class="bs-flex bs-gap-6 bs-flex-items-center">
                            <p class="bs-text-font-16">{{ $t('message.executionOutboundOperation.outboundNumber') }}：</p>
                            <p class="bs-flex-1 bs-text-font-16 bs-word-break">{{ item.shipmentNo }}</p>
                        </div>
                        <div class="bs-flex bs-gap-6 bs-flex-items-center">
                            <p class="bs-text-font-16">{{ $t('message.customerWorkOrderManage.operationTime') }}：</p>
                            <p class="bs-flex-1 bs-text-font-16 bs-word-break">{{ item.handlerTime ? item.handlerTime : '--' }}</p>
                        </div>
                        <div class="bs-flex bs-gap-6 bs-flex-items-center">
                            <p class="bs-text-font-16">{{ $t('message.commons.outboundTime') }}：</p>
                            <p class="bs-flex-1 bs-text-font-16 bs-word-break">{{ item.outTime ? item.outTime : '--' }}</p>
                        </div>
                        <div class="bs-flex bs-gap-6 bs-flex-items-center" v-if="logisticsStatus && logisticsStatus.length">
                             <p class="bs-text-font-16">{{ $t('message.commons.logisticsStatus') }}：</p>
                              <p class="bs-flex-1 bs-text-font-16 bs-word-break">{{ logisticsStatus }}</p>
                        </div>
                    <!--                    <div class="bs-flex bs-gap-6 bs-flex-items-center" v-if="expressInfo.eventInfoList && expressInfo.eventInfoList.length > 0">-->
                    <!--                         <p class="bs-text-font-16">{{ $t('message.commons.logisticsStatus') }}：</p>-->
                    <!--                         <p class="bs-flex-1 bs-text-font-16 bs-word-break">{{expressInfo.eventInfoList[expressInfo.eventInfoList.length - 1].stage}}</p>-->
                    <!--                    </div>-->
                    </div>
                  <div class="after-sale__receipt bs-flex bs-flex-space-between bs-mb-10 bs-flex-wrap bs-gap-10">
                        <div class="bs-flex bs-gap-6 bs-flex-items-center" v-if="item.oaApprovalNo">
                            <p class="bs-text-font-16">{{ $t('message.executionOutboundOperation.oaApprovalNo') }}：</p>
                            <p class="bs-flex-1 bs-text-font-16 bs-word-break">{{ item.oaApprovalNo || '--' }}</p>
                        </div>
                        <div class="bs-flex bs-gap-6 bs-flex-items-center" v-if="item.nsCustomer">
                            <p class="bs-text-font-16">{{ $t('message.executionOutboundOperation.applicantCustomerName') }}：</p>
                            <p class="bs-flex-1 bs-text-font-16 bs-word-break">{{ item.nsCustomer ? item.nsCustomer : '--' }}</p>
                        </div>
                       <div class="bs-flex bs-gap-6 bs-flex-items-center">
                         <p class="bs-text-font-16"></p>
                         <p class="bs-flex-1 bs-text-font-16 bs-word-break"></p>
                       </div>
                    </div>
                  <div class="after-sale__title">
                        <span>{{ $t('message.executionOutboundOperation.outboundProductList') }}</span>
                    </div>
                    <div class="after-sale__receipt-list-table"
                         v-if="item.productMaterialInfos && item.productMaterialInfos.length > 0">
                        <table-component v-if="item.productMaterialInfos && item.productMaterialInfos.length > 0"
                                         :is-show="false"
                                         :option="tableOption"
                                         :table-data="item.productMaterialInfos"
                                         :table-data-header="tableDataHeader">
                        </table-component>
                    </div>
                    <div v-if="item.sopItems && item.sopItems.length > 0">
                        <div v-for="(ele,index) in JSON.parse(item.sopItems)" :key="index">
                            <!-- <div class="after-sale__title bs-mt-20">
                                 <span>{{$t('message.executionOutboundOperation.outboundItems')}} {{ index + 1}}</span>
                             </div>-->
                            <div class="after-sale__receipt-card" v-if="ele.sopNodeItemVOList.length > 0">
                                <h3 class="bs-mb-10 bs-text-font-16">{{ index + 1 }}.{{ ele.title }}</h3>
                                <template v-for="(sub) in ele.sopNodeItemVOList">
                                    <div class="bs-flex bs-gap-6 bs-flex-items-center bs-mt-10"
                                         v-if="sub.value && sub.type === 'selectMulti' && sub.value && sub.value.length > 0 ">
                                        <p class="after-sale__view-name bs-text-font-16">{{ sub.name }}: </p>
                                        <p class="bs-flex-1 bs-text-font-16 bs-word-break"
                                           v-if="sub.value && sub.value != [] && sub.value.length > 0">
                                            {{ sub.value.join(',') }}</p>
                                    </div>
                                    <p v-else-if="sub.value && sub.type === 'selectMulti' && sub.value && sub.value.length === 0"
                                       class="bs-mt-10">
                                        <span class="after-sale__view-name bs-text-font-16">{{ sub.name }}: </span>
                                        <span>--</span>
                                    </p>
                                    <template v-else-if="sub.type === 'file' && sub.value && sub.value.length > 0">
                                        <p class="bs-mt-10">
                                            <span class="after-sale__view-name bs-text-font-16">{{ sub.name }}：</span>
                                        </p>
                                        <div class="bs-flex bs-flex-items-center after-sale__image-list bs-mt-10"
                                             v-if="sub.value && sub.value != [] && sub.value.length > 0">
                                            <div v-for="(m) in sub.value" :class="m.suffix">
                                                <el-tooltip class="item" effect="dark" :content="m.name"
                                                            placement="top">
                                                    <div>
                                                        <a :href="m.url || m.fileUrl" target="_blank" :class="m.suffix"
                                                           v-if="getSuffix(m)">
                                                          <preview-image :image-url="getUrl(m)">
                                                            <img :src="getUrl(m)" :alt="m.name" :class="m.suffix">
                                                          </preview-image>
                                                        </a>
                                                        <template v-else>
                                                          <preview-image :image-url="getUrl(m)">
                                                            <img :src="getUrl(m)" :alt="m.name" :class="m.suffix"
                                                                 @click="handleLook(m)">
                                                          </preview-image>
                                                        </template>
                                                    </div>
                                                </el-tooltip>
                                            </div>
                                        </div>
                                    </template>
                                    <template v-else-if="sub.type === 'file' && sub.value && sub.value.length === 0">
                                        <p class="bs-mt-10">
                                            <span class="after-sale__view-name bs-text-font-16">{{ sub.name }}：</span>
                                            <span>--</span>
                                        </p>
                                    </template>
                                    <div class="bs-flex bs-gap-6 bs-flex-items-center bs-mt-10" v-else>
                                        <p class="after-sale__view-name bs-text-font-16">{{ sub.name }}: </p>
                                        <p class="bs-flex-1 bs-text-font-16 bs-word-break">{{
                                            sub.value ? sub.value :
                                                '--'
                                          }}</p>
                                    </div>
                                </template>
                            </div>
                        </div>
                    </div>
                </div>
                <div class="bs-mb-20 bs-mt-20 after-sale__empty" v-if="(refurbishInfo.length <= 0) || !refurbishInfo">
                    <div class="after-sale__empty-box">
                        <img src="/assets/image/empty.png" alt="">
                    </div>
                    <p class="after-sale__no-text">{{ $t('message.commons.noDataAvailable') }}</p>
                </div>
                <div style="height: 10px"></div>
                <div v-if="logList.length">
                  <div class="after-sale__title">
                          <span>{{ $t('message.executionOutboundOperation.operationLog') }}</span>
                  </div>
                  <div v-for="(item,i) in logList">
                    <div class="after-sale__receipt bs-flex bs-flex-space-between bs-mb-10 bs-flex-wrap bs-gap-10" :class="{'log-line':i !== logList.length - 1}">
                          <div class="bs-flex bs-gap-6 bs-flex-items-center" style="width: 25%">
                              <p class="bs-text-font-16">{{ $t('message.commons.operator') }}：</p>
                              <p class="bs-flex-1 bs-text-font-16 bs-word-break" style="text-align: left">{{ item.operateUser || '--' }}</p>
                          </div>
                          <div class="bs-flex bs-gap-6 bs-flex-items-center" style="width: 25%">
                              <p class="bs-text-font-16">{{ $t('message.executionOutboundOperation.date') }}：</p>
                              <p class="bs-flex-1 bs-text-font-16 bs-word-break" style="text-align: left">{{ item.operateTime ? item.operateTime : '--' }}</p>
                          </div>
                         <div class="bs-flex bs-gap-6 bs-flex-items-center" style="width: 25%">
                           <p class="bs-text-font-16">{{ $t('message.executionOutboundOperation.shipmentCarrier') }}：</p>
                           <p class="bs-flex-1 bs-text-font-16 bs-word-break" style="text-align: left">{{ item.logisticsCompany || '--' }}</p>
                         </div>
                        <div class="bs-flex bs-gap-6 bs-flex-items-center" style="width: 25%">
                           <p class="bs-text-font-16">{{ $t('message.materialUsageManage.shipmentNumber') }}：</p>
                           <p class="bs-flex-1 bs-text-font-16 bs-word-break" style="text-align: left">{{ item.shippingNo || '--' }}</p>
                         </div>
                      </div>
                  </div>
                </div>
                <div v-if="logisticsLog.length">
                <div class="after-sale__title">
                        <span>{{ $t('message.executionOutboundOperation.updateLogisticsLog') }}</span>
                </div>
                <div v-for="(item,i) in logisticsLog">
                  <div class="after-sale__receipt bs-flex bs-flex-space-between bs-mb-10 bs-flex-wrap bs-gap-10" :class="{'log-line':i !== logisticsLog.length - 1}">
                        <div class="bs-flex bs-gap-6 bs-flex-items-center" style="width: 25%">
                            <p class="bs-text-font-16">{{ $t('message.commons.operator') }}：</p>
                            <p class="bs-flex-1 bs-text-font-16 bs-word-break" style="text-align: left">{{ item.operateUser || '--' }}</p>
                        </div>
                        <div class="bs-flex bs-gap-6 bs-flex-items-center" style="width: 25%">
                            <p class="bs-text-font-16">{{ $t('message.executionOutboundOperation.date') }}：</p>
                            <p class="bs-flex-1 bs-text-font-16 bs-word-break" style="text-align: left">{{ item.operateTime ? item.operateTime : '--' }}</p>
                        </div>
                       <div class="bs-flex bs-gap-6 bs-flex-items-center" style="width: 25%">
                         <p class="bs-text-font-16">{{ $t('message.commons.remark') }}：</p>
                         <p class="bs-flex-1 bs-text-font-16 bs-word-break" style="text-align: left">{{ item.remark || '--' }}</p>
                       </div>
                    </div>
                </div>
              </div>
            </el-card>
        </div>
        <div class="after-sale__view-right">
            <user-info :detail-info="customerInfo" :is-show="isShow" v-if="customerInfo"></user-info>
            <el-card class="box-card" v-if="orderProductMaterialVOS.length > 0">
                <div class="after-sale__title">
                    <span>{{ $t('message.customerWorkOrderManage.relatedProducts') }}</span>
                </div>
                <div v-for="(item,index) in orderProductMaterialVOS" v-if="orderProductMaterialVOS.length > 0" :key="index">
                    <p v-if="item.sku" class="bs-mb-10">
                        <span class="after-sale__user-span">{{ $t('message.commons.refSku') }}:</span>
                        <span class="bs-text-font-16">{{ item.sku }}</span>
                    </p>
                    <p v-if="item.sn" class="bs-mb-10">
                        <span class="after-sale__user-span">{{ $t('message.commons.refSn') }}:</span>
                        <span class="bs-text-font-16">{{ item.sn }}</span>
                    </p>
                </div>
            </el-card>
          <!--            <el-card class="box-card" v-if="expressInfo && expressInfo.eventInfos && expressInfo.eventInfos.length > 0">-->
          <!--                <div class="after-sale__title">-->
          <!--                    <span>{{ $t('message.customerWorkOrderManage.deliveryDetails') }}</span>-->
          <!--                </div>-->
          <!--                <div class="outbound-records__express-delivery" v-if="expressInfo.eventInfos && expressInfo.eventInfos.length > 0">-->
          <!--                    <el-timeline :reverse="true">-->
          <!--                        <el-timeline-item v-for="(item,index) in expressInfo.eventInfos"-->
          <!--                                          :key="index"-->
          <!--                                          :color="index === expressInfo.eventInfos.length -1 ?'#0bbd87':''"-->
          <!--                                          :timestamp="item.defaultTime ">-->
          <!--                            {{item.description}}-->
          <!--                        </el-timeline-item>-->
          <!--                    </el-timeline>-->
          <!--                </div>-->
          <!--            </el-card>-->
            <template v-if="isShowLogisticsInfo">
                <logistics-info :params="logisticsInfoParams" @logistics-status="getLogisticsStatus" :key="logisticsInfoParams.trackingNumber"></logistics-info>
            </template>
            <div class="bs-mb-20 bs-mt-20 after-sale__empty" v-if="!customerInfo && !expressInfo">
                <div class="after-sale__empty-box">
                    <img src="/assets/image/empty.png" alt="">
                </div>
                <p class="after-sale__no-text">{{ $t('message.commons.noDataAvailable') }}</p>
            </div>
        </div>
        <player-look ref="playerLook"></player-look>
       <change-logistics ref="changeLogistics" @success="getPageInfo"></change-logistics>
    </div>
</template>
<script>
    module.exports = {
      name: 'outboundRecords',
      components: {
        "table-component": httpVueLoader("/views/components/tableComponent.vue"),
        "user-info": httpVueLoader("/views/components/userInfo.vue"),
        "logistics-info": httpVueLoader("/views/components/logisticsInfo.vue"),
        "player-look": httpVueLoader("/views/components/player-look.vue"),
        "change-logistics": httpVueLoader("/views/components/changeLogistics.vue"),
      },
      data() {
        return {
          id: null,
          isEdit: false,
          tableData: [],
          tableOption: {
            height: 320,
            pageNo: 1,
            pageSize: 5,
            total: 0,
            selectable: false
          },
          receiptList: [],
          logList: [],
          logisticsLog: [],
          refurbishInfo: [],
          listKeys: [],
          isShow: false,
          customerInfo: {},
          orderType: '',
          expressInfo: {},
          orderProductMaterialVOS: [],
          logisticsInfoList: [],
          isShowLogisticsInfo: false,
          logisticsInfoParams: {
            bizId: '', // rmaNo
            courierCode: '', // 物流商code
            trackingNumber: '' // 物流单号
          },
          logisticsStatus: '',
          rmaNo: ''
        }
      },
      props: {
        detailInfo: Object,
      },
      computed: {
        tableDataHeader() {
          return [
            {
              type: 'index',
              align: 'left',
            },
            {
              label: this.$t('message.commons.sku'),
              prop: 'sku',
              width: 'auto',
              align: 'left',
              hide: false,
              isHeader: false,
              fun: (name, row) => {
                return row.sku ? row.sku : '--'
              },
            },
            {
              type: 1,
              label: this.$t('message.commons.sn'),
              prop: 'sn',
              width: 'auto',
              align: 'left',
              hide: false,
              isHeader: false,
              fun: (name, row) => {
                return row.sn ? row.sn : '--'
              },
            },
            {
              label: this.$t('message.commons.quantity'),
              prop: 'count',
              width: '100',
              align: 'left',
              hide: false,
              isHeader: false,
              fun: (name, row) => {
                return row.count
              },
            },
          ];
        }
      },
      watch: {
        // detailInfo 为了避免某些页面进入(如出库作业执行的详情页面)获取不到数据 要监听一下 获取到新数据了就重新请求
        detailInfo(val) {
          if (val) {
            const {productType = ''} = val;
            this.getPageInfo(productType);
          }
        }
      },
      mounted() {
        this.id = this.$route.query.id;
        this.isEdit = this.$route.query.isEdit == 1;
        // orderType 1 工单类型  2 其他需求工单池 3 二手品销售出库
        this.orderType = this.$route.query.orderType || 1;
        this.rmaNo = this.$route.query.rmaNo;
        this.getPageInfo();
      },
      methods: {
        //快递信息
        getExpressInfo(orderNumber) {
          this.$api.getJsonLogisticsInfo(orderNumber).then(res => {
            if (res.code === 0 && res.data && res.data.length > 0) {
              this.expressInfo = res.data[0];
            }
          })
        },
        //翻新发货
        refurbishByOrderDetailInfo() {
          this.$api.outOperationShipmentDetail(this.id, this.orderType).then(res => {
            if (res.msgCode === 0) {
              this.refurbishInfo = res.data.shipmentInfoList;
              this.logList = res.data.logList.filter(item => item.event !== 2) || [];
              this.logisticsLog = res.data.logList.filter(item => item.event === 2) || [];
              this.customerInfo = res.data.customerInfo;
              if (res.data.orderProductMaterialVOS && res.data.orderProductMaterialVOS.length > 0) this.orderProductMaterialVOS = res.data.orderProductMaterialVOS;
              if (Array.isArray(this.refurbishInfo) && this.refurbishInfo.length) {
                this.handleLogisticsInfo(this.refurbishInfo[0]);
              }
              // if(this.refurbishInfo.length > 0 && this.refurbishInfo[0].shippingOrderNo) {
              //     this.getExpressInfo(this.refurbishInfo[0].shippingOrderNo)
              // }
            }
          })
        },
        // 配件出库记录
        accessoriesDelivery() {
          this.$api.accessoryAccessoriesDelivery(this.id).then(res => {
            if (res.msgCode === 0) {
              const result = res.data;
              const logList = result && result.length ? (result[0].logList || []) : [];
              this.logList = logList.filter(item => item.event !== 2);
              this.logisticsLog = logList.filter(item => item.event === 2);
              if (result && Array.isArray(result) && result.length) {
                for (let item of result) {
                  item.shipmentNo = item.id || '--';
                  item.handlerTime = item.receiptTime || '--';
                  item.outTime = item.receiptTime || '--';
                  if (item.productList && Array.isArray(item.productList)) {
                    for (let childItem of item.productList) {
                      childItem.count = childItem.quantity || '--';
                    }
                    item.productMaterialInfos = [...item.productList];
                  }
                }

                this.refurbishInfo = res.data;
                this.handleLogisticsInfo(this.refurbishInfo[0]);
              }
            }
          })
        },
        formatVal(item) {
          let arr = [];
          if (item.data && item.data.length > 0 && item.value && item.value.length > 0) {
            item.value.forEach(ele => {
              arr.push(item.data[ele]);
            })
          }
          if (arr.length > 0) {
            return arr.toString();
          }
        },
        /**
         * 文件展示图片
         * @param item
         * @returns {string|*}
         */
        getUrl(item) {
          return win.utilsTool.getFileUrl(item);
        },
        /**
         * 文件类型
         * @param item
         * @returns {boolean}
         */
        getSuffix(item) {
          const arr = win.utilsTool.formatFile();
          if (arr.indexOf(item.suffix) >= 0) {
            return true;
          } else {
            return false;
          }
        },
        handleLook(item) {
          this.$refs.playerLook.visible(item);
        },
        handleCopy(item) {
          this.getCopy(item.shippingOrderNo);
        },
        onHandleEdit(item) {
          this.$refs.changeLogistics.visible(item, this.id, this.orderType);
        },
        /**
         * 选中样式
         * @param item
         * @param i
         */
        handleTab(item, i) {
          $('.after-sale__receipt-sku .after-sale__receipt-model').eq(i).addClass('active').siblings().removeClass('active');
          $('.refurbishment-shipment__main').eq(i).addClass('active').siblings().removeClass('active');
          this.handleLogisticsInfo(item);
        },
        handleLogisticsInfo(item) {
          if (!item) {
            return this.isShowLogisticsInfo = false;
          }
          const logisticsCode = item.logisticsCode;
          const shippingOrderNo = item.shippingOrderNo;
          if (!logisticsCode) {
            return this.isShowLogisticsInfo = false;
          }
          if (!shippingOrderNo) {
            return this.isShowLogisticsInfo = false;
          }
          this.logisticsInfoParams.bizId = this.rmaNo;
          this.logisticsInfoParams.courierCode = logisticsCode;
          this.logisticsInfoParams.trackingNumber = shippingOrderNo;
          this.isShowLogisticsInfo = true;
        },
        getLogisticsStatus(logisticsStatus) {
          this.logisticsStatus = logisticsStatus;
        },
        getPageInfo() {
          const {productType = ''} = this.detailInfo;
          // 售后工单类型出库 需要区分配件还是产品
          if (this.orderType == 1 && !productType) {
            return;
          }
          if (this.id) {
            const notDistinguishingProductTypeOrderTypeEnum = ['2', '3'];
            if (notDistinguishingProductTypeOrderTypeEnum.includes(this.orderType)) {
              return this.refurbishByOrderDetailInfo();
            }
            // 配件
            if (productType == 1) {
              const {
                customerName = '--',
                customerEmail = '--',
                customerPhone = '--',
                returnAddress = '--',
                shippingAddress = '--'
              } = this.detailInfo || {};
              this.customerInfo = {
                customerName,
                customerEmail,
                customerPhone,
                returnAddress,
                shippingAddress
              }
              this.accessoriesDelivery();
            }
            // 产品
            if (productType == 2) {
              this.refurbishByOrderDetailInfo();
            }
          }
        }
      },
    }
</script>
<style scoped>
.log-line {
  padding-bottom: 10px;
  border-bottom: 1px solid var(--button-default-bg-color);
}
</style>
