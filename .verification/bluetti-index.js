define([
    "lang_zhCN",
    "lang_en",
    "lang_de",
    "lang_ja",
    "element/locale/zh-CN",
    "element/locale/en",
    "element/locale/de",
    "element/locale/ja"
], function (lang_zhCN,
             lang_en,
             lang_de,
             lang_ja,
             elui_zhCN,
             elui_en,
             elui_de,
             elui_ja) {
    // Create VueI18n instance with options
    const i18n = new VueI18n({
        // via `this.$i18n.locale` to change user's language
        // Set the default language, will be changed it when user login.
        locale: win.SsoUtil.getUserLang() ? win.SsoUtil.getUserLang() : 'en',
        messages: {
            'zh-CN': {...lang_zhCN, ...elui_zhCN},
            'en': {...lang_en, ...elui_en},
            'de': {...lang_de, ...elui_de},
            'ja': {...lang_ja, ...elui_ja},
        }
    });
    /**
     * 判断是否有操作权限
     * @param id 操作id
     * @returns {boolean}
     */
    Vue.prototype.$isHasPermission = function(id) {
        const isViewDetail = win.PermissionUtil.isHasPermission(id);
        if (!isViewDetail) {
            const tipText = '暂无无权限访问！';
            this.$message.error(tipText);
            return false;
        }
        return true;
    }
    Vue.prototype.$bus = new Vue();
    // 自定义指令-按钮权限控制
    win.PermissionUtil.initDirective();
    // Set user info api url
    window.framework.layout.UD = 'up-down';
    if(isMixin) Vue.mixin(mixin);
    // 设置element ui的tooltip打开延时默认时长
    window['ELEMENT'].Tooltip.props.openDelay.default = 300;
    // Create a VUE instance
    new Vue({
        el: '#app',
        // mount vue i18n
        i18n,
        // 注入路由配置
        router: appRouter,
        store,
    });
    // Set element ui locale
    for (let key in i18n.messages) {
        if (key === i18n.locale) {
            window['ELEMENT'].locale({el: i18n.messages[key].el});
            break;
        }
    }
    if(!win.SsoUtil.getAccessToken()){
        window.location.href = '/login.html#/';
    }
    /**
     * 框架回调业务，框架流程执行完成后会执行下面定义的业务方法
     * @type {((function(): void)|*)[]}
     */
    window.framework.callbacks = [
        function () {
            const reg = /\/?site|login\.html/;
            const isInSystem = reg.test(location.pathname);
            const siteId = win.StorageUtil.sessionStorageGet('resource');
            if (isInSystem || !siteId) return;
            win.PermissionUtil.getPermissionList(siteId);
            //判断用户是否登录
            if (!win.SsoUtil.getAccessToken()) {
                window.location.href = '/login.html#/';
            }
        }
    ]


    /**
     * 初始化vuex的数据，比如字典数据
     */
    function getStoreData(){
        const siteInfo = win.StorageUtil.sessionStorageGet('siteInfo');
        if(win.SsoUtil.getAccessToken() && siteInfo){
            store.dispatch('getUserMenu');           //获取用户菜单(也用作权限列表)
            store.dispatch('getUserSiteBacklogNum');     //获取站点待处理工单的数量
        }else{
            setTimeout(()=>{
                getStoreData();
            },50)
        }

    }
    getStoreData();

    /**
     * 全局定义表格分页条数
     * 根据屏幕大小动态调整分页条数
     */
    window.framework.layout.PAGE_SIZE = 30;
    const bodyHeight = document.body.offsetHeight;
    if (bodyHeight > 1000) {
        window.framework.layout.PAGE_SIZE = 30;
    } else if (bodyHeight < 1000 && bodyHeight >= 910) {
        window.framework.layout.PAGE_SIZE = 25;
    } else if (bodyHeight < 910) {
        window.framework.layout.PAGE_SIZE = 20;
    }
});
