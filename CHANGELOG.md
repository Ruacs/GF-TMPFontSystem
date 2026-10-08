# Changelog

## 0.3.2 — 2026-10-08

- 按目标 TMPStyleApplier 保留参数面板展开状态和样式草稿，避免 Inspector 重建时自动折叠并丢失修改。
- Inspector 临时重建复用编辑状态，真正切换/关闭时延迟清理；程序集重载仍释放临时对象。
- 增加连续十次重建 Inspector 的展开状态与草稿保留验证。

## 0.3.1 — 2026-10-08

- 修复 TMPStyleApplier 草稿使用 HideAndDontSave 时同时带有 NotEditable，导致 Inspector 参数变灰、无法调整。
- 草稿仍不保存为资产，保存/另存为与预览隔离方式不变；增加参数可编辑验证。

## 0.3.0 — 2026-10-08

- TMPStyleApplier 在 Scene / Prefab 编辑态直接预览当前字体的 Face、描边和阴影，支持 UGUI 遮罩与 fallback 子网格。
- Inspector 内可调整样式草稿：保存更新共享预设；另存为创建独立预设、登记新 Key 并绑定当前文本。
- 草稿只影响当前选中对象；不替换已保存的 TMP 字体/材质引用，不修改字体图集。
- 可指定编辑态预览 Profile，冲突或缺失 Key 会提示；进入 Play Mode、停用组件与关闭预览时恢复。
- Refresh 不再保存整个项目；修复缺失样式 Key 无法从下拉框重新选择的问题。
- Canvas 重绘只复用预览材质；网格仅在文字/字体/参数变化时刷新。缓存组件列表并跳过隐藏 UI，避免选中组件时反复重建页面文字。
- 增加页面预览验证菜单，验证渲染像素、草稿隔离、保存/另存为、撤销、遮罩、字体/fallback 与 Prefab 保存/加载。

## 0.2.0 — 2026-10-08

- 转为 UPM 包 com.ruacs.tmpfont-system，划分 Runtime/Editor 程序集。
- 保留原有类型、序列化字段与脚本 GUID。
- 新增 ITMPFontHost 与 FontCharsetProjectBridge；项目接入层提供语言、资源路径、加载和 UI 主字体同步。
- 包附带 GF Template Integration 样例和接入协议验证。
- 正式字体、字符集、语言配置和样式资源仍由项目维护。

## 0.1.0 — 2026-10-08

- 从 GF_Template 提取六个运行时脚本和十一个字体编辑器/验证脚本，保留脚本 GUID 和内容。
- 字符集工具支持已有 Static SDF 更新、自动采样字号、图集尺寸选择、自动保存、备份与恢复。
- 样式 Inspector 和字体窗口支持预设实时预览、默认材质对照、字号/背景设置和复制预设。
- 附带独立接入、目录迁移与使用说明；正式字体和项目配置由目标工程维护。
