#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
简单的测试服务 - 用于WinServiceManager测试
每5秒输出一次当前时间戳
"""

import time
import logging
from datetime import datetime

# 配置日志
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(levelname)s - %(message)s',
    handlers=[
        logging.StreamHandler(),  # 输出到控制台
        logging.FileHandler('test-service.log', encoding='utf-8')  # 输出到文件
    ]
)

logger = logging.getLogger(__name__)

def main():
    """主服务循环"""
    logger.info("=== 简单测试服务启动 ===")
    logger.info(f"进程ID: {__import__('os').getpid()}")

    try:
        counter = 0
        while True:
            counter += 1
            timestamp = datetime.now().strftime('%Y-%m-%d %H:%M:%S')

            # 输出信息
            message = f"服务运行中 - 计数器: {counter}, 时间: {timestamp}"
            logger.info(message)

            # 每10次循环输出一次详细信息
            if counter % 10 == 0:
                logger.info(f"=== 服务状态报告 (第{counter}次循环) ===")
                logger.info(f"运行时间: {counter * 5}秒")
                logger.info(f"内存使用: 正常")
                logger.info(f"服务状态: 正常运行")

            # 等待5秒
            time.sleep(5)

    except KeyboardInterrupt:
        logger.info("收到中断信号，正在停止服务...")
    except Exception as e:
        logger.error(f"服务异常: {str(e)}")
        raise
    finally:
        logger.info("=== 简单测试服务停止 ===")

if __name__ == "__main__":
    main()