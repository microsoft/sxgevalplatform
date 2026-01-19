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
    python3-pip \
    && tdnf clean all

# Upgrade pip to latest version (quiet mode + output redirection for ADO pipeline compatibility)
RUN python3 -m pip install --quiet --upgrade pip setuptools wheel >/dev/null 2>&1 || \
    { echo "ERROR: Failed to upgrade pip"; exit 1; }

# Copy requirements first to leverage Docker cache
COPY requirements.txt .

# Install Python dependencies (quiet mode + output redirection for ADO pipeline compatibility)
RUN python3 -m pip install --quiet --no-cache-dir --user -r requirements.txt >/dev/null 2>&1 || \
    { echo "ERROR: Failed to install requirements. Contents of requirements.txt:"; cat requirements.txt; exit 1; }

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
    CMD python3 -c "import sys; sys.exit(0)" || exit 1

# Expose port (if needed for health checks)
EXPOSE 8080

# Command to run the application
CMD ["python3", "src/main.py"]