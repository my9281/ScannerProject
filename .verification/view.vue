<template>
    <div>
        <page-header :title-name="title" @back="back">
            <div slot="header-group-btn">
                <group-btn :content="$t('message.executionOutboundOperation.synchronizeShippingResults')" :is-show="false" :type="'save'"
                           @click.native="handleSync"  v-if="detailInfo.afterSaleType === 10"></group-btn>
              <template v-if="detailInfo.isOutBound !== '3' || (detailInfo.isOutBound === '3' && detailInfo.status !== 2)">
                <group-btn :is-show="false" :type="'save'" :content="$t('i18n.route.action.bluas.afterSaleClient.outBoundMgt.receivingSending')"
                           v-if="$permissionList[permission.outboundWorkOrderPool.receivingSending] && !siteInfo.wmsEnable && [0,1,5].includes(detailInfo.status)"
                           @click.native="onSubmit"></group-btn>
                <group-btn :is-show="false" :type="'className'" :content="$t('i18n.route.action.bluas.afterSaleClient.outBoundMgt.del')"
                           :class-name="'text-danger'" v-if="$permissionList[permission.outboundWorkOrderPool.del] && userInfo.uid === detailInfo.shipperUid && outStatus == 1"
                           @click.native="onClose"></group-btn>
              </template>
            </div>
        </page-header>
        <div class="bs-mt-40" style="margin-top: 50px;">
            <el-tabs v-model="activeName" type="card">
<!--              出库工单-->
                <el-tab-pane :label="$t('message.executionOutboundOperation.outboundWorkOrder')" name="1">
                    <customer-service-view
                            :is-purchase="type == 2 ? true : false"
                            :detail-info="detailInfo"
                            :express-info="expressInfo"
                            :product-list="productMaterialDetails"
                            :is-link="false"
                    ></customer-service-view>
                </el-tab-pane>
<!--              仓库收件-->
                <el-tab-pane :label="$t('message.customerWorkOrderManage.warehouseReceipt')" name="2" v-if="type !== 2">
                    <warehouse-receipt v-if="activeName === '2'" :detail-info="detailInfo"></warehouse-receipt>
                </el-tab-pane>
<!--              入库初检-->
                <el-tab-pane :label="$t('message.executionOutboundOperation.initialInspectionReceipt')" name="3" v-if="status !== 4 && status !== 1 && type !==2">
                    <inspection-receipt v-if="activeName === '3'" :detail-info="detailInfo" :is-renovate="isShow" :sn="sn"></inspection-receipt>
                </el-tab-pane>
<!--              维修工单-->
                <el-tab-pane :label="$t('message.executionOutboundOperation.repairWorkOrder')" name="4" v-if="status !== 4 && status !== 1 && type !==2">
                    <maintenance-progress v-if="activeName === '4'" :detail-info="detailInfo" :sn="sn"></maintenance-progress>
                </el-tab-pane>
<!--              出库记录-->
                <el-tab-pane :label="$t('message.executionOutboundOperation.outboundRecords')" name="5">
                    <!-- 第九批迭代 产品和配件使用同一个出库详情页面 -->
                    <refurbishment-shipment v-if="activeName === '5'" :detail-info="detailInfo" :is-renovate="isShow"></refurbishment-shipment>
                </el-tab-pane>
            </el-tabs>
        </div>
        <confirm-dialog ref="confirmationComponent" :data-name="dataName" :type="'del'">
            <div slot="button-group">
                <group-btn :is-show="false" :type="'save'" :content="$t('message.buttons.confirm')"
                           @click.native="deleteFun"></group-btn>
                <group-btn :is-show="false" :type="'className'" :content="$t('message.buttons.cancel')"
                           :class-name="'light-blue'"
                           @click.native="cancelComponent"></group-btn>
            </div>
        </confirm-dialog>
       <sync-shipping ref="syncShipping"></sync-shipping>
    </div>
</template>
<script>
    module.exports = {
        name: 'outboundOperationManageView',
        components: {
            "page-header": httpVueLoader("/views/components/pageHeader.vue"),
            "group-btn": httpVueLoader("/views/components/groupBtn.vue"),
            "customer-service-view": httpVueLoader("/views/customerWorkOrderManage/afterSalesWorkOrder/customerServiceView.vue"),
            "warehouse-receipt": httpVueLoader("/views/customerWorkOrderManage/afterSalesWorkOrder/warehouseReceipt.vue"),
            "refurbishment-shipment": httpVueLoader("/views/customerWorkOrderManage/afterSalesWorkOrder/outboundRecords.vue"),
            "maintenance-progress": httpVueLoader("/views/customerWorkOrderManage/afterSalesWorkOrder/maintenanceProgress.vue"),
            "inspection-receipt": httpVueLoader("/views/outboundOperationManage/outboundWorkOrderPool/inspectionReceipt.vue"),
            "part-view": httpVueLoader("/views/outboundOperationManage/outboundWorkOrderPool/partView.vue"),
            "ware-housing": httpVueLoader("/views/refurbishmentMachineInboundAndOutbound/inbound/refurbishmentOperation.vue"),
            "confirm-dialog": httpVueLoader("/views/components/confirmDialog.vue"),
            "sync-shipping": httpVueLoader("/views/outboundOperationManage/outboundWorkOrderPool/syncShipping.vue"),
        },
        data() {
            return {
                activeName: '5',
                detailInfo: {},
                expressInfo: {},
                title: this.$t('i18n.route.action.bluas.afterSaleClient.outBoundMgt.view'),
                productMaterialDetails: [],
                status: 0,
                isShow: false,
                sn:'',
                type:null,
                permission: {
                    ...this.$actionsList.outBound,
                },
                outStatus:'',//出库单状态
                dataName:'',
                isOutRecords:true,
                userInfo: this.$api.getSession('userInfo'),
                siteInfo: win.StorageUtil.sessionStorageGet('siteInfo'),
            }
        },
        mounted() {
            this.id = this.$route.query.id;
            win.StorageUtil.sessionStorageSet("msgInfo",true);
            this.type = this.$route.query.orderType * 1 ? this.$route.query.orderType * 1 : 1;
            this.status = this.$route.query.afterSaleType * 1;
            this.outStatus = this.$route.query.outStatus * 1;
           if(this.type){
              if(this.type == 2) this.outOperationDetail();
              if(this.type == 1) this.getWorkOrderDetail(this.id);
           }else {
               this.getWorkOrderDetail(this.id);
           }
        },
        methods: {
            getWorkOrderDetail(id) {
                this.getLoading();
                this.$api.detailWorkOrder(id).then(res => {
                    this.isLoading.close();
                    if (res.msgCode === 0 && res.data) {
                        this.formatRes(res.data);
                    }
                })
            },
            async formatRes(data){
                this.detailInfo = data;
              if(this.detailInfo.approveVoucherFile){
                const res = await this.getPreFilePath();
                const url = res?.data || '';
                this.detailInfo.approveVoucherFileList = [{
                  url:url,
                  fileUrl:url,
                  fileId:this.detailInfo.approveVoucherFile,
                  id:this.detailInfo.approveVoucherFile,
                  name:this.detailInfo.approveVoucherFile,
                  fileName:this.detailInfo.approveVoucherFile,
                }]
              }
                this.detailInfo.isOutBound = this.$route.query.active ? this.$route.query.active : 1;
                this.productMaterialDetails = data.productMaterialDetails;
                if (this.detailInfo.faultTypes && this.detailInfo.faultTypes !== '[]' && this.detailInfo.faultTypes.length > 0) {
                    this.detailInfo.faultTypes = this.detailInfo.faultTypes.toString();
                } else {
                    this.detailInfo.faultTypes = null;
                }
                if(this.detailInfo.productMaterialDetails && this.detailInfo.productMaterialDetails.length > 0){
                    let rul = this.detailInfo.productMaterialDetails.find(item=>item.sn);
                    this.sn =  rul && rul.sn? rul.sn : '';
                }
                this.isOutRecords = true;
                if(data.productType === 1){
                    this.isOutRecords = false;
                }
                if(data.shippingOrderNo){
                    this.getExpressInfo(data.shippingOrderNo);
                }
            },
          getPreFilePath(){
            const url = '~/file/v1/getFilePath/' + this.detailInfo.approveVoucherFile;
            const apiUrl = $.api($.apiModule.midpFileCenter,url);
            return win.OkHttp.get(apiUrl).then(res => res).catch(err => err);
          },
            //快递信息
            getExpressInfo(orderNumber){
                this.$api.getJsonLogisticsInfo(orderNumber).then(res=>{
                    if(res.code === 0 && res.data && res.data.length > 0){
                        this.expressInfo = res.data[0];
                    }
                })
            },
            outOperationDetail() {
                this.$api.outOperationDetail(this.id).then(res => {
                    if (res.msgCode === 0 && res.data) {
                        this.formatRes(res.data);
                    }
                })
            },
            back() {
              if(eval(this.$route.query.backFlag)){
                this.$router.back();
              }else{
                let url = '/afterSaleClient/inventory/client/outBoundRefactor/route/v2/index';
                const routePath = this.$route.query.routePath;
                this.$router.push({
                  path: routePath ? routePath : url,
                  query:{
                    active:this.$route.query.active || '1',
                  }
                });
              }
            },
            onClose() {
                this.dataName = this.$t('message.refurbishmentInboundOutbound.returnOrder');
                this.$refs.confirmationComponent.visible();
            },
            deleteFun(){
                this.getLoading();
                if(this.type == 2){
                    this.detailInfo.postSaleNo = this.id;
                }
                this.$api.chargeBack(this.detailInfo.postSaleNo, this.type).then(res => {
                    this.$refs.confirmationComponent.dialogClose();
                    this.isLoading.close();
                    if (res.msgCode === 0) {
                        this.back();
                        this.$message.success(this.$t('message.executionOutboundOperation.refundSuccessful'));
                    }
                })
            },
            cancelComponent(){
                this.$refs.confirmationComponent.dialogClose();
            },
            handleSync(row) {
                this.$refs.syncShipping.visible(row);
            },
            onSubmit(){
                if(this.detailInfo && this.detailInfo.productType) {
                    // 产品出库
                    if(this.detailInfo.productType == 1) {
                        this.$api.outOperationPickup(this.detailInfo.postSaleNo || this.detailInfo.workOrderId, this.type)
                            .then(res => {
                            if (res.msgCode === 0) {
                                this.goReceivingSendingPage();
                                this.$message.success(this.$t('message.executionOutboundOperation.paperEntitling'));
                            }
                        })
                    }
                    // 配件出库
                    if(this.detailInfo.productType == 1) {
                        this.goReceivingSendingPage();
                    }
                }
            },
            goReceivingSendingPage() {
                this.$router.push({
                    path: '/afterSaleClient/inventory/client/outBoundRefactor/route/v2/receivingSending',
                    query: {
                        id: this.$route.query.id,
                        type: this.$route.query.orderType,
                        active: this.$route.query.active,
                        afterSaleType: this.$route.query.afterSaleType,
                        realRightType: this.$route.query.realRightType,
                        workOrderStatus: this.$route.query.workOrderStatus,
                        productType: this.detailInfo.productType,
                    }
                });
            },
        },
    }
</script>
