<template>
    <div>
        <el-tabs v-model="activeName">
            <el-tab-pane v-for="ele in list"
                         :key="ele.count"
                         :label="ele.label"
                         :name="ele.count"
                         v-if="list && ele.show">
                <outbound-work-order-pool @other-demand="otherDemand" v-if="activeName === ele.count && ele.count == 1"></outbound-work-order-pool>
                <second-hand-out v-if="activeName === ele.count && ele.count == 2"></second-hand-out>
                <other-work-order-pool v-if="activeName === ele.count && ele.count == 3"></other-work-order-pool>
                <my-pending-pool v-if="activeName === ele.count && ele.count == 4"></my-pending-pool>
<!--                <delivery-accessories v-if="activeName === ele.count && ele.count == 5"></delivery-accessories>-->
                <trade-in-and-return v-if="activeName === ele.count && ele.count == 5"></trade-in-and-return>
                <other-after-sales-service v-if="activeName === ele.count && ele.count == 6"></other-after-sales-service>
            </el-tab-pane>
        </el-tabs>
    </div>
</template>
<script>
    module.exports = {
        name: 'index',
        data() {
            return {
                activeName: '1',
                permission: {
                    ...this.$actionsList.outBound,
                },
                list: [],
                labelList: [
                    this.$t('message.executionOutboundOperation.outboundWorkOrderPool'),
                    this.$t('i18n.route.action.bluas.afterSaleClient.outBoundMgt.secondHandOut'),
                    this.$t('i18n.route.action.bluas.afterSaleClient.outBoundMgt.otherWorkOrderPool'),
                    this.$t('i18n.route.action.bluas.afterSaleClient.outBoundMgt.myPendingPool'),
                    this.$t('i18n.route.action.bluas.afterSaleClient.outBoundMgt.tradeInAndReturn'),
                    this.$t('i18n.route.action.bluas.afterSaleClient.outBoundMgt.otherAfterSalesServiceDispatching'),
                ],

            }
        },
        components: {
            "outbound-work-order-pool": httpVueLoader("/views/outboundOperationManage/outboundWorkOrderPool/index.vue"),
            "other-work-order-pool": httpVueLoader("/views/outboundOperationManage/otherWorkOrderPool/index.vue"),
            "my-pending-pool": httpVueLoader("/views/outboundOperationManage/myPendingPool/index.vue"),
            "delivery-accessories": httpVueLoader("/views/outboundOperationManage/deliveryAccessories/index.vue"),
            "second-hand-out": httpVueLoader("/views/outboundOperationManage/secondHandOut/index.vue"),
            "trade-in-and-return": httpVueLoader("/views/outboundOperationManage/tradeInAndReturn/index.vue"),
            "other-after-sales-service": httpVueLoader("/views/outboundOperationManage/otherAfterSalesServiceLocations/index.vue"),
        },
        watch: {},
        mounted() {
          this.list = win.utilsTool.initTabs(this.permission, this.labelList);
          this.activeName = this.list.find(item => item.show)?.count || '1';
            if (this.$route.query && this.$route.query.active) {
                this.activeName = this.$route.query.active;
            }
            this.routeBus.$on('route-status', data => {
                if (data) {
                    this.activeName = data;
                }
            })
        },
        methods: {
            otherDemand(data) {
                if(data)this.activeName = '3';
            },
        }
    }
</script>

