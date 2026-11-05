requirejs.config({
    baseUrl: "",
    paths: {
        'lang_zhCN': "/assets/scripts/lang/zh-CN",
        'lang_en': "/assets/scripts/lang/en",
        'lang_de': "/assets/scripts/lang/de",
        'lang_ja': "/assets/scripts/lang/ja",

        // ElementUI国际化
        'element/locale/zh-CN': "/webjars/plugins/element-ui/lib/umd/locale/zh-CN",
        'element/locale/en': "/webjars/plugins/element-ui/lib/umd/locale/en",
        'element/locale/de': "/webjars/plugins/element-ui/lib/umd/locale/de",
        'element/locale/ja': "/webjars/plugins/element-ui/lib/umd/locale/ja",
    }
});

require(["/index.js"]);