# Project Context

## Purpose
MiniCharacterCreator - Unity 角色创建器项目，用于创建和定制游戏角色。

## Tech Stack
- Unity 游戏引擎
- C# 编程语言
- Odin Inspector (UI 增强)
- UniTask (异步处理)
- Addressables (资源管理)

## Project Conventions

### Code Style
- 使用 C# 命名规范
- PascalCase 用于公共成员和类
- camelCase 用于私有字段（带 _ 前缀）

### Architecture Patterns
- Unity MonoBehaviour 组件模式
- ScriptableObject 配置数据

### Testing Strategy
- Unity Test Framework

### Git Workflow
- 主分支开发

## Domain Context
游戏角色创建系统，涉及装备、外观定制等功能。

## Important Constraints
- 需兼容 Unity 编辑器版本
- 依赖 Odin Inspector 插件

## External Dependencies
- Sirenix.OdinInspector
- UniTask
- Unity Addressables
