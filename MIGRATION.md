# 从分散目录升级

本仓库整理了三个来源：TMPFontSystem 的六个运行时脚本、AAA_DevAssets/Editor/FontCharset 工具和 GameMain/Editor 的三个 TMP 编辑器脚本。完整对应关系见 SOURCE_MANIFEST.json。

## 保留现有项目

1. 检查并备份本次涉及的脚本、meta、未提交修改和字体配置。先合并目标项目特有代码，不用仓库版本覆盖未知改动。
2. 通过 Unity AssetDatabase 将原字体编辑器代码归到组件的 Editor/FontCharset、Editor/Styles、Editor/Tests。六个运行时脚本仍在组件根目录。移动时保留原 .meta/GUID；旧位置不能留下同名类型或同 GUID 文件。
3. 更新项目文档/Skill 的旧工具路径。SOURCE_MANIFEST.json 提供源文件到仓库文件映射。字体配置、源字体、字符集、XML、Preset SO 和图集不随代码迁移。
4. 完整核对备份后，在关闭 Unity 的维护窗口中转换组件目录的 Git 跟踪方式：保留主项目里的 TMPFontSystem.meta，将普通文件跟踪替换为子模块 gitlink。现有目录非空或已被跟踪时，不直接运行 submodule add，不用强制命令跳过保护。
5. 同步源码与子模块的提交内容，再打开 Unity 检查编译、SO/Preset/场景引用、工具菜单和真实加载链。主项目提交 .gitmodules、组件 gitlink、原根目录 meta 及必要文档变更。

当前本地仓库准备只是复制和整理，来源工程尚未转换为子模块。主项目的转换、接入与运行验证是单独步骤；本仓库不提供删除原目录的一键脚本。

## 文档与数据

Docs 是组件自己的使用说明，来源项目的语言配置和资源路径转换为示例文本。新文档使用新的 meta，避免与主项目原来的框架手册 GUID 冲突；项目总导航可链接组件手册，避免长期维护两份全文。

SOURCE_MANIFEST.json 是初始提取记录；它不是之后每次修改的验收结果。后续发布使用 CHANGELOG 和 Git 提交/版本标签记录改动。
