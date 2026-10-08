# 从 Assets / 子模块版本迁移到 UPM 0.2.0

## 影响范围

六个运行时脚本移入 Runtime，编辑器代码留在 Editor；增加包清单与两份 asmdef。既有脚本 GUID、类型名和序列化字段保持不变，FontCharsetConfig 的默认数据路径不变。接口调用新增 Configure(ITMPFontHost)，用于替代包对项目代码的直接调用。

## 迁移步骤

1. 检查现有组件改动并备份脚本/meta、GameEntry 初始化、Packages/manifest.json 与 packages-lock.json。字体、图集、Preset、字符集及翻译继续由项目维护。
2. 合并目标项目对 TMPFontSystem 的有效修改，确保包中的既有脚本 GUID 与目标资源引用一致。旧文件位置参考 Documentation~/InitialSourceManifest.json。
3. 在 Unity 暂停导入的维护步骤中，将旧的运行时脚本、AAA_DevAssets/Editor/FontCharset、三个散落的 TMP Editor 脚本通过 AssetDatabase 移至本次专用备份目录，保留 meta；让备份目录以 ~ 结尾，不参与导入。不要让旧 Assets 源码与 UPM 包同时编译，也不要复制丢失 meta 的旧脚本。
4. 导入 GF Template Integration 样例，或安装项目自己的 ITMPFontHost 与 Editor 路径适配。取到 TMPFontComponent 后先 Configure，再保持原预加载的 SetLanguageConfig 调用。
5. 添加本地 package.json 或指定 Git 版本；等待包解析、编译完成，确认 MonoScript 路径、场景/SO 引用和工具菜单正确。首次先本地验证，再发布/安装 Git 标签。
6. 验证扫描、预览及接入协议，再在 PlayMode 验证真实字体加载与页面显示。完成后提交主工程的接入代码、包依赖、锁定文件和必要文档变化。

已使用 Git submodule 的项目需要先保存/提交子模块修改，再解除该子模块的项目跟踪并保留备份；UPM 安装与旧子模块不能同时保留为可编译源码。私有仓库需要目标机器具备 Git 访问权限。

## 恢复

未完成迁移时停止字体更新。移除本次 UPM 依赖，恢复原 manifest/锁定文件与 GameEntry 初始化，再通过 Unity 将原脚本从专用备份移回原位置，保留 GUID。先排除重复脚本和 GUID，再刷新编译；不重建字体或配置来修复代码安装问题。

包不提供删除原组件的一键脚本。其他项目的目录、ID、资源规则和特有接入由该项目维护；来源工程验证不代表目标工程已经验收通过。
