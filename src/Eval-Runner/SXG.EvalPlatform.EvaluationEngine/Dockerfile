# Multi-stage Dockerfile for SXG Evaluation Platform
# Using Azure Linux base Python image
FROM mcr.microsoft.com/azurelinux/base/python:3 as builder

# Accept build arguments for encoding (can be overridden at build time)
ARG LANG=C.UTF-8
ARG LC_ALL=C.UTF-8
ARG PYTHONIOENCODING=utf-8

# Set UTF-8 locale and encoding environment variables
ENV LANG=${LANG} \
    LC_ALL=${LC_ALL} \
    PYTHONIOENCODING=${PYTHONIOENCODING} \
    PYTHONUNBUFFERED=1 \
    PIP_NO_CACHE_DIR=1 \
    PIP_DISABLE_PIP_VERSION_CHECK=1

# Set working directory
WORKDIR /app

# Install system dependencies
# Azure Linux uses tdnf package manager and different package names
RUN tdnf install -y \
    gcc \
    gcc-c++ \
    glibc-lang \
    && tdnf clean all

# Upgrade pip to latest version (with quiet output to avoid encoding issues)
RUN python -m pip install --upgrade --quiet pip setuptools wheel

# Copy requirements first to leverage Docker cache
COPY requirements.txt .

# Install Python dependencies with explicit UTF-8 encoding and quiet output to avoid encoding issues
RUN export PYTHONIOENCODING=utf-8 && \
    export LC_ALL=C.UTF-8 && \
    export LANG=C.UTF-8 && \
    pip install --no-cache-dir --user --quiet -r requirements.txt 2>&1 | tee /tmp/pip-install.log || \
    (echo "Pip install failed. Log contents:" && cat /tmp/pip-install.log && exit 1)

# Production stage - Azure Linux base Python image
FROM mcr.microsoft.com/azurelinux/base/python:3

# Set UTF-8 locale and encoding environment variables
ENV LANG=C.UTF-8 \
    LC_ALL=C.UTF-8 \
    PYTHONIOENCODING=utf-8 \
    PYTHONUNBUFFERED=1

# Set working directory
WORKDIR /app

# Create non-root user for security
RUN tdnf install -y shadow-utils && \
    groupadd -r evaluser && \
    useradd -r -g evaluser evaluser && \
    tdnf clean all

# Copy installed packages from builder stage
COPY --from=builder /root/.local /home/evaluser/.local

# Copy application code and all configuration files
COPY src/ ./src/
COPY appsettings*.json ./

# Set environment variables
ENV PATH=/home/evaluser/.local/bin:$PATH \
    PYTHONPATH=/app/src

# Set ownership
RUN chown -R evaluser:evaluser /app

# Switch to non-root user
USER evaluser

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=60s --retries=3 \
    CMD python -c "import sys; sys.exit(0)" || exit 1

# Expose port (if needed for health checks)
EXPOSE 8080

# Command to run the application
CMD ["python", "src/main.py"]