## MainViewModel

在这个部分记录一下MainViewModel的设计和实现细节。

框架上，MainViewModel有以下部分：
1. `Dock`系统；
2. `Settings`数据体系
3. `Maidata`及其`外围文件`数据体系
4. `临时态`
5. 程序主要`Commands`
6. `临时态`的`Wire Up`

### 数据体系
数据体系主要设计观念是：以大片代码块做好每个**UI绑定**属性的**展开和提醒**。
此处并没有引入什么`副作用`，只是单纯的用很多不怎么美丽的代码实现Observable。

### `Maidata`及其`外围文件`数据体系
这一块可能稍显大，但最主要的原因是 
1. 我觉得把路径、`TrackInfo`、`Cover` 全部放到一个数据结构里一样臃肿 
2. 目前这样符合`MaidataFile`语义
3. 省掉一些展开/转发代码

因此每个对于文件的操作都有四个内容：
* UpdatePath(dir)
* Maidata = MaidataFile.Parse(_maidataPath)
* TrackInfo = TrackReader.ReadTrack(_trackPath)
* Cover = new Bitmap(_imagePath)

（后两个被合并到`UpdateResources()`中）核心逻辑就是更新路径并读取

### 临时态
此处是指在程序运行时，各个`View`的有些状态是临时的，随着用户操作而改变。
而被加入到MainViewModel中的`临时态`属性，主要是为了给一些`Commands`操作和`Wire Up`。

## 框架组织

项目最显著的特征是解大耦，模块被抽象为`UserControl`（控件/View），几个属性从xaml绑到viewmodel里的数据，内嵌小部分逻辑优化体验。

这种设计在Windows平台上展现的优势是自然地融入了Dock体系。