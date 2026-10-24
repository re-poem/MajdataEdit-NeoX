# 命名规范

## C# 部分
**主要结构**：Models - (Managers) - MainViewModel <- Services
- Models：相对纯粹的数据模型
- Managers：数据模型塞了丑，vm塞了太乱的东西，也就是管理数据模型的
- MainViewModel：所有数据的唯一持有者，所有视图绑定的数据来源
- Services：注入于main vm的东西，分为应用程序级组合根注入和view->viewModel级注入（在dock factory里面）

## Avalonia 部分
**主要结构**：Controls - View - Tool - ToolDock...
- Controls：单纯的控件，定义上的控件
- View：拼凑控件的容器
- Tool：存View的容器，同时也方便DataTemplate
- ToolDock：喂给dock控件的。。。管不着的范围了。

## 其它
- Types：独立于体系的类，可以说是没有功能的一种Utils