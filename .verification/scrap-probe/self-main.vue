<template>
    <div class="bs-mt-10">
        <div class="bs-flex bs-flex-items-center bs-gap-20 bs-flex-wrap  bs-padding-left-20 bs-padding-right-20 ">
            <p :class="activeName === i ? 'is-active' : ''" @click="onTabs(ele,i)"
               class="bs-tabs bs-warehouse bs-cursor " v-for="(ele,i) in selfOperatedEnums">
                {{ ele.name }}
            </p>
        </div>
        <div class="bs-warehouse__content" v-for="(ele,i) in selfOperatedEnums">
            <self-main :warehouse-type="ele.id" ref="selfMain" v-if="activeName === i"></self-main>
        </div>
    </div>
</template>
<script>
    module.exports = {
      components: {
        "self-main": httpVueLoader("/views/warehouseDivision/selfWarehouse/index.vue"),
      },
      data() {
        return {
          activeName: 0,
          selfOperatedEnums: [],
          warehouseType: null
        }
      },
      mounted() {
        this.getEnumsAll();
      },
      methods: {
        getEnumsAll() {
          this.$api.getEnumsAll().then(res => {
            if (res.msgCode === 0 && res.data) {
              this.selfOperatedEnums = res.data.selfOperatedEnums;
              this.warehouseType = this.selfOperatedEnums[0].id;
            }
          })
        },
        onTabs(item, i) {
          this.activeName = i;
          this.warehouseType = item.warehouseType;
        },
      }
    }
</script>