#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
测试服务脚本
用于测试 WinServiceManager 创建和管理服务
"""

import time
import logging
import sys
import os
from datetime import datetime

# 配置日志
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(levelname)s - %(message)s',
    handlers=[
        logging.FileHandler('test_service.log'),
        logging.StreamHandler(sys.stdout)
    ]
)

logger = logging.getLogger(__name__)

def main():
    """主函数"""
    logger.info("=" * 50)
    logger.info("测试服务启动 - %s", datetime.now().strftime('%Y-%m-%d %H:%M:%S'))
    logger.info("=" * 50)

    # 获取进程信息
    process_id = os.getpid()
    logger.info("进程 ID: %d", process_id)
    logger.info("当前工作目录: %s", os.getcwd())

    try:
        # 主循环
        counter = 0
        while True:
            counter += 1
            logger.info("服务运行中... (计数: %d)", counter)

            # 模拟工作
            time.sleep(5)

            # 每60次循环输出一次统计信息
            if counter % 12 == 0:
                logger.info("=== 统计信息 ===")
                logger.info("已运行 %d 个循环", counter)
                logger.info("运行时间: %d 分钟", counter * 5 // 60)
                logger.info("================")

            # 模拟一些日志类型
            if counter % 10 == 0:
                logger.warning("这是一个警告消息示例")
            if counter % 20 == 0:
                logger.error("这是一个错误消息示例（仅用于测试）")

    except KeyboardInterrupt:
        logger.info("收到中断信号，正在优雅地关闭服务...")
    except Exception as e:
        logger.error("服务运行出错: %s", str(e), exc_info=True)
    finally:
        logger.info("测试服务已停止")

if __name__ == "__main__":
    main()