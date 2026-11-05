# FedEx 运单追踪（实验功能）

在主界面最下方“实验功能”中打开“运单追踪”。左侧输入 FedEx 数字运单号，点击确定或按回车；重复输入同一运单号会刷新。列表为当前窗口会话数据，关闭后不保留。右侧 Mapsui 地图可拖动缩放，下方显示 FedEx 扫描记录。

运行前配置环境变量并重启应用：

- `FEDEX_CLIENT_ID`：FedEx Developer 项目的 API Key。
- `FEDEX_CLIENT_SECRET`：对应 Secret Key。
- `FEDEX_SANDBOX=true`：使用测试环境；默认使用正式环境。

凭据不写入源码或运单列表。项目必须具备 FedEx Track API 权限，其授权及费用以 FedEx 账户条款为准。

地图使用 OpenStreetMap 在线瓦片，需联网，并保留来源标注。地图只标记扫描记录中返回的有效经纬度；没有坐标时提示无法定位，不做城市名称推测，不代表实时 GPS 跟踪。

当前尚未使用真实 FedEx 凭据进行端到端查询验证。

参考：
- https://developer.fedex.com/api/en-us/catalog/track/v1/docs.html
- https://mapsui.com/v4/documentation/getting-started-wpf.html
