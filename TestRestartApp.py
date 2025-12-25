#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""
Test application for WinServiceManager exit code restart feature.

This script simulates a program that can:
1. Exit with code 99 to trigger automatic restart
2. Exit with code 0 for normal shutdown (no restart)
3. Exit with other codes for error scenarios

Usage:
    python TestRestartApp.py [exit_code]

Arguments:
    exit_code: The exit code to return (default: 99)
                - 99: Trigger restart
                - 0: Normal shutdown
                - Other: Error (no restart by default)
"""

import sys
import time
import os
from datetime import datetime


def log(message):
    """Print message with timestamp."""
    timestamp = datetime.now().strftime('%Y-%m-%d %H:%M:%S')
    print(f"[{timestamp}] {message}")
    sys.stdout.flush()


def main():
    # Get exit code from command line or use default 99
    exit_code = 99
    if len(sys.argv) > 1:
        try:
            exit_code = int(sys.argv[1])
        except ValueError:
            log(f"Invalid exit code argument: {sys.argv[1]}")
            log("Using default exit code: 99")

    log("=" * 60)
    log("TestRestartApp started")
    log(f"PID: {os.getpid()}")
    log(f"Exit code: {exit_code}")
    log("=" * 60)

    # Simulate some work
    log("Performing initialization...")
    time.sleep(1)

    log("Running main task...")
    for i in range(1, 6):
        log(f"Processing step {i}/5...")
        time.sleep(1)

    log("Task completed!")

    # Explain the exit code behavior
    if exit_code == 99:
        log("Exiting with code 99 - WinSW should RESTART this service")
    elif exit_code == 0:
        log("Exiting with code 0 - Normal shutdown, WinSW should NOT restart")
    else:
        log(f"Exiting with code {exit_code} - Error exit, WinSW should NOT restart (by default)")

    log("=" * 60)
    log("TestRestartApp shutting down...")
    sys.stdout.flush()

    # Small delay to ensure all output is flushed
    time.sleep(0.5)

    sys.exit(exit_code)


if __name__ == '__main__':
    main()
