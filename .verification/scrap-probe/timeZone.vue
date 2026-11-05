<template>
    <el-dialog :title="title" :visible.sync="dialogVisible" width="25%" :close-on-click-modal="false" center
               class="after-sale__dialog" @close="handleClose">
        <div class="after-sale__dialog-body">
            <el-form :label-position="'top'" label-width="80px" :model="form" ref="rulesForm">
                <el-form-item label="">
                    <p>{{ $t('message.account.systemDefaultTimeZone') }}</p>
                    <el-input v-model="form.model" :disabled="true"></el-input>
                </el-form-item>
                <el-form-item label="">
                    <p>{{ $t('message.account.userDefaultTimeZone') }}</p>
                    <el-select v-model="form.timezone" filterable :placeholder="$t('message.account.selectDefaultTimeZone')"
                               @change="onChangeTimeZone" style="width: 100%">
                        <el-option
                            v-for="item in options"
                            :key="item.value"
                            :label="item.label"
                            :value="item.value">
                        </el-option>
                    </el-select>
                </el-form-item>
            </el-form>
            <p class="bs-text-err" style="line-height: 25px">{{ $t('message.account.attention') }}</p>
        </div>
        <div slot="footer" class="dialog-footer">
            <group-btn :is-show="false" :type="'className'" :content="$t('message.buttons.cancel')" :class-name="'light-blue'"
                       @click.native="handleClose"></group-btn>
            <group-btn :is-show="false" :type="'save'" :content="$t('message.buttons.save')" @click.native="handleSubmit"></group-btn>
        </div>
    </el-dialog>
</template>
<script>
    module.exports = {
      name: 'timeZone',
      components: {
        "group-btn": httpVueLoader("/views/components/groupBtn.vue"),
      },
      data() {
        return {
          title: this.$t('message.account.setTimeZone'),
          dialogVisible: false,
          option: {
            labelPosition: 'top',
            width: '0',
            inline: false
          },
          form: {
            model: '',
            timezone: '',
          },
          rules: {},
          options: [],
        }
      },
      computed: {},
      methods: {
        visible() {
          this.dialogVisible = true;
          this.form.model = win.utilsTool.getFormatDate();
          this.getUserName();
          this.getNationalTimezone();
        },
        getUserName() {
          this.$api.getUserName().then(res => {
            if (res.msgCode === 0 && res.data) {
              this.form.model = res.data.timezoneName;
              this.form.timezone = res.data.nationalTimezone;
            } else {
              window.location.href = '/login.html#/';
            }
          })
        },
        getNationalTimezone() {
          this.$api.getNationalTimezone().then(res => {
            if (res.code === 0) {
              this.options = res.data;
            }
          })
        },
        onChangeTimeZone(val) {
          this.$forceUpdate();
        },
        handleClose() {
          this.form = {
            model: '',
            timezone: ''
          };
          this.dialogVisible = false;
        },
        handleSubmit() {
          this.$api.setNationalTimezone(this.form).then(res => {
            this.dialogVisible = false;
            if (res.code === 0) {
              this.form = {
                model: '',
                timezone: '',
              };
              this.dialogVisible = false;
              window.location.reload();
            }
          })
        },
      },
    }
</script>
<style scoped>
    .after-sale__dialog .el-dialog {
      border-radius: 10px;
      overflow: hidden;
    }

    .after-sale__dialog .el-dialog__header {
      /*background-color: var(--color-border);*/
      border-bottom: none;
      padding: 10px 20px;
    }

    .after-sale__dialog .el-dialog__header .el-dialog__title {
      font-weight: 600;
      color: var(--color-base);
      font-size: var(--text-font-size-16);
      display: inline-block;
    }

    .el-dialog__headerbtn .el-dialog__close {
      font-weight: 600;
    }

    .after-sale__dialog-body {
      max-height: 500px;
      overflow: auto;
      padding: 0 10px;
    }

    .after-sale__dialog-template-btn {
      background-color: var(--button-background-color);
      border-radius: var(--border-radius-12);
      color: var(--color-main);
      padding: 15px 30px;
      display: inline-block;
    }

    .after-sale__dialog-template-btn:hover {
      background-color: var(--color-main);
      color: var(--color-white);
      opacity: 0.8;
    }

    .after-sale__dialog .el-dialog__header {
      padding: 0 20px !important;
    }

    .el-dialog--center .el-dialog__body {
      padding: 0 20px 10px !important;
    }
</style>