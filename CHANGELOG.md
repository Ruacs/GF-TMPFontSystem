# Changelog

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
