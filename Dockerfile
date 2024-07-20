# 基于 Ubuntu 官方镜像
FROM ubuntu:latest

# 设置环境变量以避免交互提示
ENV DEBIAN_FRONTEND=noninteractive

# 更新软件包列表并安装依赖项
RUN apt-get update && \
    apt-get install -y libgomp1 libicu-dev && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/*

# 复制二进制文件到容器中
COPY publish /opt/faces

# 确保二进制文件具有执行权限
RUN chmod +x /opt/faces/FaceSimilarityService

# 设置工作目录
WORKDIR /opt/faces

# 定义容器启动时运行的命令
CMD ["./FaceSimilarityService"]
