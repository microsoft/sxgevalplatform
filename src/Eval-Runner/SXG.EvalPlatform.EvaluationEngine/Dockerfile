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
    python3-devel \
    openssl-devel \
    libffi-devel \
    ca-certificates \
    && tdnf clean all \
    && update-ca-trust

# Set pip trusted hosts as environment variables for all pip commands
ENV PIP_TRUSTED_HOST="pypi.org pypi.python.org files.pythonhosted.org"

# Upgrade pip first (with trusted hosts)
RUN pip3 install --upgrade \
    --trusted-host pypi.org \
    --trusted-host pypi.python.org \
    --trusted-host files.pythonhosted.org \
    pip setuptools wheel

# Copy requirements first to leverage Docker cache
COPY requirements.txt .

# Install Python dependencies to a specific directory
# Using --trusted-host flags for OneBranch build environment SSL compatibility
RUN pip3 install --no-cache-dir --prefix=/install \
    --trusted-host pypi.org \
    --trusted-host pypi.python.org \
    --trusted-host files.pythonhosted.org \
    -r requirements.txt

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
COPY --from=builder /install /usr/local

# Copy application code and all configuration files
COPY src/ ./src/
COPY appsettings*.json ./

# Set environment variables
ENV PATH=/usr/local/bin:$PATH \
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