/**
 * 全局UI资源包含部署定义，指定要加载哪些公共资源
 * @Profile dev
 * @Author 许亮
 * @Mailto xht555@163.com
 * @Create 2020-5-31 0:41:30
 */
let UiAssets = window.UiAssets || {};
UiAssets.Include = (function() {
    /**
     * framework styles
     * @type {string[]}
     */
    let cssAssets = [
        "/webjars/plugins/element-ui/lib/theme-chalk/index.css",
        // 项目全局CSS，以覆盖三方插件中的CSS属性设置
        // 如果有，则在下放添加
        // "/webjars/global/main.css"
    ];

    /**
     * 3rd scripts
     * @type {string[]}
     */
    let jsThirdAssets = [
        "/webjars/plugins/jquery/jquery-cookie-1.4.1.js",
        "/webjars/plugins/axios/axios.js",
        "/webjars/plugins/vue/vue.js",
        "/webjars/plugins/vue-loader/httpVueLoader.js",
        "/webjars/plugins/vue-router/vue-router.js",
        "/webjars/plugins/element-ui/lib/index.js"
    ];

    /**
     * framework scripts
     * @type {string[]}
     */
    let jsFrameworkAssets = [
        "/webjars/global/env.js",
        "/webjars/global/main.js",
        // 下述的全局组件JS务必放在vue.js之后，需要向VUE对象中注册全局公共组件
        "/webjars/layout/layout.js",
        "/webjars/layout/header.js",
        "/webjars/layout/sidebar.js",
    ]

    /**
     * 导入公用的CSS
     */
    let includeCss = function() {
        for (let key in $.apiScriptAssets) {
            if ($.apiScriptAssets[key].style !== "") {
                cssAssets.push($.apiScriptAssets[key].style);
            }
        }
        for (let css in $.apiStyleAssets) {
            cssAssets.push($.apiStyleAssets[css]);
        }

        let head = document.head || document.getElementsByTagName("head")[0];
        for (let i = 0; i < cssAssets.length; i++) {
            head.appendChild(createStyle(cssAssets[i]));
        }
    }

    /**
     * 创建CSS元素
     * @param cssUrl
     * @return {HTMLLinkElement}
     */
    let createStyle = function(cssUrl) {
        let link = document.createElement("link");
        link.rel = "stylesheet";
        link.type = "text/css";
        link.href = cssUrl;
        return link;
    }

    /**
     * 导入公用的js
     */
    let includeJs = function() {
        for (let key in $.apiScriptAssets) {
            if ($.apiScriptAssets[key].thirdLib) {
                jsThirdAssets.push($.apiScriptAssets[key].script)
            } else {
                jsFrameworkAssets.push($.apiScriptAssets[key].script);
            }
        }

        let jsAssets = jsThirdAssets.concat(jsFrameworkAssets);
        for (let i = 0; i < jsAssets.length; i++) {
            document.write("<script type='text/javascript' src='" + jsAssets[i] + "'></script>");
        }
    };

    /**
     * 导入各服务API接口
     * @param apiModules
     */
    let includeApiJs = function (apiModules) {
        // let apiJs;
        // for (let key in apiModules) {
        //     apiJs = "/" + apiModules[key].attr + "/api.js";
        //     document.write("<script type='text/javascript' src='" + apiJs + "'></script>");
        // }
    };

    let deploy = function() {
        includeJs();
        includeCss();
    };

    return {
        uiDeploy: deploy,
        apiDeploy: includeApiJs
    }
}());

// 执行全局UI资源包含部署
UiAssets.Include.uiDeploy();