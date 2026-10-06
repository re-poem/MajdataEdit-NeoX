## MainViewModel

在这个部分记录一下MainViewModel的设计和实现细节。

框架上，MainViewModel有以下部分：
1. `Dock`系统；
2. `Settings`数据体系、`Maidata`及其`外围文件`数据体系
3. `临时态`、`连接态`
4. 程序主要`Commands`
5. `临时态`的`Wire Up`

### 数据体系
数据体系主要设计观念是：以大片代码块做好每个**UI绑定**属性的**展开和提醒**。
此处并没有引入什么`副作用`，只是单纯的用很多不怎么美丽的代码实现Observable。

### 临时态
此处是指在程序运行时，各个`View`的有些状态是临时的，随着用户操作而改变。
而被加入到MainViewModel中的`临时态`属性，主要是为了给一些`Commands`操作和`Wire Up`。