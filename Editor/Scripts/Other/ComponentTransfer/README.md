# 组件转移系统

这是一个插件式的组件转移系统，用于在 VRChat 头像之间转移各种组件。

## 架构设计

### 核心接口
- `IComponentTransferPlugin`: 插件接口，定义所有插件必须实现的方法
- `ComponentTransferBase`: 基础抽象类，提供通用的转移功能

### 插件系统
每个功能都被设计为独立的插件，位于 `Plugins/` 目录下：

#### 现有插件
1. **PhysBoneTransferPlugin** - 物理骨骼转移
   - 转移 VRCPhysBone 组件
   - 转移 VRCPhysBoneCollider 组件
   - 处理根变换和碰撞器引用

2. **MaterialTransferPlugin** - 材质转移
   - 转移所有 Renderer 组件的材质
   - 支持 SkinnedMeshRenderer 和 MeshRenderer

3. **BlendShapeTransferPlugin** - 混合形状转移
   - 转移 SkinnedMeshRenderer 的混合形状权重
   - 自动匹配同名的混合形状

4. **ConstraintTransferPlugin** - 约束转移
   - 转移 Parent、Rotation、Position 约束
   - 自动处理约束源引用

5. **ParticleSystemTransferPlugin** - 粒子系统转移
   - 转移 ParticleSystem 组件
   - 转移 ParticleSystemRenderer 组件
   - 支持子粒子系统

6. **ActiveStateTransferPlugin** - 激活状态同步
   - 同步所有 GameObject 的激活状态

## 使用方法

1. 打开工具：`Tools/YuebyTools/VRChat/Avatar/Component Transfer`
2. 选择源根对象（包含要转移组件的对象）
3. 在场景中选择目标对象
4. 配置需要启用的插件
5. 点击"开始转移"

## 添加新插件

要添加新的转移插件，需要：

1. 创建新的插件类，继承 `ComponentTransferBase`
2. 实现必要的方法：
   - `Name`: 插件名称
   - `Description`: 插件描述
   - `DrawSettings()`: 绘制设置界面
   - `ExecuteTransfer()`: 执行转移逻辑
   - `CanTransfer()`: 检查是否可以转移

3. 在 `ComponentTransferWindow.InitializePlugins()` 中注册新插件

### 示例插件结构

```csharp
public class MyTransferPlugin : ComponentTransferBase
{
    public override string Name => "我的转移插件";
    public override string Description => "转移特定组件";

    public override void DrawSettings()
    {
        // 绘制插件特定的设置
    }

    public override bool ExecuteTransfer(Transform sourceRoot, Transform targetRoot)
    {
        // 实现转移逻辑
        return true;
    }

    public override bool CanTransfer(Transform sourceRoot, Transform targetRoot)
    {
        // 检查是否可以转移
        return true;
    }
}
```

## 特性

- **插件化设计**: 每个功能都是独立插件，易于扩展和维护
- **智能对象匹配**: 支持层级路径匹配和同名对象查找
- **缓存机制**: 提高性能，避免重复计算
- **撤销支持**: 所有操作都支持撤销
- **错误处理**: 完善的异常处理和日志记录
- **用户友好**: 清晰的界面和进度反馈

## 优势

相比原来的单体设计，新的插件式架构具有以下优势：

1. **模块化**: 每个功能独立，便于维护和测试
2. **可扩展**: 轻松添加新的转移功能
3. **可配置**: 用户可以选择启用哪些功能
4. **代码复用**: 通用功能在基类中实现
5. **更好的错误隔离**: 一个插件的错误不会影响其他插件 