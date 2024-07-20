# FaceSimilarityService

用来替代 咱临时使用的这个项目: [face-recognition-service](https://gitee.com/westinyang/face-recognition-service)

## 拓展

- 实现用户 key - 图片缓存 (并分离每个调用 ip 的数据)

- 实现添查删改 api

- 支持 `单帧活体检测`

- 多线程遍历特征值 1:N

- ~~集成向量数据库来查询特征值~~(有生之年整一下)

- 支持 win64 nvidia cuda 加速

- cpu 不支持 axv2 的情况下会自动回退到 sse2 指令集

## Thanks

- [ViewFaceCore](https://github.com/ViewFaceCore/ViewFaceCore)
- [SeetaFace6Sharp](https://github.com/SeetaFace6Sharp/SeetaFace6Sharp)
