jQuery.extend({
    //日期 + 每天发版次数
    version: "20261013_01",
    appTitle: "BLUETTI After-sales Service",
    apiClient: "18880A4BA6CF791157564C2ECD0",//app-key
    appLoginUrl:'/login.html#/',
    appLoginIndex:'/afterSaleClient/refactorWorkOrder/refactorCustomerWorkOrder/client/afterSaleWorkOrder/route/v2/index',
    apiScriptAssets: {
        vueI18n: {
            name: "", thirdLib: true,
            script: "/webjars/plugins/vue-i18n/8.x/vue-i18n.min.js",
            style: ""
        },
        // JS小数精度支持
        bigNumber: {
            name: "", thirdLib: true,
            script: "/webjars/plugins/big-number.min.js", style: ""
        },
        es6Promise: {
            name: "",
            thirdLib: true,
            script: "/webjars/plugins/aliyun/es6-promise.min.js",
            style: ""
        },
        aliyunOss: {
            name: "",
            thirdLib: true,
            script: "/webjars/plugins/aliyun/aliyun-oss-sdk-6.17.1.min.js",
            style: ""
        },
        aliyunUpload: {
            name: "",
            thirdLib: true,
            script: "/webjars/plugins/aliyun/aliyun-upload-sdk-1.5.5.min.js",
            style: ""
        },
        common: {
            name: "",
            thirdLib: false,
            script: "/assets/utils/utils.js",
            style: ""
        },
        storeUtils: {
            name: "",
            thirdLib: false,
            script: "/assets/utils/storeUtils.js",
            style: ""
        },
        echarts: {
            name: "", thirdLib: true,
            script: "/webjars/plugins/echarts/echarts.min.js",
            style: ""
        },
        utilsApi: {
            name: "",
            thirdLib: false,
            script: "/assets/utils/api.js",
            style: ""
        },
         permission: {
            name: "",
            thirdLib: false,
            script: "/assets/utils/permission.js",
            style: "",
        },
        wangEditor: {
            name: "wangEditor",
            thirdLib: true,
            script: "/assets/libs/wangeditor/index.js",
            style: "/assets/libs/wangeditor/style.css"
        },
        mixin: {
            name: '', thirdLib: false,
            script: '/assets/scripts/mixin.js',
            style: ''
        },
        component: {
            name: '', thirdLib: false,
            script: '/assets/scripts/commonComponents.js',
            style: ''
        }
    },
    apiStyleAssets: [
        '/assets/style/fonts/iconfont.css',
    // "/webjars/layout/bluetti/font/bluetti-icon.css",
    ],
    apiImageAssets: {
        bluettiLogo: "/assets/image/bluettiLogo.png",
        bluettiTitle: "/assets/image/bluettiTitle.png",
        defaultUserIcon: '/assets/image/default-user-icon.png',
    },
    gwcredentialsCode:{
        register:'gwcredentials=zwAzq1H%2Bsg%2Fgr%2F8sfanJdRoHqGUUisKhqbKIJal8KSbt4iHJ%2FenC5QTeRvADbDeYM2fnVJBDNA0%3D',
        send:'gwcredentials=07my66HC6BT4ZUFUXKqAlBzR51sxPI1PHMsN6cL%2FPJRUXCUv93ak5%2FwjG8fSTOcn',
        validateByPassword:'gwcredentials=gVaRTCT6Ac6cfJLyv0ZnLmoSGYh6bvzaLpDAASGnCzqq1qB8%2B51B1%2F%2Be9Cba7qzY7kM3GibCT8ZcsEKxZQWQgpA88jQgDY2r',
        forget:'gwcredentials=hlUtY27GlBTdpuAnghK%2FX2tvk1nk5vHEu%2Bl7omTZPUpBM%2BzeV%2FhI1iUahvQ7owfPX9NLw0cq3Xs%3D',
        country:'gwcredentials=nT1smQNg3oP1MmAoOyjmb0%2BzPdTK9qx4q8BCJlGuE5JYyyJghc%2BaQRLDFaojOsrFh6WI5vqLusoDEGzP2JUmMAHqxbkYs0nCfcoFbHijaBE%3D',
    },
    apiModule: {
        midpAuthCenter: {
            attr: "MidpAuthc",
            port: 20003
        },
        bluFinanceCenter: {
            attr: "BluFiC",
            port: 20106
        },
        midpFileCenter: {
            attr: "MidpFileC",
            port: 20004
        },
        midpNotifyCenter: {
            attr: "MidpNc",
            port: 20005
        },
        bluAfterSale: {
            attr: "BluAs",
            port: 20102
        },
        midpMasterData: {
            attr: "MidpMdata",
            port: 20001
        },
        midplifecAear:{
            attr:'midplifec',
            port:20006
        },
        userAddressbluuc:{
            attr:'bluuc',
            port:20101
        },
        //仓储中心
        bluWmsCenter: {
            attr: "BluWmsC",
            port: 20107
        },
        bluUserCenter: {
            attr: "BluUc",
            port: 20101
        },
        BluSmartProd: {
            attr: "BluSmartProd",
            port: 20100
        },
        //营销中心
        bluMarketingCenter: {
            attr: "BluMktC",
            port:20109
        },
        bluProdCenter: {
            attr: "BluProdc",
            port: 20226
        },
        bluomsfcc: {
            attr: "bluomsfcc",
            port: 20304
        }
    }
});
