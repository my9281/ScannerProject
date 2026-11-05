<template>
  <el-dialog :title="title" :visible.sync="dialogVisible" width="40%" :close-on-click-modal="false" center
             class="after-sale__dialog after-sale__customer-noodle-list" @close="handleClose">
    <div class="after-sale__dialog-body">
      <div class="bs-flex bs-flex-items-center bs-mb-20">
        <h2>{{$t('message.customerWorkOrderManage.afterSalesNumber')}}：{{workOrderId}} <i class="buletti-icons mi-a-fuzhicopy bs-text-blue" @click="handleNO"></i></h2>
      </div>
      <div class="bs-flex bs-flex-items-center bs-flex-space-between">
        <div class="after-sale__title">
          <span>{{$t('message.executionOutboundOperation.shippingInformation')}}</span>
        </div>
      </div>
      <div class="after-sale__customer-box">
        <div class="bs-flex bs-flex-space-around after-sale__item">
          <p class="after-sale__item-name bs-flex-1 bs-padding-10">{{$t('message.commons.customerName')}}</p>
          <div class="bs-flex bs-flex-items-center bs-flex-space-between after-sale__item-value bs-flex-1 bs-padding-10">
            <p class="">{{ customerName || '--'}}</p>
            <i @click="getCopy(customerName)" class="buletti-icons mi-a-fuzhicopy bs-text-blue" v-if="customerName"></i>
          </div>
        </div>
        <div class="bs-flex bs-flex-space-around after-sale__item">
          <p class="after-sale__item-name bs-flex-1 bs-padding-10">{{$t('message.commons.customerEmail')}}</p>
          <div class="bs-flex bs-flex-items-center bs-flex-space-between after-sale__item-value bs-flex-1 bs-padding-10">
            <p class="">{{ customerEmail || '--' }}</p>
            <i @click="getCopy(customerEmail)" class="buletti-icons mi-a-fuzhicopy bs-text-blue" v-if="customerEmail"></i>
          </div>
        </div>
        <div class="bs-flex bs-flex-space-around after-sale__item">
          <p class="after-sale__item-name bs-flex-1 bs-padding-10">{{$t('message.commons.customerPhone')}}</p>
          <div class="bs-flex bs-flex-items-center bs-flex-space-between after-sale__item-value bs-flex-1 bs-padding-10">
            <p class="">{{ customerPhone || '--' }}</p>
            <i @click="getCopy(customerPhone)" class="buletti-icons mi-a-fuzhicopy bs-text-blue" v-if="customerPhone"></i>
          </div>
        </div>
        <div class="bs-flex bs-flex-space-around after-sale__item">
          <p class="after-sale__item-name bs-flex-1 bs-padding-10">{{$t('message.commons.shippingAddress')}}</p>
          <template v-if="isType11">
            <div class="bs-flex bs-flex-items-center bs-flex-space-between after-sale__item-value bs-flex-1 bs-padding-10">
              <p class="">{{ address || '--'}}</p>
              <i @click="getCopy(address)" class="buletti-icons mi-a-fuzhicopy bs-text-blue" v-if="address"></i>
            </div>
          </template>
          <div v-else class="bs-flex bs-flex-items-center bs-flex-space-between after-sale__item-value bs-flex-1 bs-padding-10">
            <p class="">{{info.shippingAddress || info.customerAddress ? info.shippingAddress || info.customerAddress : '--'}}</p>
            <i @click="getCopy(info.shippingAddress || info.customerAddress)" class="buletti-icons mi-a-fuzhicopy bs-text-blue" v-if="info.shippingAddress"></i>
          </div>
        </div>
        <div class="bs-flex bs-flex-space-around after-sale__item">
          <p class="after-sale__item-name bs-flex-1 bs-padding-10 bs-flex bs-flex-items-center">{{$t('message.commons.returnAddress')}}</p>
          <template v-if="isType11">
            <div class="bs-flex bs-flex-items-center bs-flex-space-between after-sale__item-value bs-flex-1 bs-padding-10">
              <!--                        <p class="">{{ address || '&#45;&#45;'}}</p>-->
              <i @click="getCopy(address)" class="buletti-icons mi-a-fuzhicopy bs-text-blue" v-if="address"></i>
              <p>
                {{ $t('message.commons.country') }}：{{ splitAddress(address,'country') }}
                <i @click="getCopy(splitAddress(info.returnAddress,'country'))" class="buletti-icons mi-a-fuzhicopy bs-text-blue"></i>
              </p>
              <p>
                {{ $t('message.commons.province') }}：{{ splitAddress(address,'province') }}
                <i @click="getCopy(splitAddress(info.returnAddress,'province'))" class="buletti-icons mi-a-fuzhicopy bs-text-blue"></i>
              </p>
              <p>
                <span style="width: 48px;display: inline-block;font-size: 16px;">{{ $t('message.commons.city') }}：</span>{{ splitAddress(address,'city') }}
                <i @click="getCopy(splitAddress(info.returnAddress,'city'))" class="buletti-icons mi-a-fuzhicopy bs-text-blue"></i>
              </p>
              <p>
                {{ $t('message.commons.zipCode') }}：{{ splitAddress(address,'postcode') }}
                <i @click="getCopy(splitAddress(info.returnAddress,'postcode'))" class="buletti-icons mi-a-fuzhicopy bs-text-blue"></i>
              </p>
              <p>
                {{ $t('message.commons.street') }}：{{ splitAddress(address,'street') }}
                <i @click="getCopy(splitAddress(info.returnAddress,'street'))" class="buletti-icons mi-a-fuzhicopy bs-text-blue"></i>
              </p>
              <p>
                {{ $t('message.commons.address') }}：{{ splitAddress(address,'address') }}
                <i @click="getCopy(splitAddress(info.returnAddress,'address'))" class="buletti-icons mi-a-fuzhicopy bs-text-blue"></i>
              </p>
            </div>
          </template>
          <div v-else class="bs-flex-items-center bs-flex-space-between after-sale__item-value bs-flex-1 bs-padding-10">
            <!--                        <p class="">{{info.returnAddress ? info.returnAddress : '&#45;&#45;'}}</p>-->
            <i @click="getCopy(info.returnAddress)" class="buletti-icons mi-a-fuzhicopy bs-text-blue" v-if="info.returnAddress"></i>
            <p>
              {{ $t('message.commons.country') }}：{{ splitAddress(info.returnAddress,'country') }}
              <i @click="getCopy(splitAddress(info.returnAddress,'country'))" class="buletti-icons mi-a-fuzhicopy bs-text-blue"></i>
            </p>
            <p>
              {{ $t('message.commons.province') }}：{{ splitAddress(info.returnAddress,'province') }}
              <i @click="getCopy(splitAddress(info.returnAddress,'province'))" class="buletti-icons mi-a-fuzhicopy bs-text-blue"></i>
            </p>
            <p>
              <span style="width: 48px;display: inline-block;font-size: 16px;">{{ $t('message.commons.city') }}：</span>{{ splitAddress(info.returnAddress,'city') }}
              <i @click="getCopy(splitAddress(info.returnAddress,'city'))" class="buletti-icons mi-a-fuzhicopy bs-text-blue"></i>
            </p>
            <p>
              {{ $t('message.commons.zipCode') }}：{{ splitAddress(info.returnAddress,'postcode') }}
              <i @click="getCopy(splitAddress(info.returnAddress,'postcode'))" class="buletti-icons mi-a-fuzhicopy bs-text-blue"></i>
            </p>
            <p v-if="splitAddress(info.returnAddress,'street')">
              {{ $t('message.commons.street') }}：{{ splitAddress(info.returnAddress,'street') }}
              <i @click="getCopy(splitAddress(info.returnAddress,'street'))" class="buletti-icons mi-a-fuzhicopy bs-text-blue"></i>
            </p>
            <p>
              {{ $t('message.commons.address') }}：{{ splitAddress(info.returnAddress,'address') }}
              <i @click="getCopy(splitAddress(info.returnAddress,'address'))" class="buletti-icons mi-a-fuzhicopy bs-text-blue"></i>
            </p>

          </div>
        </div>
      </div>
    </div>
    <div slot="footer" class="dialog-footer">
      <group-btn
          :is-show="false"
          :type="'className'"
          :content="$t('message.buttons.cancel')"
          :class-name="'light-blue'"
          @click.native="handleClose"
      >
      </group-btn>
    </div>
  </el-dialog>
</template>
<script>
module.exports = {
  name: 'noodleList',
  components: {
    "group-btn": httpVueLoader("/views/components/groupBtn.vue"),
  },
  data() {
    return {
      title:this.$t('message.executionOutboundOperation.noodleListInfo'),
      dialogVisible: false,
      workOrderId: '',
      info: {},
    }
  },
  computed: {
    // 以旧换新
    isType11() {
      return this.$route.query.afterSaleType === '11';
    },
    customerName() {
      if (this.isType11) {
        return this.info.recycleCustomerInfo && this.info.recycleCustomerInfo.customerName;
      }
      return this.info.customerName;
    },
    customerEmail() {
      if (this.isType11) {
        return this.info.recycleCustomerInfo && this.info.recycleCustomerInfo.customerEmail;
      }
      return this.info.customerEmail;
    },
    customerPhone() {
      if (this.isType11) {
        return this.info.recycleCustomerInfo && this.info.recycleCustomerInfo.customerPhone;
      }
      return this.info.customerPhone;
    },
    address() {
      return this.info.recycleCustomerInfo && this.info.recycleCustomerInfo.address;
    }
  },
  methods: {
    visible(row) {
      this.workOrderId = row.workOrderId
      let params = {
        workOrderId:row.workOrderId,
        orderType:row.type
      }
      this.outOperationCustomer(params)
      this.dialogVisible = true
    },
    outOperationCustomer(params) {
      if (this.isType11) {
        this.$api.getRecycleOrderCustomerInfo(params).then(res => {
          if (res.msgCode === 0) {
            this.info = res.data
          }
        })
        return
      }
      this.$api.outOperationCustomer(params).then(res => {
        if (res.msgCode === 0) {
          this.info = res.data
        }
      })
    },
    handleClose() {
      this.dialogVisible = false
    },
    handleNO() {
      this.getCopy(this.workOrderId)
    },
    splitAddress(address,name) {
      if (address) {
        const result = address.split('/')
        if(name === 'country') return result[result.length - 1]
        if(name === 'province') return result[result.length - 3]
        if(name === 'city') return result[result.length - 4]
        if(name === 'postcode') return result[result.length - 2]
        if(name === 'address') return result[0]
        if(result.length === 6){
          if(name === 'street') return result[1]
        }
      }
      return '--'
    },
  }
}
</script>
