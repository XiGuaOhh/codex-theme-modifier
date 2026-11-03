# 工作原理与维护

## 文件与启动

发行包是单个 Windows 程序，内嵌本项目的主题脚本、启动器和维护程序。安装时仅将这些文件写入当前用户的 `CodexCustomTheme` 目录，创建自己的快捷方式和卸载登记。不复制官方应用，也不修改 `WindowsApps`、ASAR、应用签名或官方更新服务。

主题入口通过 Windows `IApplicationActivationManager` 启动已注册的 `OpenAI.Codex` 应用，每次从当前包清单解析主应用和可执行文件。关闭主题时，入口回退到 `shell:AppsFolder` 的官方注册入口。

只有已归属于旧版加载器的快捷方式才会被升级接管，普通官方入口不被替换。卸载只处理登记的已知快捷方式；如果用户后来改变了快捷方式目标，则保留它。恢复官方快捷方式使用应用的 Shell PIDL，避免绑定会随更新变化的版本目录。

## 外观加载

启动时启用随机本机端口：`--remote-debugging-port=0 --remote-debugging-address=127.0.0.1`。加载器核对端口的进程、官方包身份、当前可执行文件路径以及 `DevToolsActivePort` 中的浏览器会话标识，再建立连接。

只允许 `app://-/index.html` 与 `app://-/detached-window.html` 的页面目标，并排除头像浮层路由。主题仅追加自己的样式、独立 Shadow DOM 控件、CSS 变量和相应事件监听。没有网络请求，没有替换原生 `fetch`、WebSocket、存储 API 或 `electronBridge`。

调试连接本身不带本项目额外设计的认证，同一台电脑上的其他进程可能接入，因此不能将它视为安全隔离机制。软件不会远程开放此端口。关闭后外观可以即时移除；退出并重开 Codex 才能移除原启动参数和对应端口。

## 设置与图片

修改器的文件设置位于 `preferences/theme.json`，所选图片副本位于 `preferences/background.bin`。文件只用于本机。

Codex 页面中只操作本项目的这些存储项：

- localStorage：`codex.custom-background.settings.v1`
- localStorage：`codex.custom-background.image-revision.v1`
- localStorage：`codex.custom-background.manager-revision.v1`
- IndexedDB：`codex-custom-background-v1`，对象存储 `images`，键 `background`

图片在写入前通过浏览器解码检查，不以文件名或字符串形式执行。保存采用独立修订号，已经应用的文件设置不会在每次启动时覆盖后来从应用内调色盘更换的背景。卸载只有在用户选择删除外观设置时才清除这些项，不清除整个浏览器资料目录。

## 关闭、卸载、失败处理

关闭主题先通知加载器，允许它撤销已登记的新文档脚本、样式和事件监听。超时只停止经路径、进程和命令行验证为本项目的加载器，不停止 Codex。旧版 1.x 已运行的窗口可能需要正常重开才能完全移除旧事件监听。

维护程序对固定的当前用户安装路径、产品标识、安装实例标识进行检查，只删除内置的已知文件名单。目录链接被拒绝；未知文件、旧备份和官方用户资料保留，不递归清空安装目录。

卸载时默认留下 `preferences` 供重新安装使用，也可明确选择删除背景与外观设置。删除后只保留一个很小的重置标记，防止下次安装时重新读入旧的应用内背景；无法连接 Codex 时不会直接改写它的资料数据库。源目录中未登记的旧备份不会因此被删除。运行中的 Codex 会保留；无需结束工作来更新修改器。

## 修改器自身更新

仅查询 `XiGuaOhh/codex-theme-modifier` 的最新非预发布 Release。固定资产名为 `CodexThemeModifier.exe`。检查版本号、大小、GitHub Release 资产 `digest` 中的 SHA-256，并限制 HTTPS 下载与重定向域名。下载完整后再次核对哈希和程序集名称，验证失败不会执行文件。

维护程序等待修改器退出后原子替换，替换失败保留旧文件；随后打开新版控制台，重新部署新加载器。没有强制结束 Codex 的步骤。校验依赖 GitHub 和仓库发布权限，不替代代码签名或对 GitHub 账号的保护。

网络使用系统代理和 HTTPS 证书验证。GitHub 不可达、未发布资产或缺少校验值时，显示提示并保留现有功能。软件不内置镜像或网络代理。

参考：[GitHub Release API](https://docs.github.com/en/rest/releases/releases)、[Release 资产校验值](https://github.blog/changelog/2025-06-03-releases-now-expose-digests-for-release-assets/)、[Microsoft IShellLinkW::SetIDList](https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nf-shobjidl_core-ishelllinkw-setidlist)。
