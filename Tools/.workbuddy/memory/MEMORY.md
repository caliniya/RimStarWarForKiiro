# KiiroStarWARForRim 项目长期备忘

## 目录约定
- 本工作区 `D:\project\KiiroStarWARForRim\Tools` **只是工具脚本目录**，
  真正的工程根目录是它的上级 `D:\project\KiiroStarWARForRim`。

## 工程概况
RimWorld 1.6 中文模组「绮罗：星际战争」，C# + XML Defs，packageId `caliniya.rimWorld.starWar.Kiiro`。
目标框架 net472，程序集输出 `Assemblies/StarWarKiiro.dll`。
游戏本体在 `E:\app\rimworld\RimWorld16`（csproj 的 HintPath 指向它）。

## 构建与验证
- 编译：`dotnet build`（工程根目录），VS Code 默认生成任务也是它
- 部署：`build.ps1`（robocopy /MIR 到 `E:\app\rimworld\RimWorld16\Mods\StarWarKiiro`），`-Zip` 额外出分发包
- **本会话的 Bash 与 PowerShell stdout 捕获都不可用**，
  需要看命令输出时改为 `命令 2>&1 | Out-File -FilePath <绝对路径> -Encoding utf8` 再用 Read 读文件。
  PowerShell 输出中文会是 GBK 乱码，但错误信息仍可辨认。

## 开发约定
- 注释与 UI 文案全部用中文，代码风格偏向显式、带解释性注释
- **建筑一律不做材质选择**：ThingDef 不写 `<stuffCategories>` / `<costStuffCount>`，只用固定 `<costList>`，
  避免 label 出现「钢铁火箭发射台」之类的材质前缀（用户明确要求，2026-09-29）
- 贴图是 `Tools/*.ps1` 用 System.Drawing 程序化生成的占位图，不是手绘资源
- 火箭发射的「点火升空」部分目前仍是占位实现（只弹消息）

## 排查运行时问题的路径
- RimWorld 日志：`C:\Users\yiyi\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Player.log`
  （启动参数带 `-savedatafolder=E:\data\rim`，但那个目录下没有 Logs 文件夹，看 Player.log 就够，
  它能捕获 RimWorld 自己的 Log.Error / Log.Message）
- 排除法顺序：先看日志有无 Def 报错 → 再对比 `E:\app\rimworld\RimWorld16\Mods\StarWarKiiro`
  与工程产物的时间戳，确认不是跑的旧副本 → 最后才查代码逻辑
- 部署前用 `Get-Process RimWorldWin64` 确认游戏没在跑，否则 DLL 被锁

## RimWorld 踩坑记录
- 覆写 `Tick()` 的 ThingDef **必须显式写 `<tickerType>`**（BuildingBase 不设，默认 Never）
- `Reserve` / `CanReserve` 的签名是 `(target, maxPawns, stackCount)`。
  给**非堆叠物**（建筑等 stackCount 恒为 1 的东西）预留时 stackCount 必须传 -1（用默认值），
  传具体数字会让 `ReservationManager` 判定「要求数量 > 实际存在数量」而**永远返回 false**，
  表现是 WorkGiver 恒返回 null —— 自动派活和右键手动指令会同时消失，且日志里**不会报错**
- 已 DeSpawn 且脱离容器的 Thing 不能用 `LookMode.Reference` 存档，要用 `LookMode.Deep`
- `Thing.SplitOff(count)`：count >= stackCount 时返回原物并移出容器，否则返回新建的那一段
- `Pawn_CarryTracker.TryDropCarriedThing(IntVec3, ThingPlaceMode, out Thing)` —— 没有 Map 参数
