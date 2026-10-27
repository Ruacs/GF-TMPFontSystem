# GF TMPFont System

GF 框架的 UPM 字体组件：语言字体加载、样式预设与材质缓存、TMP 样式应用、字符集/SDF 同步及实时样式预览。

包名：`com.ruacs.tmpfont-system`。版本：`0.3.2`。适用 Unity 2022.3，依赖 TextMeshPro 3.0.9 与 UGUI 1.0.0。

## 安装

先确保目标工程已有 `GameFramework` 和 `UnityGameFramework.Runtime` 两个程序集。它们是外部框架依赖，本包不会自动复制 GF 或业务代码。

在 Window → Package Manager → ＋ → Add package from git URL 输入：

```text
https://github.com/Ruacs/GF-TMPFontSystem.git#v0.3.2
```

开发时可通过 Add package from disk 选择本地仓库的 `package.json`，或使用不带版本标签的 Git URL。私有仓库需提前配置 Git 凭据/SSH；UPM 不会弹出交互登录。正式工程固定版本标签，升级时改为新的标签并验证。

已有 Assets 版本时先阅读 [迁移说明](MIGRATION.md)，不要把两份同名脚本同时安装。Git 安装的包位于 Packages/包缓存，字体、图集、源 TTF/OTF、字符集、样式预设和语言配置仍由目标工程维护在 Assets 中。

## 项目接入

包内部通过 `ITMPFontHost` 获取当前语言、解析路径、加载字体和同步 UI 主字体；不直接依赖项目的 Lokas.GameEntry、AssetUtility 或 UGuiForm。

1. 在 Package Manager 的 Samples 中导入 **GF Template Integration**。样例包含项目侧运行时 host 和 Editor 路径接入，使用 Lokas 命名空间；其他 GF 项目按自己已有入口适配。已有接入脚本时对照合并，不重复导入。
2. 保留 Launcher 上已有 TMPFontComponent。GameEntry.InitCustomComponents 在取到组件后配置 host：

```csharp
TMPFont = UnityGameFramework.Runtime.GameEntry.GetComponent<TMPFontComponent>();
TMPFont?.Configure(new TMPFontProjectHost());
```

3. Editor 接入脚本通过 InitializeOnLoad 设置 `FontCharsetProjectBridge.FontAssetPathResolver`，使工具和运行时使用相同字体路径规则。缺接入时工具会明确报错，不猜测资源路径。
4. 继续由现有预加载流程调用 `SetLanguageConfig`，保留字体、语言配置、资源收集及 UI 页面引用。TMPStyleApplier 通过 GF 的组件查询获取 TMPFontComponent，无须另建 Manager 或 Singleton。

现有类型名、字段名和脚本 GUID 保持不变；编辑器与运行时现分属 Ruacs.GF.TMPFont.Editor、Ruacs.GF.TMPFont.Runtime 程序集。接口配置不替代实际语言切换或预加载。

## 使用

- 字符集工具：Game Framework → 字体与字符集 → 打开窗口。
- 样式预览：同菜单 → 预览样式，或选中 FontStylePreset，在 Inspector 调整参数、字号和背景。
- 页面内调样式：选中 TMPStyleApplier，选择 Style Key，在“样式参数（草稿）”直接调整。保存更新当前共享预设；另存为新样式创建独立预设和 Key，再保存 Prefab / 场景以保留绑定。编辑态预览默认开启，Profile 选择仅用于编辑器，不改变运行时语言配置。
- 默认制作配置：`Assets/AAA_DevAssets/Fonts/Editor/FontCharsetConfig.asset`。默认创建使用 MFont_CNS/CNT/JP/KR 四语言约定；真实数据和配置留在项目中。
- 自动化：扫描全部、更新选中语言、恢复未完成更新。检查结果报告，不把菜单执行成功当作生成成功。

详见 [模块手册](Documentation~/README.md) 和 [图集制作流程](Documentation~/FontAuthoring.md)。样式、翻译、字库和页面布局分别验证；预览只显示当前字库已有字形，不加载运行时 fallback。

## 验证和升级

编辑态可运行菜单中的 **运行接入协议验证**、**运行样式预览验证**、**运行页面样式预览验证**、**运行工具验证**。页面验证需要至少两个 Static 字体，检查 Canvas 网格像素、遮罩、草稿/保存/另存为和 Prefab 保存/加载；临时资源自动清理。协议验证检查加载器、合并请求、缓存、加载顺序和 UI/Profile 通知；它不模拟 PlayMode 生命周期。完整工具验证在临时副本生成图集，需要目标工程有效的制作配置。

正式接入还需在 PlayMode 检查启动加载、语言字体、页面样式、关闭重开及语言切换。UPM 的 Git 安装会锁定具体提交；刷新界面不等于升级到远端最新提交，正式升级请更新版本标签。

修改包时使用本地仓库/file 依赖，提交后发布新版本；Git 下载的包缓存不是开发工作目录。提交所有 Runtime/Editor 资源及原有 meta，保留 GUID。文档与 Samples~ 不参与编译；样例只有导入 Assets 后才编译。

## 当前版本

0.4.0 增加统一艺术字入口、分层/渐变/弧形和当前文字覆盖；0.3.0 增加页面内编辑态预览与 TMPStyleApplier 样式草稿制作；0.2.0 完成 UPM 转换与项目接入接口。详见 [变更记录](CHANGELOG.md)。初始源文件清单保存在 Documentation~/InitialSourceManifest.json，仅供追溯旧版，不是当前版本文件清单或验证结论。

## 艺术字统一入口（0.4.0）

选中文字上的 `TMPStyleApplier`，在原有样式草稿中调整字面、上下渐变、描边、底层和“整段分层”。UGUI 固定按 Underlay -2、Outline -1、Face 0 绘制，三层共享最终文字网格；文字内容、字号、换行仍在 TMP 中编辑。分层使用简化 SDF，不包含原材质的 Bevel、Glow 或自定义贴图效果。

- **保存共享预设**：更新引用该 SO 的所有文字。
- **保存到当前文字**：保存独立参数快照，只影响该文字，随场景/Prefab 持久化。运行时仍按语言 Profile 更新字体。点击“清除当前覆盖”恢复 Profile 外观。
- **另存为新样式**：继续沿用创建 SO、登记新 Key 的流程。另存时自动清除当前覆盖，改为跟随新 Key。
- **弧形排版**：保存在当前组件，不写入共享预设。半径倍数越大越平；关闭后恢复原始排版。已有启用的 `TMPCurvedText` 继续负责弯曲，避免叠加变形；包不自动移除旧组件。

编辑预览不改写已保存的 TMP 字体/材质引用。分层仅支持 `TextMeshProUGUI`；3D TMP 可用渐变/弧形。分层、渐变和弧形均默认关闭。SDF 粗边仍受字体图集 Padding 限制，三层增加 UI 绘制开销，正式页面需按目标设备验收。

验证入口：`Game Framework/字体与字符集/运行艺术字编辑验证`、`运行艺术字运行验证`。后者切到临时空场景进入 Play Mode，退出后恢复原场景；报告位于 `Library/FontCharset/ArtTextValidation/`。

### 从 0.4.0 回退到 0.3.2

Package Manager 的 Git URL 改回 `https://github.com/Ruacs/GF-TMPFontSystem.git#v0.3.2`，等待解析和编译。旧标签保留，不能移动或覆盖。现有组件和共享样式字段/GUID 未变，旧功能继续可用；新版弧形、整段分层、渐变和当前覆盖在旧包中不可用。

**精确回到升级前外观**：同时恢复升级前备份的场景/Prefab、Preset/Profile 和包清单/锁定文件。只降级包不会撤销你升级后对共享预设原有颜色/描边参数的编辑；旧 Unity 序列化器重新保存资产时也可能丢弃未知新字段。降级前另存新版资产，避免丢失新配置。不要删除 `.meta` 或重建字体资产。
