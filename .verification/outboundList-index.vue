<template>
    <div>
        <el-tabs v-model="activeName" v-has="'19e3dbde641bc91b273a4173db8'">
            <el-tab-pane v-for="(ele,i) in labelList"
                         :key="i"
                         :label="ele"
                         :name="(i + 1).toString()">
                <outbound-work-order-pool v-if="activeName == 1"></outbound-work-order-pool>
                <second-hand-out v-if="activeName == 2"></second-hand-out>
                <other-work-order-pool v-if="activeName == 3"></other-work-order-pool>
              <!--                <delivery-accessories v-if="activeName === ele.count && ele.count == 5"></delivery-accessories>-->
                <trade-in-and-return v-if="activeName == 4"></trade-in-and-return>
                <other-after-sales-service v-if="activeName == 5"></other-after-sales-service>
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
            this.$t('i18n.route.action.bluas.afterSaleClient.outBoundMgt.tradeInAndReturn'),
            this.$t('i18n.route.action.bluas.afterSaleClient.outBoundMgt.otherAfterSalesServiceDispatching'),
          ],

        }
      },
      components: {
        "outbound-work-order-pool": httpVueLoader("/views/outboundOperationManage/outboundList/outboundWorkOrderPool.vue"),
        "other-work-order-pool": httpVueLoader("/views/outboundOperationManage/outboundList/otherWorkOrderPool.vue"),
        "delivery-accessories": httpVueLoader("/views/outboundOperationManage/outboundList/deliveryAccessories.vue"),
        "second-hand-out": httpVueLoader("/views/outboundOperationManage/outboundList/secondHandOut.vue"),
        "trade-in-and-return": httpVueLoader("/views/outboundOperationManage/outboundList/tradeInAndReturn.vue"),
        "other-after-sales-service": httpVueLoader("/views/outboundOperationManage/outboundList/otherAfterSalesServiceLocations.vue"),
      },
      mounted() {
        if (this.$route.query && this.$route.query.active) {
          this.activeName = this.$route.query.active || '1';
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

