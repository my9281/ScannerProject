/**
 * author:gy
 * 2030-6-3
 * 用于管理系统的接口
 */

Vue.prototype.$api = {
  //==========================公共方法 ==========================================
  http(params) {
    const isGet = params.methods === 'get';
    let data = {};
    if (!isGet) data = params.data;
    const httpPromise = isGet ? win.OkHttp.get(params.url, params.config) : win.OkHttp[params.methods](params.url, data, params.config);
    return httpPromise.then(response => {
      this.responseHandle(response);
      return response;
    }).catch(error => error);
  },
  get(url, config) {
    return this.http({methods: 'get', url, config});
  },
  post(url, data, config) {
    return this.http({methods: 'post', url, data, config});
  },
  delete: function (url, data, config) {
    return this.http({methods: 'delete', url, data, config});
  },
  put: function (url, data, config) {
    return this.http({methods: 'put', url, data, config});
  },
  responseHandle(res) {
    if (res.msgCode >= 800 && res.msgCode <= 999) {
      sessionStorage.clear();
      localStorage.clear();
      const statusCode = [801, 803, 805];
      if (statusCode.indexOf(res.msgCode) === -1) {
        setTimeout(() => {
          window.location.href = '/login.html#/';
        }, 100);
      }
    }
  },
  getSession(name) {
    return win.StorageUtil.sessionStorageGet(name);
  },
  //获取国家列表
  getCountryList(jwt) {
    let str = "~/iso/country/v1/localized/all-countries";
    let url = jwt ? str + '?' + $.gwcredentialsCode.country : str;
    return this.get($.api($.apiModule.midpMasterData, url));
  },
  //获取省市列表
  getProvinceList(geoNameId, jwt) {
    let str = '~/iso/country/v1/localized/administrative-divisions?geoNameId=' + geoNameId + '&level=1'
    const apiPath = jwt ? str + '&' + $.gwcredentialsCode.province : str;
    const apiUrl = $.api($.apiModule.midpMasterData, apiPath);
    return this.get(apiUrl);
  },
  //获取城市
  getCityList(geoNameId) {
    const apiPath = '~/iso/country/v1/localized/administrative-divisions?geoNameId=' + geoNameId + '&level=' + 2;
    const apiUrl = $.api($.apiModule.midpMasterData, apiPath);
    return this.get(apiUrl);
  },
  getCurrency() {
    const apiUrl = $.api($.apiModule.midpMasterData, '~/iso/country/currencies');
    return this.get(apiUrl);
  },
  //===============================获取验证码===============================
  sendCode(data) {
    const url = $.api($.apiModule.midpNotifyCenter, '~/captcha/send/v1?' + $.gwcredentialsCode.send);
    return this.post(url, data);
  },
  //===============================忘记密码================================
  userForgotpwd(params) {
    const obj = Object.assign({}, params);
    const data = this.parameterProcessing(obj);
    const url = $.api($.apiModule.midpAuthCenter, '~/user/v1/mgt/forgotpwd?' + $.gwcredentialsCode.forget);
    return this.post(url, data);
  },
  userRegister(data) {
    let url = $.api($.apiModule.midpAuthCenter, "~/user/mgt/register/v1?" + $.gwcredentialsCode.register)
    return this.post(url, data);
  },

  //处理参数
  parameterProcessing(arr) {
    let fileData = new FormData()
    for (let item in arr) {
      if (arr[item]) fileData.append(item, arr[item]);
    }
    return fileData
  },
  //=======================上传文件================
  defaults: {
    file: '',
    operation: 'Append',
    appId: 'blu-dist-center',
    uid: '',
    refBizId: '',
  },
  getApiUrl(url) {
    return $.api($.apiModule.midpFileCenter, url);
  },
  /**
   * 删除文件
   * @param params 参数
   * @param type batch:批量文件删除  single:单个文件删除
   * @returns {*}
   */
  deleteFile(params, type) {
    const url = type === 'batch' ? this.getApiUrl('~/file/v1/delete/multi') : this.getApiUrl('~/file/v1/delete');
    return this.delete(url, params);
  },
  /**
   * 上传文件
   * @param params
   * @param type batch/single 批量上传 单个上传
   * @returns {*}
   */
  postUploadFile(params, type) {
    const obj = Object.assign(this.defaults, params);
    const data = this.parameterProcessing(obj);
    const url = type === 'batch' ? this.getApiUrl('~/file/v1/upload/multi') : this.getApiUrl('~/file/v1/upload');
    return this.post(url, data);
  },
  /**
   * 查询组别列表
   * @param data
   * @returns {*}
   */
  getBaseInfo(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/baseInfo/v1/getGroupDict')
    return this.post(url, data);
  },
  //查询所有组别列表
  getBaseInfoAll(siteId) {
    let path = '/client/baseInfo/v1/getGroupDictAll'
    if (siteId) path += '?siteId=' + siteId
    let url = $.api($.apiModule.bluAfterSale, path)
    return this.get(url);
  },
  //通过uid查询用户当前站点关联渠道
  getChannelsByUid(uid, siteId) {
    let url = $.api($.apiModule.bluAfterSale, '/client/user/v2/getChannelsByUid?uid=' + uid + '&siteId=' + siteId)
    return this.get(url);
  },
  //通过uid查询用户zendeskId和zohoId
  getZendeskIdByUid(uid) {
    let url = $.api($.apiModule.bluAfterSale, '/client/user/v2/getZendeskIdByUid?uid=' + uid)
    return this.get(url);
  },
  //查询所有组别列表在登录站点之前
  getBaseInfoAllbeforeLoginSite() {
    const data = {
      "enable": 1,
      "group": "puCh"
    }
    let url = $.api($.apiModule.bluAfterSale, '/client/baseInfo/v1/getGroupDict')
    return this.post(url, data);
  },
//查询当前所有站点
  getUserAllSites(uid) {
    let url = $.api($.apiModule.bluAfterSale, '/client/user/v1/getUserSite?uid=' + uid)
    return this.get(url);
  },
  //获取时区列表
  getNationalTimezone() {
    let url = $.api($.apiModule.bluAfterSale, '/client/user/v1/getNationalTimezone')
    return this.get(url);
  },
  //设置用户时区
  setNationalTimezone(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/user/v1/setNationalTimezone')
    return this.put(url, data);
  },
  //获取站点用户信息
  getUserName() {
    let url = $.api($.apiModule.bluAfterSale, '/client/user/v1/getUserName')
    return this.get(url);
  },
  getUserSiteBacklogNum() {
    let url = $.api($.apiModule.bluAfterSale, '/client/user/v2/getUserSiteBacklogNum')
    return this.get(url);
  },
  // 权限接口
  routes() {
    let url = $.api($.apiModule.midpAuthCenter, '~/route/routes/user')
    return this.get(url);
  },
//    =================== 客服工单 start===============================================
  getMaterialInfo(sku){
    const url = $.api($.apiModule.bluAfterSale,'/client/workOrder/postSale/getMaterialInfo?sku=' + sku);
    return this.get(url);
  },
  //NS原单信息
  orderInfo(data) {
    const url = $.api($.apiModule.bluAfterSale, '/client/ns/orderInfo')
    return this.post(url, data);
  },
  workOrderPostSale(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/workOrder/postSale/page')
    return this.post(url, data);
  },
  getToRepairList(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/repairAndRefurbishment/getToRepairList')
    return this.post(url, data);
  },
  allWorkOrderPostSale(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/workOrder/postSale/pageAll')
    return this.post(url, data);
  },
  notifyPage(data) {
    let url = $.api($.apiModule.bluAfterSale, '/workOrderNotify/notifyPage')
    return this.post(url, data);
  },
  //通过Uid获取该站点的下属人员
  getSubUserList(uid) {
    let url = $.api($.apiModule.bluAfterSale, '/client/user/v2/getSubUserList?uid=' + uid);
    return this.get(url)
  },
  //批量更新客服工单处理人
  updateBatchHandler(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/workOrder/postSale/updateBatchHandler')
    return this.post(url, data);
  },
  //签收确认(旧)
  unWorkOrderPostSale(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/boundPreEntry/getDeliverList')
    return this.post(url, data);
  },
  workOrderPostSale2(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/recycleOrder/v2/page')
    return this.post(url, data);
  },
  //签收确认
  receivedConfirm(workOrderId) {
    let url = $.api($.apiModule.bluAfterSale, '/client/workOrder/postSale/receivedConfirm?workOrderId=' + workOrderId);
    return this.get(url)
  },
  updateIsDispose(workOrderId) {
    let url = $.api($.apiModule.bluAfterSale, '/client/workOrder/postSale/updateIsDispose?id=' + workOrderId);
    return this.get(url)
  },
  changeStatusProcessed(data) {
    let url = $.api($.apiModule.bluAfterSale, '/workOrderNotify/changeStatusProcessed?ids=' + data);
    return this.post(url)
  },
  saveConfirmRefund(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/workOrder/postSale/saveConfirmRefund');
    return this.post(url, data)
  },
//    新增编辑售后工单
  workOrderUpdate(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/workOrder/postSale/addOrUpdate')
    return this.post(url, data);
  },
  //保存为草稿类型的工单
  workOrderSaveDraft(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/workOrder/postSale/saveDraft')
    return this.post(url, data);
  },
  //提交审核
  submitAndExamine(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/workOrder/postSale/submitAndExamine')
    return this.post(url, data);
  },
  saleOrderQuery(data){
    let url = $.api($.apiModule.bluAfterSale,'/client/ns/saleOrderQuery');
    return this.post(url, data);
  },
  // 变更售后类型
  changeHandlerType(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/workOrder/postSale/changeHandlerType')
    return this.post(url, data);
  },
  //主管审批
  workOrderExamine() {
    let url = $.api($.apiModule.bluAfterSale, '/client/workOrder/postSale/examine')
    return this.post(url, data);
  },
  getReasonByHandleCode(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/baseInfo/getReasonByHandleCode?code=' + data)
    return this.get(url);
  },
  getReasonByHandleCodeList(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/baseInfo/getReasonByHandleCodeList?codes=' + data)
    return this.get(url);
  },
  delDraft(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/workOrder/postSale/delDraft?workOrderId=' + data)
    return this.get(url);
  },
  //产品类目
  categoryAvailable(type) {
    let path = '/client/site/category/selectCategory'
    if (type) path += '?type=' + type
    let url = $.api($.apiModule.bluAfterSale, path)
    return this.get(url);
  },
  // 根据RmaNo获取收件商品
  getRmaNoList(rmaNo) {
    let url = $.api($.apiModule.bluAfterSale, '/client/warehouse/receipt/getProductByRmaNo?rmaNo=' + rmaNo)
    return this.get(url);
  },
  // 根据RmaNo获取以旧换新商品信息
  getRmaNoTradeInList(rmaNo) {
    let url = $.api($.apiModule.bluAfterSale, '/client/recycleOrder/getRecycleProductByRma?rma=' + rmaNo)
    return this.get(url);
  },
  //获取已签收的商品
  baseInfoProduct(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/product/baseInfo/pagedAndRepertory')
    return this.post(url, data);
  },
  //获取预报单收件类型列表
  getboundPreEntryTypeList() {
    let url = $.api($.apiModule.bluAfterSale, '/client/boundPreEntry/getDeliverTypeList')
    return this.get(url);
  },
  //随机获取运单号/sn
  generateSerialNumber(type) {
    let url = $.api($.apiModule.bluAfterSale, '/client/workOrder/postSale/generateSerialNumber?type=' + type)
    return this.get(url);
  },
  //校验是否为储能产品并修改sku
  checkAndChangSku(sku,selfWarehouse = true) {
    let url = $.api($.apiModule.bluAfterSale, '/client/product/baseInfo/checkAndChangSku?sku=' + sku + '&selfWarehouse=' + selfWarehouse)
    return this.get(url);
  },
  queryAllModel() {
    let url = $.api($.apiModule.bluWmsCenter, '~/wms/afterSales/queryAllModel')
    return this.get(url);
  },
  //获取部分收件rma的商品信息
  getPartialReceiptList(rmaNo) {
    let url = $.api($.apiModule.bluAfterSale, '/client/warehouse/receipt/getSignProduct?rmaNo=' + rmaNo)
    return this.get(url);
  },
  //通过型号或者SKU查询 对应SKU信息
  querySku(data) {
    let url = $.api($.apiModule.bluWmsCenter, '~/wms/afterSales/querySku')
    return this.post(url, data);
  },
  closeWorkOrder(id) {
    let url = $.api($.apiModule.bluAfterSale, '/client/workOrder/postSale/close?workOrderId=' + id)
    return this.get(url);
  },
  //关闭工单-售后
  closeWorkOrderPostSale(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/workOrder/postSale/closeWorkOrder')
    return this.post(url, data);
  },
  //工单详情
  detailWorkOrder(id) {
    let url = $.api($.apiModule.bluAfterSale, '/client/workOrder/postSale/detail?postSaleNo=' + id)
    return this.get(url);
  },
  // 获取工单待出库信息
  getOutBoundInfo(id){
    let url = $.api($.apiModule.bluAfterSale, '/client/workOrder/postSale/getOutBoundInfo?workOrderId=' + id)
    return this.get(url);
  },
  getEnumsAll() {
    let url = $.api($.apiModule.bluAfterSale, '/client/baseInfo/getEnums')
    return this.get(url);
  },
  //根据客服工单Id查询仓库收件信息
  queryByWorkOrderId(id) {
    let url = $.api($.apiModule.bluAfterSale, '/client/warehouse/receipt/queryByWorkOrderId?workOrderId=' + id)
    return this.get(url);
  },
  //维修进展
  queryBySN(rmaNo, sn) {
    let url = $.api($.apiModule.bluAfterSale, '/client/repairAndRefurbishment/queryRepairAndRefurbishBySn?rmaNo=' + rmaNo + '&sn=' + sn)
    return this.get(url);
  },
//翻新发货
  outOperationShipmentDetail(workOrderId, orderType) {
    let url = $.api($.apiModule.bluAfterSale, '/client/outOperation/shipmentDetail?workOrderId=' + workOrderId + '&orderType=' + orderType)
    return this.get(url);
  },
  //快递信息
  getJsonLogisticsInfo(orderNumber) {
    const url = $.api($.apiModule.bluAfterSale, '/client/workOrder/postSale/getLogisticsInfo?number=' + orderNumber)
    return this.get(url)
  },
  closeCheck(workOrderId) {
    let url = $.api($.apiModule.bluAfterSale, '/client/workOrder/postSale/closeCheck?workOrderId=' + workOrderId)
    return this.get(url);
  },
  revokeCheck(params) {
    let url = $.api($.apiModule.bluAfterSale, '/client/workOrder/postSale/rollback')
    return this.post(url, params);
  },
  // 以旧换新订单列表
  getRecycleOrder(params) {
    const apiUrl = $.api($.apiModule.bluAfterSale, '/client/recycleOrder/v2/page');
    return this.post(apiUrl, params);
  },
  // 获取用户回收订单详情
  getUserRecoveryOrderDetail(rmaNo) {
    const apiUrl = $.api($.apiModule.bluMarketingCenter, '~/marketing/recovery/order/userRecoveryOrderDetailByRmaNo?rmaNo=' + rmaNo);
    return this.get(apiUrl);
  },
  // 获取以旧换新订单详情
  getRecoveryOrderDetail(rmaNo) {
    const apiUrl = $.api($.apiModule.bluAfterSale, '/client/recycleOrder/detail?rmaNo=' + rmaNo);
    return this.get(apiUrl);
  },
  // 根据等级和回收设备ID查询优惠券列表
  getRecoveryCouponList(params) {
    const apiUrl = $.api($.apiModule.bluMarketingCenter, '~/marketing/recovery/couponconf/listByLevel');
    return this.post(apiUrl, params);
  },
  // 更新物流状态
  updateRecycleOrderLogistics(params) {
    const apiUrl = $.api($.apiModule.bluAfterSale, '/client/recycleOrder/update/Logistics');
    return this.put(apiUrl, params);
  },
  // 更新不接受订单
  updateRecycleOrderReject(params) {
    const apiUrl = $.api($.apiModule.bluAfterSale, '/client/recycleOrder/update/reject');
    return this.put(apiUrl, params);
  },
  // 更新复核订单
  reviewOrderHandle(params) {
    const apiUrl = $.api($.apiModule.bluAfterSale, '/client/recycleOrder/update/review');
    return this.put(apiUrl, params);
  },
  // 获取以旧换新订单-仓库收件、设备初检
  getRecycleInspectionDetail(rmaNo) {
    const apiUrl = $.api($.apiModule.bluAfterSale, '/client/recycleOrder/getRecycleInspectionDetail?rmaNo=' + rmaNo);
    return this.get(apiUrl);
  },
//    =================== 客服工单 end===============================================
//    =================== 配件工单 start===============================================
//配件分页查询
  accessoryPage(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/workOrder/accessory/page')
    return this.post(url, data);
  },
  addOrUpdate(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/workOrder/accessory/addOrUpdate')
    return this.post(url, data);
  },
  accessoryDetail(accessoryNo) {
    let url = $.api($.apiModule.bluAfterSale, '/client/workOrder/accessory/detail?accessoryNo=' + accessoryNo)
    return this.get(url);
  },

  //以旧换新退货详情
  tradeInReturnDetail(workId) {
    let url = $.api($.apiModule.bluAfterSale, '/client/recycleOrder/getOutBoundDetail?workId=' + workId)
    return this.get(url);
  },
  accessoryClose(orderNo) {
    let url = $.api($.apiModule.bluAfterSale, '/client/workOrder/accessory/close?workOrderId=' + orderNo)
    return this.get(url);
  },
  //配件发货
  accessoryAccessoriesDelivery(orderNo) {
    let url = $.api($.apiModule.bluAfterSale, '/client/warehouse/accessoriesDelivery/queryByWorkOrderId?workOrderId=' + orderNo)
    return this.get(url);
  },
  sendPreEntry(workOrderId) {
    let url = $.api($.apiModule.bluAfterSale, '/client/warehouse/accessoriesDelivery/sendPreEntry?workOrderId=' + workOrderId)
    return this.post(url);
  },

  //配件申领物料列表
  getApplyForByWorkOrderId(orderNo) {
    let url = $.api($.apiModule.bluAfterSale, '/client/warehouse/accessoriesDelivery/getApplyForByWorkOrderId?workOrderId=' + orderNo)
    return this.get(url);
  },
  // 发货登记-配件申领
  applyFor(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/warehouse/accessoriesDelivery/applyFor')
    return this.post(url, data);
  },
  //发货配件-选择-申领物料列表
  getDeliveryApplyForList(workOrderId) {
    let url = $.api($.apiModule.bluAfterSale, '/client/warehouse/accessoriesDelivery/getDeliveryApplyForList?workOrderId=' + workOrderId)
    return this.get(url);
  },
  // 配件申领-记录
  getApplyForLog(workOrderId) {
    let url = $.api($.apiModule.bluAfterSale, '/client/warehouse/accessoriesDelivery/getApplyForLog?workOrderId=' + workOrderId)
    return this.get(url);
  },
  //配件申领-取消申领
  cancelApplyFor(id) {
    let url = $.api($.apiModule.bluAfterSale, '/client/warehouse/accessoriesDelivery/cancelApplyFor?id=' + id)
    return this.delete(url);
  },
  //材料出库配件发货
  accessoryAccessoriesOut(orderNo) {
    let url = $.api($.apiModule.bluAfterSale, '/client/warehouse/accessoriesDelivery/queryAccessoryOut?workOrderId=' + orderNo)
    return this.get(url);
  },
//    =================== 配件工单 end===============================================
//    =================== 仓库收发件管理 start===============================================
  warehouseAdjustDetail(id){
    const url = $.api($.apiModule.bluAfterSale, '/client/warehouse/segmentation/warehouseAdjustDetail?id=' + id);
    return this.get(url);
    },
  checkNoRmaChangeSku(id) {
    let url = $.api($.apiModule.bluAfterSale, '/client/warehouse/receipt/checkNoRmaChangeSku?id=' + id);
    return this.get(url);
  },
  searchConfig() {
    let url = $.api($.apiModule.bluAfterSale, '/client/warehouse/receipt/searchConfig')
    return this.get(url);
  },
  warehouseReceiptPage(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/warehouse/receipt/paged')
    return this.post(url, data);
  },
  unclaimedDetailList(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/warehouse/receipt/unclaimedDetailList')
    return this.post(url, data);
  },
  warehouseReceiptAdd(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/warehouse/receipt/add')
    return this.post(url, data);
  },
  warehouseReceiptEdit(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/warehouse/receipt/edit')
    return this.put(url, data);
  },
  // 仓库异步收件
  warehouseReceiptAsyncAdd(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/warehouse/receipt/asyncAdd');
    return this.post(url, data);
  },
  // 获取收件进度详情
  getAsyncReceiptDetail(workOrderId){
    let url = '/client/warehouse/receipt/getAsyncReceiptDetail?workOrderId=' + workOrderId;
    const apiUrl =  $.api($.apiModule.bluAfterSale,url);
    return this.get(apiUrl);
  },
  // 导出
  exportReceiptDetail(workOrderId){
    let url = '/client/warehouse/receipt/exportReceiptDetail?workOrderId=' + workOrderId;
    const apiUrl =  $.api($.apiModule.bluAfterSale,url);
    return this.get(apiUrl);
  },
  getNsPeriodBySiteId(siteId){
    let url = $.api($.apiModule.bluAfterSale,'/client/ns/getNsPeriodBySiteId?siteId=' + siteId);
    return this.get(url);
  },
  warehouseReceiptDel(id) {
    let url = $.api($.apiModule.bluAfterSale, '/client/warehouse/receipt/delete?id=' + id)
    return this.delete(url);
  },
  cancelReceipted(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/warehouse/receipt/cancelReceipt')
    return this.post(url, data);
  },
  updateWareHousingStatusToReplaceNew(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/warehouse/receipt/updateWareHousingStatusToReplaceNew')
    return this.post(url, data);
  },
  warehouseReceiptDetail(id) {
    let url = $.api($.apiModule.bluAfterSale, '/client/warehouse/receipt/detail?id=' + id)
    return this.get(url);
  },
  accessoriesDeliverySearch() {
    let url = $.api($.apiModule.bluAfterSale, '/client/warehouse/accessoriesDelivery/searchConfig')
    return this.get(url);
  },
  accessoriesDeliveryPag(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/warehouse/accessoriesDelivery/paged')
    return this.post(url, data);
  },
  accessoriesDeliveryDetail(id) {
    let url = $.api($.apiModule.bluAfterSale, '/client/warehouse/accessoriesDelivery/detail?id=' + id)
    return this.get(url);
  },

  //发货登记-发货
  accessoriesDeliveryDeliver(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/warehouse/accessoriesDelivery/deliver')
    return this.post(url, data);
  },
  //发货登记-保存
  accessoriesDeliverySave(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/warehouse/accessoriesDelivery/save')
    return this.post(url, data);
  },
  //发货登记-撤销
  accessoriesDeliveryCancelByWorkOrderId(workOrderId) {
    let url = $.api($.apiModule.bluAfterSale, '/client/warehouse/accessoriesDelivery/cancelByWorkOrderId?workOrderId=' + workOrderId)
    return this.delete(url);
  },
  //领单撤销
  accessoriesDeliveryCancel(id) {
    let url = $.api($.apiModule.bluAfterSale, '/client/warehouse/accessoriesDelivery/cancel?id=' + id)
    return this.delete(url);
  },
  //仓库收发件 -- 通过仓库id查询售后工单详情
  postSaleDetail(id) {
    let url = $.api($.apiModule.bluAfterSale, '/client/warehouse/receipt/postSaleDetail?id=' + id)
    return this.get(url);
  },
  //仓库收发件 -- 通过仓库id查询售后工单详情
  shipmentRegistration(id) {
    let url = $.api($.apiModule.bluAfterSale, '/client/workOrder/accessory/shipmentRegistration?workOrderId=' + id)
    return this.get(url);
  },
  cancelRegistration(id) {
    let url = $.api($.apiModule.bluAfterSale, '/client/workOrder/accessory/cancelRegistration?workOrderId=' + id)
    return this.get(url);
  },
//    =================== 仓库收发件管理 end===============================================
//    =================== 出库作业 start===============================================
  updateOutBoundInfo(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/outOperation/updateOutBoundInfo')
    return this.post(url, data);
  },
  outWarehousePage(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/outOperation/outWarehouse')
    return this.post(url, data);
  },
  syncUsedOrderMarket(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/ns/syncUsedOrderMarket')
    return this.get(url, data);
  },
  preownedPush(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/ns/preownedPush?workOrderId=' + data)
    return this.put(url);
  },
  preownedPage(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/outOperation/preownedPage')
    return this.post(url, data);
  },
  preownedDetails(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/outOperation/preownedDetails?id=' + data)
    return this.get(url, data);
  },
  outOperationChange(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/outOperation/changeType?workOrderId=' + data)
    return this.put(url);
  },
  findShippingDetails(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/outOperation/findShippingDetails?workOrderId=' + data)
    return this.get(url);
  },
  otherPage(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/outOperation/otherPage')
    return this.post(url, data);
  },
  receiptPage(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/outOperation/receiptPage')
    return this.post(url, data);
  },
  salePage(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/outOperation/salePage')
    return this.post(url, data);
  },
  otherSaleShipping(data){
    const url = $.api($.apiModule.bluAfterSale,'/client/outOperation/otherSaleShipping');
    return this.post(url,data);
  },
  outOperationSendPreEntry(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/outOperation/sendPreEntry')
    return this.post(url, data);
  },
  //新增需求工单
  addDemandWorkOrder(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/outOperation/addDemandWorkOrder')
    return this.post(url, data);
  },
  //其他需求单详情
  outOperationDetail(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/outOperation/detail?workOrderId=' + data)
    return this.get(url);
  },
  chargeBack(data, orderType) {
    let url = $.api($.apiModule.bluAfterSale, '/client/outOperation/chargeBack?workOrderId=' + data + '&orderType=' + orderType)
    return this.get(url, data);
  },
  //面单信息接口
  outOperationCustomer(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/outOperation/customerInfo?workOrderId=' + data.workOrderId + '&orderType=' + data.orderType)
    return this.post(url);
  },
  // 以旧换新-出库-获取客户详情
  getRecycleOrderCustomerInfo(params) {
    const url = $.api($.apiModule.bluAfterSale, '/client/recycleOrder/getOrderDetailById?workOrderId=' + params.workOrderId)
    return this.get(url);
  },
  batchOutWarehouse(data){
    const url = '/client/outOperation/batchOutWarehouse';
    const apiUrl = $.api($.apiModule.bluAfterSale,url);
    return this.post(apiUrl,data);
  },
  //解析二手品出库导入文件，生成出库预览数据
  parsePreownedOutbound(data){
    const url = $.api($.apiModule.bluAfterSale,'/client/outOperation/parsePreownedOutbound');
    return this.post(url,data);
  },
  //根据rmaNo查询通知列表
  getNoticeList(rmaNo) {
    let url = $.api($.apiModule.bluAfterSale, '/workOrderNotify/getNotify?rmaNo=' + rmaNo)
    return this.post(url);
  },
  preOutWarehouseDetail(data, type) {
    let url = $.api($.apiModule.bluAfterSale, '/client/outOperation/preOutWarehouseDetail?workOrderId=' + data)
    return this.get(url);
  },
  outOperationPickup(data, type) {
    let url = $.api($.apiModule.bluAfterSale, '/client/outOperation/pickUp?workOrderId=' + data + '&orderType=' + type)
    return this.get(url);
  },
  //出库前查询出库(Sn)信息
  preOutWarehouseSnList(workOrderId) {
    let url = $.api($.apiModule.bluAfterSale, '/client/outOperation/preOutWarehouseSnList?workOrderId=' + workOrderId)
    return this.get(url);
  },
  //出库关联工单产品信息
  listOutWarehouseRefOrderProductMaterial(workOrderId) {
    let url = $.api($.apiModule.bluAfterSale, '/client/outOperation/listOutWarehouseRefOrderProductMaterial?workOrderId=' + workOrderId)
    return this.get(url);
  },

  getTrandeInAndReturnList(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/recycleOrder/getOutBoundPage')
    return this.post(url, data);
  },
//    =================== 出库作业 end===============================================
//    =================== 基础数据配置 start===============================================
  getSubReasonByReason(code){
    // 根据售后原因获取次级原因枚举列表
    const url = '/client/baseInfo/getSubReasonByReason?code=' + code;
    const apiUrl = $.api($.apiModule.bluAfterSale, url)
    return this.get(apiUrl);
  },
  //获取NS店铺列表
  findStoreList(data) {
    const url = $.api($.apiModule.bluAfterSale, '/client/baseInfo/findStoreList')
    return this.post(url, data);
  },
  nsStoreList() {
    let url = $.api($.apiModule.bluAfterSale, '/client/ns/storeList')
    return this.get(url);
  },
  //获取NS子公司仓库列表
  queryNsStashPageList(data) {
    const url = $.api($.apiModule.bluAfterSale, '/client/ns/v1/queryNsStashPageList')
    return this.post(url, data);
  },
  // 其他售后站点信息
  getSubsidiaryWarehouse(data){
    const url = $.api($.apiModule.bluAfterSale, '/client/warehouse/segmentation/getSubsidiaryWarehouse')
    return this.get(url, data);
  },
  getInventoryBySiteSegmentation(siteId,productType){
    const url = '/client/warehouse/segmentation/getInventoryBySite?siteId=' + siteId + '&productType=' + productType ;
    const apiUrl = $.api($.apiModule.bluAfterSale,url);
    return this.get(apiUrl);
    },
  warehouseList(data) {
    const url = $.api($.apiModule.bluAfterSale, '/client/ns/warehouseList')
    return this.get(url, data);
  },
  logisticsList(data) {
    const url = $.api($.apiModule.bluAfterSale, '/client/logistics/provider/v1/page')
    return this.post(url, data);
  },
  pullLogisticsProviders() {
    let url = $.api($.apiModule.bluAfterSale, '/client/ns/pullLogisticsProviders')
    return this.get(url);
  },
  outTypePage(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/out/type/v1/page')
    return this.post(url, data);
  },
  getSiteUsers(id) {
    let url = $.api($.apiModule.bluAfterSale, '/client/user/v1/getSiteUsers')
    return this.get(url);
  },
  // 基础数据列表
  basicDataList(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/baseInfo/v1/getDictList')
    return this.post(url, data);
  },
  basicGetDictPage(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/baseInfo/v1/getDictPage')
    return this.post(url, data);
  },
  // 基础数据列表__保存
  basicDataSave(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/baseInfo/v1/saveDict')
    return this.post(url, data);
  },
  // 基础数据列表__编辑
  basicDataEdit(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/baseInfo/v1/editDict')
    return this.put(url, data);
  },
  // 基础数据列表__是否启用/停用
  basicDataEditStatus(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/baseInfo/v1/editDictStatus')
    return this.put(url, data);
  },
  // 基础数据列表删除
  basicDataDelete(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/baseInfo/v1/deleteDictById?id=' + data)
    return this.delete(url);
  },

//    =================== 基础数据配置 end===============================================
  //    =================== 售后人员管理__用户信息 start===============================================
  //判断用户是否权限
  existPermissions() {
    let url = $.api($.apiModule.bluAfterSale, '/client/role/v1/existPermissions')
    return this.get(url);
  },
  // 售后人员管理__用户信息列表
  getUserRolePage(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/user/v1/getUserRolePage')
    return this.post(url, data);
  },
  // 售后人员管理__用户信息列表__启用/停用
  editUserStatus(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/user/v1/editUserStatus')
    return this.put(url, data);
  },
  // 售后人员管理__用户信息列表__原始用户数据接口
  basicUserList(data) {
    let url = $.api($.apiModule.bluUserCenter, "~/uc/basic/list")
    return this.post(url, data);
  },
  // 售后人员管理__用户信息列表__选择角色列表
  basicSiteUsersList(data) {
    let url = $.api($.apiModule.bluAfterSale, "/client/role/v1/getSiteAllRoles?siteId=" + data);
    return this.get(url);
  },
  getSyncRoleCode(){
    let url = $.api($.apiModule.bluAfterSale, "/client/user/getSyncRoleCode");
    return this.get(url);
  },
  getAdminSiteByUid(){
    const url = $.api($.apiModule.bluAfterSale,'/client/user/getAdminSiteByUid');
    return this.get(url);
  },
  dsyncUser(data){
    const url = $.api($.apiModule.bluAfterSale,'/client/user/syncUser');
    return this.post(url, data);
  },
  // 售后人员管理__用户信息列表__导出
  exportUserRolePage(data) {
    const url = $.api($.apiModule.bluAfterSale, '/client/user/v1/exportUserRolePage');
    return this.post(url, data, {responseType: 'blob'});
  },
  // 售后人员管理__用户信息列表__保存用户角色
  basicSaveUserRole(data) {
    let url = $.api($.apiModule.bluAfterSale, "/client/user/v1/saveUserRole")
    return this.post(url, data);
  },
  // 售后人员管理__用户信息列表__出入库审批接口
  basicSaveUserApproval(userId, approveId) {
    let url = $.api($.apiModule.bluAfterSale, "/client/warehouseApprove/approve/addApproveUser?userId=" + userId + '&approveId=' + approveId)
    return this.get(url);
  },
  // 售后人员管理__用户信息列表__编辑用户角色
  basicEditUserRole(data) {
    let url = $.api($.apiModule.bluAfterSale, "/client/user/v1/editUserRole")
    return this.put(url, data);
  },
  // 售后人员管理__用户信息列表__详情
  basicGetUserDetail(data) {
    let url = $.api($.apiModule.bluAfterSale, "/client/user/v1/getUserDetailById?id=" + data)
    return this.get(url);
  },
  // 售后人员管理__根据站点+角色编码查询待同步用户
  getUserBySiteId(data) {
    let url = $.api($.apiModule.bluAfterSale, "/client/user/getUserBySiteId")
    return this.post(url,data);
  },
  getSiteUserByRole(data){
    let url = $.api($.apiModule.bluAfterSale, "/client/user/v1/getSiteUserByRole")
    return this.post(url,data);
  },
  //    =================== 售后人员管理__用户信息 end===============================================

  //    =================== 售后人员管理__角色信息 start===============================================
  // 售后人员管理__角色信息权限保存
  saveRoleAction(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/role/v1/saveRoleAction')
    return this.post(url, data);
  },
  // 售后人员管理__角色信息详情
  getRoleAction(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/role/v1/getRoleAction?roleId=' + data)
    return this.get(url);
  },
  // 售后人员管理__角色信息列表
  userRoleDataList(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/role/v1/getPage')
    return this.post(url, data);
  },
  // 售后人员管理__角色信息列表__新增
  saveRole(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/role/v1/saveRole')
    return this.post(url, data);
  },
  // 售后人员管理__角色信息列表__编辑
  editRole(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/role/v1/editRole')
    return this.put(url, data);
  },

  // 售后人员管理__角色信息列表__编辑
  deleteRole(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/role/v1/deleteRole?' + data)
    return this.delete(url);
  },
  // 售后人员管理__角色信息列表__启用/停用
  editRoleStatus(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/role/v1/editRoleStatus')
    return this.put(url, data);
  },
  //    =================== 售后人员管理__角色信息 end===============================================
  //    =================== SOP与流程设置 start===============================================
  // SOP与流程设置列表
  sopPage(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/sopNode/config/page')
    return this.post(url, data);
  },
  // SOP与流程设置列表__保存
  addSopNode(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/sopNode/config/addSopNode')
    return this.post(url, data);
  },
  // SOP与流程设置列表__编辑
  updateSopNode(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/sopNode/config/updateSopNode')
    return this.post(url, data);
  },
  // SOP与流程设置列表__删除数据
  sopNodeDelete(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/sopNode/config/delete/' + data)
    return this.delete(url);
  },
  // SOP与流程设置列表__是否启用
  sopUpdateStatus(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/sopNode/config/updateStatus?' + data)
    return this.get(url);
  },
  //    =================== SOP与流程设置 end=================================================


  //    =================== 物料使用管理 start===============================================
  //    物料使用管理-产品中心 物料使用管理-分页
  getProductBaseInfoList(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/product/baseInfo/pagedAndRepertory')
    return this.post(url, data);
  },
  //    物料使用管理-产品中心 根据料号获取SKU-分页
  getSkuByMaterialNos(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/product/baseInfo/getSkuByMaterialNos')
    return this.post(url, data);
  },
  // 物料入库-分页
  getMaterialStorageList(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/material/in/paged')
    return this.post(url, data);
  },
  // 物料入库-查看详情
  getMaterialStorageDetail(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/material/in/detail?id=' + data.id)
    return this.get(url);
  },
  // 物料入库-编辑
  getMaterialStorageEdit(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/material/in/edit')
    return this.put(url, data);
  },
  // 物料入库-直接入库
  getMaterialStorageWarehousing(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/material/in/warehousing')
    return this.post(url, data);
  },
  // 物料入库-作废
  getMaterialStorageCancel(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/material/in/cancel?id=' + data.id)
    return this.delete(url);
  },
  // 物料入库-列表搜索下拉数据获取
  getMaterialStorageSearchConfig(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/material/in/searchConfig')
    return this.get(url);
  },
  // 物料入库-恢复
  getMaterialStorageRestore(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/material/in/restore?id=' + data.id)
    return this.put(url);
  },
  //物料入库-直接入库和物流运输推送NS
  materialInPushNs(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/material/in/pushNs')
    return this.post(url, data);
  },
  //物料出库-直接入库推送NS
  materialOutPushNs(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/material/out/pushNs')
    return this.post(url, data);
  },
  // 物料入库-物流运输
  getTransportType() {
    let url = $.api($.apiModule.bluAfterSale, '/client/material/in/getTransportType')
    return this.get(url);
  },
  // 物料出库-查看可出库配件工单ID
  getApplyForOutAccessoryList() {
    let url = $.api($.apiModule.bluAfterSale, '/client/warehouse/accessoriesDelivery/getApplyForOutAccessoryList')
    return this.get(url);
  },

  // 物料出库-分页
  getMaterialStorageOutList(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/material/out/paged')
    return this.post(url, data);
  },
  // 物料出库-查看详情
  getMaterialStorageOutDetail(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/material/out/detail?id=' + data.id)
    return this.get(url);
  },
  // 物料出库-编辑
  getMaterialStorageOutEdit(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/material/out/edit')
    return this.put(url, data);
  },
  // 物料出库-直接出库
  getMaterialStorageOutWarehousing(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/material/out/outbound')
    return this.post(url, data);
  },
  // 物料出库-作废
  getMaterialStorageOutCancel(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/material/out/cancel?id=' + data.id)
    return this.delete(url);
  },
  // 物料出库-列表搜索下拉数据获取
  getMaterialStorageOutSearchConfig(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/material/out/searchConfig')
    return this.get(url);
  },
  // 物料出库-恢复
  getMaterialStorageOutRestore(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/material/out/restore?id=' + data.id)
    return this.put(url);
  },
  // 维修翻新--sn和id列表
  queryFieldsRepairRefurbish(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/repairAndRefurbishment/queryFieldsRepairRefurbish?type=' + data.type)
    return this.get(url);
  },
  // 维修翻新--根据维修翻新表id查询维修物料申请表中物料相关信息
  queryMaterialByRepairId(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/repairAndRefurbishment/queryMaterialByRepairId?repairRefurbishmentId=' + data.repairRefurbishmentId)
    return this.get(url);
  },
  getRepairResultStatus(data) {
    const url = $.api($.apiModule.bluAfterSale, '/client/repairAndRefurbishment/getRepairResultStatus');
    return this.post(url, data);
  },
  //翻新机入库--查询产品型号model列表
  refurbishmentInManagerModelList() {
    let url = $.api($.apiModule.bluAfterSale, '/client/refurbishmentManager/refurbishmentInManagerModelList')
    return this.get(url);
  },
  refurbishmentInManagerSkuSnList() {
    let url = $.api($.apiModule.bluAfterSale, '/client/refurbishmentManager/refurbishmentInManagerSkuSnList')
    return this.get(url);
  },
  refurbishOutOperationList(orderType, workOrderId) {
    let url = $.api($.apiModule.bluAfterSale, '/client/outOperation/getOutboundOperationProductList?orderType=' + orderType + '&workOrderId=' + workOrderId)
    return this.get(url);
  },
  //翻新机入库--查询sku列表
  refurbishmentInManagerSkuList() {
    let url = $.api($.apiModule.bluAfterSale, '/client/refurbishmentManager/refurbishmentInManagerSkuList')
    return this.get(url);
  },
  // 翻新机入库--根据产品sku查询sn列表
  refurbishmentInManagerSnListBySku(sku) {
    let url = $.api($.apiModule.bluAfterSale, '/client/refurbishmentManager/refurbishmentInManagerSnListBySku?sku=' + sku)
    return this.get(url);
  },
  // 翻新机批量入库
  batchAdditionRefurbish(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/refurbishmentManager/batchAdditionRefurbish')
    return this.post(url, data);
  },
  // 物料基本信息-分页-弹窗
  getBaseInfoList(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/material/baseInfo/selectPaged')
    return this.post(url, data);
  },
  // 物料基本信息录入-出入库选择物料信息
  baseInfoMaterialList(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/material/baseInfo/inAndOutMaterialList')
    return this.post(url, data);
  },
  // 领料用途设置-新增
  materialPurposeAdd(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/material/purpose/add')
    return this.post(url, data);
  },
  // 领料用途设置-启用停用
  materialPurposeEdit(data) {
    let url = $.api($.apiModule.bluAfterSale, `/client/material/purpose/edit`)
    return this.put(url, data);
  },
  // 领料用途设置-启用停用
  materialPurposeChangeEnable(data) {
    let url = $.api($.apiModule.bluAfterSale, `/client/material/purpose/changeEnable?enable=${data.enable}&id=${data.id}`)
    return this.put(url, data);
  },
  // 领料用途设置-删除
  materialPurposeDelete(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/material/purpose/delete?id=' + data.id)
    return this.delete(url);
  },
  // 领料用途设置-分页列表
  materialPurposeList(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/material/purpose/paged')
    return this.post(url, data);
  },
  //    =================== 物料使用管理 end=================================================


  //    =================== 翻新机出入库 start===============================================
  //翻新机出入库合二一列表
  pageRefurbishmentInOutList(data) {
    const url = $.api($.apiModule.bluAfterSale, '/client/refurbishmentManager/pageRefurbishmentInOutList')
    return this.post(url, data)
  },
  getRefurbishmentInList(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/refurbishmentManager/pageRefurbishmentInList')
    return this.post(url, data);
  },

  //翻新机入库详情
  getRefurbishmentInboundDetail(id) {
    let url = $.api($.apiModule.bluAfterSale, '/client/refurbishmentManager/refurbishmentInDetail?businessId=' + id)
    return this.get(url);
  },
  //翻新机批量入库详情
  refurbishmentInBatchDetail(id) {
    let url = $.api($.apiModule.bluAfterSale, '/client/refurbishmentManager/refurbishmentInBatchDetail?id=' + id)
    return this.get(url);
  },

  getRefurbishmentOutList(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/refurbishmentManager/pageRefurbishmentOutList')
    return this.post(url, data);
  },

  //    =================== 翻新机出入库 end===============================================
  //    =================== 入库检测维修 start===============================================

  // 入库检测--入库检测SOP作业
  wareHousingInspection(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/wareHousingInspection/client/wareHousingInspection')
    return this.post(url, data);
  },
  // 入库检测--入库检测SOP作业
  getSkuBusinessCategory(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/product/baseInfo/getSkuBusinessCategory?sku=' + data)
    return this.get(url, data);
  },

  //批量调拨入库的检测提交
  multipleInspection(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/wareHousingInspection/client/batchWareHousingInspection')
    return this.post(url, data);
  },
  afterSalleApproveList(data){
    const url = $.api($.apiModule.bluAfterSale,'/client/afterSalleApprove/page');
    return this.post(url,data);
  },
  afterSalleApprove(data){
    const url = $.api($.apiModule.bluAfterSale,'/client/afterSalleApprove/approve');
    return this.post(url,data);
  },
  // 入库检测表接口
  // 入库检测--入库检测列表
  pageWareHosingList(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/wareHousingInspection/pageWareHosingList')
    return this.post(url, data);
  },
  queryWareHousingModelSkuSn() {
    let url = $.api($.apiModule.bluAfterSale, '/client/wareHousingInspection/queryWareHousingModelSkuSn')
    return this.get(url);
  },
  queryWareHousingInspectionBySn(data) {
    let url = $.api($.apiModule.bluAfterSale, `/client/wareHousingInspection/queryWareHousingInspectionBySn?rmaNo=${data.rmaNo}&sn=${data.sn}`)
    return this.get(url);
  },
  // 入库检测--维修型号sku-sn
  queryRepairAndRefurbishModelSkuSn(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/repairAndRefurbishment/queryRepairAndRefurbishModelSkuSn?type=' + data)
    return this.get(url);
  },
  // 入库检测--维修/翻新第二步保存
  repairAndRefurbishJob(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/repairAndRefurbishment/repairAndRefurbishJob');
    return this.post(url, data);
  },
  //获取货物物权接口
  segmentationRealType(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/warehouse/segmentation/getRealType');
    return this.post(url, data);
  },
  // 入库检测--维修/翻新物料申领新增申请
  materialApplication(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/repairAndRefurbishment/materialApplication');
    return this.post(url, data);
  },
  // 入库检测--维修/翻新物料申领列表
  materialApplicationPage(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/repairAndRefurbishment/materialApplicationPage?repairRefurbishmentId=' + data)
    return this.get(url);
  },
  // 入库检测--物料申领-分页
  queryMaterialApplicationPage(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/repairAndRefurbishment/queryMaterialApplicationPage')
    return this.post(url, data);
  },
  // 入库检测--物料撤销申领
  cancelMaterialApplication(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/repairAndRefurbishment/cancelMaterialApplication?materialApplicationId=' + data)
    return this.post(url);
  },
  // 非wms站点 物料撤销
  cancelMaterialApplicationAndNs(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/repairAndRefurbishment/cancelMaterialApplicationAndNs?materialApplicationId=' + data)
    return this.post(url);
  },
  //入库检测--产品翻新表 用type区分维修还是翻新表
  pageRepairAndRefurbishmentList(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/repairAndRefurbishment/pageRepairAndRefurbishmentList')
    return this.post(url, data);
  },
  //    =================== 入库检测维修 end===============================================
  //    =================== 产品报废处理 start===============================================
  //产品报废回收--产品报废回收列表
  pageProductScrapRecycleList(data) {
    const url = $.api($.apiModule.bluAfterSale, '/client/productScrapRecycle/pageProductScrapRecycleList')
    return this.post(url, data)
  },
  //产品报废回收--物料回收撤销申请
  cancelMaterialRecyleApplication(data) {
    const url = $.api($.apiModule.bluAfterSale, '/client/productScrapRecycle/cancelMaterialRecyleApplication?materialRecyleApplicationId=' + data)
    return this.post(url, data)
  },
  cancelMaterialRecyleAll(data) {
    const url = $.api($.apiModule.bluAfterSale, '/client/productScrapRecycle/cancelByProductScrapRecycleId?productScrapRecycleId=' + data)
    return this.get(url)
  },
  //产品报废回收--物料回收撤销申请
  materialRecycleApplication(data) {
    const url = $.api($.apiModule.bluAfterSale, '/client/productScrapRecycle/materialRecycleApplication')
    return this.post(url, data)
  },
  //产品报废回收--物料回收申请列表
  materialRecyleApplicationList(id) {
    const url = $.api($.apiModule.bluAfterSale, '/client/productScrapRecycle/materialRecyleApplicationList?productScrapRecyleId=' + id)
    return this.get(url)
  },
  //产品报废回收--产品报废回收列表
  productScrapRecycle(data) {
    const url = $.api($.apiModule.bluAfterSale, '/client/productScrapRecycle/productScrapRecycle')
    return this.post(url, data)
  },
  // 产品报废回收--产品报废回收详情
  productScrapRecycleDetail(data) {
    const url = $.api($.apiModule.bluAfterSale, '/client/productScrapRecycle/productScrapRecycleDetail?productScrapRecycleId=' + data)
    return this.get(url)
  },
  //物料使用管理--物料出库中--产品报废物料回收rmaNo、id列表
  queryFieldsProductMaterialRecycle() {
    const url = $.api($.apiModule.bluAfterSale, '/client/productScrapRecycle/queryFieldsProductMaterialRecycle')
    return this.get(url)
  },
  //物料使用管理--物料出库中--通用接口：产品配件报废物料回收申请中列表
  queryMaterialRecycleByBizId(bizId) {
    const url = $.api($.apiModule.bluAfterSale, '/client/productScrapRecycle/queryMaterialRecycleByBizId?bizId=' + bizId)
    return this.get(url)
  },

  //    =================== 产品报废处理 end===============================================
  //    =================== 配件报废处理 start===============================================
  //物料使用管理--物料出库中--配件报废物料回收rmaNo、id列表
  queryFieldsAttachmentMaterialRecycle() {
    const url = $.api($.apiModule.bluAfterSale, '/client/attachmentScrapRecycle/queryFieldsAttachmentMaterialRecycle')
    return this.get(url)
  },
  //配件报废回收--配件报废回收列表
  pageAttachmentScrapRecycleList(data) {
    const url = $.api($.apiModule.bluAfterSale, '/client/attachmentScrapRecycle/pageAttachmentScrapRecycleList')
    return this.post(url, data)
  },
  //配件报废回收--配件物料回收申请
  attachmentMaterialRecycleApply(data) {
    const url = $.api($.apiModule.bluAfterSale, '/client/attachmentScrapRecycle/attachmentMaterialRecycleApply')
    return this.post(url, data)
  },
  //配件报废回收--配件报废回收SOP作业
  attachmentMaterialRecyleApplyList(data) {
    const url = $.api($.apiModule.bluAfterSale, '/client/attachmentScrapRecycle/attachmentMaterialRecyleApplyList?attachmentScrapRecycleId=' + data)
    return this.get(url)
  },
  //配件报废回收--配件报废回收SOP作业
  attachmentScrapRecycle(data) {
    const url = $.api($.apiModule.bluAfterSale, '/client/attachmentScrapRecycle/attachmentScrapRecycle')
    return this.post(url, data)
  },

  //配件报废回收--配件报废回收详情
  attachmentScrapRecycleDetail(data) {
    const url = $.api($.apiModule.bluAfterSale, '/client/attachmentScrapRecycle/attachmentScrapRecycleDetail?attachmentScrapRecycleId=' + data)
    return this.get(url)
  },
  //配件报废回收--物料回收撤销申请
  cancelAttachmentMaterialRecyleApply(data) {
    const url = $.api($.apiModule.bluAfterSale, '/client/attachmentScrapRecycle/cancelAttachmentMaterialRecyleApply?materialRecyleApplicationId=' + data)
    return this.post(url, data)
  },
  //    =================== 配件报废处理 end===============================================
  //    =================== 版本更新 start===============================================
  versionRecordList(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/site/versionRecord/v1/page')
    return this.post(url, data);
  },
  //    =================== 版本更新 end===============================================
  //=========================操作使用说 start===========================//
  //操作使用说明--操作使用说明列表
  pageOperationList(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/operation/pageOperationList')
    return this.post(url, data);
  },
  //操作使用说明--添加操作使用说明操作使用说明--操作使用说明列表
  createOperation(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/operation/createAndEditOperation')
    return this.post(url, data);
  },
  queryOperationById(operationId) {
    let url = $.api($.apiModule.bluAfterSale, '/client/operation/queryOperationById?operationId=' + operationId)
    return this.get(url);
  },
  // 操作使用说明--操作使用说明停用和删除二合一
  suspendAndDelete(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/operation/suspendAndDelete')
    return this.put(url, data);
  },
  //=========================操作使用说 end===========================//

  //=========================数据汇总 start===========================//
  //出库记录列表
  pageOutOperationList(data) {
    const url = $.api($.apiModule.bluAfterSale, '/client/siteDataCollectManager/pageOutOperationList')
    return this.post(url, data)
  },
  //退货入库列表
  pageRebackWareHouseList(data) {
    const url = $.api($.apiModule.bluAfterSale, '/client/siteDataCollectManager/pageRebackWareHouseList')
    return this.post(url, data)
  },
//=========================数据汇总 end===========================//
  //=========================仓库分仓 start===========================//
  // SKU调整
  segmentationWarehouseAdjust(data){
    const url = $.api($.apiModule.bluAfterSale,'/client/warehouse/segmentation/warehouseAdjust');
    return this.post(url, data);
  },
  partAdjustPage(data){
    const url = $.api($.apiModule.bluAfterSale,'/client/warehouse/segmentation/partAdjustPage');
    return this.post(url, data);
  },
  // 内调整分页列表
  warehouseAdjustPage(data){
    const url = $.api($.apiModule.bluAfterSale, '/client/warehouse/segmentation/warehouseAdjustPage');
    return this.post(url, data);
  },
  //导入最大报废数
  importMaxRecycleCount(data) {
    const url = $.api($.apiModule.bluAfterSale,'/client/warehouse/segmentation/importMaxRecycleCount');
    return this.post(url, data);
  },
  //仓库分页列表
  warehouseClassificationList(data) {
    const url = $.api($.apiModule.bluAfterSale, '/client/warehouse/segmentation/page')
    return this.post(url, data)
  },
  getInventoryBySku(data){
    const url = $.api($.apiModule.bluAfterSale, '/client/warehouse/segmentation/getInventoryBySKu')
    return this.post(url, data)
  },
  //仓库分仓库存分页列表
  skuInventoryPage(data) {
    const url = $.api($.apiModule.bluAfterSale, '/client/warehouse/segmentation/skuInventory/page')
    return this.post(url, data)
  },
  //配件库存(带非库存SKU)
  accessoriesPage(data) {
    const url = $.api($.apiModule.bluAfterSale, '/client/warehouse/segmentation/skuInventory/accessoriesPage')
    return this.post(url, data)
  },
  // 导出分仓
  segmentationExport(data) {
    const url = $.api($.apiModule.bluAfterSale, '/client/warehouse/segmentation/export')
    return this.post(url, data)
  },
  otherStorageTypes(group) {
    const url = $.api($.apiModule.bluAfterSale, '/client/refurbishmentManager/otherStorageTypes?group=' + group)
    return this.get(url)
  },
  //禁售列表
  pageBanSnList(data) {
    const url = $.api($.apiModule.bluAfterSale, '/client/banSn/pageBanSnList');
    return this.post(url, data);
  },
  //添加禁售
  pageBanSnAdd(data) {
    const url = $.api($.apiModule.bluAfterSale, '/client/banSn/add');
    return this.post(url, data);
  },
  //解除禁售
  pageBanSnRemove(data) {
    const url = $.api($.apiModule.bluAfterSale, '/client/banSn/remove');
    return this.post(url, data);
  },
  //在库良品仓库存明细列表
  pageBanInvSnList(data) {
    const url = $.api($.apiModule.bluAfterSale, '/client/warehouse/segmentation/pageUsable');
    return this.post(url, data);
  },
  //=========================仓库分仓 end===========================//
  //=========================app H5==================================//
  // 获取库存SKU详情
  getSkuDetails(id) {
    let url = $.api($.apiModule.bluWmsCenter, '/product/sku/findById?id=' + id)
    return this.get(url);
  },
  // 获取仓管用户站点
  getWarehouseSite() {
    let url = $.api($.apiModule.bluAfterSale, '/client/user/v1/getWarehouseSite')
    return this.get(url);
  },
  // 获取仓管用户站点详情
  getSiteById(id) {
    let url = $.api($.apiModule.bluAfterSale, '/client/site/v1/getSiteById?id=' + id)
    return this.get(url);
  },
  //=========================app H5 end==============================//
  //=========================出入库预报单==================================//
  //列表
  boundPreEntryPage(data) {
    const url = $.api($.apiModule.bluAfterSale, '/client/boundPreEntry/page')
    return this.post(url, data)
  },
  //详情
  boundPreEntryDetail(id) {
    const url = $.api($.apiModule.bluAfterSale, '/client/boundPreEntry/detail?id=' + id)
    return this.get(url)
  },
  //作废
  cancelPut(tranid) {
    const url = $.api($.apiModule.bluAfterSale, '/client/boundPreEntry/cancelPut?tranid=' + tranid)
    return this.get(url)
  },
  //预报单入库方式
  getBusinessType() {
    const url = $.api($.apiModule.bluAfterSale, '/client/boundPreEntry/getType')
    return this.get(url)
  },
  //
  // 编辑预报单
  editPreEntry(data) {
    const url = $.api($.apiModule.bluAfterSale, '/client/boundPreEntry/editPreEntry')
    return this.post(url, data)
  },
  // 预报单推送NS
  pushNSPreEntry(data) {
    const url = $.api($.apiModule.bluAfterSale, '/client/boundPreEntry/preEntryPushNs')
    return this.post(url, data);
  },
  transferRelation(data){
    const url = $.api($.apiModule.bluAfterSale, '/client/boundPreEntry/transferRelation')
    return this.post(url, data);
  },
  //=========================出入库预报单 end==================================//
  //=========================其他入库 ====================================//
  getOtherWarehouse(data) {
    const url = $.api($.apiModule.bluAfterSale, '/client/boundPreEntry/getOtherWarehouse')
    return this.post(url, data)
  },
  getWarehouseList(productType) {
    const url = $.api($.apiModule.bluAfterSale, '/client/boundPreEntry/getOtherWarehouseType?productType=' + productType)
    return this.get(url)
  },
  exportOtherWarehouse(data){
    const url = '/client/boundPreEntry/exportOtherWarehouse';
    const apiUrl = $.api($.apiModule.bluAfterSale,url);
    return this.post(apiUrl,data);
  },
  //=========================其他入库 end=================================//

  //=========================以旧换新待确认表==================================//
  getTradeInList(data) {
    const url = $.api($.apiModule.bluAfterSale, '/client/recycleOrder/getRecycleInspection')
    return this.post(url, data)
  },
  //=========================以旧换新待确认表 end==================================//


  //==================================新流程的接口==================================//

  supervisorApproval(data) {      //主管审批
    const url = $.api($.apiModule.bluAfterSale, '/voucherRecord/chargeVoucher')
    return this.post(url, data)
  },
  managerApproval(data) {      //经理审批
    const url = $.api($.apiModule.bluAfterSale, '/voucherRecord/managerVoucher')
    return this.post(url, data)
  },
  reissueExamine(data) {      //上传审核凭证 (钉钉审批流)
    const url = $.api($.apiModule.bluAfterSale, '/voucherRecord/uploadApprovalVoucher')
    return this.post(url, data)
  },
  // 28-上传审批凭证
  uploadPaymentVoucher(data){
    const url = $.api($.apiModule.bluAfterSale, '/voucherRecord/uploadPaymentVoucher')
    return this.post(url, data)
  },
  updateExpressLabel(data){
    const url = $.api($.apiModule.bluAfterSale, '/client/workOrder/postSale/updateExpressLabel')
    return this.post(url,data)
  },
  customerConfirmed(postSaleNo) {      //客服确认
    const url = $.api($.apiModule.bluAfterSale, '/client/workOrder/postSale/serviceConfirm?postSaleNo=' + postSaleNo)
    return this.post(url)
  },
  getPostSaleDetail(workOrderId) {     //流程记录
    const url = $.api($.apiModule.bluAfterSale, '/afterSalesWorkOrderOperationLog/getByWorkId?workOrderId=' + workOrderId)
    return this.get(url)
  },
  getFlowChart(workOrderId) {     //流程图
    const url = $.api($.apiModule.bluAfterSale, '/afterSalesWorkOrderOperationLog/getFlowChartByOrderId?workOrderId=' + workOrderId)
    return this.get(url)
  },
  supervisorVoucher(data) {        //主管上传退款凭证
    const url = $.api($.apiModule.bluAfterSale, '/voucherRecord/chargeUploadVoucher')
    return this.post(url, data)
  },
  changeLogistics(data) {        //修改物流商
    const url = $.api($.apiModule.bluAfterSale, '/client/workOrder/postSale/updateLogicInfo')
    return this.post(url, data)
  },
  changeShippingWarehouse(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/workOrder/postSale/changeShippingWarehouse')
    return this.post(url, data)
  },
  getShippingProductInfo(workOrderId) {
    let url = $.api($.apiModule.bluAfterSale, '/client/workOrder/postSale/getShippingProductInfo?workOrderId=' + workOrderId)
    return this.get(url)
  },
  //==================================新流程的接口 end==================================//

  //==================================自定义表头==================================//

  getCustomHeadList(defaultOrder) {        //获取工单展示列信息
    const url = $.api($.apiModule.bluAfterSale, '/customHead/list?defaultOrder=' + defaultOrder)
    return this.get(url)
  },
  saveCustomHead(data) {        //保存工单展示列信息
    const url = $.api($.apiModule.bluAfterSale, '/customHead/save')
    return this.post(url, data)
  },
  //==================================自定义表头 end==================================//

  getReplaceNewApproveList(data) {         //查询换翻待确认列表
    const url = $.api($.apiModule.bluAfterSale, '/client/warehouse/receipt/getReplaceNewApproveList')
    return this.post(url, data)
  },
  getOutboundOperationProductList(workOrderId, sn, orderType = 1) {         //出库sku下拉
    let path = '/client/outOperation/getOutboundOperationProductList?orderType=' + orderType + '&workOrderId=' + workOrderId
    if (sn) path += '&sn=' + sn
    const url = $.api($.apiModule.bluAfterSale, path)
    return this.get(url)
  },
  userMarkOrderReturn(workOrderId) {         //出库sku下拉
    let path = '/client/outOperation/returnOrder?workOrderId=' + workOrderId;
    const url = $.api($.apiModule.bluAfterSale, path);
    return this.get(url);
  },
  //取消发货
  closeOrderUsedMarket(tranId) {
    let path = '/client/outOperation/closeOrderUsedMarket?tranId=' + tranId;
    const url = $.api($.apiModule.bluAfterSale, path);
    return this.get(url);
  },
  getWorkOrderOutBoundList(workOrderId) {         //出库工单产品列表
    const url = $.api($.apiModule.bluAfterSale, '/client/outOperation/getWorkOrderOutBoundList?workOrderId=' + workOrderId)
    return this.get(url)
  },
  // 已出库列表
  getShippingList(data){
    const url = '/client/outOperation/getShippingList';
    const apiUrl = $.api($.apiModule.bluAfterSale,url);
    return this.post(apiUrl, data)

  },
  getWorkOrderOutBoundListForCrossSite(workOrderId){
    const url = $.api($.apiModule.bluAfterSale,'/client/outOperation/getWorkOrderOutBoundListForCrossSite?workOrderId=' + workOrderId);
    return this.get(url);
  },
  getReceiptList(data) {
    const url = $.api($.apiModule.bluAfterSale, '/client/warehouse/accessoriesDelivery/getUnClaim')
    return this.post(url, data)
  },
  getHasUploadList(rmaNo, sn) {
    const url = $.api($.apiModule.bluAfterSale, '/client/repairAndRefurbishment/queryInspectionAndRepairAndRefurbishBySn?rmaNo=' + rmaNo + '&sn=' + sn)
    return this.get(url)
  },
  getAllModel(data) {      //获取设备型号
    const url = $.api($.apiModule.BluSmartProd, '~/device/model/v1/list/page')
    return this.post(url, data)
  },
  addRecycleOrder(data) {
    const url = $.api($.apiModule.bluAfterSale, '/client/recycleOrder/addRecycleOrder')
    return this.post(url, data)
  },
  getTrandinInfo(rmaNo) {
    const apiUrl = $.api($.apiModule.bluAfterSale, '/client/recycleOrder/recycleOrderDetailByWeb?rmaNo=' + rmaNo);
    return this.get(apiUrl);
  },
  handleReceiveByWeb(data) {
    const apiUrl = $.api($.apiModule.bluAfterSale, '/client/recycleOrder/receiveByWeb');
    return this.post(apiUrl, data);
  },
  handleRejectByWeb(data) {
    const apiUrl = $.api($.apiModule.bluAfterSale, '/client/recycleOrder/rejectByWeb');
    return this.post(apiUrl, data);
  },
  getSourceByRma(rmaNo) {
    const url = $.api($.apiModule.bluAfterSale, '/client/recycleOrder/getSourceByRma?rmaNo=' + rmaNo)
    return this.get(url)
  },
  saveChangeAddress(data) { //保存修改发货地址
    const url = $.api($.apiModule.bluAfterSale, '/client/workOrder/postSale/updateShippingAddress')
    return this.post(url, data)
  },
  getQuotationInfo(rmaNo, sn) {     //查询维修报价
    let url = $.api($.apiModule.bluAfterSale, '/client/repairAndRefurbishment/getRepairCosts?rmaNo=' + rmaNo)
    if (sn) url += '&sn=' + sn
    return this.get(url)
  },
  submitQuotation(data) {
    const url = $.api($.apiModule.bluAfterSale, '/client/repairAndRefurbishment/saveRepairCosts')
    return this.post(url, data)
  },
  cancelRecycleWeb(rmaNo) {
    const url = $.api($.apiModule.bluAfterSale, '/client/recycleOrder/cancelRecycleWeb?rmaNo=' + rmaNo)
    return this.get(url)
  },
  getOrderRecord(orderNo) {
    const paramOrderNo = encodeURIComponent(orderNo)
    const url = $.api($.apiModule.bluAfterSale, '/client/workOrder/postSale/getOrderRecord?orderNo=' + paramOrderNo)
    console.log(url)
    return this.get(url)
  },
  materialRecovery(data) {
    const url = $.api($.apiModule.bluAfterSale, '/client/warehouse/segmentation/materialRecovery')
    return this.post(url, data)
  },
  materialRecoveryList(data) {
    const url = $.api($.apiModule.bluAfterSale, '/client/warehouse/segmentation/materialRecoveryList')
    return this.post(url, data)
  },
  //报废仓回收撤销
  cancelMaterialRecovery(data) {
    const url = $.api($.apiModule.bluAfterSale, '/client/warehouse/segmentation/cancelMaterialRecovery')
    return this.put(url, data)
  },
  scrapApplication(data) {
    const url = $.api($.apiModule.bluAfterSale, '/client/scrapOut/application/application')
    return this.post(url, data)
  },
  getOutboundList(data) {
    const url = $.api($.apiModule.bluAfterSale, '/client/scrapOut/application/page')
    return this.post(url, data)
  },
  getScrapDetail(id) {
    const url = $.api($.apiModule.bluAfterSale, '/client/scrapOut/application/detail?id=' + id)
    return this.get(url)
  },
  scrapRevocation(id) {
    const url = $.api($.apiModule.bluAfterSale, '/client/scrapOut/application/revocation?id=' + id)
    return this.get(url)
  },
  outboundConfirmation(data) {
    const url = $.api($.apiModule.bluAfterSale, '/client/scrapOut/application/outboundConfirmation')
    return this.post(url, data)
  },
  inventoryComparison(data) {  //库存对比
    const url = $.api($.apiModule.bluAfterSale, '/client/warehouse/segmentation/skuInventory/inventoryComparison')
    return this.post(url, data)
  },
  getHistoricalInventoryPage(data) {
    const url = $.api($.apiModule.bluAfterSale, '/client/warehouse/segmentation/skuInventory/getHistoricalInventoryPage')
    return this.post(url, data)
  },
  materialRecoveryNoPageList(data) {
    const url = $.api($.apiModule.bluAfterSale, '/client/warehouse/segmentation/materialRecoveryNoPageList')
    return this.post(url, data)
  },
  getMaxRecycleCount(data) {
    const url = $.api($.apiModule.bluAfterSale, '/client/warehouse/segmentation/getMaxRecycleCount')
    return this.post(url, data)
  },
  getPrintInfo(data) {        //id,type,sn,sku
    const url = $.api($.apiModule.bluAfterSale, '/client/warehouse/receipt/getPrintInfo?id=' + data.id + '&type=' + data.type + '&sn=' + data.sn + '&sku=' + data.sku)
    return this.get(url)
  },
  checkSubsidiary(data) {
    const url = $.api($.apiModule.bluAfterSale, '/client/ns/checkSubsidiary')
    return this.post(url, data)
  },
  receiptSnCheck(data) {
    const url = $.api($.apiModule.bluAfterSale, '/client/warehouse/receipt/receiptSnCheck')
    return this.post(url, data)
  },
  changeProductSn(data) {
    const url = $.api($.apiModule.bluAfterSale, '/client/workOrder/postSale/changeProductSn')
    return this.post(url, data)
  },
  bindReceipt(data) {      //待收件关联无人认领
    const url = $.api($.apiModule.bluAfterSale, '/client/workOrder/postSale/bindReceipt')
    return this.post(url, data)
  },
  warehouseApproveList(data) {      //出入库审批列表
    const url = $.api($.apiModule.bluAfterSale, '/client/warehouseApprove/approve/page')
    return this.post(url, data)
  },
  getAllLogisticsWorkOrder(data) {      //出入库审批列表
    const url = $.api($.apiModule.bluAfterSale, '/client/workOrder/logisticsPick/getAllLogisticsWorkOrder')
    return this.post(url, data)
  },
  getApproveDetail(approveId) {      //出入库审批列表
    const url = $.api($.apiModule.bluAfterSale, '/client/warehouseApprove/approve/approveDetail?approveId=' + approveId)
    return this.get(url)
  },
  warehouseSubmitApprove(data) {      //出入库审批列表
    const url = $.api($.apiModule.bluAfterSale, '/client/warehouseApprove/approve/approve')
    return this.post(url, data)
  },
  logisticsSure(data){
    const url = $.api($.apiModule.bluAfterSale,'/client/workOrder/logisticsPick/logisticsSure');
    return this.post(url, data);
  },
  closeOtherWorkOrder(id) {      //关闭其他需求工单
    const url = $.api($.apiModule.bluAfterSale, '/client/outOperation/closeOtherWorkOrder?id=' + id)
    return this.get(url)
  },
  getSkuPage(data) {      //出入库审批列表
    const url = $.api($.apiModule.bluAfterSale, '/client/warehouseApprove/approve/getSkuPage')
    return this.post(url, data)
  },
  adjustApprove(data) {        //盘盈盘亏申请
    const url = $.api($.apiModule.bluAfterSale, '/client/warehouseApprove/approve/adjustApprove')
    return this.post(url, data)
  },
  getSkuPageByWarehouse(data) {
    const url = $.api($.apiModule.bluAfterSale, '/client/warehouseApprove/approve/getSkuPageByWarehouse')
    return this.post(url, data)
  },
  getAddressByPostCode(code) {
    const url = $.api($.apiModule.bluAfterSale, '/client/ns/getAddressByPostCode?code=' + code)
    return this.get(url)
  },
  cancelApprove(approveId) {
    const url = $.api($.apiModule.bluAfterSale, '/client/warehouseApprove/approve/cancelApprove?approveId=' + approveId)
    return this.get(url)
  },
  //所有工单详情
  allWorkOrderDetail(id) {
    let url = $.api($.apiModule.bluAfterSale, '/client/workOrder/postSale/allWorkOrderDetail?postSaleNo=' + id)
    return this.get(url);
  },
  //所有可用站点列表
  getSiteList() {
    let url = $.api($.apiModule.bluAfterSale, '/client/site/v1/list')
    return this.post(url, {enable: 1});
  },
  changeSKU(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/workOrder/postSale/changeSKU')
    return this.post(url, data);
  },
  getBomVOList(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/bom/getBomVOList')
    return this.post(url, data);
  },
  getInspection(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/statistics/ticket/getInspection')
    return this.post(url, data);
  },
  getRepair(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/statistics/ticket/getRepair')
    return this.post(url, data);
  },
  getRepairMaterial(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/statistics/ticket/getRepairMaterial')
    return this.post(url, data);
  },
  getSegmentationByInAndOut(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/statistics/ticket/getSegmentationByInAndOut')
    return this.post(url, data);
  },
  getTicketByAfterSaleType(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/statistics/ticket/getTicketByAfterSaleType')
    return this.post(url, data);
  },
  getTicketByAfterStatus(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/statistics/ticket/getTicketByAfterStatus')
    return this.post(url, data);
  },
  getTicketNumber(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/statistics/ticket/getTicketNumber')
    return this.post(url, data);
  },
  noRmaChangeSku(data) {
    let url = $.api($.apiModule.bluAfterSale, '/client/warehouse/receipt/noRmaChangeSku')
    return this.post(url, data);
  },
  cancelNoRmaRelation(productId) {
    let url = $.api($.apiModule.bluAfterSale, `/client/warehouse/receipt/cancelNoRmaRelation?productId=${productId}`)
    return this.post(url);
  },
  bindReceipt(data) {
    let url = $.api($.apiModule.bluAfterSale, `/client/workOrder/postSale/bindReceipt`)
    return this.post(url, data);
  },
  //更新技术备注
  updateTechnologyRemark(data) {
    const url = $.api($.apiModule.bluAfterSale, '/client/workOrder/postSale/updateTechnologyRemark');
    return this.post(url, data);
  },
  saveRepairRemarkType(data) {
    let url = $.api($.apiModule.bluAfterSale, `/client/repairAndRefurbishment/saveRepairRemarkType`)
    return this.post(url, data);
  },
  // 仓库地址-新增
  addWarehouseAddress(data) {
    let url = $.api($.apiModule.bluAfterSale, `/client/baseConfig/warehouseAddress/add`)
    return this.post(url, data);
  },
  // 仓库地址-编辑
  editWarehouseAddress(data) {
    let url = $.api($.apiModule.bluAfterSale, `/client/baseConfig/warehouseAddress/edit`)
    return this.put(url, data);
  },
  // 仓库地址-删除
  deleteWarehouseAddress(data) {
    let url = $.api($.apiModule.bluAfterSale, `/client/baseConfig/warehouseAddress/delete?id=` + data.id)
    return this.delete(url);
  },
  // 仓库地址-启用/停用
  changeEnableWarehouseAddress(data) {
    let url = $.api($.apiModule.bluAfterSale, `/client/baseConfig/warehouseAddress/changeEnable`)
    return this.put(url, data);
  },
  // 仓库地址-列表(分页)
  warehouseAddressList(data) {
    let url = $.api($.apiModule.bluAfterSale, `/client/baseConfig/warehouseAddress/page`)
    return this.post(url, data);
  },
  warehouseAddressGetList() {
    let url = $.api($.apiModule.bluAfterSale, `/client/baseConfig/warehouseAddress/getList`)
    return this.post(url);
  },
  // 查询物流信息
  searchCollaboration(data) {
    let url = $.api($.apiModule.bluomsfcc, `~/logistics/collaboration/search`)
    return this.post(url, data);
  },
  // 关闭入库单
  closePreEntry(params) {
    const url = $.api($.apiModule.bluAfterSale, `/client/boundPreEntry/closePreEntry?tranid=${params.tranid}`)
    return this.get(url)
  },
  // 工时费用配置新增
  addHourlyRateConfig(params) {
    let url = $.api($.apiModule.bluAfterSale, `/client/hourlyRateConfig/add`)
    return this.post(url, params);
  },
  // 工时费用配置编辑
  editHourlyRateConfig(params) {
    let url = $.api($.apiModule.bluAfterSale, `/client/hourlyRateConfig/edit`)
    return this.post(url, params);
  },
  // 工时费用配置删除
  deleteHourlyRateConfig(params) {
    let url = $.api($.apiModule.bluAfterSale, `/client/hourlyRateConfig/del?id=${params.id}`)
    return this.delete(url);
  },
  // 工时费用列表分页查询
  hourlyRateConfigPage(params) {
    let url = $.api($.apiModule.bluAfterSale, `/client/hourlyRateConfig/page`)
    return this.post(url, params);
  },
  // 根据SKU查询物料价格
  getPriceBySku(data) {
    const url = $.api($.apiModule.bluAfterSale, `/client/repairAndRefurbishment/getPriceBySku`)
    return this.post(url, data)
  },
  // 异常收件
  errorReceiptAdd(data) {
    const url = $.api($.apiModule.bluAfterSale, `/client/warehouse/receipt/errorReceiptAdd`)
    return this.post(url, data)
  },
  //获取文件路径
  getFilesUrl(data) {
    const url = $.api($.apiModule.midpFileCenter, `~/file/v1/getFilesUrl`)
    return this.post(url, data)
  },
}

