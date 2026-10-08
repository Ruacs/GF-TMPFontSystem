# GF-TMPFontSystem

GF Unity 工程的字体组件：语言字体加载、样式预设与材质缓存、TMP 文本样式应用、字符集/SDF 更新和编辑器实时预览。

## 适用范围与依赖

当前来源使用 Unity 2022.3.62f3c1、TextMeshPro 3.0.9；其他版本需验证 API 兼容性。本仓库按 Assets 子模块分发，代码保留现有类型、命名空间和 GUID。

- UnityGameFramework / GameFramework：GameFrameworkComponent、Resource、Localization.Language 等现有接口。
- Lokas.GameEntry：TMPFont、Localization、Resource 的访问和初始化；GameEntry.TMPFont 需指向已挂载的 TMPFontComponent。
- Lokas.AssetUtility.GetTMPFontAsset(string, bool)：解析字体资源路径。
- Lokas.UGuiForm.SetMainFont：更新 UI 主字体。
- TextMeshPro / TextCore：字体、材质与图集 API。

本仓库包含组件代码与工具。字体、图集、源字体、字符集 TXT、样式 SO、翻译 XML 和项目配置由目标工程提供；复制代码不会自动接通预加载或生成这些数据。

## 目录

```text
GF-TMPFontSystem/
├─ *.cs / *.cs.meta       六个运行时脚本，保留原文件名与 GUID
├─ Editor/
│  ├─ FontCharset/        字符收集、制作配置、SDF 同步窗口
│  ├─ Styles/             样式预览、Inspector 与材质转预设
│  └─ Tests/              字符集与预览验证
├─ Docs/                 模块手册与图集制作流程
├─ MIGRATION.md          旧版目录升级注意事项
└─ CHANGELOG.md
```

## GitHub 接入

将 `REPOSITORY_URL` 替换为你创建的 GF-TMPFontSystem 私有仓库 HTTPS 地址。首次接入从目标项目根目录执行，目标目录应为空且不被主仓库作为普通文件跟踪：

```sh
git submodule add -b main REPOSITORY_URL Assets/GameMain/CustomComponents/TMPFontSystem
git add .gitmodules Assets/GameMain/CustomComponents/TMPFontSystem
git commit -m "Add TMPFontSystem"
```

现有组件升级先阅读 [迁移说明](MIGRATION.md)，避免重复脚本/GUID。接入后等待 Unity 编译，确认上述框架接口存在，再设置 TMPFontComponent、预加载语言配置和资源收集。语言/字体配置不会因添加子模块自动装配。

## 工具入口与项目数据

- 字符集：Game Framework → 字体与字符集 → 打开窗口。
- 样式预览：同菜单 → 预览样式，或直接选中 FontStylePreset。
- 验证：同菜单 → 运行工具验证 / 运行样式预览验证；需要目标工程已有有效字体与制作配置。

制作配置默认读取 `Assets/AAA_DevAssets/Fonts/Editor/FontCharsetConfig.asset`。默认创建逻辑使用 MFont_CNS、CNT、JP、KR 四语言约定，需要对应字体和 Fonts 父目录；其他字体/语言通过配置条目接入。运行时语言配置、字符集、源字体和 XML 仍使用目标工程自己的引用。

详细步骤见 [模块手册](Docs/README.md) 和 [字符集与 SDF 制作](Docs/FontAuthoring.md)。

## 更新与维护

获取主项目记录的版本：

```sh
git submodule update --init --recursive
```

升级到组件 main 最新提交：

```sh
git submodule update --init --remote -- Assets/GameMain/CustomComponents/TMPFontSystem
```

先确认子模块没有未提交修改。升级后验证编译、工具和实际页面，再在主项目提交组件的新版本记录。要修改组件代码时，先在子模块创建工作分支并把提交推送到组件仓库；主项目提交只记录组件版本，不会代替组件仓库提交代码。

所有资源与 `.meta` 一起提交，不忽略 `.meta`，不重新生成现有脚本 GUID。组件根目录外的 `TMPFontSystem.meta` 由主项目维护；当前工程转换子模块时保留它。回退时在主项目恢复先前的组件提交记录，再运行 submodule update。

## 当前验证边界

初始版本取自 GF_Template 的现有代码。独立仓库的脚本内容和 GUID 已与来源核对；目标工程的加载、语言切换与页面样式需要接入后验证。文档里的来源配置与历史验收描述不代表目标项目运行通过。
